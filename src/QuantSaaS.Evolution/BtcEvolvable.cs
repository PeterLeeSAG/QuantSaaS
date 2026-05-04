using System;
using System.Text.Json;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;
using QuantSaaS.Quant;
using QuantSaaS.Strategy.BtcSpot;

namespace QuantSaaS.Evolution;

/// <summary>
/// IEvolvableStrategy adapter for the BTC/USDT spot strategy.
/// Reference implementation showing how to wire a crypto strategy to the GA engine.
/// </summary>
public sealed class BtcEvolvable : IEvolvableStrategy
{
    private const decimal FatalMaxDD = 0.88m;

    public string StrategyId() => "btc-spot-v1";

    public Chromosome Sample(Random rng)
    {
        return new BtcChromosome
        {
            Beta = RandDecimal(rng, Chromosome.Bounds.BetaMin, Chromosome.Bounds.BetaMax),
            Gamma = RandDecimal(rng, Chromosome.Bounds.GammaMin, Chromosome.Bounds.GammaMax),
            SigmaFloor = RandDecimal(rng, Chromosome.Bounds.SigmaFloorMin, Chromosome.Bounds.SigmaFloorMax),
            CoefX1 = RandDecimal(rng, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax),
            CoefX2 = RandDecimal(rng, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax),
            CoefX3 = RandDecimal(rng, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax),
            DeltaWeightThreshold = RandDecimal(rng, Chromosome.Bounds.DeltaWeightMin, Chromosome.Bounds.DeltaWeightMax),
            VolatilityRatioThreshold = RandDecimal(rng, Chromosome.Bounds.VolRatioThresholdMin, Chromosome.Bounds.VolRatioThresholdMax),
            MinOrderThreshold = RandDecimal(rng, Chromosome.Bounds.MinOrderMin, Chromosome.Bounds.MinOrderMax),
            MonthlyMacroBuyQuote = 200m + (decimal)(rng.NextDouble() * 2800),
        }.Clamp();
    }

    public void Mutate(Chromosome chromosome, double prob, double scale, Random rng)
    {
        if (chromosome is not BtcChromosome bc) return;

        bc.Beta = MutateField(bc.Beta, prob, scale, 0.1, rng);
        bc.Gamma = MutateField(bc.Gamma, prob, scale, 0.05, rng);
        bc.SigmaFloor = MutateField(bc.SigmaFloor, prob, scale, 0.001, rng);
        bc.CoefX1 = MutateField(bc.CoefX1, prob, scale, 0.1, rng);
        bc.CoefX2 = MutateField(bc.CoefX2, prob, scale, 0.1, rng);
        bc.CoefX3 = MutateField(bc.CoefX3, prob, scale, 0.1, rng);
        bc.DeltaWeightThreshold = MutateField(bc.DeltaWeightThreshold, prob, scale, 0.005, rng);
        bc.VolatilityRatioThreshold = MutateField(bc.VolatilityRatioThreshold, prob, scale, 0.05, rng);
        bc.MinOrderThreshold = MutateField(bc.MinOrderThreshold, prob, scale, 0.5, rng);
        bc.MonthlyMacroBuyQuote = MutateField(bc.MonthlyMacroBuyQuote, prob, scale, 50.0, rng);
        bc.Clamp();
    }

    public Chromosome Crossover(Chromosome parent1, Chromosome parent2, Random rng)
    {
        var p1 = parent1 as BtcChromosome ?? BtcChromosome.DefaultSeed;
        var p2 = parent2 as BtcChromosome ?? BtcChromosome.DefaultSeed;

        return new BtcChromosome
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
            MonthlyMacroBuyQuote = Pick(p1.MonthlyMacroBuyQuote, p2.MonthlyMacroBuyQuote, rng),
        }.Clamp();
    }

    public string Fingerprint(Chromosome chromosome)
    {
        if (chromosome is not BtcChromosome bc) return string.Empty;
        return FNV1a64.Hash([
            bc.Beta, bc.Gamma, bc.SigmaFloor,
            bc.CoefX1, bc.CoefX2, bc.CoefX3,
            bc.DeltaWeightThreshold, bc.VolatilityRatioThreshold, bc.MinOrderThreshold,
            bc.MonthlyMacroBuyQuote,
        ]);
    }

    public FitnessResult Evaluate(EvaluablePlan plan, Chromosome chromosome)
    {
        if (chromosome is not BtcChromosome bc)
            return Fatal();

        var strategy = new BtcSpotStrategy(bc);
        var instrument = Instrument.BtcUsdt();
        var engine = new BacktestEngine(strategy);

        WindowScore? score6m = null, score2y = null, score5y = null, scoreFull = null;
        decimal total = 0m;
        decimal worstMaxDD = 0m;

        foreach (var window in plan.Windows)
        {
            var result = engine.Run(window.AllBars, window.EvalStartIdx, instrument, plan.Spawn);
            var dca = plan.DcaBaselines[Array.IndexOf(plan.Windows, window)];

            decimal alpha = result.ROI - dca.ROI;
            decimal excessDD = Math.Max(0m, result.MaxDrawdown - dca.MaxDrawdown);
            decimal sliceScore = alpha - 1.5m * excessDD;
            bool fatal = result.MaxDrawdown >= FatalMaxDD;

            if (fatal) { sliceScore = -99999m; total = -99999m; }

            var ws = new WindowScore
            {
                Label = window.Label, Weight = window.Weight,
                Alpha = alpha, SliceScore = sliceScore, MaxDrawdown = result.MaxDrawdown, IsFatal = fatal,
            };

            if (window.Label == "6m") score6m = ws;
            else if (window.Label == "2y") score2y = ws;
            else if (window.Label == "5y") score5y = ws;
            else scoreFull = ws;

            if (result.MaxDrawdown > worstMaxDD) worstMaxDD = result.MaxDrawdown;
            if (!fatal && total != -99999m) total += window.Weight * sliceScore;
            if (fatal) break;
        }

        return new FitnessResult
        {
            ScoreTotal = total, MaxDrawdown = worstMaxDD, IsFatal = total == -99999m,
            Score6m = score6m ?? new WindowScore { Label = "6m" },
            Score2y = score2y ?? new WindowScore { Label = "2y" },
            Score5y = score5y ?? new WindowScore { Label = "5y" },
            ScoreFull = scoreFull ?? new WindowScore { Label = "full" },
        };
    }

    public Chromosome DecodeElite(string? paramPackJson)
    {
        if (string.IsNullOrWhiteSpace(paramPackJson)) return BtcChromosome.DefaultSeed;
        try
        {
            using var doc = JsonDocument.Parse(paramPackJson);
            var cfg = doc.RootElement.GetProperty("btc_spot_config");
            return JsonSerializer.Deserialize<BtcChromosome>(cfg.GetRawText())?.Clamp()
                   ?? BtcChromosome.DefaultSeed;
        }
        catch { return BtcChromosome.DefaultSeed; }
    }

    public string EncodeResult(Chromosome champion, SpawnPoint spawn)
    {
        var pack = new { spawn_point = spawn, btc_spot_config = champion as BtcChromosome ?? BtcChromosome.DefaultSeed };
        return JsonSerializer.Serialize(pack);
    }

    private static FitnessResult Fatal() => new FitnessResult
    {
        ScoreTotal = -99999m, IsFatal = true,
        Score6m = new WindowScore { Label = "6m", IsFatal = true },
        Score2y = new WindowScore { Label = "2y", IsFatal = true },
        Score5y = new WindowScore { Label = "5y", IsFatal = true },
        ScoreFull = new WindowScore { Label = "full", IsFatal = true },
    };

    private static decimal RandDecimal(Random rng, decimal min, decimal max)
        => min + (decimal)rng.NextDouble() * (max - min);
    private static decimal Pick(decimal a, decimal b, Random rng)
        => rng.NextDouble() < 0.5 ? a : b;
    private static decimal MutateField(decimal field, double prob, double scale, double step, Random rng)
        => rng.NextDouble() < prob ? field + (decimal)(rng.NextGaussian() * step * scale) : field;
}
