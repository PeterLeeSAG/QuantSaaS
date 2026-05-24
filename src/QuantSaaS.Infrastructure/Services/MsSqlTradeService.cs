using Dapper;
using QuantSaaS.Core.Models;
using QuantSaaS.Infrastructure.Data;

namespace QuantSaaS.Infrastructure.Services;

public sealed class MsSqlTradeService : ITradeService
{
    private const int PageSize = 25;
    private readonly DbConnectionFactory _db;

    public MsSqlTradeService(DbConnectionFactory db) => _db = db;

    public async Task<TradePageDto> GetPagedAsync(
        Guid userId, string? symbol, string? action, int page, CancellationToken ct = default)
    {
        using var conn = await _db.OpenAsync(ct);

        var conditions = new List<string> { "si.user_id = @UserId" };
        if (!string.IsNullOrWhiteSpace(symbol))
            conditions.Add("tr.symbol LIKE @Symbol");
        if (!string.IsNullOrWhiteSpace(action))
            conditions.Add("tr.action = @Action");

        var where = string.Join(" AND ", conditions);

        var total = await conn.ExecuteScalarAsync<int>($"""
            SELECT COUNT(*) FROM dbo.trade_records tr
            JOIN dbo.strategy_instances si ON si.id = tr.instance_id
            WHERE {where}
            """,
            new { UserId = userId, Symbol = $"%{symbol}%", Action = action?.ToUpperInvariant() });

        int totalPages = (int)Math.Ceiling(total / (double)PageSize);
        page = Math.Clamp(page, 1, Math.Max(1, totalPages));

        // T-SQL paging: OFFSET ... ROWS FETCH NEXT ... ROWS ONLY (requires ORDER BY)
        var rows = await conn.QueryAsync<dynamic>($"""
            SELECT tr.symbol, tr.asset_class, tr.action, tr.filled_qty, tr.filled_price, tr.fee, tr.status, tr.filled_at
            FROM dbo.trade_records tr
            JOIN dbo.strategy_instances si ON si.id = tr.instance_id
            WHERE {where}
            ORDER BY tr.filled_at DESC
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY
            """,
            new { UserId = userId, Symbol = $"%{symbol}%", Action = action?.ToUpperInvariant(), Limit = PageSize, Offset = (page - 1) * PageSize });

        var trades = rows.Select(r => new TradeRowDto(
            (string)r.symbol,
            (AssetClass)(int)r.asset_class,
            (string)r.action,
            (decimal)r.filled_qty,
            (decimal)r.filled_price,
            (decimal)r.fee,
            (string)r.status,
            (DateTime)r.filled_at)).ToList();

        return new TradePageDto(trades, page, totalPages, total);
    }
}
