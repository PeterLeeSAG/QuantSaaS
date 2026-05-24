using QuantSaaS.Core.Models;

namespace QuantSaaS.Core.Interfaces;

/// <summary>
/// Pure strategy interface. Same Step() called for both backtest and live trading.
/// Iron Rule: Step() MUST be deterministic and side-effect-free.
/// No if(isBacktest) branching allowed anywhere in implementations.
/// </summary>
public interface IPureStrategy
{
    string StrategyId { get; }
    string DisplayName { get; }
    string Version { get; }
    bool IsSpotOnly { get; }

    /// <summary>
    /// Pure function: StrategyInput → StrategyOutput.
    /// MUST NOT: perform I/O, access timers, read/write DB, call network.
    /// MUST: be deterministic - same input → identical output.
    /// </summary>
    StrategyOutput Step(StrategyInput input);
}
