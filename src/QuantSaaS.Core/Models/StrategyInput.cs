namespace QuantSaaS.Core.Models;

/// <summary>
/// Immutable snapshot passed to Step(). Strategy must only read from this.
/// Iron Rule: Step() must be a pure function - no I/O, only reads this input.
/// </summary>
public record StrategyInput
{
    /// <summary>Close prices in ascending time order (ACL-downgraded from Bar[])</summary>
    public required decimal[] ClosePrices { get; init; }

    /// <summary>Timestamps aligned with ClosePrices (milliseconds UTC)</summary>
    public required long[] Timestamps { get; init; }

    /// <summary>Most recent close price</summary>
    public decimal CurrentPrice => ClosePrices.Length > 0 ? ClosePrices[^1] : 0;

    /// <summary>Portfolio snapshot at start of this tick</summary>
    public required PortfolioState Portfolio { get; init; }

    /// <summary>Market state from perception layer</summary>
    public required MarketState Market { get; init; }

    /// <summary>Evolvable parameters (champion chromosome)</summary>
    public required Chromosome Config { get; init; }

    /// <summary>Epoch-frozen capital/risk policy</summary>
    public required SpawnPoint Spawn { get; init; }

    /// <summary>Strategy-specific cross-tick runtime state (deserialized from JSON)</summary>
    public StrategyRuntimeState RuntimeState { get; init; } = new();
}
