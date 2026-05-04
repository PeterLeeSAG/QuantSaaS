using System;
using System.Collections.Generic;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;

namespace QuantSaaS.SaaS.Services;

/// <summary>
/// Idempotent cron tick driver.
/// Responsibilities:
///   1. Verify the instance is running.
///   2. Check market calendar (skip if market is closed).
///   3. Idempotent bucket check: skip if this bar was already processed.
///   4. Build StrategyInput via the ACL.
///   5. Call Step() (pure function, no I/O).
///   6. Translate StrategyOutput into TradeCommands.
///   7. Persist updated RuntimeState and PortfolioState.
///   8. Dispatch TradeCommands to the connected Agent over WebSocket.
/// </summary>
public sealed class IdempotentTickDriver
{
    private readonly IMarketCalendar _calendar;

    public IdempotentTickDriver(IMarketCalendar calendar)
    {
        _calendar = calendar;
    }

    /// <summary>
    /// Executes one cron tick for a strategy instance.
    /// Returns the commands dispatched to the agent (empty if skipped or no-action).
    /// </summary>
    public IReadOnlyList<TradeCommand> ExecuteTick(
        TickContext ctx,
        Func<StrategyInput, StrategyOutput> stepFn,
        StrategyInput input)
    {
        // ── Guard: instance not running ───────────────────────────────────────
        if (ctx.Status != "running")
            return [];

        // ── Guard: market closed ──────────────────────────────────────────────
        if (!_calendar.IsOpen(ctx.NowUtc))
            return [];

        // ── Idempotent bucket check ───────────────────────────────────────────
        if (input.Timestamps.Length > 0 &&
            input.Timestamps[^1] <= input.Portfolio.LastProcessedBarMs)
            return [];

        // ── Call Step() (pure function) ───────────────────────────────────────
        var output = stepFn(input);

        // ── Translate intents to TradeCommands ────────────────────────────────
        var commands = new List<TradeCommand>();
        foreach (var intent in output.Intents)
        {
            long ts = input.Timestamps[^1];
            string orderId = $"inst{ctx.InstanceId}-{intent.Engine.ToString().ToUpper()}-{ts}";

            commands.Add(new TradeCommand
            {
                ClientOrderId = orderId,
                Action = intent.Action == TradingAction.Buy ? "BUY" : "SELL",
                Engine = intent.Engine.ToString().ToUpper(),
                Symbol = input.Instrument.Symbol,
                AssetClass = input.Instrument.AssetClass,
                AmountQuote = intent.AmountQuote > 0 ? intent.AmountQuote : null,
                QtyAsset = intent.QtyAsset > 0 ? intent.QtyAsset : null,
                LotType = intent.LotType.ToString().ToUpper(),
            });
        }

        // ── Persist updated runtime state (caller's responsibility to save ctx) ─
        ctx.UpdatedRuntimeStateJson = output.UpdatedRuntimeStateJson;
        ctx.LastProcessedBarMs = input.Timestamps.Length > 0 ? input.Timestamps[^1] : 0;

        return commands;
    }
}

public sealed class TickContext
{
    public Guid InstanceId { get; init; }
    public string Status { get; set; } = "stopped";
    public DateTime NowUtc { get; init; } = DateTime.UtcNow;

    // Outputs (updated by ExecuteTick)
    public string UpdatedRuntimeStateJson { get; set; } = "{}";
    public long LastProcessedBarMs { get; set; }
}
