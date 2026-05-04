using QuantSaaS.Core.Models;

namespace QuantSaaS.Core.Interfaces;

/// <summary>
/// Pure strategy interface.
/// Iron Rule: Step() is a pure function – deterministic, no I/O, no timers,
/// no network calls, no database access, no DateTime.Now.
/// Identical implementation is used for both backtest and live trading.
/// </summary>
public interface IPureStrategy
{
    /// <summary>Strategy unique identifier (e.g., "btc-spot-v1", "us-stock-v1").</summary>
    string StrategyId { get; }

    /// <summary>Human-readable display name (no internal terms).</summary>
    string DisplayName { get; }

    /// <summary>
    /// Pure function: StrategyInput → StrategyOutput.
    /// MUST NOT: perform I/O, access system clock, read/write database, call network.
    /// MUST: be deterministic – same input always produces identical output.
    /// </summary>
    StrategyOutput Step(StrategyInput input);
}
