namespace QuantSaaS.Core.Models;

/// <summary>
/// Classifies the type of financial instrument.
/// </summary>
public enum AssetClass
{
    /// <summary>Cryptocurrency spot pair (e.g., BTC/USDT). 24/7 trading, no settlement delay.</summary>
    Crypto,

    /// <summary>Exchange-listed stock (e.g., AAPL, MSFT). NYSE/NASDAQ hours, T+2 settlement.</summary>
    Stock,

    /// <summary>Exchange-traded fund (e.g., SPY, QQQ). NYSE/NASDAQ hours, T+2 settlement.</summary>
    ETF
}
