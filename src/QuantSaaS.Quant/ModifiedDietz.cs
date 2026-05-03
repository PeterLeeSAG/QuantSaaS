using System;
using System.Collections.Generic;

namespace QuantSaaS.Quant;

/// <summary>
/// Modified Dietz ROI: removes cash flow timing distortion.
/// Must be used for BOTH strategy and GhostDCA backtests.
/// Formula: (End - Start - SumFlows) / (Start + Σ(Flow_i × Weight_i))
/// Weight_i = (TotalBars - BarIndex) / TotalBars
/// </summary>
public static class ModifiedDietz
{
    public static decimal Calculate(
        decimal[] navCurve,
        List<(decimal amount, int barIndex)> cashFlows,
        int totalBars)
    {
        if (navCurve == null || navCurve.Length < 2 || totalBars <= 0) return 0m;

        decimal startEquity = navCurve[0];
        decimal endEquity = navCurve[^1];
        decimal sumFlows = 0m;
        decimal weightedFlows = 0m;

        foreach (var (amount, barIdx) in cashFlows)
        {
            sumFlows += amount;
            decimal weight = totalBars > barIdx
                ? (decimal)(totalBars - barIdx) / totalBars
                : 0m;
            weightedFlows += amount * weight;
        }

        decimal denominator = startEquity + weightedFlows;
        if (denominator == 0) return 0m;
        return (endEquity - startEquity - sumFlows) / denominator;
    }
}
