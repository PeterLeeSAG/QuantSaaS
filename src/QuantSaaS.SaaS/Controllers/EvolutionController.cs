using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.SaaS.Models;
using System.Text.Json;

namespace QuantSaaS.SaaS.Controllers;

file static class JsonElementExtensions
{
    public static decimal GetDecimalOrDefault(this JsonElement element, string propertyName, decimal defaultValue = 0) =>
        element.TryGetProperty(propertyName, out var prop) ? prop.GetDecimal() : defaultValue;
}

[ApiController]
[Route("api/v1/evolution")]
[Authorize]
public class EvolutionController : ControllerBase
{
    private readonly QuantDbContext _db;

    public EvolutionController(QuantDbContext db) => _db = db;

    [HttpGet("tasks")]
    public async Task<IActionResult> ListTasks()
    {
        var tasks = await _db.EvolutionTasks
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .ToListAsync();

        var result = tasks.Select(t => new EvolutionTaskSummary(
            t.Id, t.Status, t.PopSize, t.MaxGenerations, t.Progress,
            null, null, t.CreatedAt, t.CompletedAt, t.ErrorMessage)).ToList();

        return Ok(result);
    }

    [HttpPost("tasks")]
    public async Task<IActionResult> TriggerEvolution([FromBody] TriggerEvolutionRequest req)
    {
        var task = new EvolutionTaskEntity
        {
            PopSize = req.PopSize,
            MaxGenerations = req.MaxGenerations,
            Status = "pending",
            ConfigJson = JsonSerializer.Serialize(req)
        };
        _db.EvolutionTasks.Add(task);
        await _db.SaveChangesAsync();
        return Ok(new { taskId = task.Id });
    }

    [HttpGet("genomes")]
    public async Task<IActionResult> ListGenomes()
    {
        var genomes = await _db.GeneRecords
            .OrderByDescending(g => g.CreatedAt)
            .Take(50)
            .ToListAsync();

        var result = genomes.Select(g =>
        {
            var ws = new List<WindowScoreDto>();
            try
            {
                var arr = JsonSerializer.Deserialize<JsonElement[]>(g.WindowScoresJson);
                if (arr != null)
                    ws = arr.Select(e => new WindowScoreDto(
                        e.GetProperty("label").GetString() ?? "",
                        e.GetDecimalOrDefault("weight"),
                        e.GetDecimalOrDefault("sliceScore"),
                        e.GetDecimalOrDefault("alpha"),
                        e.GetDecimalOrDefault("maxDrawdown")
                    )).ToList();
            }
            catch { }

            return new GenomeSummary(g.Id, g.Role, g.ScoreTotal, g.MaxDrawdown, g.CreatedAt, g.PromotedAt, ws);
        }).ToList();

        return Ok(result);
    }

    [HttpPost("genomes/{id}/promote")]
    public async Task<IActionResult> PromoteGenome(Guid id)
    {
        // Retire current champion
        var currentChampion = await _db.GeneRecords
            .FirstOrDefaultAsync(g => g.Role == "champion");
        if (currentChampion != null)
            currentChampion.Role = "retired";

        // Promote challenger
        var genome = await _db.GeneRecords.FindAsync(id);
        if (genome == null) return NotFound();
        genome.Role = "champion";
        genome.PromotedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }
}
