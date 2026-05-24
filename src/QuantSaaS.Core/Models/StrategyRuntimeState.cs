namespace QuantSaaS.Core.Models;

/// <summary>
/// Cross-tick persistent state for the strategy engine.
/// Stored as JSONB in Postgres, deserialized per tick.
/// </summary>
public class StrategyRuntimeState
{
    /// <summary>Last processed bar timestamp (for idempotency)</summary>
    public long LastProcessedBarTime { get; set; }

    /// <summary>Cumulative USDT spent on macro DCA purchases</summary>
    public decimal TotalMacroSpentUsdt { get; set; }

    /// <summary>Timestamp of last macro DCA purchase</summary>
    public long LastMacroBuyTime { get; set; }

    /// <summary>Running EMA short value (cached for warm start)</summary>
    public decimal CachedEmaShort { get; set; }

    /// <summary>Running EMA long value (cached for warm start)</summary>
    public decimal CachedEmaLong { get; set; }
}
