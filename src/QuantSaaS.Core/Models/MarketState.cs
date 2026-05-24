namespace QuantSaaS.Core.Models;

/// <summary>
/// Market environment snapshot produced by the strategy's perception layer.
/// Influences both the macro DCA engine and the micro Sigmoid engine.
/// </summary>
public record MarketState
{
    /// <summary>Human-readable label ("Bull", "Bear", "Quiet", "Normal").</summary>
    public string State { get; init; } = "Normal";

    /// <summary>When true, micro dust orders are suppressed (IsQuiet gate).</summary>
    public bool IsQuiet { get; init; }

    /// <summary>Multiplier applied to the micro Sigmoid β (1.0 = normal).</summary>
    public decimal BetaMultiplier { get; init; } = 1.0m;

    /// <summary>
    /// Stretches macro engine time windows.
    /// &gt;1 expands look-back (used during high-volatility regimes), &lt;1 shrinks it.
    /// </summary>
    public decimal TimeDilationMultiplier { get; init; } = 1.0m;

    public static MarketState Default => new();
    public static MarketState Normal => new() { State = "Normal" };
    public static MarketState Quiet  => new() { State = "Quiet",  IsQuiet = true };
    public static MarketState Bull   => new() { State = "Bull" };
    public static MarketState Bear   => new() { State = "Bear" };
}
