using System.Text;
using System.Text.Json;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;
using QuantSaaS.Quant;
using QuantSaaS.Strategy.BtcSpot;

namespace QuantSaaS.Evolution;

/// <summary>
/// BTC spot strategy adapter for GA engine.
/// Implements IEvolvableStrategy 8-verb contract.
/// Located in Evolution project to avoid circular dependency:
/// Strategy → Quant → Core; Evolution → Strategy (one-way)
/// </summary>
public class BtcSpotEvolvable : IEvolvableStrategy
{
    // BTC/USDT crypto instrument used for backtesting
    private static readonly Instrument BtcInstrument = new()
    {
        Symbol = "BTC/USDT",
        AssetClass = AssetClass.Crypto,
        QuoteCurrency = "USDT",
        LotStep = 0.00001m,
        LotMin = 0.00001m,
        TickSize = 0.01m,
        FractionAllowed = true,
        SettlementDays = 0
    };

    public string StrategyId() => "btc-spot-sigmoid-v1";

    public Chromosome Sample(Random rng)
    {
        return new Chromosome
        {
            Beta = RandomInRange(rng, Chromosome.Bounds.BetaMin, Chromosome.Bounds.BetaMax),
            Gamma = RandomInRange(rng, Chromosome.Bounds.GammaMin, Chromosome.Bounds.GammaMax),
            SigmaFloor = RandomInRange(rng, Chromosome.Bounds.SigmaFloorMin, Chromosome.Bounds.SigmaFloorMax),
            CoefX1 = RandomInRange(rng, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax),
            CoefX2 = RandomInRange(rng, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax),
            CoefX3 = RandomInRange(rng, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax),
            DeltaWeightThreshold = RandomInRange(rng, Chromosome.Bounds.ThresholdMin, Chromosome.Bounds.ThresholdMax),
            VolatilityRatioThreshold = RandomInRange(rng, Chromosome.Bounds.VolRatioThresholdMin, Chromosome.Bounds.VolRatioThresholdMax),
            MinOrderThreshold = RandomInRange(rng, Chromosome.Bounds.MinOrderMin, Chromosome.Bounds.MinOrderMax),
            MacroDcaIntervalDays = RandomInRange(rng, Chromosome.Bounds.MacroDcaDaysMin, Chromosome.Bounds.MacroDcaDaysMax),
            MacroDcaBuyFraction = RandomInRange(rng, Chromosome.Bounds.MacroDcaBuyFractionMin, Chromosome.Bounds.MacroDcaBuyFractionMax),
            MacroBullAccelMultiplier = RandomInRange(rng, Chromosome.Bounds.MacroBullAccelMin, Chromosome.Bounds.MacroBullAccelMax)
        }.Clamp();
    }

    public void Mutate(Chromosome c, double prob, double scale, Random rng)
    {
        c.Beta = MutateField(c.Beta, Chromosome.Bounds.BetaMin, Chromosome.Bounds.BetaMax, 0.5m, prob, scale, rng);
        c.Gamma = MutateField(c.Gamma, Chromosome.Bounds.GammaMin, Chromosome.Bounds.GammaMax, 0.2m, prob, scale, rng);
        c.SigmaFloor = MutateField(c.SigmaFloor, Chromosome.Bounds.SigmaFloorMin, Chromosome.Bounds.SigmaFloorMax, 0.005m, prob, scale, rng);
        c.CoefX1 = MutateField(c.CoefX1, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax, 0.3m, prob, scale, rng);
        c.CoefX2 = MutateField(c.CoefX2, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax, 0.3m, prob, scale, rng);
        c.CoefX3 = MutateField(c.CoefX3, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax, 0.3m, prob, scale, rng);
        c.DeltaWeightThreshold = MutateField(c.DeltaWeightThreshold, Chromosome.Bounds.ThresholdMin, Chromosome.Bounds.ThresholdMax, 0.01m, prob, scale, rng);
        c.VolatilityRatioThreshold = MutateField(c.VolatilityRatioThreshold, Chromosome.Bounds.VolRatioThresholdMin, Chromosome.Bounds.VolRatioThresholdMax, 0.2m, prob, scale, rng);
        c.MinOrderThreshold = MutateField(c.MinOrderThreshold, Chromosome.Bounds.MinOrderMin, Chromosome.Bounds.MinOrderMax, 2m, prob, scale, rng);
        c.MacroDcaIntervalDays = MutateField(c.MacroDcaIntervalDays, Chromosome.Bounds.MacroDcaDaysMin, Chromosome.Bounds.MacroDcaDaysMax, 7m, prob, scale, rng);
        c.MacroDcaBuyFraction = MutateField(c.MacroDcaBuyFraction, Chromosome.Bounds.MacroDcaBuyFractionMin, Chromosome.Bounds.MacroDcaBuyFractionMax, 0.05m, prob, scale, rng);
        c.MacroBullAccelMultiplier = MutateField(c.MacroBullAccelMultiplier, Chromosome.Bounds.MacroBullAccelMin, Chromosome.Bounds.MacroBullAccelMax, 0.3m, prob, scale, rng);
        c.Clamp();
    }

    public Chromosome Crossover(Chromosome p1, Chromosome p2, Random rng)
    {
        return new Chromosome
        {
            Beta = rng.NextDouble() < 0.5 ? p1.Beta : p2.Beta,
            Gamma = rng.NextDouble() < 0.5 ? p1.Gamma : p2.Gamma,
            SigmaFloor = rng.NextDouble() < 0.5 ? p1.SigmaFloor : p2.SigmaFloor,
            CoefX1 = rng.NextDouble() < 0.5 ? p1.CoefX1 : p2.CoefX1,
            CoefX2 = rng.NextDouble() < 0.5 ? p1.CoefX2 : p2.CoefX2,
            CoefX3 = rng.NextDouble() < 0.5 ? p1.CoefX3 : p2.CoefX3,
            DeltaWeightThreshold = rng.NextDouble() < 0.5 ? p1.DeltaWeightThreshold : p2.DeltaWeightThreshold,
            VolatilityRatioThreshold = rng.NextDouble() < 0.5 ? p1.VolatilityRatioThreshold : p2.VolatilityRatioThreshold,
            MinOrderThreshold = rng.NextDouble() < 0.5 ? p1.MinOrderThreshold : p2.MinOrderThreshold,
            MacroDcaIntervalDays = rng.NextDouble() < 0.5 ? p1.MacroDcaIntervalDays : p2.MacroDcaIntervalDays,
            MacroDcaBuyFraction = rng.NextDouble() < 0.5 ? p1.MacroDcaBuyFraction : p2.MacroDcaBuyFraction,
            MacroBullAccelMultiplier = rng.NextDouble() < 0.5 ? p1.MacroBullAccelMultiplier : p2.MacroBullAccelMultiplier
        }.Clamp();
    }

    public string Fingerprint(Chromosome c)
    {
        const decimal precision = 0.000001m;
        var sb = new StringBuilder();
        void Append(decimal v) => sb.Append($"{Math.Round(v / precision) * precision:F6}|");
        Append(c.Beta); Append(c.Gamma); Append(c.SigmaFloor);
        Append(c.CoefX1); Append(c.CoefX2); Append(c.CoefX3);
        Append(c.DeltaWeightThreshold); Append(c.VolatilityRatioThreshold);
        Append(c.MinOrderThreshold); Append(c.MacroDcaIntervalDays);
        Append(c.MacroDcaBuyFraction); Append(c.MacroBullAccelMultiplier);

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        // FNV-1a 64-bit
        ulong hash = 14695981039346656037UL;
        foreach (var b in bytes) { hash ^= b; hash *= 1099511628211UL; }
        return hash.ToString("x16");
    }

    public FitnessResult Evaluate(EvaluablePlan plan, Chromosome chromosome)
    {
        const decimal fatalDD = 0.88m;
        const decimal ddPenalty = 1.5m;

        var strategy = new BtcSpotStrategy(chromosome as BtcChromosome ?? BtcChromosome.DefaultSeed);
        var engine = new BacktestEngine(strategy);

        var scores = new List<WindowScore>();
        double totalScore = 0;
        decimal worstDD = 0;

        // Cascading short-circuit: windows are ordered short→long
        foreach (var window in plan.Windows)
        {
            var dcaBaseline = plan.DcaBaselines[Array.IndexOf(plan.Windows, window)];

            // Convert close prices + timestamps to Bar[]
            var bars = ClosesToBars(window.Closes, window.Timestamps);
            int evalStartIdx = FindEvalStartIdx(window.Timestamps, window.EvalStartMs);

            var result = engine.Run(bars, evalStartIdx, BtcInstrument, plan.Spawn);

            var alpha = result.ROI - dcaBaseline.ROI;
            var excessDD = Math.Max(0, result.MaxDrawdown - dcaBaseline.MaxDrawdown);
            var sliceScore = alpha - ddPenalty * excessDD;

            if (result.MaxDrawdown >= fatalDD)
            {
                scores.Add(new WindowScore
                {
                    Label = window.Label, Weight = window.Weight,
                    Alpha = alpha, SliceScore = -99999m,
                    MaxDrawdown = result.MaxDrawdown,
                    StrategyRoi = result.ROI, DcaRoi = dcaBaseline.ROI
                });
                return new FitnessResult
                {
                    ScoreTotal = -99999,
                    MaxDrawdown = result.MaxDrawdown,
                    IsFatal = true,
                    WindowScores = scores.ToArray()
                };
            }

            if (result.MaxDrawdown > worstDD) worstDD = result.MaxDrawdown;
            totalScore += (double)(sliceScore * window.Weight);
            scores.Add(new WindowScore
            {
                Label = window.Label, Weight = window.Weight,
                Alpha = alpha, SliceScore = sliceScore,
                MaxDrawdown = result.MaxDrawdown,
                StrategyRoi = result.ROI, DcaRoi = dcaBaseline.ROI
            });
        }

        return new FitnessResult
        {
            ScoreTotal = totalScore,
            MaxDrawdown = worstDD,
            IsFatal = false,
            WindowScores = scores.ToArray()
        };
    }

    private static Bar[] ClosesToBars(decimal[] closes, long[] timestamps)
    {
        var bars = new Bar[closes.Length];
        for (int i = 0; i < closes.Length; i++)
        {
            bars[i] = new Bar { OpenTimeMs = timestamps[i], Close = closes[i],
                Open = closes[i], High = closes[i], Low = closes[i], Volume = 1m };
        }
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
        if (string.IsNullOrWhiteSpace(paramPackJson)) return Chromosome.DefaultSeed;
        try
        {
            var doc = JsonDocument.Parse(paramPackJson);
            if (doc.RootElement.TryGetProperty("btc_spot_config", out var cfgEl))
                return JsonSerializer.Deserialize<Chromosome>(cfgEl.GetRawText()) ?? Chromosome.DefaultSeed;
        }
        catch { }
        return Chromosome.DefaultSeed;
    }

    public string EncodeResult(Chromosome champion, SpawnPoint spawn)
    {
        return JsonSerializer.Serialize(new
        {
            spawn_point = spawn,
            btc_spot_config = champion
        });
    }

    private static decimal RandomInRange(Random rng, decimal min, decimal max) =>
        min + (decimal)rng.NextDouble() * (max - min);

    private static decimal MutateField(decimal value, decimal min, decimal max, decimal step,
        double prob, double scale, Random rng)
    {
        if (rng.NextDouble() >= prob) return value;
        var delta = (decimal)(NextGaussian(rng) * (double)step * scale);
        return Math.Clamp(value + delta, min, max);
    }

    private static double NextGaussian(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
