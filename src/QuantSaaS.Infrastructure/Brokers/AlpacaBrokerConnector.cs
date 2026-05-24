using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Infrastructure.Brokers;

/// <summary>
/// Alpaca Markets broker connector for US stocks and ETFs.
/// Paper and live trading via Alpaca's REST API.
/// Iron Rule: API credentials NEVER leave LocalAgent – they are read from config.agent.yaml only.
/// </summary>
public sealed class AlpacaBrokerConnector : IBrokerConnector
{
    private const string PaperBaseUrl = "https://paper-api.alpaca.markets";
    private const string LiveBaseUrl = "https://api.alpaca.markets";
    private const string DataBaseUrl = "https://data.alpaca.markets";

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public string ConnectorId => "alpaca";

    /// <param name="apiKey">Alpaca API key (LocalAgent only, never in SaaS).</param>
    /// <param name="secretKey">Alpaca secret key (LocalAgent only).</param>
    /// <param name="isPaper">True to use paper-trading endpoint.</param>
    public AlpacaBrokerConnector(string apiKey, string secretKey, bool isPaper = true)
    {
        _baseUrl = isPaper ? PaperBaseUrl : LiveBaseUrl;
        _http = new HttpClient();
        _http.BaseAddress = new Uri(_baseUrl);
        _http.DefaultRequestHeaders.Add("APCA-API-KEY-ID", apiKey);
        _http.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", secretKey);
    }

    public async Task<OrderResult> PlaceOrderAsync(TradeCommand command, CancellationToken ct = default)
    {
        // Map TradeCommand to Alpaca order request
        var req = new
        {
            symbol = command.Symbol,
            qty = command.QtyAsset?.ToString("F0") ?? null,
            notional = command.AmountQuote?.ToString("F2") ?? null,
            side = command.Action.ToLower(),
            type = "market",
            time_in_force = "day",
            client_order_id = command.ClientOrderId,
        };

        try
        {
            var response = await _http.PostAsJsonAsync("/v2/orders", req, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                return new OrderResult
                {
                    ClientOrderId = command.ClientOrderId,
                    Status = "failed",
                    ErrorMessage = err,
                };
            }

            var result = await response.Content.ReadFromJsonAsync<AlpacaOrderResponse>(cancellationToken: ct);
            return new OrderResult
            {
                ClientOrderId = command.ClientOrderId,
                Status = result?.Status ?? "submitted",
                FilledQty = result?.FilledQty ?? 0m,
                FilledPrice = result?.FilledAvgPrice ?? 0m,
            };
        }
        catch (Exception ex)
        {
            return new OrderResult
            {
                ClientOrderId = command.ClientOrderId,
                Status = "failed",
                ErrorMessage = ex.Message,
            };
        }
    }

    public async Task<DeltaReport> GetBalancesAsync(CancellationToken ct = default)
    {
        var account = await _http.GetFromJsonAsync<AlpacaAccountResponse>("/v2/account", ct);
        var positions = await _http.GetFromJsonAsync<List<AlpacaPositionResponse>>("/v2/positions", ct);

        var balances = new List<AssetBalance>
        {
            new()
            {
                Asset = "USD",
                Available = account?.BuyingPower ?? 0m,
                Frozen = (account?.PortfolioValue ?? 0m) - (account?.BuyingPower ?? 0m),
            }
        };

        foreach (var pos in positions ?? [])
        {
            balances.Add(new AssetBalance
            {
                Asset = pos.Symbol,
                Available = pos.Qty,
                Frozen = 0m,
            });
        }

        return new DeltaReport { Balances = balances };
    }

    public async Task<OrderResult> GetOrderStatusAsync(string clientOrderId, CancellationToken ct = default)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<AlpacaOrderResponse>(
                $"/v2/orders:by_client_order_id?client_order_id={clientOrderId}", ct);

            return new OrderResult
            {
                ClientOrderId = clientOrderId,
                Status = result?.Status ?? "unknown",
                FilledQty = result?.FilledQty ?? 0m,
                FilledPrice = result?.FilledAvgPrice ?? 0m,
            };
        }
        catch (Exception ex)
        {
            return new OrderResult { ClientOrderId = clientOrderId, Status = "failed", ErrorMessage = ex.Message };
        }
    }

    // ── Alpaca response DTOs ──────────────────────────────────────────────────

    private sealed class AlpacaOrderResponse
    {
        public string? Status { get; set; }
        public decimal FilledQty { get; set; }
        public decimal FilledAvgPrice { get; set; }
    }

    private sealed class AlpacaAccountResponse
    {
        public decimal BuyingPower { get; set; }
        public decimal PortfolioValue { get; set; }
    }

    private sealed class AlpacaPositionResponse
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Qty { get; set; }
    }
}
