using QuantSaaS.Quant;

namespace QuantSaaS.Tests.Quant;

public class CrucibleWindowBuilderTests
{
    private static long[] MakeTimestamps(int count, long startMs = 1_000_000_000_000L, long stepMs = 86_400_000L)
    {
        var ts = new long[count];
        for (int i = 0; i < count; i++) ts[i] = startMs + i * stepMs;
        return ts;
    }

    [Fact]
    public void Build_EmptyCloses_ReturnsEmpty()
    {
        var windows = CrucibleWindowBuilder.Build([], []);
        Assert.Empty(windows);
    }

    [Fact]
    public void Build_InsufficientBars_ReturnsFourWindows()
    {
        // Even with only 10 bars, Build always returns 4 windows (with warmup taken from start)
        var closes = Enumerable.Repeat(100m, 10).ToArray();
        var ts = MakeTimestamps(10);
        var windows = CrucibleWindowBuilder.Build(closes, ts);
        Assert.Equal(4, windows.Length);
    }

    [Fact]
    public void Build_LargeDataset_ReturnsFourWindows()
    {
        int years = 10;
        int days = years * 365;
        var closes = Enumerable.Range(1, days).Select(i => (decimal)(100 + i)).ToArray();
        var ts = MakeTimestamps(days);
        var windows = CrucibleWindowBuilder.Build(closes, ts);
        Assert.Equal(4, windows.Length);
    }

    [Fact]
    public void Build_Windows_HaveCorrectLabels()
    {
        var closes = Enumerable.Repeat(100m, 3000).ToArray();
        var ts = MakeTimestamps(3000);
        var windows = CrucibleWindowBuilder.Build(closes, ts);
        Assert.Equal("6m", windows[0].Label);
        Assert.Equal("2y", windows[1].Label);
        Assert.Equal("5y", windows[2].Label);
        Assert.Equal("full", windows[3].Label);
    }

    [Fact]
    public void Build_Windows_HaveCorrectWeights()
    {
        var closes = Enumerable.Repeat(100m, 3000).ToArray();
        var ts = MakeTimestamps(3000);
        var windows = CrucibleWindowBuilder.Build(closes, ts);
        Assert.Equal(0.10m, windows[0].Weight);
        Assert.Equal(0.20m, windows[1].Weight);
        Assert.Equal(0.30m, windows[2].Weight);
        Assert.Equal(0.40m, windows[3].Weight);
    }

    [Fact]
    public void Build_TotalWeight_SumsToOne()
    {
        var closes = Enumerable.Repeat(100m, 3000).ToArray();
        var ts = MakeTimestamps(3000);
        var windows = CrucibleWindowBuilder.Build(closes, ts);
        var totalWeight = windows.Sum(w => w.Weight);
        Assert.InRange(totalWeight, 0.999m, 1.001m);
    }

    [Fact]
    public void Build_FullWindow_ContainsAllData()
    {
        var closes = Enumerable.Repeat(100m, 200).ToArray();
        var ts = MakeTimestamps(200);
        var windows = CrucibleWindowBuilder.Build(closes, ts);
        var fullWindow = windows.First(w => w.Label == "full");
        Assert.Equal(200, fullWindow.Closes.Length);
    }

    [Fact]
    public void Build_WindowClosesLength_AlignedWithTimestamps()
    {
        var closes = Enumerable.Repeat(100m, 3000).ToArray();
        var ts = MakeTimestamps(3000);
        var windows = CrucibleWindowBuilder.Build(closes, ts);
        foreach (var window in windows)
        {
            Assert.Equal(window.Closes.Length, window.Timestamps.Length);
        }
    }

    [Fact]
    public void Build_ShorterWindows_HaveFewerBars()
    {
        var closes = Enumerable.Repeat(100m, 3000).ToArray();
        var ts = MakeTimestamps(3000);
        var windows = CrucibleWindowBuilder.Build(closes, ts);

        // 6m window should have far fewer bars than full window
        Assert.True(windows[0].Closes.Length < windows[3].Closes.Length);
        // Windows ordered from short to long (more bars each step)
        Assert.True(windows[0].Closes.Length <= windows[1].Closes.Length);
        Assert.True(windows[1].Closes.Length <= windows[2].Closes.Length);
        Assert.True(windows[2].Closes.Length <= windows[3].Closes.Length);
    }
}
