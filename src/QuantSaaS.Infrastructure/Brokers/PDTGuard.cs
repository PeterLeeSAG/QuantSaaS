using System;
using System.Threading;
using System.Threading.Tasks;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Infrastructure.Brokers;

/// <summary>
/// Pattern Day Trader (PDT) rule guard for US equity accounts.
///
/// FINRA Rule 4210:
///   A "day trade" = buying and selling the same security in the same trading day.
///   Accounts with &lt; $25,000 equity may execute at most 3 day-trades in any rolling 5-business-day window.
///   Exceeding this limit results in the account being marked as a Pattern Day Trader and restricted.
///
/// This guard is enforced in LocalAgent, never in SaaS.
/// When a command would violate the PDT rule, the agent refuses it and reports via DeltaReport.refusal_reason.
/// </summary>
public sealed class PDTGuard
{
    private const int MaxDayTrades = 3;
    private const int RollingWindowDays = 5;
    private const decimal PDTEquityThreshold = 25_000m;

    private readonly IMarketCalendar _calendar;
    private readonly System.Collections.Generic.Queue<(DateTime TradeDayUtc, string Symbol)> _recentTrades = new();

    public PDTGuard(IMarketCalendar calendar)
    {
        _calendar = calendar;
    }

    /// <summary>
    /// Returns a refusal reason if executing <paramref name="command"/> would violate the PDT rule,
    /// or null if the command is permitted.
    /// </summary>
    /// <param name="command">Trade command to evaluate.</param>
    /// <param name="accountEquity">Current account equity in USD.</param>
    /// <param name="nowUtc">Current UTC time.</param>
    public string? CheckPDT(TradeCommand command, decimal accountEquity, DateTime nowUtc)
    {
        // PDT only applies to stocks/ETFs with equity below the threshold
        if (command.AssetClass == AssetClass.Crypto) return null;
        if (accountEquity >= PDTEquityThreshold) return null;
        if (command.Action.ToUpper() != "SELL") return null; // Opening buys are fine; closings trigger PDT

        // Remove trades outside the rolling 5-business-day window
        while (_recentTrades.Count > 0)
        {
            var oldest = _recentTrades.Peek().TradeDayUtc;
            if (_calendar.TradingDaysInRange(oldest, nowUtc.Date) > RollingWindowDays)
                _recentTrades.Dequeue();
            else
                break;
        }

        // Count day-trades: SELL of a symbol that was also bought today
        int dayTrades = CountDayTrades(command.Symbol, nowUtc.Date);
        if (dayTrades >= MaxDayTrades)
        {
            return $"{UserTerms.PDTRule}: executing this trade would exceed the 3-day-trade limit " +
                   $"in a rolling 5-business-day window for accounts under $25,000. " +
                   $"Command {command.ClientOrderId} refused.";
        }

        return null;
    }

    /// <summary>
    /// Records a completed day-trade (call after a SELL is filled on the same day as a BUY).
    /// </summary>
    public void RecordDayTrade(string symbol, DateTime tradeDayUtc)
    {
        _recentTrades.Enqueue((tradeDayUtc.Date, symbol));
    }

    private int CountDayTrades(string symbol, DateTime todayUtc)
    {
        int count = 0;
        foreach (var (day, sym) in _recentTrades)
        {
            if (day == todayUtc.Date && sym == symbol)
                count++;
        }
        return count;
    }
}
