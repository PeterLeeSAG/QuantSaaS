using System;
using System.Collections.Generic;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Evolution;

/// <summary>
/// 8-verb interface for GA evolution.
/// The engine interacts with any strategy through exactly these methods,
/// and has zero visibility into chromosome field names.
/// Iron Rule: Adding a new strategy requires ONLY implementing this interface,
/// never modifying engine.cs.
/// </summary>
public interface IEvolvableStrategy
{
    /// <summary>Returns unique strategy identifier (e.g., "btc-spot-v1", "us-stock-v1").</summary>
    string StrategyId();

    /// <summary>
    /// Randomly samples a chromosome from the legal gene space.
    /// Must call Clamp() before returning.
    /// </summary>
    Chromosome Sample(Random rng);

    /// <summary>
    /// Applies additive Gaussian mutation to each dimension with independent Bernoulli probability <paramref name="prob"/>.
    /// Mutation magnitude = NormalRandom() × geneStep × <paramref name="scale"/>.
    /// Must call Clamp() after mutation.
    /// </summary>
    void Mutate(Chromosome chromosome, double prob, double scale, Random rng);

    /// <summary>
    /// Uniform crossover: each dimension independently selected 50/50 from parent1 or parent2.
    /// Must call Clamp() on the child before returning.
    /// </summary>
    Chromosome Crossover(Chromosome parent1, Chromosome parent2, Random rng);

    /// <summary>
    /// Deterministic fingerprint for deduplication (FNV-1a-64, precision 1e-6).
    /// Chromosomes within 1e-6 distance produce the same fingerprint.
    /// </summary>
    string Fingerprint(Chromosome chromosome);

    /// <summary>
    /// Evaluates a chromosome against the multi-window crucible.
    /// Implements cascading short-circuit: fatal (MaxDD ≥ 88%) → immediate exit.
    /// </summary>
    FitnessResult Evaluate(EvaluablePlan plan, Chromosome chromosome);

    /// <summary>
    /// Decodes an elite chromosome from a ParamPack JSON string stored in the database.
    /// Returns DefaultSeed when json is null or parse fails.
    /// </summary>
    Chromosome DecodeElite(string? paramPackJson);

    /// <summary>
    /// Encodes the champion chromosome + spawn point into a ParamPack JSON blob for DB storage.
    /// Format: { "spawn_point": {...}, "[strategy]_config": {...} }
    /// </summary>
    string EncodeResult(Chromosome champion, SpawnPoint spawn);
}

/// <summary>Read-only evaluation context. Built once per Epoch, immutable within the epoch.</summary>
public record EvaluablePlan
{
    public string Pair { get; init; } = string.Empty;
    public string TemplateName { get; init; } = string.Empty;
    public SpawnPoint Spawn { get; init; } = null!;
    public decimal LotStep { get; init; }
    public decimal LotMin { get; init; }
    public bool FractionAllowed { get; init; }

    /// <summary>Four evaluation windows in short→long order (6m, 2y, 5y, full).</summary>
    public CrucibleWindow[] Windows { get; init; } = null!;

    /// <summary>Pre-computed Ghost DCA baselines, one per window.</summary>
    public GhostDCABaseline[] DcaBaselines { get; init; } = null!;
}

public record CrucibleWindow
{
    public string Label { get; init; } = string.Empty;     // "6m", "2y", "5y", "full"
    public decimal Weight { get; init; }
    public Bar[] AllBars { get; init; } = null!;           // includes warmup prefix
    public int EvalStartIdx { get; init; }                 // first bar counted in fitness
}

public record GhostDCABaseline
{
    public string Label { get; init; } = string.Empty;
    public decimal ROI { get; init; }
    public decimal MaxDrawdown { get; init; }
}

public record FitnessResult
{
    public decimal ScoreTotal { get; init; }
    public decimal MaxDrawdown { get; init; }
    public bool IsFatal { get; init; }
    public WindowScore Score6m { get; init; } = null!;
    public WindowScore Score2y { get; init; } = null!;
    public WindowScore Score5y { get; init; } = null!;
    public WindowScore ScoreFull { get; init; } = null!;
}

public record WindowScore
{
    public string Label { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public decimal Alpha { get; init; }
    public decimal SliceScore { get; init; }
    public decimal MaxDrawdown { get; init; }
    public bool IsFatal { get; init; }
}
