using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.Services;
using QuantSaaS.SaaS.ViewModels;

namespace QuantSaaS.SaaS.Pages.Instances;

public class IndexModel : PageModel
{
    private readonly IInstanceService _instances;

    public IndexModel(IInstanceService instances) => _instances = instances;

    public InstanceListViewModel Vm { get; private set; } = new();

    public async Task OnGetAsync([FromQuery] string tab = "All")
    {
        var userId = DbInitializer.SeedUserId;
        var filter = tab == "All" ? null : tab;
        var rows = await _instances.GetListAsync(userId, filter);

        Vm = new InstanceListViewModel
        {
            ActiveTab = tab,
            Instances = rows.Select(r => new InstanceRow
            {
                Id          = r.Id,
                Symbol      = r.Symbol,
                AssetClass  = r.AssetClass,
                BrokerType  = r.BrokerType,
                Status      = r.Status,
                Equity      = r.Equity,
                LastTickAt  = r.LastTickAt
            }).ToList()
        };
    }
}
