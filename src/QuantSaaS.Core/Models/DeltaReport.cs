using System;
using System.Collections.Generic;

namespace QuantSaaS.Core.Models;

/// <summary>
/// Execution report sent from LocalAgent to SaaS after a trade (or on reconnect).
/// Extended to carry equity-specific settlement data.
/// </summary>
public record DeltaReport
{
    /// <summary>
    /// Matching client order ID from the TradeCommand.
    /// Null when this is an initial balance snapshot (sent on agent reconnect).
    /// </summary>
    public string? ClientOrderId { get; init; }

    /// <summary>Current balances for all assets held in the brokerage account.</summary>
    public IReadOnlyList<AssetBalance> Balances { get; init; } = [];

    /// <summary>Execution details for the matched TradeCommand.</summary>
    public ExecutionDetail? Execution { get; init; }

    /// <summary>
    /// Snapshot of open T+2 settlement items (equity accounts only).
    /// SaaS uses this to update the pending_settlements ledger accurately.
    /// </summary>
    public IReadOnlyList<PendingSettlementItem> PendingSettlements { get; init; } = [];

    /// <summary>
    /// Non-null when the agent refused to execute a command (e.g., PDT limit reached).
    /// </summary>
    public string? RefusalReason { get; init; }
}

public record AssetBalance
{
    public string Asset { get; init; } = string.Empty;
    public decimal Available { get; init; }
    public decimal Frozen { get; init; }
}

public record ExecutionDetail
{
    public decimal FilledQty { get; init; }
    public decimal FilledPrice { get; init; }
    public decimal Fee { get; init; }
    public string Status { get; init; } = string.Empty;  // "filled" | "partial" | "failed"
    public DateTime TimestampUtc { get; init; }
}
