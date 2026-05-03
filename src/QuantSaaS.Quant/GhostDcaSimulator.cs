using System;
using System.Collections.Generic;

namespace QuantSaaS.Quant;

public record GhostDcaConfig(decimal InitialCapital, decimal MonthlyInject);

public record GhostDcaResult
{
    public decimal FinalEquity { get; init; }
    public decimal TotalInjected { get; init; }
    public decimal MaxDrawdown { get; init; }
    public decimal Roi { get; init; }
    public decimal[] NavCurve { get; init; } = Array.Empty<decimal>();
}

/// <summary>
/// Passive DCA benchmark: buy all initial capital at start,
/// then inject MonthlyInject at every calendar month boundary.
/// Uses Modified Dietz ROI (no timing distortion from injections).
/// </summary>
public static class GhostDcaSimulator
{
    public static GhostDcaResult Simulate(decimal[] closes, long[] timestamps, GhostDcaConfig config)
    {
        if (closes == null || closes.Length == 0 || closes[0] == 0)
            return new GhostDcaResult { FinalEquity = config.InitialCapital, TotalInjected = config.InitialCapital };

        var btcHeld = config.InitialCapital / closes[0];
        var totalInjected = config.InitialCapital;
        var navCurve = new List<decimal> { config.InitialCapital };
        var cashFlows = new List<(decimal amount, int barIdx)>();

        var lastMonth = DateTimeOffset.FromUnixTimeMilliseconds(timestamps[0]).UtcDateTime.Month;
        var lastYear = DateTimeOffset.FromUnixTimeMilliseconds(timestamps[0]).UtcDateTime.Year;

        for (int i = 1; i < closes.Length; i++)
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamps[i]).UtcDateTime;
            // Inject at new calendar month
            if (dt.Month != lastMonth || dt.Year != lastYear)
            {
                var newBtc = config.MonthlyInject / closes[i];
                btcHeld += newBtc;
                totalInjected += config.MonthlyInject;
                cashFlows.Add((config.MonthlyInject, i));
                lastMonth = dt.Month;
                lastYear = dt.Year;
            }
            navCurve.Add(btcHeld * closes[i]);
        }

        var navArr = navCurve.ToArray();
        var finalEquity = btcHeld * closes[^1];
        var maxDD = MathUtils.MaxDrawdown(navArr);
        var roi = ModifiedDietz.Calculate(navArr, cashFlows, closes.Length);

        return new GhostDcaResult
        {
            FinalEquity = finalEquity,
            TotalInjected = totalInjected,
            MaxDrawdown = maxDD,
            Roi = roi,
            NavCurve = navArr
        };
    }
}
