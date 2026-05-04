using System;

namespace QuantSaaS.Quant;

/// <summary>
/// Pure mathematical utilities for strategy signal computation.
/// Iron Rule: All price-related calculations must be dimensionless (ratios / log-returns).
/// </summary>
public static class MathUtils
{
    /// <summary>
    /// Exponential moving average of the last <paramref name="period"/> values.
    /// Returns the single latest EMA value.
    /// </summary>
    public static decimal Ema(decimal[] series, int period)
    {
        if (series is null || series.Length == 0) return 0m;
        if (period <= 0) throw new ArgumentOutOfRangeException(nameof(period));

        decimal k = 2m / (period + 1);
        decimal ema = series[0];
        for (int i = 1; i < series.Length; i++)
            ema = series[i] * k + ema * (1 - k);
        return ema;
    }

    /// <summary>
    /// Sample standard deviation of the last <paramref name="period"/> values.
    /// Returns 0 when there are fewer than 2 data points.
    /// </summary>
    public static decimal StdDev(decimal[] series, int period)
    {
        if (series is null || series.Length < 2) return 0m;
        int count = Math.Min(period, series.Length);
        int start = series.Length - count;

        decimal sum = 0m;
        for (int i = start; i < series.Length; i++) sum += series[i];
        decimal mean = sum / count;

        decimal variance = 0m;
        for (int i = start; i < series.Length; i++)
        {
            decimal d = series[i] - mean;
            variance += d * d;
        }
        return (decimal)Math.Sqrt((double)(variance / (count - 1)));
    }

    /// <summary>
    /// Mean Absolute change of consecutive closing prices over <paramref name="window"/> bars.
    /// Formula: Σ|close[i] - close[i-1]| / (L-1)  for the last L bars.
    /// NOT ATR – does not use High/Low, only Close.
    /// </summary>
    public static decimal MavAbsChange(decimal[] closes, int window)
    {
        if (closes is null || closes.Length < 2) return 0m;
        int L = Math.Min(window, closes.Length);
        int start = closes.Length - L;
        decimal sum = 0m;
        for (int i = start + 1; i < closes.Length; i++)
            sum += Math.Abs(closes[i] - closes[i - 1]);
        return sum / (L - 1);
    }

    /// <summary>
    /// Volatility ratio for wedge-filter: clip(MAV_short / MAV_long, 0.1, 3.0).
    /// Returns 1.0 when there is insufficient data.
    /// </summary>
    public static decimal VolatilityRatio(decimal[] closes, int shortBars = 16, int longBars = 112)
    {
        if (closes is null || closes.Length < longBars) return 1.0m;
        decimal mavLong = MavAbsChange(closes, longBars);
        if (mavLong == 0) return 1.0m;
        decimal ratio = MavAbsChange(closes, shortBars) / mavLong;
        return Math.Clamp(ratio, 0.1m, 3.0m);
    }

    /// <summary>Clamps a decimal value to [lo, hi].</summary>
    public static decimal Clip(decimal value, decimal lo, decimal hi) => Math.Clamp(value, lo, hi);

    /// <summary>Round to 2 decimal places (quote currency precision).</summary>
    public static decimal RoundQuote(decimal value) => Math.Round(value, 2);

    /// <summary>Log-return: ln(p_current / p_prev). Dimensionless price change.</summary>
    public static double LogReturn(decimal prev, decimal current)
    {
        if (prev <= 0 || current <= 0) return 0.0;
        return Math.Log((double)(current / prev));
    }
}
