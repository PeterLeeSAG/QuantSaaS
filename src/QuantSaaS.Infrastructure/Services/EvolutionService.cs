using Dapper;
using QuantSaaS.Core.Models;
using QuantSaaS.Infrastructure.Data;

namespace QuantSaaS.Infrastructure.Services;

public sealed class EvolutionService : IEvolutionService
{
    private readonly DbConnectionFactory _db;

    public EvolutionService(DbConnectionFactory db) => _db = db;

    public async Task<LabDto> GetLabAsync(Guid userId, CancellationToken ct = default)
    {
        using var conn = await _db.OpenAsync(ct);

        // Evolution tasks for templates used by this user's instances
        var taskRows = await conn.QueryAsync<dynamic>("""
            SELECT DISTINCT et.id, et.symbol, et.asset_class, et.status, et.progress, et.created_at, et.completed_at
            FROM evolution_tasks et
            JOIN strategy_templates st ON st.id = et.template_id
            JOIN strategy_instances si ON si.template_id = st.id AND si.user_id = @UserId
            ORDER BY et.created_at DESC
            """, new { UserId = userId });

        var tasks = taskRows.Select(r => new EvolutionTaskDto(
            (Guid)r.id,
            (string)r.symbol,
            (AssetClass)(int)r.asset_class,
            (string)r.status,
            (int)r.progress,
            (DateTime)r.created_at,
            (DateTime?)r.completed_at)).ToList();

        // Champion genes for templates used by this user
        var geneRows = await conn.QueryAsync<dynamic>("""
            SELECT DISTINCT gr.id, gr.symbol, gr.asset_class, gr.role, gr.score_total, gr.max_drawdown, gr.evolved_at
            FROM gene_records gr
            JOIN strategy_templates st ON st.id = gr.template_id
            JOIN strategy_instances si ON si.template_id = st.id AND si.user_id = @UserId
            WHERE gr.role = 'champion'
            ORDER BY gr.evolved_at DESC
            """, new { UserId = userId });

        var champions = geneRows.Select(r => new GeneChampionDto(
            (Guid)r.id,
            (string)r.symbol,
            (AssetClass)(int)r.asset_class,
            (string)r.role,
            (decimal)r.score_total,
            (decimal)r.max_drawdown,
            (DateTime)r.evolved_at)).ToList();

        return new LabDto(tasks, champions);
    }
}
