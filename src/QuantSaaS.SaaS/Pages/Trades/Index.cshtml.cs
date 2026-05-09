using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QuantSaaS.Core.Models;
using QuantSaaS.SaaS.ViewModels;

namespace QuantSaaS.SaaS.Pages.Trades;

public class IndexModel : PageModel
{
    private const int PageSize = 25;

    public TradesViewModel Vm { get; private set; } = new();

    public void OnGet([FromQuery] string? symbol, [FromQuery] string? action, [FromQuery] int page = 1)
    {
        var rng = new Random(7);
        var symbols = new[] { ("BTC/USDT", AssetClass.Crypto), ("ETH/USDT", AssetClass.Crypto), ("AAPL", AssetClass.Stock), ("SPY", AssetClass.ETF), ("MSFT", AssetClass.Stock) };

        var all = Enumerable.Range(0, 120).Select(i =>
        {
            var (sym, cls) = symbols[i % symbols.Length];
            return new TradeRow
            {
                Symbol = sym,
                AssetClass = cls,
                Action = i % 3 == 0 ? "SELL" : "BUY",
                FilledQty = Math.Round((decimal)(rng.NextDouble() * 0.5 + 0.001), 5),
                FilledPrice = cls == AssetClass.Crypto
                    ? Math.Round(60_000m + (decimal)(rng.NextDouble() * 3000 - 1500), 2)
                    : Math.Round(150m + (decimal)(rng.NextDouble() * 50 - 25), 2),
                Fee = Math.Round((decimal)(rng.NextDouble() * 1.5), 4),
                Status = "filled",
                FilledAt = DateTime.UtcNow.AddHours(-i * 4)
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(symbol))
            all = all.Where(t => t.Symbol.Contains(symbol, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(action))
            all = all.Where(t => t.Action.Equals(action, StringComparison.OrdinalIgnoreCase)).ToList();

        var total = all.Count;
        var pages = (int)Math.Ceiling(total / (double)PageSize);
        page = Math.Clamp(page, 1, Math.Max(1, pages));
        var paged = all.Skip((page - 1) * PageSize).Take(PageSize).ToList();

        Vm = new TradesViewModel
        {
            Trades = paged,
            Page = page,
            TotalPages = pages,
            TotalCount = total,
            SymbolFilter = symbol,
            ActionFilter = action
        };
    }
}
