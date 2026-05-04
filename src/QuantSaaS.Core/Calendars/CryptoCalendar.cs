using System;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Core.Calendars;

/// <summary>
/// Crypto market calendar: always open, 24/7/365, no settlement delay.
/// </summary>
public sealed class CryptoCalendar : IMarketCalendar
{
    public AssetClass AssetClass => AssetClass.Crypto;

    public bool IsOpen(DateTime utc) => true;

    public DateTime NextOpen(DateTime utc) => utc;

    public DateTime PrevClose(DateTime utc) => utc;

    public int TradingDaysInRange(DateTime from, DateTime to)
    {
        // Crypto trades every calendar day
        return (int)Math.Ceiling((to - from).TotalDays) + 1;
    }

    public DateTime AddSettlementDays(DateTime tradeDate, int settlementDays)
    {
        // No settlement delay for crypto
        return tradeDate;
    }
}
