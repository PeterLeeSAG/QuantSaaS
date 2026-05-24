using System;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Quant;

/// <summary>
/// Macro engine: Long-term DCA accumulation with state-based acceleration.
/// Iron Rule: Only produces BUY intents, never SELL.
/// </summary>
public static class MacroEngine
{
    public record Input
    {
        public required decimal SpendableUsdt { get; init; }
        public required long CurrentBarTimeMs { get; init; }
        public required long LastMacroBuyTimeMs { get; init; }
        public required MarketState Market { get; init; }
        public required Chromosome Config { get; init; }
        public required SpawnPoint Spawn { get; init; }
    }

    public record Output
    {
        public decimal OrderUsdt { get; init; }
        public bool ShouldBuy { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    private const decimal MinOrderUsdt = 10.1m;

    public static Output Compute(Input input)
    {
        if (input.SpendableUsdt < MinOrderUsdt)
            return new Output { Reason = "Insufficient spendable USDT" };

        // Effective DCA interval adjusted for market time dilation and state
        var baseIntervalMs = (double)input.Config.MacroDcaIntervalDays * 24 * 3600 * 1000;
        var dilationFactor = (double)input.Market.TimeDilationMultiplier;

        // Bull state: accelerate DCA (shorter interval)
        // Bear state: decelerate (longer interval via dilation)
        double stateMultiplier = input.Market.State switch
        {
            "Bull" => (double)input.Config.MacroBullAccelMultiplier,
            "Bear" => 0.6,   // slow down in bear
            "Quiet" => 0.3,  // very slow in quiet
            _ => 1.0
        };

        var effectiveIntervalMs = baseIntervalMs * dilationFactor / stateMultiplier;
        var elapsedMs = (double)(input.CurrentBarTimeMs - input.LastMacroBuyTimeMs);

        // Deadline safety: if capital idle too long, force deploy
        var deadlineMs = (double)input.Spawn.Capital.DeadlineYears * 365.25 * 24 * 3600 * 1000;
        bool deadlineForce = input.LastMacroBuyTimeMs > 0 && elapsedMs >= deadlineMs;
        bool regularTrigger = input.LastMacroBuyTimeMs == 0 || elapsedMs >= effectiveIntervalMs;

        if (!regularTrigger && !deadlineForce)
            return new Output { Reason = "DCA interval not yet elapsed" };

        // Order size: buy fraction of spendable USDT
        var buyAmount = input.SpendableUsdt * input.Config.MacroDcaBuyFraction;
        buyAmount = Math.Clamp(buyAmount, MinOrderUsdt, input.SpendableUsdt);

        return new Output
        {
            ShouldBuy = true,
            OrderUsdt = buyAmount,
            Reason = deadlineForce ? "DeadlineForce" : "ScheduledDCA"
        };
    }
}
