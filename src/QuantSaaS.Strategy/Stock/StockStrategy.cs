using System;
using System.Collections.Generic;
using System.Text.Json;
using QuantSaaS.Core.Attributes;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;
using QuantSaaS.Quant;

namespace QuantSaaS.Strategy.Stock;

/// <summary>
/// US Stock / ETF strategy implementing IPureStrategy.
///
/// Position semantics:
///   DeadStack  = Long-term Holdings  (macro DCA accumulation, &gt; MinHoldingDays old)
///   FloatStack = Active Position     (available for Sigmoid micro-engine trading)
///   ColdSealed = Sealed Holdings     (permanently locked; never released)
///
/// Iron Rules:
///   – Step() is a pure function: no I/O, no DateTime.Now, no network calls.
///   – No if(isBacktest) branching anywhere.
///   – All features passed to Sigmoid are dimensionless.
///   – Settlement awareness: SpendableQuote = cash − pending settlement (resolved by ACL).
/// </summary>
[StrategyPurity]
public sealed class StockStrategy : IPureStrategy
{
    private readonly StockChromosome _params;

    public string StrategyId => "us-stock-v1";
    public string DisplayName => "US Equity Strategy v1";

    public StockStrategy(StockChromosome chromosome)
    {
        _params = chromosome;
    }

    public StrategyOutput Step(StrategyInput input)
    {
        // ── Guard: minimum data ───────────────────────────────────────────────
        int minBars = (int)Math.Max(_params.EmaLongBars, SigmoidEngine.MinBarsRequired);
        if (input.Closes.Length < minBars)
            return StrategyOutput.NoAction(input.RuntimeStateJson);

        decimal price = input.CurrentPrice;
        if (price <= 0m)
            return StrategyOutput.NoAction(input.RuntimeStateJson);

        var portfolio = input.Portfolio;
        decimal totalEquity = portfolio.TotalEquity;
        if (totalEquity <= 0m)
            return StrategyOutput.NoAction(input.RuntimeStateJson);

        // ── Market state ──────────────────────────────────────────────────────
        var market = input.Market;

        // ── Load runtime state ────────────────────────────────────────────────
        var state = DeserializeState(input.RuntimeStateJson);

        // ── Macro engine: monthly DCA into DeadStack ──────────────────────────
        var intents = new List<TradingIntent>();
        bool isMonthlyBoundary = IsMonthlyBoundary(input.Timestamps);
        if (isMonthlyBoundary && portfolio.SpendableQuote >= _params.MinOrderThreshold)
        {
            decimal maxAlloc = Math.Min(
                portfolio.SpendableQuote,
                totalEquity * _params.MaxAllocationFraction * (decimal)market.TimeDilationMultiplier);

            // Clamp to SpendableQuote and enforce minimum
            decimal macroAmount = Math.Max(0m, Math.Min(maxAlloc, portfolio.SpendableQuote));
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

        // ── Micro engine: Sigmoid dynamic weight on FloatStack ────────────────
        decimal floatValue = portfolio.FloatStackQty * price;
        decimal currentMicroWeight = totalEquity > 0m ? floatValue / totalEquity : 0m;

        var sigmoidInput = new SigmoidEngine.Input
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
        };

        var sigmoidOutput = SigmoidEngine.Compute(sigmoidInput);

        if (sigmoidOutput.OrderQuote > 0)
        {
            decimal amount = Math.Min(sigmoidOutput.OrderQuote, portfolio.SpendableQuote);
            if (amount >= _params.MinOrderThreshold)
            {
                intents.Add(new TradingIntent
                {
                    Action = TradingAction.Buy,
                    Engine = EngineLayer.Micro,
                    LotType = LotType.Floating,
                    AmountQuote = amount,
                });
            }
        }
        else if (sigmoidOutput.OrderQuote < 0)
        {
            decimal sellAmount = Math.Abs(sigmoidOutput.OrderQuote);
            decimal sellQty = sellAmount / price;
            decimal available = portfolio.FloatStackQty;

            // Check minimum holding period
            if (state.FloatStackAgeInDays >= (int)_params.MinHoldingDays && available > 0)
            {
                sellQty = Math.Min(sellQty, available);
                if (sellQty * price >= _params.MinOrderThreshold)
                {
                    intents.Add(new TradingIntent
                    {
                        Action = TradingAction.Sell,
                        Engine = EngineLayer.Micro,
                        LotType = LotType.Floating,
                        QtyAsset = sellQty,
                    });
                }
            }
        }

        // ── Update runtime state ──────────────────────────────────────────────
        state.LastProcessedBarMs = input.Timestamps[^1];
        if (intents.Count > 0)
            state.FloatStackAgeInDays = 0; // reset on any trade
        else
            state.FloatStackAgeInDays++;

        return new StrategyOutput
        {
            Intents = intents,
            UpdatedRuntimeStateJson = JsonSerializer.Serialize(state),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsMonthlyBoundary(long[] timestamps)
    {
        if (timestamps.Length < 2) return false;
        var prev = DateTimeOffset.FromUnixTimeMilliseconds(timestamps[^2]).UtcDateTime;
        var curr = DateTimeOffset.FromUnixTimeMilliseconds(timestamps[^1]).UtcDateTime;
        return curr.Month != prev.Month || curr.Year != prev.Year;
    }

    private static StockRuntimeState DeserializeState(string json)
    {
        try { return JsonSerializer.Deserialize<StockRuntimeState>(json) ?? new StockRuntimeState(); }
        catch { return new StockRuntimeState(); }
    }
}

internal sealed class StockRuntimeState
{
    public long LastProcessedBarMs { get; set; }
    public int FloatStackAgeInDays { get; set; }
}
