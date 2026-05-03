namespace QuantSaaS.Core.Models;

public enum OrderAction { None, Buy, Sell }
public enum EngineLayer { Macro, Micro }
public enum LotType { DeadStack, Floating, ColdSealed }

/// <summary>
/// Trading intent produced by Step(). SaaS translates this to TradeCommand.
/// </summary>
public record StrategyOutput
{
    // Macro engine intent
    public decimal MacroOrderUsdt { get; init; }
    public OrderAction MacroAction { get; init; }

    // Micro engine intent
    public decimal MicroOrderUsdt { get; init; }
    public OrderAction MicroAction { get; init; }

    // Micro Sigmoid debug info
    public decimal TargetWeight { get; init; }
    public decimal Signal { get; init; }
    public decimal VolatilityRatio { get; init; }

    // Dead release intent (SaaS-side ledger only, no Agent command)
    public DeadReleaseIntent? ReleaseIntent { get; init; }

    // Updated runtime state (persisted after tick)
    public StrategyRuntimeState NewRuntimeState { get; init; } = new();
}

public record DeadReleaseIntent
{
    public decimal ReleaseBtc { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool IsSoftRelease { get; init; }
}
