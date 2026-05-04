using QuantSaaS.Core.Models;

namespace QuantSaaS.Quant;

/// <summary>
/// Cost model abstraction for backtest engine.
/// Computes the effective fill price including slippage and commission.
/// </summary>
public abstract class CostModel
{
    /// <summary>
    /// Returns the effective fill price after costs.
    /// For a BUY: filled price may be slightly above the bar's close (slippage).
    /// For a SELL: filled price may be slightly below the bar's close.
    /// </summary>
    public abstract decimal ComputeFillPrice(
        Instrument instrument,
        decimal nominalPrice,
        TradingAction direction);

    /// <summary>
    /// Returns the commission charged for a trade in quote currency.
    /// </summary>
    public abstract decimal ComputeCommission(
        Instrument instrument,
        decimal qty,
        decimal fillPrice,
        TradingAction direction);
}

/// <summary>
/// Zero-cost model: used for crypto backtests where fee is baked into SpawnPoint.RiskBounds.FeeRate.
/// </summary>
public sealed class ZeroCostModel : CostModel
{
    public static readonly ZeroCostModel Instance = new();

    public override decimal ComputeFillPrice(Instrument instrument, decimal nominalPrice, TradingAction direction)
        => nominalPrice;

    public override decimal ComputeCommission(Instrument instrument, decimal qty, decimal fillPrice, TradingAction direction)
        => 0m;
}

/// <summary>
/// Fixed per-share commission model (e.g., $0.005/share for IBKR).
/// Alpaca charges $0 commissions for US equities – use ratePerShare = 0.
/// </summary>
public sealed class FixedCommissionModel : CostModel
{
    private readonly decimal _ratePerShare;
    private readonly decimal _slippageFraction;

    /// <param name="ratePerShare">Commission per share (e.g., 0.005 for IBKR).</param>
    /// <param name="slippageFraction">Fraction of fill price added as slippage (e.g., 0.0001).</param>
    public FixedCommissionModel(decimal ratePerShare = 0m, decimal slippageFraction = 0.0001m)
    {
        _ratePerShare = ratePerShare;
        _slippageFraction = slippageFraction;
    }

    public override decimal ComputeFillPrice(Instrument instrument, decimal nominalPrice, TradingAction direction)
    {
        // BUY: pay slightly more; SELL: receive slightly less
        decimal slippage = nominalPrice * _slippageFraction;
        return direction == TradingAction.Buy
            ? nominalPrice + slippage
            : nominalPrice - slippage;
    }

    public override decimal ComputeCommission(Instrument instrument, decimal qty, decimal fillPrice, TradingAction direction)
        => _ratePerShare * qty;
}
