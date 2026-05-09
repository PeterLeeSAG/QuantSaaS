using Microsoft.AspNetCore.Mvc;
using QuantSaaS.Core.Models;

namespace QuantSaaS.SaaS.Controllers.Api;

[ApiController]
[Route("api/dashboard")]
public class DashboardApiController : ControllerBase
{
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        return Ok(new
        {
            totalEquity = 57_801.50,
            activeInstances = 3,
            todayTrades = 14,
            availableFunds = 4_820.30,
            generatedAt = DateTime.UtcNow
        });
    }
}
