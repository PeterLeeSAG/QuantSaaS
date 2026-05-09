using Microsoft.AspNetCore.Mvc.RazorPages;
using QuantSaaS.Core.Models;
using QuantSaaS.SaaS.ViewModels;

namespace QuantSaaS.SaaS.Pages;

public class IndexModel : PageModel
{
    public DashboardViewModel Vm { get; private set; } = new();

    public void OnGet()
    {
        var rng = new Random(42);
        var base_ = 50_000m;
        var curve = new List<decimal>();
        var labels = new List<string>();

        for (int i = 29; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddDays(-i);
            labels.Add(date.ToString("MMM dd"));
            base_ += (decimal)(rng.NextDouble() * 400 - 150);
            curve.Add(Math.Max(base_, 0));
        }

        var instances = new List<InstanceSummary>
        {
            new() { Id = Guid.NewGuid(), Symbol = "BTC/USDT", AssetClass = AssetClass.Crypto, Status = "running", Equity = 18_420.50m, LastTickAt = DateTime.UtcNow.AddMinutes(-3) },
            new() { Id = Guid.NewGuid(), Symbol = "ETH/USDT", AssetClass = AssetClass.Crypto, Status = "running", Equity = 9_881.00m, LastTickAt = DateTime.UtcNow.AddMinutes(-3) },
            new() { Id = Guid.NewGuid(), Symbol = "AAPL",     AssetClass = AssetClass.Stock,  Status = "stopped", Equity = 12_340.00m, LastTickAt = DateTime.UtcNow.AddHours(-2) },
            new() { Id = Guid.NewGuid(), Symbol = "SPY",      AssetClass = AssetClass.ETF,    Status = "running", Equity = 9_960.00m,  LastTickAt = DateTime.UtcNow.AddMinutes(-5) },
            new() { Id = Guid.NewGuid(), Symbol = "MSFT",     AssetClass = AssetClass.Stock,  Status = "error",   Equity = 7_200.00m,  LastTickAt = DateTime.UtcNow.AddHours(-1) },
        };

        Vm = new DashboardViewModel
        {
            TotalEquity = instances.Sum(i => i.Equity),
            ActiveInstances = instances.Count(i => i.Status == "running"),
            TodayTrades = 14,
            AvailableFunds = 4_820.30m,
            EquityCurve = curve,
            EquityLabels = labels,
            Instances = instances
        };
    }
}
