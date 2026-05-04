namespace QuantSaaS.Core.Models;

/// <summary>
/// Epoch-level frozen configuration.
/// Shared by the entire GA population; never altered by crossover/mutation;
/// excluded from the genome fingerprint.
/// </summary>
public record SpawnPoint
{
    public CapitalPolicy Capital { get; init; } = new();
    public RiskBounds Risk { get; init; } = new();

    public static SpawnPoint Default => new();

    /// <summary>Random sampling (used when spawn_mode = random_once).</summary>
    public static SpawnPoint SampleRandom(Random rng) => new()
    {
        Capital = new CapitalPolicy
        {
            MonthlyInjectQuote = 500m + (decimal)(rng.NextDouble() * 1500),
            DeadlineYears = 2 + rng.Next(4),
            ReleaseAfterMonths = 12 + rng.Next(24),
        },
        Risk = new RiskBounds
        {
            FeeRate = 0.001m,
            GlobalStopLoss = null,
        },
    };
}

public record CapitalPolicy
{
    /// <summary>Monthly DCA injection in the instrument's quote currency (USDT or USD).</summary>
    public decimal MonthlyInjectQuote { get; init; } = 1000m;

    /// <summary>If macro has unused capital after X years, force deployment.</summary>
    public int DeadlineYears { get; init; } = 4;

    /// <summary>Months before DeadStack lots qualify for soft-release.</summary>
    public int ReleaseAfterMonths { get; init; } = 12;

    /// <summary>Max fraction of DeadStack that can be soft-released per cycle.</summary>
    public decimal MaxReleaseRatio { get; init; } = 0.20m;

    /// <summary>
    /// Maximum concentration in a single position as a fraction of total equity.
    /// Relevant for stock strategies; crypto default is 1.0 (no limit).
    /// </summary>
    public decimal MaxPositionConcentration { get; init; } = 1.0m;
}

public record RiskBounds
{
    /// <summary>Trading fee rate (0.001 = 0.1%).</summary>
    public decimal FeeRate { get; init; } = 0.001m;

    /// <summary>Global stop-loss drawdown threshold. Null = disabled.</summary>
    public decimal? GlobalStopLoss { get; init; }
}
