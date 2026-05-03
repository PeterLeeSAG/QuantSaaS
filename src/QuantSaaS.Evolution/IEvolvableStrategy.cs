using QuantSaaS.Core.Models;
using QuantSaaS.Quant;

namespace QuantSaaS.Evolution;

/// <summary>
/// 8-verb interface for GA engine ↔ strategy adapter decoupling.
/// Engine knows NOTHING about chromosome field names - only these 8 verbs.
/// </summary>
public interface IEvolvableStrategy
{
    string StrategyId();
    Chromosome Sample(Random rng);
    void Mutate(Chromosome chromosome, double prob, double scale, Random rng);
    Chromosome Crossover(Chromosome parent1, Chromosome parent2, Random rng);
    string Fingerprint(Chromosome chromosome);
    FitnessResult Evaluate(EvaluablePlan plan, Chromosome chromosome);
    Chromosome DecodeElite(string? paramPackJson);
    string EncodeResult(Chromosome champion, SpawnPoint spawn);
}

public record FitnessResult
{
    public double ScoreTotal { get; init; }
    public decimal MaxDrawdown { get; init; }
    public bool IsFatal { get; init; }
    public WindowScore[] WindowScores { get; init; } = Array.Empty<WindowScore>();
}

public record WindowScore
{
    public string Label { get; init; } = null!;
    public decimal Weight { get; init; }
    public decimal Alpha { get; init; }
    public decimal SliceScore { get; init; }
    public decimal MaxDrawdown { get; init; }
    public decimal StrategyRoi { get; init; }
    public decimal DcaRoi { get; init; }
}

public record EvaluablePlan
{
    public string Pair { get; init; } = null!;
    public string TemplateName { get; init; } = null!;
    public required SpawnPoint Spawn { get; init; }
    public decimal LotStep { get; init; }
    public decimal LotMin { get; init; }
    public CrucibleWindow[] Windows { get; init; } = null!;
    public GhostDcaResult[] DcaBaselines { get; init; } = null!;
}
