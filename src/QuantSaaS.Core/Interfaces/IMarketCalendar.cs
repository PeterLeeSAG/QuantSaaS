using System;
using System.Collections.Generic;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Core.Interfaces;

/// <summary>
/// Market calendar interface.
/// Determines when a market is open for trading, and computes settlement dates.
/// Iron Rule: Implementations must be injected from outside Step().
/// Step() never calls this interface; the ACL calls it before building StrategyInput.
/// </summary>
public interface IMarketCalendar
{
    /// <summary>Asset class this calendar governs.</summary>
    AssetClass AssetClass { get; }

    /// <summary>Returns true if the market is open at the given UTC instant.</summary>
    bool IsOpen(DateTime utc);

    /// <summary>Returns the next market-open instant at or after <paramref name="utc"/>.</summary>
    DateTime NextOpen(DateTime utc);

    /// <summary>Returns the most recent market-close instant at or before <paramref name="utc"/>.</summary>
    DateTime PrevClose(DateTime utc);

    /// <summary>Returns the number of trading days in [from, to] (both inclusive).</summary>
    int TradingDaysInRange(DateTime from, DateTime to);

    /// <summary>
    /// Adds <paramref name="settlementDays"/> business days to <paramref name="tradeDate"/>.
    /// Used to compute T+2 settlement dates.
    /// </summary>
    DateTime AddSettlementDays(DateTime tradeDate, int settlementDays);
}
