using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QuantSaaS.Core.Models;
using QuantSaaS.SaaS.ViewModels;

namespace QuantSaaS.SaaS.Pages.Instances;

public class DetailModel : PageModel
{
    public InstanceDetailViewModel Vm { get; private set; } = new();

    public IActionResult OnGet(Guid id)
    {
        var rng = new Random(id.GetHashCode());
        var base_ = 15_000m + (decimal)(rng.NextDouble() * 5000);
        var curve = new List<decimal>();
        var labels = new List<string>();

        for (int i = 29; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddDays(-i);
            labels.Add(date.ToString("MMM dd"));
            base_ += (decimal)(rng.NextDouble() * 200 - 80);
            curve.Add(Math.Max(base_, 0));
        }

        var trades = Enumerable.Range(0, 20).Select(n => new TradeRow
        {
            Symbol = "BTC/USDT",
            AssetClass = AssetClass.Crypto,
            Action = n % 3 == 0 ? "SELL" : "BUY",
            FilledQty = Math.Round((decimal)(rng.NextDouble() * 0.05 + 0.001), 5),
            FilledPrice = Math.Round(60_000m + (decimal)(rng.NextDouble() * 2000 - 1000), 2),
            Fee = Math.Round((decimal)(rng.NextDouble() * 0.5), 4),
            Status = "filled",
            FilledAt = DateTime.UtcNow.AddHours(-n * 2)
        }).ToList();

        Vm = new InstanceDetailViewModel
        {
            Id = id,
            Symbol = "BTC/USDT",
            AssetClass = AssetClass.Crypto,
            BrokerType = "okx",
            Status = "running",
            CreatedAt = DateTime.UtcNow.AddDays(-45),
            LastTickAt = DateTime.UtcNow.AddMinutes(-3),
            TotalEquity = curve.Last(),
            AvailableFunds = 4_820.30m,
            LongTermHoldingsQty = 0.15423m,
            ActivePositionQty = 0.03201m,
            SealedQty = 0.05000m,
            PendingSettlementAmount = 0m,
            EquityCurve = curve,
            EquityLabels = labels,
            RecentTrades = trades,
            PendingSettlements = []
        };

        return Page();
    }
}
