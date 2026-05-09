using Microsoft.AspNetCore.Mvc;

namespace QuantSaaS.SaaS.Controllers.Api;

[ApiController]
[Route("api/evolution")]
public class EvolutionApiController : ControllerBase
{
    [HttpGet("tasks")]
    public IActionResult GetTasks()
    {
        return Ok(new[]
        {
            new { id = Guid.NewGuid(), symbol = "BTC/USDT", assetClass = "Crypto", status = "completed", progress = 100, createdAt = DateTime.UtcNow.AddDays(-5) },
            new { id = Guid.NewGuid(), symbol = "SOL/USDT", assetClass = "Crypto", status = "running",   progress = 63,  createdAt = DateTime.UtcNow.AddHours(-2) },
            new { id = Guid.NewGuid(), symbol = "AAPL",     assetClass = "Stock",  status = "pending",   progress = 0,   createdAt = DateTime.UtcNow.AddMinutes(-30) },
        });
    }
}
