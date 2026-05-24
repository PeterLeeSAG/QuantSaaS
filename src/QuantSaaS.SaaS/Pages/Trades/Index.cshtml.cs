using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.Services;
using QuantSaaS.SaaS.ViewModels;

namespace QuantSaaS.SaaS.Pages.Trades;

public class IndexModel : PageModel
{
    private readonly ITradeService _trades;

    public IndexModel(ITradeService trades) => _trades = trades;

    public TradesViewModel Vm { get; private set; } = new();

    public async Task OnGetAsync(
        [FromQuery] string? symbol,
        [FromQuery] string? action,
        [FromQuery] int page = 1)
    {
        var userId = DbInitializer.SeedUserId;
        var result = await _trades.GetPagedAsync(userId, symbol, action, page);

        Vm = new TradesViewModel
        {
            Trades = result.Trades.Select(t => new TradeRow
            {
                Symbol      = t.Symbol,
                AssetClass  = t.AssetClass,
                Action      = t.Action,
                FilledQty   = t.FilledQty,
                FilledPrice = t.FilledPrice,
                Fee         = t.Fee,
                Status      = t.Status,
                FilledAt    = t.FilledAt
            }).ToList(),
            Page          = result.Page,
            TotalPages    = result.TotalPages,
            TotalCount    = result.TotalCount,
            SymbolFilter  = symbol,
            ActionFilter  = action
        };
    }
}
