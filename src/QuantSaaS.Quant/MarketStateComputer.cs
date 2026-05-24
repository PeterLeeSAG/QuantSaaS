using QuantSaaS.Core.Models;

namespace QuantSaaS.Quant;

/// <summary>
/// Computes market state from close price series.
/// Output feeds both macro and micro engines.
/// </summary>
public static class MarketStateComputer
{
    private const int TrendEmaPeriod = 50;
    private const int VolEmaShort = 10;
    private const int VolEmaLong = 50;
    private const decimal BullThreshold = 0.02m;  // EMA ratio above 1.02 = bull
    private const decimal BearThreshold = -0.02m; // EMA ratio below 0.98 = bear
    private const decimal QuietVolRatio = 0.5m;   // VolRatio < 0.5 = quiet

    public static MarketState Compute(decimal[] closes)
    {
        if (closes == null || closes.Length < TrendEmaPeriod)
            return MarketState.Normal;

        var emaShort = MathUtils.Ema(closes, 20);
        var emaLong = MathUtils.Ema(closes, TrendEmaPeriod);
        var volRatio = MathUtils.VolatilityRatio(closes, VolEmaShort, VolEmaLong);

        // Quiet state check first (suppresses dust orders)
        if (volRatio < QuietVolRatio)
            return MarketState.Quiet;

        if (emaLong == 0) return MarketState.Normal;
        var trend = (emaShort - emaLong) / emaLong;

        if (trend >= BullThreshold)
            return MarketState.Bull with { TimeDilationMultiplier = 0.8m, BetaMultiplier = 1.5m };
        if (trend <= BearThreshold)
            return MarketState.Bear with { TimeDilationMultiplier = 1.5m, BetaMultiplier = 1.2m };

        return MarketState.Normal;
    }
}
