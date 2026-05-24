using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using QuantSaaS.Core.Interfaces;

namespace QuantSaaS.Infrastructure.Providers;

/// <summary>
/// Corporate action feed backed by Alpaca's corporate actions API.
/// Returns splits and dividends for US equity symbols.
/// </summary>
public sealed class AlpacaCorporateActionFeed : ICorporateActionFeed
{
    private readonly HttpClient _http;

    public AlpacaCorporateActionFeed(string apiKey, string secretKey)
    {
        _http = new HttpClient { BaseAddress = new Uri("https://data.alpaca.markets") };
        _http.DefaultRequestHeaders.Add("APCA-API-KEY-ID", apiKey);
        _http.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", secretKey);
    }

    public async Task<IReadOnlyList<CorporateAction>> GetActionsAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var result = new List<CorporateAction>();

        // Fetch splits
        var splitsUrl = $"/v1beta1/corporate-actions?symbols={symbol}&types=forward_split,reverse_split" +
                        $"&start={from:yyyy-MM-dd}&end={to:yyyy-MM-dd}";
        try
        {
            var splits = await _http.GetFromJsonAsync<AlpacaSplitResponse>(splitsUrl, ct);
            foreach (var s in splits?.ForwardSplits ?? [])
            {
                result.Add(new CorporateAction
                {
                    Symbol = symbol,
                    Type = CorporateActionType.Split,
                    ExDateUtc = s.ExDate,
                    Amount = s.NewRate / s.OldRate,
                });
            }
        }
        catch { /* swallow – return partial results */ }

        // Fetch dividends
        var divsUrl = $"/v1beta1/corporate-actions?symbols={symbol}&types=cash_dividend" +
                      $"&start={from:yyyy-MM-dd}&end={to:yyyy-MM-dd}";
        try
        {
            var divs = await _http.GetFromJsonAsync<AlpacaDividendResponse>(divsUrl, ct);
            foreach (var d in divs?.CashDividends ?? [])
            {
                result.Add(new CorporateAction
                {
                    Symbol = symbol,
                    Type = CorporateActionType.Dividend,
                    ExDateUtc = d.ExDate,
                    Amount = d.Rate,
                });
            }
        }
        catch { /* swallow */ }

        return result;
    }

    // ── Response DTOs ─────────────────────────────────────────────────────────

    private sealed class AlpacaSplitResponse
    {
        public List<SplitItem>? ForwardSplits { get; set; }
        public List<SplitItem>? ReverseSplits { get; set; }
    }

    private sealed class SplitItem
    {
        public DateTime ExDate { get; set; }
        public decimal OldRate { get; set; }
        public decimal NewRate { get; set; }
    }

    private sealed class AlpacaDividendResponse
    {
        public List<DividendItem>? CashDividends { get; set; }
    }

    private sealed class DividendItem
    {
        public DateTime ExDate { get; set; }
        public decimal Rate { get; set; }
    }
}
