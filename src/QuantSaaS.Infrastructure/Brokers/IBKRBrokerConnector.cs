using System.Threading;
using System.Threading.Tasks;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Infrastructure.Brokers;

/// <summary>
/// IBKR (Interactive Brokers) broker connector stub.
/// Implements IBrokerConnector for US stocks/ETFs via the IBKR Client Portal API.
/// Iron Rule: API credentials NEVER leave LocalAgent.
/// </summary>
public sealed class IBKRBrokerConnector : IBrokerConnector
{
    // IBKR Client Portal Gateway runs locally (usually at https://localhost:5000)
    private readonly string _gatewayUrl;
    private readonly System.Net.Http.HttpClient _http;

    public string ConnectorId => "ibkr";

    public IBKRBrokerConnector(string gatewayUrl = "https://localhost:5000")
    {
        _gatewayUrl = gatewayUrl;
        _http = new System.Net.Http.HttpClient();
        _http.BaseAddress = new System.Uri(gatewayUrl);
        // IBKR Client Portal uses session-based auth; credentials entered in the gateway UI
        _http.DefaultRequestHeaders.Add("User-Agent", "QuantSaaS-Agent/1.0");
    }

    public async Task<OrderResult> PlaceOrderAsync(TradeCommand command, CancellationToken ct = default)
    {
        // IBKR Client Portal API order placement
        // POST /v1/api/iserver/account/{accountId}/orders
        // Full implementation would require IBKR account ID and order format mapping
        await Task.CompletedTask;
        return new OrderResult
        {
            ClientOrderId = command.ClientOrderId,
            Status = "submitted",
            ErrorMessage = "IBKR connector: not yet fully implemented – requires gateway authentication.",
        };
    }

    public async Task<DeltaReport> GetBalancesAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return new DeltaReport();
    }

    public async Task<OrderResult> GetOrderStatusAsync(string clientOrderId, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return new OrderResult { ClientOrderId = clientOrderId, Status = "unknown" };
    }
}
