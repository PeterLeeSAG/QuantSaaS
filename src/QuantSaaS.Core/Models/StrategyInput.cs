using System;
using System.Collections.Generic;

namespace QuantSaaS.Core.Models;

/// <summary>
/// Immutable input snapshot injected into Step().
/// Iron Rule: Step() is a pure function; it MUST depend only on this record.
/// No I/O, no DateTime.Now, no network calls inside Step().
/// </summary>
public record StrategyInput
{
    // ── Market data ──────────────────────────────────────────────────────────

    /// <summary>
    /// Closing price sequence, most-recent last.
    /// Iron Rule: OHLCV → []decimal conversion happens in the ACL before Step().
    /// The strategy kernel never sees raw Bar objects.
    /// </summary>
    public decimal[] Closes { get; init; } = Array.Empty<decimal>();

    /// <summary>Unix-ms timestamps aligned 1:1 with Closes.</summary>
    public long[] Timestamps { get; init; } = Array.Empty<long>();

    /// <summary>Current mark-to-market price (latest close).</summary>
    public decimal CurrentPrice { get; init; }

    // ── Instrument ───────────────────────────────────────────────────────────

    /// <summary>Instrument being traded. Never null.</summary>
    public Instrument Instrument { get; init; } = null!;

    // ── Portfolio ────────────────────────────────────────────────────────────

    /// <summary>Immutable portfolio snapshot at the time this tick fires.</summary>
    public PortfolioState Portfolio { get; init; } = null!;

    // ── Market state ─────────────────────────────────────────────────────────

    /// <summary>Pre-computed market regime (computed by the perception layer before Step()).</summary>
    public MarketState Market { get; init; } = MarketState.Default;

    // ── Market context (dimensionless, computed by ACL) ───────────────────────

    /// <summary>
    /// Fraction of current trading session elapsed [0, 1].
    /// 0 = market open, 1 = market close. Always 0.5 for crypto.
    /// Iron Rule: This is a dimensionless ratio, not absolute wall-clock time.
    /// </summary>
    public decimal SessionFractionElapsed { get; init; } = 0.5m;

    /// <summary>
    /// Business days until next earnings release, normalized by 252 (trading year).
    /// 0 = today is earnings, 1 = one full trading year away. Always 1.0 for crypto.
    /// </summary>
    public decimal NormalizedDaysToEarnings { get; init; } = 1.0m;

    // ── Strategy runtime state ────────────────────────────────────────────────

    /// <summary>Strategy-specific persisted runtime state from the previous tick.</summary>
    public string RuntimeStateJson { get; init; } = "{}";
}
