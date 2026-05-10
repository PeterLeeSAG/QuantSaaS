using Dapper;
using QuantSaaS.Core.Models;
using QuantSaaS.Infrastructure.Data;

namespace QuantSaaS.Infrastructure.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly DbConnectionFactory _db;

    public DashboardService(DbConnectionFactory db) => _db = db;

    public async Task<DashboardSummaryDto> GetSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        using var conn = await _db.OpenAsync(ct);

        // Aggregate equity across all instances for this user
        var instances = await conn.QueryAsync<dynamic>("""
            SELECT si.id, si.instrument_symbol AS symbol, si.asset_class, si.status, si.last_tick_at,
                   COALESCE(ps.total_equity, 0) AS equity
            FROM strategy_instances si
            LEFT JOIN portfolio_snapshots ps ON ps.instance_id = si.id
            WHERE si.user_id = @UserId
            ORDER BY si.created_at
            """, new { UserId = userId });

        var instList = instances.Select(r => new InstanceSummaryDto(
            (Guid)r.id,
            (string)r.symbol,
            (AssetClass)(int)r.asset_class,
            (string)r.status,
            (decimal)r.equity,
            (DateTime?)r.last_tick_at)).ToList();

        // Today's trade count
        var today = DateTime.UtcNow.Date;
        var todayTrades = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM trade_records tr
            JOIN strategy_instances si ON si.id = tr.instance_id
            WHERE si.user_id = @UserId AND tr.filled_at >= @Today
            """, new { UserId = userId, Today = today });

        // Available funds = sum of cash balances
        var availableFunds = await conn.ExecuteScalarAsync<decimal>("""
            SELECT COALESCE(SUM(ps.cash_balance), 0)
            FROM portfolio_snapshots ps
            JOIN strategy_instances si ON si.id = ps.instance_id
            WHERE si.user_id = @UserId
            """, new { UserId = userId });

        // 30-day user-level equity curve
        var snapshots = await conn.QueryAsync<dynamic>("""
            SELECT date_utc, equity FROM equity_snapshots
            WHERE user_id = @UserId AND instance_id IS NULL
              AND date_utc >= @From
            ORDER BY date_utc
            """, new { UserId = userId, From = today.AddDays(-29) });

        var curve   = snapshots.Select(s => (decimal)s.equity).ToList();
        var labels  = snapshots.Select(s => ((DateTime)s.date_utc).ToString("MMM dd")).ToList();

        // Fallback: if no snapshots exist yet (e.g. first boot before cron runs),
        // derive a flat curve from current equity.
        if (curve.Count == 0)
        {
            var total = instList.Sum(i => i.Equity);
            for (int d = 29; d >= 0; d--)
            {
                curve.Add(total);
                labels.Add(DateTime.UtcNow.AddDays(-d).ToString("MMM dd"));
            }
        }

        return new DashboardSummaryDto(
            TotalEquity:      instList.Sum(i => i.Equity),
            ActiveInstances:  instList.Count(i => i.Status == "running"),
            TodayTrades:      todayTrades,
            AvailableFunds:   availableFunds,
            EquityCurve:      curve,
            EquityLabels:     labels,
            Instances:        instList);
    }
}
