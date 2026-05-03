namespace QuantSaaS.Core.Models;

/// <summary>
/// Epoch-level frozen configuration. Shared by entire population,
/// never altered by crossover/mutation, excluded from genome fingerprint.
/// </summary>
public record SpawnPoint
{
    public CapitalPolicy Capital { get; init; } = new();
    public RiskBounds Risk { get; init; } = new();

    public static SpawnPoint Default => new();

    public static SpawnPoint SampleRandom(Random rng) => new()
    {
        Capital = new CapitalPolicy
        {
            SeedCapitalUsdt = 1000m + (decimal)(rng.NextDouble() * 9000),
            MonthlyInjectUsdt = 500m + (decimal)(rng.NextDouble() * 1500),
            DeadlineYears = 2 + rng.Next(4),
            ReleaseAfterMonths = 12 + rng.Next(24),
            MicroReservePct = 0.05m + (decimal)(rng.NextDouble() * 0.15)
        },
        Risk = new RiskBounds
        {
            FeeRate = 0.001m,
            GlobalStopLoss = null
        }
    };
}

public record CapitalPolicy
{
    /// <summary>Initial seed capital in USDT</summary>
    public decimal SeedCapitalUsdt { get; init; } = 5000m;

    /// <summary>Monthly DCA injection (USDT)</summary>
    public decimal MonthlyInjectUsdt { get; init; } = 1000m;

    /// <summary>Deadline: if macro has unused capital after X years, force deployment</summary>
    public int DeadlineYears { get; init; } = 4;

    /// <summary>Months before DeadStack lots can be soft-released</summary>
    public int ReleaseAfterMonths { get; init; } = 12;

    /// <summary>Reserve pct of TotalEquity kept as USDT floor [0.05, 0.20]</summary>
    public decimal MicroReservePct { get; init; } = 0.10m;
}

public record RiskBounds
{
    /// <summary>Trading fee rate (e.g. 0.001 for 0.1%)</summary>
    public decimal FeeRate { get; init; } = 0.001m;

    /// <summary>Global stop-loss drawdown (null = disabled)</summary>
    public decimal? GlobalStopLoss { get; init; }
}
