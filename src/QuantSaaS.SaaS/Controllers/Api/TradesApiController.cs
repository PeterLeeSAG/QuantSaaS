using Microsoft.AspNetCore.Mvc;
using QuantSaaS.Core.Models;

namespace QuantSaaS.SaaS.Controllers.Api;

[ApiController]
[Route("api/trades")]
public class TradesApiController : ControllerBase
{
    [HttpGet]
    public IActionResult GetTrades([FromQuery] string? symbol, [FromQuery] int page = 1)
    {
        const int pageSize = 25;
        var rng = new Random(7);
        var symbols = new[] { ("BTC/USDT", "Crypto"), ("ETH/USDT", "Crypto"), ("AAPL", "Stock"), ("SPY", "ETF") };

        var all = Enumerable.Range(0, 120).Select(i =>
        {
            var (sym, cls) = symbols[i % symbols.Length];
            return new
            {
                symbol = sym,
                assetClass = cls,
                action = i % 3 == 0 ? "SELL" : "BUY",
                filledQty = Math.Round(rng.NextDouble() * 0.5 + 0.001, 5),
                filledPrice = Math.Round(cls == "Crypto" ? 60_000 + rng.NextDouble() * 3000 - 1500 : 150 + rng.NextDouble() * 50, 2),
                fee = Math.Round(rng.NextDouble() * 1.5, 4),
                status = "filled",
                filledAt = DateTime.UtcNow.AddHours(-i * 4)
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(symbol))
            all = all.Where(t => t.symbol.Contains(symbol, StringComparison.OrdinalIgnoreCase)).ToList();

        var total = all.Count;
        var pages = (int)Math.Ceiling(total / (double)pageSize);
        page = Math.Clamp(page, 1, Math.Max(1, pages));

        return Ok(new
        {
            page,
            totalPages = pages,
            totalCount = total,
            trades = all.Skip((page - 1) * pageSize).Take(pageSize)
        });
    }
}
