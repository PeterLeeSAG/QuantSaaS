using Microsoft.AspNetCore.Mvc;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.Services;

namespace QuantSaaS.SaaS.Controllers.Api;

[ApiController]
[Route("api/trades")]
public class TradesApiController : ControllerBase
{
    private readonly ITradeService _trades;

    public TradesApiController(ITradeService trades)
    {
        _trades = trades;
    }

    [HttpGet]
    public async Task<IActionResult> GetTrades(
        [FromQuery] Guid userId,
        [FromQuery] string? symbol,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        var effectiveUserId = userId == Guid.Empty ? DbInitializer.SeedUserId : userId;
        var result = await _trades.GetPagedAsync(effectiveUserId, symbol, action, page, ct);
        return Ok(new
        {
            page        = result.Page,
            totalPages  = result.TotalPages,
            totalCount  = result.TotalCount,
            trades      = result.Trades
        });
    }
}
