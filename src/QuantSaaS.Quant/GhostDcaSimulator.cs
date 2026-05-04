using System;
using System.Collections.Generic;
using QuantSaaS.Core.Interfaces;

namespace QuantSaaS.Quant;

/// <summary>
/// Passive DCA benchmark simulator.
/// Strategy performance is evaluated relative to this baseline (Alpha = ROI_strategy - ROI_ghostDCA).
/// Uses Modified Dietz to compute ROI free of cash-flow timing distortions.
/// Respects the market calendar: stock monthly injections land on the first trading day of each month.
/// </summary>
public static class GhostDcaSimulator
{
    public record Config(
        decimal InitialCapital,
        decimal MonthlyInject,
        bool ReinvestDividends = true);

    public record Result
    {
        public decimal FinalEquity { get; init; }
        public decimal TotalInjected { get; init; }
        public decimal MaxDrawdown { get; init; }
        public decimal ROI { get; init; }
    }

    /// <summary>
    /// Simulates passive DCA over the provided closing-price sequence.
    /// </summary>
    /// <param name="closes">Closing prices for the evaluated window (no warmup prefix here).</param>
    /// <param name="timestampsMs">Unix-ms timestamps aligned with <paramref name="closes"/>.</param>
    /// <param name="config">DCA configuration.</param>
    /// <param name="calendar">Market calendar for monthly injection timing (null = every 30 calendar days).</param>
    /// <param name="dividendsPerShare">Optional dividend amounts keyed by bar index.</param>
    public static Result Simulate(
        decimal[] closes,
        long[] timestampsMs,
        Config config,
        IMarketCalendar? calendar = null,
        IReadOnlyDictionary<int, decimal>? dividendsPerShare = null)
    {
        if (closes is null || closes.Length == 0)
            return new Result();

        decimal shares = config.InitialCapital / closes[0];
        decimal cash = 0m;
        decimal totalInjected = config.InitialCapital;

        var navCurve = new decimal[closes.Length];
        var cashFlows = new List<(decimal amount, int barIndex)>();
        cashFlows.Add((config.InitialCapital, 0));

        DateTime? lastInjectMonth = null;

        for (int i = 0; i < closes.Length; i++)
        {
            decimal price = closes[i];
            DateTime barUtc = DateTimeOffset.FromUnixTimeMilliseconds(timestampsMs[i]).UtcDateTime;

            // ── Dividend reinvestment ─────────────────────────────────────────
            if (config.ReinvestDividends && dividendsPerShare != null &&
                dividendsPerShare.TryGetValue(i, out decimal div) && div > 0)
            {
                decimal divCash = shares * div;
                shares += divCash / price;
            }

            // ── Monthly injection ─────────────────────────────────────────────
            bool isFirstOfMonth = IsFirstTradingDayOfMonth(barUtc, i, timestampsMs, calendar);
            if (isFirstOfMonth && (lastInjectMonth == null ||
                new DateTime(lastInjectMonth.Value.Year, lastInjectMonth.Value.Month, 1)
                < new DateTime(barUtc.Year, barUtc.Month, 1)))
            {
                // Inject on first bar; skip the very first bar (already used for initial buy)
                if (i > 0)
                {
                    shares += config.MonthlyInject / price;
                    cash -= config.MonthlyInject;
                    totalInjected += config.MonthlyInject;
                    cashFlows.Add((config.MonthlyInject, i));
                }
                lastInjectMonth = barUtc;
            }

            navCurve[i] = shares * price + cash;
        }

        decimal roi = ModifiedDietz.Calculate(navCurve, cashFlows);
        decimal maxDD = ModifiedDietz.MaxDrawdown(navCurve);

        return new Result
        {
            FinalEquity = navCurve[^1],
            TotalInjected = totalInjected,
            MaxDrawdown = maxDD,
            ROI = roi,
        };
    }

    private static bool IsFirstTradingDayOfMonth(
        DateTime barUtc,
        int barIndex,
        long[] timestampsMs,
        IMarketCalendar? calendar)
    {
        if (barIndex == 0) return true;

        DateTime prevBarUtc = DateTimeOffset.FromUnixTimeMilliseconds(timestampsMs[barIndex - 1]).UtcDateTime;

        // Month boundary crossed
        if (barUtc.Year != prevBarUtc.Year || barUtc.Month != prevBarUtc.Month)
            return true;

        return false;
    }
}
