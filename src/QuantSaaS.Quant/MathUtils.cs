using System;

namespace QuantSaaS.Quant;

/// <summary>Pure math utilities. All price calculations must be dimensionless.</summary>
public static class MathUtils
{
    /// <summary>
    /// Exponential Moving Average (most recent value).
    /// </summary>
    public static decimal Ema(decimal[] values, int period)
    {
        if (values == null || values.Length == 0) return 0;
        if (period <= 1) return values[^1];
        var k = 2m / (period + 1);
        var ema = values[0];
        for (int i = 1; i < values.Length; i++)
            ema = values[i] * k + ema * (1 - k);
        return ema;
    }

    /// <summary>
    /// Sample standard deviation over last 'period' values.
    /// </summary>
    public static decimal StdDev(decimal[] values, int period)
    {
        if (values == null || values.Length < 2) return 0;
        int start = Math.Max(0, values.Length - period);
        int count = values.Length - start;
        if (count < 2) return 0;
        decimal mean = 0;
        for (int i = start; i < values.Length; i++) mean += values[i];
        mean /= count;
        decimal variance = 0;
        for (int i = start; i < values.Length; i++)
            variance += (values[i] - mean) * (values[i] - mean);
        return (decimal)Math.Sqrt((double)(variance / (count - 1)));
    }

    /// <summary>
    /// Mean Absolute Change: average of |Close[i] - Close[i-1]| over last 'window' bars.
    /// NOT ATR - uses close prices only. Dimensionless if used as ratio.
    /// </summary>
    public static decimal MavAbsChange(decimal[] closes, int window)
    {
        if (closes == null || closes.Length < 2) return 0;
        if (window < 2) window = 2;
        int start = Math.Max(1, closes.Length - window + 1);
        int count = closes.Length - start;
        if (count <= 0) return 0;
        decimal sumAbs = 0;
        for (int i = start; i < closes.Length; i++)
            sumAbs += Math.Abs(closes[i] - closes[i - 1]);
        return sumAbs / count;
    }

    /// <summary>
    /// Volatility ratio: clip(MAV_short / MAV_long, 0.1, 3.0)
    /// </summary>
    public static decimal VolatilityRatio(decimal[] closes, int shortBars = 16, int longBars = 112)
    {
        if (closes == null || closes.Length < longBars) return 1.0m;
        var mavShort = MavAbsChange(closes, shortBars);
        var mavLong = MavAbsChange(closes, longBars);
        if (mavLong == 0) return 1.0m;
        return Math.Clamp(mavShort / mavLong, 0.1m, 3.0m);
    }

    public static decimal ClipFloat(decimal value, decimal lo, decimal hi) => Math.Clamp(value, lo, hi);
    public static decimal RoundUsdt(decimal value) => Math.Round(value, 2);

    /// <summary>
    /// Calculates maximum peak-to-trough drawdown from NAV curve.
    /// </summary>
    public static decimal MaxDrawdown(decimal[] navCurve)
    {
        if (navCurve == null || navCurve.Length == 0) return 0;
        decimal peak = navCurve[0], maxDD = 0;
        foreach (var v in navCurve)
        {
            if (v > peak) peak = v;
            if (peak > 0)
            {
                var dd = (peak - v) / peak;
                if (dd > maxDD) maxDD = dd;
            }
        }
        return maxDD;
    }
}
