using Microsoft.AspNetCore.Mvc.RazorPages;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.Services;
using QuantSaaS.SaaS.ViewModels;

namespace QuantSaaS.SaaS.Pages;

public class IndexModel : PageModel
{
    private readonly IDashboardService _dashboard;

    public IndexModel(IDashboardService dashboard) => _dashboard = dashboard;

    public DashboardViewModel Vm { get; private set; } = new();

    public async Task OnGetAsync()
    {
        // TODO: replace with real session user once auth is implemented.
        var userId = DbInitializer.SeedUserId;
        var dto = await _dashboard.GetSummaryAsync(userId);

        Vm = new DashboardViewModel
        {
            TotalEquity     = dto.TotalEquity,
            ActiveInstances = dto.ActiveInstances,
            TodayTrades     = dto.TodayTrades,
            AvailableFunds  = dto.AvailableFunds,
            EquityCurve     = dto.EquityCurve.ToList(),
            EquityLabels    = dto.EquityLabels.ToList(),
            Instances       = dto.Instances.Select(i => new InstanceSummary
            {
                Id          = i.Id,
                Symbol      = i.Symbol,
                AssetClass  = i.AssetClass,
                Status      = i.Status,
                Equity      = i.Equity,
                LastTickAt  = i.LastTickAt
            }).ToList()
        };
    }
}
