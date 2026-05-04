namespace QuantSaaS.Core.Models;

/// <summary>
/// Describes a tradable instrument. Injected into StrategyInput so Step() is instrument-aware
/// without any hard-coded asset references.
/// Iron Rule: Strategy code must never hard-code symbol names or asset-class checks.
/// </summary>
public record Instrument
{
    /// <summary>Ticker symbol, e.g. "BTC/USDT", "AAPL", "SPY".</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Asset class: Crypto, Stock, or ETF.</summary>
    public AssetClass AssetClass { get; init; }

    /// <summary>Exchange identifier, e.g. "OKX", "NYSE", "NASDAQ", "ALPACA".</summary>
    public string Exchange { get; init; } = string.Empty;

    /// <summary>Quote / settlement currency, e.g. "USDT", "USD".</summary>
    public string QuoteCurrency { get; init; } = "USD";

    /// <summary>Minimum order increment (quantity). For most US stocks = 1 share.</summary>
    public decimal LotStep { get; init; } = 1m;

    /// <summary>Minimum order quantity. For US stocks = 1 share (unless fractional).</summary>
    public decimal LotMin { get; init; } = 1m;

    /// <summary>Minimum price increment (tick size).</summary>
    public decimal TickSize { get; init; } = 0.01m;

    /// <summary>Whether the broker supports fractional share orders.</summary>
    public bool FractionAllowed { get; init; } = false;

    /// <summary>Settlement lag in business days (0 for crypto, 2 for US equities).</summary>
    public int SettlementDays { get; init; } = 0;

    // ── Convenience factories ──────────────────────────────────────────────────

    public static Instrument BtcUsdt(string exchange = "OKX") => new()
    {
        Symbol = "BTC/USDT",
        AssetClass = AssetClass.Crypto,
        Exchange = exchange,
        QuoteCurrency = "USDT",
        LotStep = 0.00001m,
        LotMin = 0.00001m,
        TickSize = 0.01m,
        FractionAllowed = true,
        SettlementDays = 0,
    };

    public static Instrument UsStock(string symbol, string exchange = "NYSE") => new()
    {
        Symbol = symbol,
        AssetClass = AssetClass.Stock,
        Exchange = exchange,
        QuoteCurrency = "USD",
        LotStep = 1m,
        LotMin = 1m,
        TickSize = 0.01m,
        FractionAllowed = false,
        SettlementDays = 2,
    };

    public static Instrument UsEtf(string symbol, string exchange = "NYSE") => new()
    {
        Symbol = symbol,
        AssetClass = AssetClass.ETF,
        Exchange = exchange,
        QuoteCurrency = "USD",
        LotStep = 1m,
        LotMin = 1m,
        TickSize = 0.01m,
        FractionAllowed = false,
        SettlementDays = 2,
    };
}
