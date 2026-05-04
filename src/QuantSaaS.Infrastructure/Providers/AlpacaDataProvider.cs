using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using QuantSaaS.Core.Interfaces;

namespace QuantSaaS.Infrastructure.Providers;

/// <summary>
/// Alpaca market data provider for US stocks and ETFs.
/// Uses the Alpaca Data API v2.
/// </summary>
public sealed class AlpacaDataProvider : IMarketDataProvider
{
    private const string BaseUrl = "https://data.alpaca.markets";
    private readonly HttpClient _http;

    public string ProviderId => "alpaca";

    public AlpacaDataProvider(string apiKey, string secretKey)
    {
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _http.DefaultRequestHeaders.Add("APCA-API-KEY-ID", apiKey);
        _http.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", secretKey);
    }

    public async Task<IReadOnlyList<Bar>> FetchBarsAsync(
        string symbol,
        string timeframe,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var bars = new List<Bar>();
        string? pageToken = null;

        do
        {
            string url = $"/v2/stocks/{Uri.EscapeDataString(symbol)}/bars" +
                         $"?timeframe={timeframe}" +
                         $"&start={from:O}&end={to:O}" +
                         $"&limit=10000&adjustment=split" +
                         (pageToken != null ? $"&page_token={pageToken}" : "");

            var response = await _http.GetFromJsonAsync<AlpacaBarsResponse>(url, ct);
            if (response?.Bars != null)
            {
                foreach (var b in response.Bars)
                {
                    bars.Add(new Bar
                    {
                        OpenTimeMs = new DateTimeOffset(b.T).ToUnixTimeMilliseconds(),
                        Open = b.O,
                        High = b.H,
                        Low = b.L,
                        Close = b.C,
                        Volume = b.V,
                    });
                }
            }

            pageToken = response?.NextPageToken;
        } while (pageToken != null);

        return bars;
    }

    public async Task<Bar?> FetchLatestBarAsync(string symbol, string timeframe, CancellationToken ct = default)
    {
        var response = await _http.GetFromJsonAsync<AlpacaLatestBarResponse>(
            $"/v2/stocks/{Uri.EscapeDataString(symbol)}/bars/latest?timeframe={timeframe}", ct);

        var b = response?.Bar;
        if (b == null) return null;

        return new Bar
        {
            OpenTimeMs = new DateTimeOffset(b.T).ToUnixTimeMilliseconds(),
            Open = b.O,
            High = b.H,
            Low = b.L,
            Close = b.C,
            Volume = b.V,
        };
    }

    // ── Response DTOs ─────────────────────────────────────────────────────────

    private sealed class AlpacaBarsResponse
    {
        public List<AlpacaBar>? Bars { get; set; }
        public string? NextPageToken { get; set; }
    }

    private sealed class AlpacaLatestBarResponse
    {
        public AlpacaBar? Bar { get; set; }
    }

    private sealed class AlpacaBar
    {
        public DateTime T { get; set; }
        public decimal O { get; set; }
        public decimal H { get; set; }
        public decimal L { get; set; }
        public decimal C { get; set; }
        public decimal V { get; set; }
    }
}
