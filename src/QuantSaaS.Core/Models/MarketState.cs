namespace QuantSaaS.Core.Models;

public record MarketState
{
    public string State { get; init; } = "Normal";
    public bool IsQuiet { get; init; }
    public decimal BetaMultiplier { get; init; } = 1.0m;
    /// <summary>Stretches macro DCA time windows. >1 expands look-back.</summary>
    public decimal TimeDilationMultiplier { get; init; } = 1.0m;

    public static readonly MarketState Bull = new() { State = "Bull", BetaMultiplier = 1.5m, TimeDilationMultiplier = 0.8m };
    public static readonly MarketState Bear = new() { State = "Bear", BetaMultiplier = 1.2m, TimeDilationMultiplier = 1.5m };
    public static readonly MarketState Quiet = new() { State = "Quiet", IsQuiet = true, BetaMultiplier = 0.8m, TimeDilationMultiplier = 1.2m };
    public static readonly MarketState Normal = new() { State = "Normal", BetaMultiplier = 1.0m, TimeDilationMultiplier = 1.0m };
}
