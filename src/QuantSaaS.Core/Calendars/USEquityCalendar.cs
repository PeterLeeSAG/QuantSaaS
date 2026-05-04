using System;
using System.Collections.Generic;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Core.Calendars;

/// <summary>
/// US Equity market calendar (NYSE / NASDAQ).
/// Regular hours: Monday–Friday 09:30–16:00 Eastern Time (ET).
/// Excludes US federal holidays and early-close days (e.g., day before Christmas).
/// </summary>
public sealed class USEquityCalendar : IMarketCalendar
{
    // Eastern Time zone
    private static readonly TimeZoneInfo ET =
        TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private static readonly TimeOnly MarketOpen = new(9, 30);
    private static readonly TimeOnly MarketClose = new(16, 0);

    public AssetClass AssetClass => AssetClass.Stock;

    /// <summary>
    /// Returns true if the US equity market is open at the given UTC instant.
    /// A market is "open" if: it is a business day, not a holiday, and the
    /// current ET time is in [09:30, 16:00).
    /// </summary>
    public bool IsOpen(DateTime utc)
    {
        var et = TimeZoneInfo.ConvertTimeFromUtc(utc, ET);
        if (et.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        if (IsHoliday(et.Date)) return false;
        var time = TimeOnly.FromDateTime(et);
        return time >= MarketOpen && time < MarketClose;
    }

    public DateTime NextOpen(DateTime utc)
    {
        var et = TimeZoneInfo.ConvertTimeFromUtc(utc, ET);
        var date = et.Date;
        // If today is already past close or is not a trading day, advance to tomorrow
        var currentTime = TimeOnly.FromDateTime(et);
        if (currentTime >= MarketClose || et.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || IsHoliday(date))
            date = date.AddDays(1);

        // Walk forward until we find an open trading day
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || IsHoliday(date))
            date = date.AddDays(1);

        var openEt = date.Add(MarketOpen.ToTimeSpan());
        return TimeZoneInfo.ConvertTimeToUtc(openEt, ET);
    }

    public DateTime PrevClose(DateTime utc)
    {
        var et = TimeZoneInfo.ConvertTimeFromUtc(utc, ET);
        var date = et.Date;
        // If market hasn't opened yet today, look at the previous trading day's close
        if (TimeOnly.FromDateTime(et) < MarketOpen ||
            et.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || IsHoliday(date))
            date = date.AddDays(-1);

        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || IsHoliday(date))
            date = date.AddDays(-1);

        var closeEt = date.Add(MarketClose.ToTimeSpan());
        return TimeZoneInfo.ConvertTimeToUtc(closeEt, ET);
    }

    public int TradingDaysInRange(DateTime from, DateTime to)
    {
        int count = 0;
        var d = from.Date;
        while (d <= to.Date)
        {
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && !IsHoliday(d))
                count++;
            d = d.AddDays(1);
        }
        return count;
    }

    public DateTime AddSettlementDays(DateTime tradeDate, int settlementDays)
    {
        var d = tradeDate.Date;
        int added = 0;
        while (added < settlementDays)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && !IsHoliday(d))
                added++;
        }
        return d;
    }

    // ── Holiday computation ───────────────────────────────────────────────────

    private static bool IsHoliday(DateTime date)
    {
        int y = date.Year, m = date.Month, d = date.Day;

        // New Year's Day
        if (m == 1 && d == 1) return true;
        if (m == 1 && d == 2 && date.DayOfWeek == DayOfWeek.Monday) return true; // observed

        // Martin Luther King Jr. Day – 3rd Monday in January
        if (m == 1 && date.DayOfWeek == DayOfWeek.Monday && d is >= 15 and <= 21) return true;

        // Presidents' Day – 3rd Monday in February
        if (m == 2 && date.DayOfWeek == DayOfWeek.Monday && d is >= 15 and <= 21) return true;

        // Good Friday
        var (gfM, gfD) = GoodFriday(y);
        if (m == gfM && d == gfD) return true;

        // Memorial Day – last Monday in May
        if (m == 5 && date.DayOfWeek == DayOfWeek.Monday && d >= 25) return true;

        // Juneteenth – June 19 (observed Mon if weekend)
        if (m == 6 && d == 19) return true;
        if (m == 6 && d == 20 && date.DayOfWeek == DayOfWeek.Monday) return true;
        if (m == 6 && d == 18 && date.DayOfWeek == DayOfWeek.Friday) return true;

        // Independence Day – July 4 (observed)
        if (m == 7 && d == 4) return true;
        if (m == 7 && d == 5 && date.DayOfWeek == DayOfWeek.Monday) return true;
        if (m == 7 && d == 3 && date.DayOfWeek == DayOfWeek.Friday) return true;

        // Labor Day – 1st Monday in September
        if (m == 9 && date.DayOfWeek == DayOfWeek.Monday && d <= 7) return true;

        // Thanksgiving – 4th Thursday in November
        if (m == 11 && date.DayOfWeek == DayOfWeek.Thursday && d is >= 22 and <= 28) return true;

        // Christmas – Dec 25 (observed)
        if (m == 12 && d == 25) return true;
        if (m == 12 && d == 26 && date.DayOfWeek == DayOfWeek.Monday) return true;
        if (m == 12 && d == 24 && date.DayOfWeek == DayOfWeek.Friday) return true;

        return false;
    }

    /// <summary>Computes Good Friday (month, day) using the Anonymous Gregorian algorithm.</summary>
    private static (int Month, int Day) GoodFriday(int year)
    {
        // Easter Sunday first, then subtract 2 days
        int a = year % 19, b = year / 100, c = year % 100;
        int d = b / 4, e = b % 4, f = (b + 8) / 25;
        int g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m2 = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m2 + 114) / 31;
        int day = ((h + l - 7 * m2 + 114) % 31) + 1;
        // Good Friday = Easter - 2 days
        var easter = new DateTime(year, month, day);
        var gf = easter.AddDays(-2);
        return (gf.Month, gf.Day);
    }
}
