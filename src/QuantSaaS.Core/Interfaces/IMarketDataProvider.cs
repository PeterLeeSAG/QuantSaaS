using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Core.Interfaces;

/// <summary>
/// Market data provider interface.
/// Provider selection is resolved at runtime from Instrument.Exchange via a registry;
/// strategy code never knows which provider is active.
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>Identifier for this provider (e.g., "alpaca", "yahoo", "okx").</summary>
    string ProviderId { get; }

    /// <summary>
    /// Fetches historical OHLCV bars for the given symbol and timeframe.
    /// </summary>
    Task<IReadOnlyList<Bar>> FetchBarsAsync(
        string symbol,
        string timeframe,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);

    /// <summary>Fetches the most recently completed bar (used in live tick).</summary>
    Task<Bar?> FetchLatestBarAsync(string symbol, string timeframe, CancellationToken ct = default);
}

/// <summary>
/// A single OHLCV candlestick bar.
/// </summary>
public record Bar
{
    public long OpenTimeMs { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
}
