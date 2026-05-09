using Microsoft.AspNetCore.Mvc.RazorPages;
using QuantSaaS.Core.Models;
using QuantSaaS.SaaS.ViewModels;

namespace QuantSaaS.SaaS.Pages.Lab;

public class IndexModel : PageModel
{
    public LabViewModel Vm { get; private set; } = new();

    public void OnGet()
    {
        Vm = new LabViewModel
        {
            Champions =
            [
                new GeneRow { Id = Guid.NewGuid(), Symbol = "BTC/USDT", AssetClass = AssetClass.Crypto, Role = "champion", ScoreTotal = 1.842m, MaxDrawdown = 0.083m, EvolvedAt = DateTime.UtcNow.AddDays(-5) },
                new GeneRow { Id = Guid.NewGuid(), Symbol = "ETH/USDT", AssetClass = AssetClass.Crypto, Role = "champion", ScoreTotal = 1.523m, MaxDrawdown = 0.121m, EvolvedAt = DateTime.UtcNow.AddDays(-12) },
                new GeneRow { Id = Guid.NewGuid(), Symbol = "SPY",      AssetClass = AssetClass.ETF,    Role = "champion", ScoreTotal = 0.974m, MaxDrawdown = 0.065m, EvolvedAt = DateTime.UtcNow.AddDays(-3) },
            ],
            Tasks =
            [
                new EvolutionTaskRow { Id = Guid.NewGuid(), Symbol = "BTC/USDT", AssetClass = AssetClass.Crypto, Status = "completed", Progress = 100, CreatedAt = DateTime.UtcNow.AddDays(-5), CompletedAt = DateTime.UtcNow.AddDays(-5).AddHours(3) },
                new EvolutionTaskRow { Id = Guid.NewGuid(), Symbol = "SOL/USDT", AssetClass = AssetClass.Crypto, Status = "running",   Progress = 63,  CreatedAt = DateTime.UtcNow.AddHours(-2) },
                new EvolutionTaskRow { Id = Guid.NewGuid(), Symbol = "AAPL",     AssetClass = AssetClass.Stock,  Status = "pending",   Progress = 0,   CreatedAt = DateTime.UtcNow.AddMinutes(-30) },
                new EvolutionTaskRow { Id = Guid.NewGuid(), Symbol = "ETH/USDT", AssetClass = AssetClass.Crypto, Status = "completed", Progress = 100, CreatedAt = DateTime.UtcNow.AddDays(-12), CompletedAt = DateTime.UtcNow.AddDays(-12).AddHours(4) },
                new EvolutionTaskRow { Id = Guid.NewGuid(), Symbol = "QQQ",      AssetClass = AssetClass.ETF,    Status = "failed",    Progress = 22,  CreatedAt = DateTime.UtcNow.AddDays(-2), CompletedAt = DateTime.UtcNow.AddDays(-2).AddHours(1) },
            ]
        };
    }
}
