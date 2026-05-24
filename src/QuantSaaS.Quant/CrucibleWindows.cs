using System;

namespace QuantSaaS.Quant;

public record CrucibleWindow
{
    public string Label { get; init; } = null!;
    public decimal Weight { get; init; }
    public decimal[] Closes { get; init; } = null!;
    public long[] Timestamps { get; init; } = null!;
    public long EvalStartMs { get; init; }
}

public static class CrucibleWindowBuilder
{
    private const int WarmupDays = 1200;

    /// <summary>
    /// Builds four crucible windows in short→long order for cascading short-circuit.
    /// Windows: 6m (0.10), 2y (0.20), 5y (0.30), full (0.40)
    /// </summary>
    public static CrucibleWindow[] Build(decimal[] allCloses, long[] allTimestamps)
    {
        if (allCloses.Length == 0) return Array.Empty<CrucibleWindow>();

        var latestMs = allTimestamps[^1];

        return new[]
        {
            BuildWindow("6m",   0.10m, allCloses, allTimestamps, latestMs, 183, WarmupDays),
            BuildWindow("2y",   0.20m, allCloses, allTimestamps, latestMs, 730, WarmupDays),
            BuildWindow("5y",   0.30m, allCloses, allTimestamps, latestMs, 1825, WarmupDays),
            BuildFullWindow(    0.40m, allCloses, allTimestamps),
        };
    }

    private static CrucibleWindow BuildWindow(
        string label, decimal weight,
        decimal[] allCloses, long[] allTimestamps,
        long latestMs, int evalDays, int warmupDays)
    {
        long evalStartMs = latestMs - (long)evalDays * 86400_000L;
        long warmupStartMs = evalStartMs - (long)warmupDays * 86400_000L;

        // Find indices
        int warmupIdx = FindFirstIndexAtOrAfter(allTimestamps, warmupStartMs);
        int evalIdx = FindFirstIndexAtOrAfter(allTimestamps, evalStartMs);

        var closes = allCloses[warmupIdx..];
        var timestamps = allTimestamps[warmupIdx..];
        var actualEvalStartMs = evalIdx < allTimestamps.Length ? allTimestamps[evalIdx] : evalStartMs;

        return new CrucibleWindow
        {
            Label = label,
            Weight = weight,
            Closes = closes,
            Timestamps = timestamps,
            EvalStartMs = actualEvalStartMs
        };
    }

    private static CrucibleWindow BuildFullWindow(
        decimal weight,
        decimal[] allCloses, long[] allTimestamps)
    {
        return new CrucibleWindow
        {
            Label = "full",
            Weight = weight,
            Closes = allCloses,
            Timestamps = allTimestamps,
            EvalStartMs = allTimestamps.Length > 0 ? allTimestamps[0] : 0
        };
    }

    private static int FindFirstIndexAtOrAfter(long[] timestamps, long targetMs)
    {
        for (int i = 0; i < timestamps.Length; i++)
            if (timestamps[i] >= targetMs) return i;
        return 0;
    }
}
