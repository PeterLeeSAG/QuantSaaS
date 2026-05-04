using System;
using System.Collections.Generic;

namespace QuantSaaS.Quant;

/// <summary>
/// Modified Dietz return calculation.
/// Strips the effect of external cash-flow timing from NAV changes so that
/// strategy ROI is comparable to Ghost DCA ROI on equal footing.
/// </summary>
public static class ModifiedDietz
{
    /// <summary>
    /// Computes the Modified Dietz return given a NAV curve and external cash flows.
    /// </summary>
    /// <param name="navCurve">Equity values after each bar (length = total evaluated bars).</param>
    /// <param name="cashFlows">
    ///   List of (amount, barIndex).
    ///   amount &gt; 0 = injection, amount &lt; 0 = withdrawal.
    /// </param>
    /// <returns>Modified Dietz return as a fraction (e.g., 0.15 = 15%).</returns>
    public static decimal Calculate(
        decimal[] navCurve,
        List<(decimal amount, int barIndex)> cashFlows)
    {
        if (navCurve is null || navCurve.Length < 2) return 0m;

        int totalBars = navCurve.Length - 1;
        decimal startEquity = navCurve[0];
        decimal endEquity = navCurve[^1];

        decimal sumFlows = 0m;
        decimal weightedFlows = 0m;

        foreach (var (amount, barIdx) in cashFlows)
        {
            sumFlows += amount;
            decimal weight = totalBars > 0
                ? Math.Max(0m, (decimal)(totalBars - barIdx) / totalBars)
                : 0m;
            weightedFlows += amount * weight;
        }

        decimal denominator = startEquity + weightedFlows;
        if (denominator == 0m) return 0m;

        return (endEquity - startEquity - sumFlows) / denominator;
    }

    /// <summary>
    /// Peak-to-trough maximum relative drawdown of a NAV curve.
    /// Returns 0 if the curve has fewer than 2 points.
    /// </summary>
    public static decimal MaxDrawdown(decimal[] navCurve)
    {
        if (navCurve is null || navCurve.Length < 2) return 0m;

        decimal peak = navCurve[0];
        decimal maxDD = 0m;

        foreach (var nav in navCurve)
        {
            if (nav > peak) peak = nav;
            if (peak > 0)
            {
                decimal dd = (peak - nav) / peak;
                if (dd > maxDD) maxDD = dd;
            }
        }
        return maxDD;
    }
}
