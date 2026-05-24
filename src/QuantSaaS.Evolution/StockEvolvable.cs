using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;
using QuantSaaS.Quant;
using QuantSaaS.Strategy.Stock;

namespace QuantSaaS.Evolution;

/// <summary>
/// IEvolvableStrategy adapter for the US Stock / ETF strategy.
/// Resides in the Evolution project (not Strategy) to avoid import cycles:
///   Evolution → Strategy is one-directional.
/// </summary>
public sealed class StockEvolvable : IEvolvableStrategy
{
    private readonly IMarketCalendar _calendar;
    private const decimal FatalMaxDD = 0.88m;

    public StockEvolvable(IMarketCalendar calendar)
    {
        _calendar = calendar;
    }

    public string StrategyId() => "us-stock-v1";

    public Chromosome Sample(Random rng)
    {
        return new StockChromosome
        {
            Beta = RandDecimal(rng, Chromosome.Bounds.BetaMin, Chromosome.Bounds.BetaMax),
            Gamma = RandDecimal(rng, Chromosome.Bounds.GammaMin, Chromosome.Bounds.GammaMax),
            SigmaFloor = RandDecimal(rng, Chromosome.Bounds.SigmaFloorMin, Chromosome.Bounds.SigmaFloorMax),
            CoefX1 = RandDecimal(rng, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax),
            CoefX2 = RandDecimal(rng, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax),
            CoefX3 = RandDecimal(rng, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax),
            DeltaWeightThreshold = RandDecimal(rng, Chromosome.Bounds.ThresholdMin, Chromosome.Bounds.ThresholdMax),
            VolatilityRatioThreshold = RandDecimal(rng, Chromosome.Bounds.VolRatioThresholdMin, Chromosome.Bounds.VolRatioThresholdMax),
            MinOrderThreshold = RandDecimal(rng, Chromosome.Bounds.MinOrderMin, Chromosome.Bounds.MinOrderMax),
            EmaShortBars = RandDecimal(rng, StockChromosome.Bounds.EmaShortMin, StockChromosome.Bounds.EmaShortMax),
            EmaLongBars = RandDecimal(rng, StockChromosome.Bounds.EmaLongMin, StockChromosome.Bounds.EmaLongMax),
            MaxAllocationFraction = RandDecimal(rng, StockChromosome.Bounds.MaxAllocMin, StockChromosome.Bounds.MaxAllocMax),
            MinHoldingDays = RandDecimal(rng, StockChromosome.Bounds.MinHoldMin, StockChromosome.Bounds.MinHoldMax),
        }.Clamp();
    }

    public void Mutate(Chromosome chromosome, double prob, double scale, Random rng)
    {
        if (chromosome is not StockChromosome sc) return;

        sc.Beta = MutateField(sc.Beta, prob, scale, 0.1, rng);
        sc.Gamma = MutateField(sc.Gamma, prob, scale, 0.05, rng);
        sc.SigmaFloor = MutateField(sc.SigmaFloor, prob, scale, 0.001, rng);
        sc.CoefX1 = MutateField(sc.CoefX1, prob, scale, 0.1, rng);
        sc.CoefX2 = MutateField(sc.CoefX2, prob, scale, 0.1, rng);
        sc.CoefX3 = MutateField(sc.CoefX3, prob, scale, 0.1, rng);
        sc.DeltaWeightThreshold = MutateField(sc.DeltaWeightThreshold, prob, scale, 0.005, rng);
        sc.VolatilityRatioThreshold = MutateField(sc.VolatilityRatioThreshold, prob, scale, 0.05, rng);
        sc.MinOrderThreshold = MutateField(sc.MinOrderThreshold, prob, scale, 0.5, rng);
        sc.EmaShortBars = MutateField(sc.EmaShortBars, prob, scale, 1.0, rng);
        sc.EmaLongBars = MutateField(sc.EmaLongBars, prob, scale, 5.0, rng);
        sc.MaxAllocationFraction = MutateField(sc.MaxAllocationFraction, prob, scale, 0.05, rng);
        sc.MinHoldingDays = MutateField(sc.MinHoldingDays, prob, scale, 1.0, rng);
        sc.Clamp();
    }

    public Chromosome Crossover(Chromosome parent1, Chromosome parent2, Random rng)
    {
        var p1 = parent1 as StockChromosome ?? StockChromosome.DefaultSeed;
        var p2 = parent2 as StockChromosome ?? StockChromosome.DefaultSeed;

        return new StockChromosome
        {
            Beta = Pick(p1.Beta, p2.Beta, rng),
            Gamma = Pick(p1.Gamma, p2.Gamma, rng),
            SigmaFloor = Pick(p1.SigmaFloor, p2.SigmaFloor, rng),
            CoefX1 = Pick(p1.CoefX1, p2.CoefX1, rng),
            CoefX2 = Pick(p1.CoefX2, p2.CoefX2, rng),
            CoefX3 = Pick(p1.CoefX3, p2.CoefX3, rng),
            DeltaWeightThreshold = Pick(p1.DeltaWeightThreshold, p2.DeltaWeightThreshold, rng),
            VolatilityRatioThreshold = Pick(p1.VolatilityRatioThreshold, p2.VolatilityRatioThreshold, rng),
            MinOrderThreshold = Pick(p1.MinOrderThreshold, p2.MinOrderThreshold, rng),
            EmaShortBars = Pick(p1.EmaShortBars, p2.EmaShortBars, rng),
            EmaLongBars = Pick(p1.EmaLongBars, p2.EmaLongBars, rng),
            MaxAllocationFraction = Pick(p1.MaxAllocationFraction, p2.MaxAllocationFraction, rng),
            MinHoldingDays = Pick(p1.MinHoldingDays, p2.MinHoldingDays, rng),
        }.Clamp();
    }

    public string Fingerprint(Chromosome chromosome)
    {
        if (chromosome is not StockChromosome sc) return string.Empty;
        return FNV1a64.Hash([
            sc.Beta, sc.Gamma, sc.SigmaFloor,
            sc.CoefX1, sc.CoefX2, sc.CoefX3,
            sc.DeltaWeightThreshold, sc.VolatilityRatioThreshold, sc.MinOrderThreshold,
            sc.EmaShortBars, sc.EmaLongBars, sc.MaxAllocationFraction, sc.MinHoldingDays,
        ]);
    }

    public FitnessResult Evaluate(EvaluablePlan plan, Chromosome chromosome)
    {
        if (chromosome is not StockChromosome sc)
            return Fatal();

        var strategy = new StockStrategy(sc);
        var instrument = Instrument.UsStock(plan.Pair);
        var costModel = new FixedCommissionModel(ratePerShare: 0m, slippageFraction: 0.0001m);
        var engine = new BacktestEngine(strategy, costModel, _calendar);

        var scores = new List<WindowScore>();
        double total = 0.0;
        decimal worstMaxDD = 0m;
        bool isFatal = false;

        // Cascading short-circuit: evaluate 6m → 2y → 5y → full
        foreach (var window in plan.Windows)
        {
            var bars = ClosesToBars(window.Closes, window.Timestamps);
            int evalStartIdx = FindEvalStartIdx(window.Timestamps, window.EvalStartMs);
            var result = engine.Run(bars, evalStartIdx, instrument, plan.Spawn);
            var dca = plan.DcaBaselines[Array.IndexOf(plan.Windows, window)];

            decimal alpha = result.ROI - dca.ROI;
            decimal excessDD = Math.Max(0m, result.MaxDrawdown - dca.MaxDrawdown);
            decimal sliceScore = alpha - 1.5m * excessDD;
            bool fatal = result.MaxDrawdown >= FatalMaxDD;

            if (fatal)
            {
                sliceScore = -99999m;
                total = -99999;
                isFatal = true;
            }

            var ws = new WindowScore
            {
                Label = window.Label,
                Weight = window.Weight,
                Alpha = alpha,
                SliceScore = sliceScore,
                MaxDrawdown = result.MaxDrawdown,
                StrategyRoi = result.ROI,
                DcaRoi = dca.ROI,
                IsFatal = fatal,
            };
            scores.Add(ws);

            if (result.MaxDrawdown > worstMaxDD) worstMaxDD = result.MaxDrawdown;

            if (!fatal && !isFatal)
                total += (double)(window.Weight * sliceScore);

            if (fatal) break;
        }

        return new FitnessResult
        {
            ScoreTotal = isFatal ? -99999 : total,
            MaxDrawdown = worstMaxDD,
            IsFatal = isFatal,
            WindowScores = scores.ToArray(),
        };
    }

    private static Bar[] ClosesToBars(decimal[] closes, long[] timestamps)
    {
        var bars = new Bar[closes.Length];
        for (int i = 0; i < closes.Length; i++)
            bars[i] = new Bar { OpenTimeMs = timestamps[i], Close = closes[i],
                Open = closes[i], High = closes[i], Low = closes[i], Volume = 1m };
        return bars;
    }

    private static int FindEvalStartIdx(long[] timestamps, long evalStartMs)
    {
        for (int i = 0; i < timestamps.Length; i++)
            if (timestamps[i] >= evalStartMs) return i;
        return 0;
    }

    public Chromosome DecodeElite(string? paramPackJson)
    {
        if (string.IsNullOrWhiteSpace(paramPackJson))
            return StockChromosome.DefaultSeed;
        try
        {
            using var doc = JsonDocument.Parse(paramPackJson);
            var cfg = doc.RootElement.GetProperty("us_stock_config");
            return JsonSerializer.Deserialize<StockChromosome>(cfg.GetRawText())?.Clamp()
                   ?? StockChromosome.DefaultSeed;
        }
        catch { return StockChromosome.DefaultSeed; }
    }

    public string EncodeResult(Chromosome champion, SpawnPoint spawn)
    {
        var pack = new
        {
            spawn_point = spawn,
            us_stock_config = champion as StockChromosome ?? StockChromosome.DefaultSeed,
        };
        return JsonSerializer.Serialize(pack);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FitnessResult Fatal() => new FitnessResult
    {
        ScoreTotal = -99999,
        IsFatal = true,
        WindowScores = new[] { new WindowScore { Label = "fatal", IsFatal = true } }
    };

    private static decimal RandDecimal(Random rng, decimal min, decimal max)
        => min + (decimal)rng.NextDouble() * (max - min);

    private static decimal Pick(decimal a, decimal b, Random rng)
        => rng.NextDouble() < 0.5 ? a : b;

    private static decimal MutateField(decimal field, double prob, double scale, double step, Random rng)
    {
        if (rng.NextDouble() < prob)
            return field + (decimal)(rng.NextGaussian() * step * scale);
        return field;
    }
}

// ── Random Gaussian extension ─────────────────────────────────────────────────

internal static class RandomExtensions
{
    /// <summary>Box-Muller transform to generate a standard normal sample.</summary>
    public static double NextGaussian(this Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
