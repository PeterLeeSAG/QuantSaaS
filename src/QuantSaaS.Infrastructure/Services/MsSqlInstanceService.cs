using Dapper;
using QuantSaaS.Core.Models;
using QuantSaaS.Infrastructure.Data;

namespace QuantSaaS.Infrastructure.Services;

public sealed class MsSqlInstanceService : IInstanceService
{
    private readonly MsSqlConnectionFactory _db;

    public MsSqlInstanceService(MsSqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<InstanceRowDto>> GetListAsync(
        Guid userId, string? assetClassFilter, CancellationToken ct = default)
    {
        using var conn = await _db.OpenAsync(ct);

        var rows = await conn.QueryAsync<dynamic>("""
            SELECT si.id, si.instrument_symbol AS symbol, si.asset_class,
                   si.broker_type, si.status, si.last_tick_at,
                   COALESCE(ps.total_equity, 0) AS equity
            FROM dbo.strategy_instances si
            LEFT JOIN dbo.portfolio_snapshots ps ON ps.instance_id = si.id
            WHERE si.user_id = @UserId
            ORDER BY si.created_at
            """, new { UserId = userId });

        var result = rows.Select(r => new InstanceRowDto(
            (Guid)r.id,
            (string)r.symbol,
            (AssetClass)(int)r.asset_class,
            (string)r.broker_type,
            (string)r.status,
            (decimal)r.equity,
            (DateTime?)r.last_tick_at)).ToList();

        if (!string.IsNullOrWhiteSpace(assetClassFilter) &&
            Enum.TryParse<AssetClass>(assetClassFilter, true, out var filterEnum))
        {
            result = result.Where(r => r.AssetClass == filterEnum).ToList();
        }

        return result;
    }

    public async Task<InstanceDetailDto?> GetDetailAsync(
        Guid instanceId, CancellationToken ct = default)
    {
        using var conn = await _db.OpenAsync(ct);

        // ── Instance + Portfolio ──────────────────────────────────────────────
        var inst = await conn.QuerySingleOrDefaultAsync<dynamic>("""
            SELECT si.id, si.instrument_symbol AS symbol, si.asset_class,
                   si.broker_type, si.status, si.created_at, si.last_tick_at,
                   COALESCE(ps.cash_balance, 0)              AS available_funds,
                   COALESCE(ps.dead_stack_qty, 0)            AS long_term_holdings_qty,
                   COALESCE(ps.float_stack_qty, 0)           AS active_position_qty,
                   COALESCE(ps.cold_sealed_qty, 0)           AS sealed_qty,
                   COALESCE(ps.pending_settlement_amount, 0) AS pending_settlement_amount,
                   COALESCE(ps.total_equity, 0)              AS total_equity
            FROM dbo.strategy_instances si
            LEFT JOIN dbo.portfolio_snapshots ps ON ps.instance_id = si.id
            WHERE si.id = @Id
            """, new { Id = instanceId });

        if (inst is null) return null;

        // ── 30-day instance equity curve ──────────────────────────────────────
        var today = DateTime.UtcNow.Date;
        var snapshots = await conn.QueryAsync<dynamic>("""
            SELECT date_utc, equity FROM dbo.equity_snapshots
            WHERE instance_id = @Id AND date_utc >= @From
            ORDER BY date_utc
            """, new { Id = instanceId, From = today.AddDays(-29) });

        var curve  = snapshots.Select(s => (decimal)s.equity).ToList();
        var labels = snapshots.Select(s => ((DateTime)s.date_utc).ToString("MMM dd")).ToList();

        if (curve.Count == 0)
        {
            var eq = (decimal)inst.total_equity;
            for (int d = 29; d >= 0; d--)
            {
                curve.Add(eq);
                labels.Add(DateTime.UtcNow.AddDays(-d).ToString("MMM dd"));
            }
        }

        // ── Recent trades (TOP 20 in T-SQL) ───────────────────────────────────
        var trades = await conn.QueryAsync<dynamic>("""
            SELECT TOP 20 symbol, asset_class, action, filled_qty, filled_price, fee, status, filled_at
            FROM dbo.trade_records
            WHERE instance_id = @Id
            ORDER BY filled_at DESC
            """, new { Id = instanceId });

        var tradeList = trades.Select(t => new TradeRowDto(
            (string)t.symbol,
            (AssetClass)(int)t.asset_class,
            (string)t.action,
            (decimal)t.filled_qty,
            (decimal)t.filled_price,
            (decimal)t.fee,
            (string)t.status,
            (DateTime)t.filled_at)).ToList();

        // ── Pending settlements ───────────────────────────────────────────────
        var settlements = await conn.QueryAsync<dynamic>("""
            SELECT client_order_id, amount, settlement_date_utc, is_settled
            FROM dbo.pending_settlements
            WHERE instance_id = @Id AND is_settled = 0
            ORDER BY settlement_date_utc
            """, new { Id = instanceId });

        var settlementList = settlements.Select(s => new PendingSettlementDto(
            (string)s.client_order_id,
            (decimal)s.amount,
            (DateTime)s.settlement_date_utc,
            (bool)s.is_settled)).ToList();

        return new InstanceDetailDto(
            Id:                      (Guid)inst.id,
            Symbol:                  (string)inst.symbol,
            AssetClass:              (AssetClass)(int)inst.asset_class,
            BrokerType:              (string)inst.broker_type,
            Status:                  (string)inst.status,
            CreatedAt:               (DateTime)inst.created_at,
            LastTickAt:              (DateTime?)inst.last_tick_at,
            TotalEquity:             (decimal)inst.total_equity,
            AvailableFunds:          (decimal)inst.available_funds,
            LongTermHoldingsQty:     (decimal)inst.long_term_holdings_qty,
            ActivePositionQty:       (decimal)inst.active_position_qty,
            SealedQty:               (decimal)inst.sealed_qty,
            PendingSettlementAmount: (decimal)inst.pending_settlement_amount,
            EquityCurve:             curve,
            EquityLabels:            labels,
            RecentTrades:            tradeList,
            PendingSettlements:      settlementList);
    }
}
