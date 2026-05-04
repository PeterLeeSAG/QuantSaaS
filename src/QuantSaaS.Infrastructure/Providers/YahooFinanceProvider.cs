using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using QuantSaaS.Core.Interfaces;

namespace QuantSaaS.Infrastructure.Providers;

/// <summary>
/// Yahoo Finance historical data provider.
/// Used for backtest data ingestion (no API key required).
/// Note: Yahoo Finance is unofficial; for production use a licensed data feed.
/// </summary>
public sealed class YahooFinanceProvider : IMarketDataProvider
{
    private readonly HttpClient _http;

    public string ProviderId => "yahoo";

    public YahooFinanceProvider()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "QuantSaaS/1.0");
    }

    public async Task<IReadOnlyList<Bar>> FetchBarsAsync(
        string symbol,
        string timeframe,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        long fromEpoch = new DateTimeOffset(from).ToUnixTimeSeconds();
        long toEpoch = new DateTimeOffset(to).ToUnixTimeSeconds();
        string interval = MapTimeframe(timeframe);

        string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}" +
                     $"?period1={fromEpoch}&period2={toEpoch}&interval={interval}&events=div,split";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);

        return ParseYahooResponse(json);
    }

    public async Task<Bar?> FetchLatestBarAsync(string symbol, string timeframe, CancellationToken ct = default)
    {
        var bars = await FetchBarsAsync(symbol, timeframe, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, ct);
        return bars.Count > 0 ? bars[^1] : null;
    }

    private static string MapTimeframe(string timeframe) => timeframe.ToLower() switch
    {
        "1d" => "1d",
        "1h" => "1h",
        "4h" => "4h",
        "1w" => "1wk",
        "1m" => "1mo",
        _ => "1d",
    };

    private static IReadOnlyList<Bar> ParseYahooResponse(string json)
    {
        // Minimal JSON parsing without external dependencies
        var bars = new List<Bar>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var result = doc.RootElement
                .GetProperty("chart")
                .GetProperty("result")[0];

            var timestamps = result.GetProperty("timestamp");
            var quote = result.GetProperty("indicators").GetProperty("quote")[0];

            var opens = quote.GetProperty("open");
            var highs = quote.GetProperty("high");
            var lows = quote.GetProperty("low");
            var closes = quote.GetProperty("close");
            var volumes = quote.GetProperty("volume");

            for (int i = 0; i < timestamps.GetArrayLength(); i++)
            {
                long ts = timestamps[i].GetInt64();
                decimal o = GetDecimal(opens[i]);
                decimal h = GetDecimal(highs[i]);
                decimal l = GetDecimal(lows[i]);
                decimal c = GetDecimal(closes[i]);
                decimal v = GetDecimal(volumes[i]);

                if (c <= 0) continue;

                bars.Add(new Bar
                {
                    OpenTimeMs = ts * 1000,
                    Open = o,
                    High = h,
                    Low = l,
                    Close = c,
                    Volume = v,
                });
            }
        }
        catch
        {
            // Return empty on parse failure; caller logs and retries
        }
        return bars;
    }

    private static decimal GetDecimal(System.Text.Json.JsonElement el)
    {
        if (el.ValueKind == System.Text.Json.JsonValueKind.Number)
            return el.GetDecimal();
        return 0m;
    }
}
