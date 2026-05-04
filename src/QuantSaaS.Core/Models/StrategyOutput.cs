using System.Collections.Generic;

namespace QuantSaaS.Core.Models;

/// <summary>
/// Output produced by Step(). Contains a list of trading intents.
/// The SaaS layer translates these intents into concrete TradeCommands.
/// </summary>
public record StrategyOutput
{
    /// <summary>List of buy/sell intents. Empty = no action this tick.</summary>
    public IReadOnlyList<TradingIntent> Intents { get; init; } = [];

    /// <summary>
    /// Updated strategy runtime state JSON blob.
    /// SaaS persists this and rehydrates it at the next tick.
    /// </summary>
    public string UpdatedRuntimeStateJson { get; init; } = "{}";

    /// <summary>Optional diagnostics forwarded to the audit log (non-trading signals).</summary>
    public string? DiagnosticsJson { get; init; }

    public static StrategyOutput NoAction(string runtimeStateJson = "{}") => new()
    {
        UpdatedRuntimeStateJson = runtimeStateJson
    };
}

/// <summary>
/// A single abstract trading intent.
/// SaaS converts this to a concrete TradeCommand for a specific broker.
/// </summary>
public record TradingIntent
{
    public TradingAction Action { get; init; }
    public EngineLayer Engine { get; init; }
    public LotType LotType { get; init; }

    /// <summary>
    /// For BUY intents: nominal quote-currency amount to spend.
    /// For SELL intents: 0 (use QtyAsset instead).
    /// </summary>
    public decimal AmountQuote { get; init; }

    /// <summary>
    /// For SELL intents: quantity of asset to sell.
    /// For BUY intents: 0 (use AmountQuote instead).
    /// </summary>
    public decimal QtyAsset { get; init; }
}

public enum TradingAction { Buy, Sell }
public enum EngineLayer { Macro, Micro }
public enum LotType { DeadStack, Floating, ColdSealed }
