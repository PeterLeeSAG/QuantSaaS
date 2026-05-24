using System.Threading;
using System.Threading.Tasks;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Core.Interfaces;

/// <summary>
/// Broker connector interface.
/// Generalises the earlier crypto-only IExchangeConnector to support
/// any broker (crypto, Alpaca, IBKR, etc.).
/// Iron Rule: API credentials are NEVER passed through this interface to SaaS.
/// Only LocalAgent implementations hold credentials in config.agent.yaml.
/// </summary>
public interface IBrokerConnector
{
    /// <summary>Identifier for this connector (e.g., "okx", "alpaca", "ibkr").</summary>
    string ConnectorId { get; }

    /// <summary>Places an order described by the TradeCommand.</summary>
    Task<OrderResult> PlaceOrderAsync(TradeCommand command, CancellationToken ct = default);

    /// <summary>Returns current balances for all assets in the account.</summary>
    Task<DeltaReport> GetBalancesAsync(CancellationToken ct = default);

    /// <summary>Returns the current fill status of a previously placed order.</summary>
    Task<OrderResult> GetOrderStatusAsync(string clientOrderId, CancellationToken ct = default);
}

public record OrderResult
{
    public string ClientOrderId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal FilledQty { get; init; }
    public decimal FilledPrice { get; init; }
    public decimal Fee { get; init; }
    public string? ErrorMessage { get; init; }
}
