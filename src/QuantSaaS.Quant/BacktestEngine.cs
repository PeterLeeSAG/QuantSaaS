using System;
using System.Collections.Generic;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Quant;

/// <summary>
/// Backtest engine adapter.
/// Converts a historical bar sequence into a series of Step() calls,
/// tracks NAV / positions, and returns a BacktestResult.
///
/// Iron Rules:
///   – Same Step() implementation used here and in live trading.
///   – No if(isBacktest) inside Step(); the engine controls the context externally.
///   – For equity instruments, bars outside market hours are skipped.
///   – All price adjustments (splits, dividends) are applied before calling Step().
/// </summary>
public sealed class BacktestEngine
{
    private readonly IPureStrategy _strategy;
    private readonly CostModel _costModel;
    private readonly IMarketCalendar? _calendar;

    public BacktestEngine(
        IPureStrategy strategy,
        CostModel? costModel = null,
        IMarketCalendar? calendar = null)
    {
        _strategy = strategy;
        _costModel = costModel ?? ZeroCostModel.Instance;
        _calendar = calendar;
    }

    /// <summary>
    /// Runs a full backtest on the provided bar sequence.
    /// </summary>
    /// <param name="allBars">Full bar sequence including warmup prefix (bars[evalStartIdx..] are evaluated).</param>
    /// <param name="evalStartIdx">Index of the first bar that counts toward the fitness score.</param>
    /// <param name="instrument">Instrument being traded.</param>
    /// <param name="spawn">Epoch-frozen capital and risk policy.</param>
    /// <param name="dividendsPerBar">Optional per-bar dividend per share (for equity instruments).</param>
    public BacktestResult Run(
        Bar[] allBars,
        int evalStartIdx,
        Instrument instrument,
        SpawnPoint spawn,
        IReadOnlyDictionary<int, decimal>? dividendsPerBar = null)
    {
        if (allBars is null || allBars.Length == 0)
            return BacktestResult.Empty;

        // ── ACL: extract closes and timestamps ────────────────────────────────
        decimal[] closes = new decimal[allBars.Length];
        long[] tsMs = new long[allBars.Length];
        for (int i = 0; i < allBars.Length; i++)
        {
            closes[i] = allBars[i].Close;
            tsMs[i] = allBars[i].OpenTimeMs;
        }

        // ── Initial state ─────────────────────────────────────────────────────
        // Seed capital = first monthly injection (no separate initial-capital field in this engine version)
        decimal cash = spawn.Capital.MonthlyInjectQuote;
        decimal deadStack = 0m, floatStack = 0m;
        string runtimeState = "{}";

        int evalBars = allBars.Length - evalStartIdx;
        var navCurve = new decimal[evalBars];
        var cashFlows = new List<(decimal, int)>();
        cashFlows.Add((spawn.Capital.MonthlyInjectQuote, 0));

        DateTime? lastInjectMonth = null;
        int navIdx = 0;

        for (int i = 1; i < allBars.Length; i++)
        {
            decimal price = allBars[i].Close;
            DateTime barUtc = DateTimeOffset.FromUnixTimeMilliseconds(tsMs[i]).UtcDateTime;

            // ── Skip if market is closed (equity only) ────────────────────────
            if (_calendar != null && !_calendar.IsOpen(barUtc))
                continue;

            // ── Apply dividends before Step() ────────────────────────────────
            if (dividendsPerBar != null && dividendsPerBar.TryGetValue(i, out decimal div) && div > 0)
            {
                decimal divCash = (deadStack + floatStack) * div;
                cash += divCash; // dividend credited as cash
            }

            // ── Monthly injection ─────────────────────────────────────────────
            if (IsFirstTradingDayOfMonth(barUtc, i, tsMs) &&
                (lastInjectMonth == null ||
                 new DateTime(lastInjectMonth.Value.Year, lastInjectMonth.Value.Month, 1)
                 < new DateTime(barUtc.Year, barUtc.Month, 1)))
            {
                if (i > evalStartIdx)
                {
                    cash += spawn.Capital.MonthlyInjectQuote;
                    cashFlows.Add((spawn.Capital.MonthlyInjectQuote, navIdx));
                }
                lastInjectMonth = barUtc;
            }

            // ── Build StrategyInput ───────────────────────────────────────────
            decimal totalEquity = cash + (deadStack + floatStack) * price;
            decimal currentMicroWeight = floatStack * price / Math.Max(totalEquity, 1m);
            decimal spendableQuote = Math.Max(0m, cash);

            var portfolio = new PortfolioState
            {
                SpendableQuote = spendableQuote,
                RawCashBalance = cash,
                DeadStackQty = deadStack,
                FloatStackQty = floatStack,
                TotalEquity = totalEquity,
                LastProcessedBarMs = tsMs[i - 1],
            };

            var input = new StrategyInput
            {
                Closes = closes[..i],
                Timestamps = tsMs[..i],
                CurrentPrice = price,
                Instrument = instrument,
                Portfolio = portfolio,
                RuntimeStateJson = runtimeState,
            };

            // ── Call Step() ───────────────────────────────────────────────────
            var output = _strategy.Step(input);
            runtimeState = output.UpdatedRuntimeStateJson;

            // ── Execute intents ───────────────────────────────────────────────
            foreach (var intent in output.Intents)
            {
                if (intent.Action == TradingAction.Buy && intent.AmountQuote > 0)
                {
                    decimal amount = Math.Min(intent.AmountQuote, spendableQuote);
                    if (amount < instrument.LotMin * price) continue;

                    decimal fillPrice = _costModel.ComputeFillPrice(instrument, price, TradingAction.Buy);
                    decimal qty = FloorToLotStep(amount / fillPrice, instrument.LotStep, instrument.FractionAllowed);
                    decimal commission = _costModel.ComputeCommission(instrument, qty, fillPrice, TradingAction.Buy);
                    decimal cost = qty * fillPrice + commission;

                    if (cost > spendableQuote) continue;
                    cash -= cost;
                    spendableQuote -= cost;

                    if (intent.LotType == LotType.DeadStack) deadStack += qty;
                    else floatStack += qty;
                }
                else if (intent.Action == TradingAction.Sell && intent.QtyAsset > 0)
                {
                    decimal qty = Math.Min(intent.QtyAsset, floatStack);
                    if (qty <= 0) continue;

                    decimal fillPrice = _costModel.ComputeFillPrice(instrument, price, TradingAction.Sell);
                    decimal commission = _costModel.ComputeCommission(instrument, qty, fillPrice, TradingAction.Sell);
                    decimal proceeds = qty * fillPrice - commission;

                    floatStack -= qty;
                    cash += proceeds;
                }
            }

            // ── Record NAV after eval start ───────────────────────────────────
            if (i >= evalStartIdx)
            {
                navCurve[navIdx++] = cash + (deadStack + floatStack) * price;
            }
        }

        if (navIdx == 0)
            return BacktestResult.Empty;

        // Trim to actual length
        var evalNav = navCurve[..navIdx];
        decimal roi = ModifiedDietz.Calculate(evalNav, cashFlows);
        decimal maxDD = ModifiedDietz.MaxDrawdown(evalNav);

        return new BacktestResult
        {
            FinalEquity = evalNav[^1],
            ROI = roi,
            MaxDrawdown = maxDD,
            EvaluatedBars = navIdx,
        };
    }

    private static decimal FloorToLotStep(decimal qty, decimal lotStep, bool fractionAllowed)
    {
        if (fractionAllowed) return qty;
        return Math.Floor(qty / lotStep) * lotStep;
    }

    private static bool IsFirstTradingDayOfMonth(DateTime barUtc, int i, long[] tsMs)
    {
        if (i == 0) return true;
        var prev = DateTimeOffset.FromUnixTimeMilliseconds(tsMs[i - 1]).UtcDateTime;
        return barUtc.Month != prev.Month || barUtc.Year != prev.Year;
    }
}

public record BacktestResult
{
    public decimal FinalEquity { get; init; }
    public decimal ROI { get; init; }
    public decimal MaxDrawdown { get; init; }
    public int EvaluatedBars { get; init; }

    public static BacktestResult Empty => new();
}
