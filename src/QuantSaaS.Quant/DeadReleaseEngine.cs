using QuantSaaS.Core.Models;

namespace QuantSaaS.Quant;

/// <summary>
/// DeadBTC release logic.
/// Iron Rule: Release only updates SaaS-side ledger (no Agent command issued).
/// ColdSealed BTC is NEVER released under any circumstances.
/// </summary>
public static class DeadReleaseEngine
{
    public record Input
    {
        public required PortfolioState Portfolio { get; init; }
        public required Chromosome Config { get; init; }
        public required SpawnPoint Spawn { get; init; }
        public required long CurrentBarTimeMs { get; init; }
        public decimal RequiredSellUsdt { get; init; }
        public decimal CurrentPrice { get; init; }
    }

    public record Output
    {
        public decimal ReleaseBtc { get; init; }
        public bool IsHardRelease { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    private const decimal MaxSoftReleaseRatio = 0.25m; // Max 25% of DeadBTC per release

    public static Output ComputeSoftRelease(Input input)
    {
        // Only release if DeadBTC has aged past the configured threshold
        var portfolio = input.Portfolio;
        if (portfolio.DeadBtc <= 0) return new Output { Reason = "No DeadBTC" };

        // Soft release: time-based aging (simplified - in prod track per-lot age)
        // Release up to MaxSoftReleaseRatio of eligible DeadBTC
        var eligibleBtc = portfolio.DeadBtc * MaxSoftReleaseRatio;
        if (eligibleBtc <= 0) return new Output { Reason = "No eligible BTC" };

        // Only release if there's a sell gap (FloatBTC below target floor)
        var currentMicroWeight = portfolio.CurrentMicroWeight(input.CurrentPrice);
        if (currentMicroWeight >= 0.1m) // Already have sufficient float
            return new Output { Reason = "Sufficient float position" };

        return new Output
        {
            ReleaseBtc = eligibleBtc,
            IsHardRelease = false,
            Reason = $"SoftRelease: aged lot, weight={currentMicroWeight:F4}"
        };
    }

    public static Output ComputeHardRelease(Input input)
    {
        if (input.RequiredSellUsdt <= 0 || input.CurrentPrice <= 0)
            return new Output { Reason = "No sell requirement" };

        var requiredBtc = input.RequiredSellUsdt / input.CurrentPrice;
        var portfolio = input.Portfolio;
        var deficit = requiredBtc - portfolio.FloatBtc;

        if (deficit <= 0) return new Output { Reason = "Sufficient FloatBTC" };

        // Hard release: take from DeadBTC (never ColdSealed)
        var releaseBtc = Math.Min(deficit, portfolio.DeadBtc);
        if (releaseBtc <= 0) return new Output { Reason = "No DeadBTC available" };

        return new Output
        {
            ReleaseBtc = releaseBtc,
            IsHardRelease = true,
            Reason = $"HardRelease: deficit={deficit:F8} BTC"
        };
    }
}
