using System;
using System.Collections.Generic;
using System.Text.Json;
using QuantSaaS.Core.Attributes;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;
using QuantSaaS.Quant;

namespace QuantSaaS.Strategy.BtcSpot;

/// <summary>
/// BTC/USDT spot trading strategy.
/// Reference implementation of IPureStrategy for the Sigmoid macro/micro framework.
/// </summary>
[StrategyPurity]
public sealed class BtcSpotStrategy : IPureStrategy
{
    private readonly BtcChromosome _params;

    public string StrategyId => "btc-spot-v1";
    public string DisplayName => "BTC Spot Strategy v1";
    public string Version => "1.0.0";
    public bool IsSpotOnly => true;

    public BtcSpotStrategy(BtcChromosome chromosome)
    {
        _params = chromosome;
    }

    public StrategyOutput Step(StrategyInput input)
    {
        if (input.Closes.Length < SigmoidEngine.MinBarsRequired)
            return StrategyOutput.NoAction(input.RuntimeStateJson);

        decimal price = input.CurrentPrice;
        var portfolio = input.Portfolio;
        decimal totalEquity = portfolio.TotalEquity;
        if (price <= 0m || totalEquity <= 0m)
            return StrategyOutput.NoAction(input.RuntimeStateJson);

        var market = input.Market;
        var intents = new List<TradingIntent>();

        // ── Macro: monthly DCA ────────────────────────────────────────────────
        if (IsMonthlyBoundary(input.Timestamps))
        {
            decimal macroAmount = Math.Min(
                portfolio.SpendableQuote,
                _params.MonthlyMacroBuyQuote * (decimal)market.TimeDilationMultiplier);

            if (macroAmount >= _params.MinOrderThreshold)
            {
                intents.Add(new TradingIntent
                {
                    Action = TradingAction.Buy,
                    Engine = EngineLayer.Macro,
                    LotType = LotType.DeadStack,
                    AmountQuote = macroAmount,
                });
            }
        }

        // ── Micro: Sigmoid ────────────────────────────────────────────────────
        decimal floatValue = portfolio.FloatStackQty * price;
        decimal currentMicroWeight = totalEquity > 0 ? floatValue / totalEquity : 0m;

        var sigmoidOut = SigmoidEngine.Compute(new SigmoidEngine.Input
        {
            Closes = input.Closes,
            CurrentPrice = price,
            CurrentMicroWeight = currentMicroWeight,
            TotalEquity = totalEquity,
            SpendableQuote = portfolio.SpendableQuote,
            Beta = _params.Beta,
            Gamma = _params.Gamma,
            SigmaFloor = _params.SigmaFloor,
            CoefX1 = _params.CoefX1,
            CoefX2 = _params.CoefX2,
            CoefX3 = _params.CoefX3,
            DeltaWeightThreshold = _params.DeltaWeightThreshold,
            VolatilityRatioThreshold = _params.VolatilityRatioThreshold,
            MinOrderThreshold = _params.MinOrderThreshold,
            BetaMultiplier = (decimal)market.BetaMultiplier,
            IsQuiet = market.IsQuiet,
        });

        if (sigmoidOut.OrderQuote > 0)
        {
            decimal amount = Math.Min(sigmoidOut.OrderQuote, portfolio.SpendableQuote);
            if (amount >= _params.MinOrderThreshold)
                intents.Add(new TradingIntent
                {
                    Action = TradingAction.Buy, Engine = EngineLayer.Micro,
                    LotType = LotType.Floating, AmountQuote = amount,
                });
        }
        else if (sigmoidOut.OrderQuote < 0)
        {
            decimal sellQty = Math.Abs(sigmoidOut.OrderQuote) / price;
            sellQty = Math.Min(sellQty, portfolio.FloatStackQty);
            if (sellQty * price >= _params.MinOrderThreshold)
                intents.Add(new TradingIntent
                {
                    Action = TradingAction.Sell, Engine = EngineLayer.Micro,
                    LotType = LotType.Floating, QtyAsset = sellQty,
                });
        }

        var state = new BtcRuntimeState { LastProcessedBarMs = input.Timestamps[^1] };
        return new StrategyOutput
        {
            Intents = intents,
            UpdatedRuntimeStateJson = JsonSerializer.Serialize(state),
        };
    }

    private static bool IsMonthlyBoundary(long[] timestamps)
    {
        if (timestamps.Length < 2) return false;
        var prev = DateTimeOffset.FromUnixTimeMilliseconds(timestamps[^2]).UtcDateTime;
        var curr = DateTimeOffset.FromUnixTimeMilliseconds(timestamps[^1]).UtcDateTime;
        return curr.Month != prev.Month || curr.Year != prev.Year;
    }
}

internal sealed class BtcRuntimeState
{
    public long LastProcessedBarMs { get; set; }
}
