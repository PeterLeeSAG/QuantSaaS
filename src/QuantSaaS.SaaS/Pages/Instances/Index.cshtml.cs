using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QuantSaaS.Core.Models;
using QuantSaaS.SaaS.ViewModels;

namespace QuantSaaS.SaaS.Pages.Instances;

public class IndexModel : PageModel
{
    public InstanceListViewModel Vm { get; private set; } = new();

    public void OnGet([FromQuery] string tab = "All")
    {
        var all = new List<InstanceRow>
        {
            new() { Id = Guid.NewGuid(), Symbol = "BTC/USDT", AssetClass = AssetClass.Crypto, BrokerType = "okx",    Status = "running", Equity = 18_420.50m, LastTickAt = DateTime.UtcNow.AddMinutes(-3) },
            new() { Id = Guid.NewGuid(), Symbol = "ETH/USDT", AssetClass = AssetClass.Crypto, BrokerType = "okx",    Status = "running", Equity = 9_881.00m,  LastTickAt = DateTime.UtcNow.AddMinutes(-3) },
            new() { Id = Guid.NewGuid(), Symbol = "SOL/USDT", AssetClass = AssetClass.Crypto, BrokerType = "okx",    Status = "stopped", Equity = 3_100.00m,  LastTickAt = DateTime.UtcNow.AddHours(-6) },
            new() { Id = Guid.NewGuid(), Symbol = "AAPL",     AssetClass = AssetClass.Stock,  BrokerType = "alpaca", Status = "stopped", Equity = 12_340.00m, LastTickAt = DateTime.UtcNow.AddHours(-2) },
            new() { Id = Guid.NewGuid(), Symbol = "MSFT",     AssetClass = AssetClass.Stock,  BrokerType = "alpaca", Status = "error",   Equity = 7_200.00m,  LastTickAt = DateTime.UtcNow.AddHours(-1) },
            new() { Id = Guid.NewGuid(), Symbol = "SPY",      AssetClass = AssetClass.ETF,    BrokerType = "ibkr",   Status = "running", Equity = 9_960.00m,  LastTickAt = DateTime.UtcNow.AddMinutes(-5) },
            new() { Id = Guid.NewGuid(), Symbol = "QQQ",      AssetClass = AssetClass.ETF,    BrokerType = "ibkr",   Status = "stopped", Equity = 5_400.00m,  LastTickAt = DateTime.UtcNow.AddDays(-1) },
        };

        var filtered = tab switch
        {
            "Crypto" => all.Where(r => r.AssetClass == AssetClass.Crypto).ToList(),
            "Stock"  => all.Where(r => r.AssetClass == AssetClass.Stock).ToList(),
            "ETF"    => all.Where(r => r.AssetClass == AssetClass.ETF).ToList(),
            _        => all
        };

        Vm = new InstanceListViewModel { Instances = filtered, ActiveTab = tab };
    }
}
