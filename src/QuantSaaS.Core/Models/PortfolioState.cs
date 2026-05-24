namespace QuantSaaS.Core.Models;

/// <summary>
/// Snapshot of the strategy instance's portfolio.
/// Updated by DeltaReport from LocalAgent.
/// </summary>
public class PortfolioState
{
    public decimal UsdtBalance { get; set; }
    public decimal DeadBtc { get; set; }
    public decimal FloatBtc { get; set; }
    public decimal ColdSealedBtc { get; set; }
    public long LastProcessedBarTime { get; set; }
    public string Symbol { get; set; } = "BTCUSDT";
    public string AggregationPeriod { get; set; } = "1d";

    public decimal TotalEquity(decimal currentPrice) =>
        UsdtBalance + (DeadBtc + FloatBtc + ColdSealedBtc) * currentPrice;

    public decimal SpendableUsdt(decimal currentPrice, decimal microReservePct)
    {
        var equity = TotalEquity(currentPrice);
        var reserveFloor = equity * microReservePct;
        return Math.Max(0, UsdtBalance - reserveFloor);
    }

    public decimal CurrentMicroWeight(decimal currentPrice)
    {
        var equity = TotalEquity(currentPrice);
        if (equity <= 0) return 0;
        return FloatBtc * currentPrice / equity;
    }
}
