namespace QuantSaaS.Core.Models;

/// <summary>
/// Trade command sent from SaaS to LocalAgent over WebSocket.
/// Extended to support both crypto and equity instruments.
/// </summary>
public record TradeCommand
{
    /// <summary>
    /// Globally unique client order ID.
    /// Format: inst{instanceId}-{engine}-{ts}  (e.g., inst42-MACRO-1714857600000)
    /// Used for deduplication and audit trail.
    /// </summary>
    public string ClientOrderId { get; init; } = string.Empty;

    /// <summary>BUY or SELL.</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>MACRO or MICRO – which engine layer produced this command.</summary>
    public string Engine { get; init; } = string.Empty;

    /// <summary>Instrument symbol (e.g., "BTC/USDT", "AAPL", "SPY").</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Asset class for the broker to route correctly.</summary>
    public AssetClass AssetClass { get; init; }

    /// <summary>
    /// Nominal quote-currency amount for BUY orders (e.g., 500 USDT, 1000 USD).
    /// Null for SELL orders.
    /// </summary>
    public decimal? AmountQuote { get; init; }

    /// <summary>
    /// Asset quantity for SELL orders.
    /// For crypto: fractional (e.g., 0.01 BTC).
    /// For stocks/ETFs: whole shares (unless FractionAllowed = true).
    /// Null for BUY orders.
    /// </summary>
    public decimal? QtyAsset { get; init; }

    /// <summary>Position semantic: DEAD_STACK or FLOATING.</summary>
    public string LotType { get; init; } = string.Empty;
}
