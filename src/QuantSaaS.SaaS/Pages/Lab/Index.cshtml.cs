using Microsoft.AspNetCore.Mvc.RazorPages;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.Services;
using QuantSaaS.SaaS.ViewModels;

namespace QuantSaaS.SaaS.Pages.Lab;

public class IndexModel : PageModel
{
    private readonly IEvolutionService _evolution;

    public IndexModel(IEvolutionService evolution) => _evolution = evolution;

    public LabViewModel Vm { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var userId = DbInitializer.SeedUserId;
        var dto = await _evolution.GetLabAsync(userId);

        Vm = new LabViewModel
        {
            Champions = dto.Champions.Select(c => new GeneRow
            {
                Id          = c.Id,
                Symbol      = c.Symbol,
                AssetClass  = c.AssetClass,
                Role        = c.Role,
                ScoreTotal  = c.ScoreTotal,
                MaxDrawdown = c.MaxDrawdown,
                EvolvedAt   = c.EvolvedAt
            }).ToList(),
            Tasks = dto.Tasks.Select(t => new EvolutionTaskRow
            {
                Id          = t.Id,
                Symbol      = t.Symbol,
                AssetClass  = t.AssetClass,
                Status      = t.Status,
                Progress    = t.Progress,
                CreatedAt   = t.CreatedAt,
                CompletedAt = t.CompletedAt
            }).ToList()
        };
    }
}
