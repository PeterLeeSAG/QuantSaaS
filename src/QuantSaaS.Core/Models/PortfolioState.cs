using System;
using System.Collections.Generic;

namespace QuantSaaS.Core.Models;

/// <summary>
/// Immutable snapshot of the portfolio state passed into Step().
/// QuoteCurrency replaces the earlier hard-coded USDT references to support
/// both crypto (USDT) and equity (USD) instruments.
/// </summary>
public record PortfolioState
{
    // ── Quote currency balances ───────────────────────────────────────────────

    /// <summary>
    /// Spendable quote-currency balance = cash − pending settlement.
    /// For crypto: USDT available. For stocks: USD cash minus T+2 obligations.
    /// </summary>
    public decimal SpendableQuote { get; init; }

    /// <summary>Raw cash balance before subtracting pending settlement.</summary>
    public decimal RawCashBalance { get; init; }

    /// <summary>Total quote proceeds locked in open T+2 settlements.</summary>
    public decimal PendingSettlementAmount { get; init; }

    // ── Position stacks ───────────────────────────────────────────────────────

    /// <summary>
    /// Long-term holdings (DeadStack): acquired by the macro DCA engine.
    /// Crypto: BTC quantity. Stocks/ETFs: share quantity.
    /// </summary>
    public decimal DeadStackQty { get; init; }

    /// <summary>
    /// Active position (FloatStack): available for tactical micro-engine trading.
    /// Crypto: BTC quantity. Stocks/ETFs: share quantity.
    /// </summary>
    public decimal FloatStackQty { get; init; }

    /// <summary>
    /// Sealed holdings (ColdSealedStack): permanently locked, never released.
    /// </summary>
    public decimal ColdSealedQty { get; init; }

    // ── Equity ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Total portfolio equity in quote currency at the current mark-to-market price.
    /// TotalEquity = SpendableQuote + PendingSettlementAmount + (DeadStack + FloatStack + ColdSealed) × MarkPrice
    /// </summary>
    public decimal TotalEquity { get; init; }

    // ── State tracking ────────────────────────────────────────────────────────

    /// <summary>Unix-ms timestamp of the last bar processed by Step().</summary>
    public long LastProcessedBarMs { get; init; }

    /// <summary>Pending settlement items (for equity instruments, T+2).</summary>
    public IReadOnlyList<PendingSettlementItem> PendingSettlements { get; init; } = [];
}

/// <summary>Represents proceeds from a sell trade awaiting T+2 settlement.</summary>
public record PendingSettlementItem
{
    public string ClientOrderId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime SettlementDateUtc { get; init; }
}
