using System;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;
using QuantSaaS.Quant;

namespace QuantSaaS.Strategy.Stock;

/// <summary>
/// Anti-Corruption Layer adapter for stock/ETF strategies.
/// Converts raw OHLCV bars into the dimensionless arrays that Step() receives,
/// and computes market context (session fraction, normalized earnings distance).
///
/// Iron Rule: All calendar-aware computations happen here, NEVER inside Step().
/// </summary>
public sealed class StockAcl
{
    private readonly IMarketCalendar _calendar;

    public StockAcl(IMarketCalendar calendar)
    {
        _calendar = calendar;
    }

    /// <summary>
    /// Converts raw bars into a StrategyInput for a stock/ETF strategy.
    /// </summary>
    public StrategyInput BuildInput(
        Bar[] allBars,
        Instrument instrument,
        PortfolioState portfolio,
        MarketState marketState,
        string runtimeStateJson,
        DateTime? nextEarningsDateUtc = null)
    {
        if (allBars is null || allBars.Length == 0)
            throw new ArgumentException("Bar sequence cannot be empty.", nameof(allBars));

        // ── ACL: OHLCV → dimensionless arrays ────────────────────────────────
        decimal[] closes = new decimal[allBars.Length];
        long[] timestamps = new long[allBars.Length];
        for (int i = 0; i < allBars.Length; i++)
        {
            closes[i] = allBars[i].Close;
            timestamps[i] = allBars[i].OpenTimeMs;
        }

        decimal currentPrice = closes[^1];
        DateTime latestBarUtc = DateTimeOffset.FromUnixTimeMilliseconds(timestamps[^1]).UtcDateTime;

        // ── Session fraction (dimensionless ratio) ────────────────────────────
        decimal sessionFraction = ComputeSessionFraction(latestBarUtc);

        // ── Normalized days to earnings ────────────────────────────────────────
        decimal normalizedEarnings = ComputeNormalizedDaysToEarnings(latestBarUtc, nextEarningsDateUtc);

        return new StrategyInput
        {
            Closes = closes,
            Timestamps = timestamps,
            CurrentPrice = currentPrice,
            Instrument = instrument,
            Portfolio = portfolio,
            Market = marketState,
            SessionFractionElapsed = sessionFraction,
            NormalizedDaysToEarnings = normalizedEarnings,
            RuntimeStateJson = runtimeStateJson,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private decimal ComputeSessionFraction(DateTime utc)
    {
        // Compute how far through the current session we are [0, 1]
        // Uses Eastern Time business hours 09:30–16:00
        try
        {
            var et = TimeZoneInfo.ConvertTimeFromUtc(
                utc,
                TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));

            var open = et.Date.AddHours(9).AddMinutes(30);
            var close = et.Date.AddHours(16);
            double totalMinutes = (close - open).TotalMinutes;
            double elapsed = (et - open).TotalMinutes;
            return (decimal)Math.Clamp(elapsed / totalMinutes, 0.0, 1.0);
        }
        catch
        {
            return 0.5m;
        }
    }

    private decimal ComputeNormalizedDaysToEarnings(
        DateTime utc,
        DateTime? nextEarningsUtc)
    {
        if (nextEarningsUtc == null) return 1.0m; // unknown → no signal

        int tradingDays = _calendar.TradingDaysInRange(utc, nextEarningsUtc.Value);
        // Normalize by 252 trading days (one year)
        return Math.Clamp((decimal)tradingDays / 252m, 0m, 1m);
    }
}
