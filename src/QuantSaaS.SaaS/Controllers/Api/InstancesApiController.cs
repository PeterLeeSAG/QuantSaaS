using Microsoft.AspNetCore.Mvc;
using QuantSaaS.Core.Models;

namespace QuantSaaS.SaaS.Controllers.Api;

[ApiController]
[Route("api/instances")]
public class InstancesApiController : ControllerBase
{
    private static readonly object[] StubInstances =
    [
        new { id = Guid.NewGuid(), symbol = "BTC/USDT", assetClass = "Crypto", brokerType = "okx",    status = "running", equity = 18_420.50 },
        new { id = Guid.NewGuid(), symbol = "ETH/USDT", assetClass = "Crypto", brokerType = "okx",    status = "running", equity = 9_881.00  },
        new { id = Guid.NewGuid(), symbol = "AAPL",     assetClass = "Stock",  brokerType = "alpaca", status = "stopped", equity = 12_340.00 },
        new { id = Guid.NewGuid(), symbol = "SPY",      assetClass = "ETF",    brokerType = "ibkr",   status = "running", equity = 9_960.00  },
        new { id = Guid.NewGuid(), symbol = "MSFT",     assetClass = "Stock",  brokerType = "alpaca", status = "error",   equity = 7_200.00  },
    ];

    [HttpGet]
    public IActionResult GetAll() => Ok(StubInstances);

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return Ok(new
        {
            id,
            symbol = "BTC/USDT",
            assetClass = "Crypto",
            brokerType = "okx",
            status = "running",
            totalEquity = 18_420.50,
            availableFunds = 4_820.30,
            longTermHoldingsQty = 0.15423,
            activePositionQty = 0.03201,
            sealedQty = 0.05000
        });
    }
}
