using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuantSaaS.Infrastructure.WebSocket;

namespace QuantSaaS.Agent;

/// <summary>
/// Bitget REST API v2 exchange adapter.
/// Iron Rule: API Key ONLY lives here in LocalAgent, never in SaaS layer.
/// </summary>
public class BitgetExchange
{
    private readonly ExchangeConfig _config;
    private readonly HttpClient _http;

    private const string BaseUrl = "https://api.bitget.com";

    public BitgetExchange(ExchangeConfig config)
    {
        _config = config;
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }

    public async Task<PlaceOrderResult> PlaceOrderAsync(TradeCommand cmd)
    {
        var side = cmd.Action.ToLower();
        var orderType = "market";

        object body;
        if (cmd.Action == "BUY")
        {
            body = new
            {
                symbol = cmd.Symbol,
                side,
                orderType,
                force = "gtc",
                size = cmd.AmountUsdt?.ToString("F2")
            };
        }
        else
        {
            body = new
            {
                symbol = cmd.Symbol,
                side,
                orderType,
                force = "gtc",
                size = cmd.QtyAsset?.ToString("F8")
            };
        }

        var path = "/api/v2/spot/trade/place-order";
        var bodyJson = JsonSerializer.Serialize(body);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var signature = GenerateSignature(timestamp, "POST", path, bodyJson);

        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("ACCESS-KEY", _config.ApiKey);
        request.Headers.Add("ACCESS-SIGN", signature);
        request.Headers.Add("ACCESS-TIMESTAMP", timestamp);
        request.Headers.Add("ACCESS-PASSPHRASE", _config.Passphrase);
        request.Headers.Add("locale", "en-US");
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        // In sandbox mode, return mock result
        if (_config.Sandbox)
        {
            return new PlaceOrderResult
            {
                Success = true,
                OrderId = $"SANDBOX-{Guid.NewGuid():N}",
                FilledQty = cmd.QtyAsset ?? (cmd.AmountUsdt ?? 100m) / 50000m,
                FilledPrice = 50000m,
                Fee = 0.001m
            };
        }

        var response = await _http.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        return new PlaceOrderResult
        {
            Success = doc.RootElement.GetProperty("code").GetString() == "00000",
            OrderId = doc.RootElement.TryGetProperty("data", out var data)
                ? data.GetProperty("orderId").GetString() ?? string.Empty
                : string.Empty
        };
    }

    public async Task<BalanceSnapshot> GetBalancesAsync()
    {
        if (_config.Sandbox)
        {
            return new BalanceSnapshot
            {
                BtcAvailable = 0.1m,
                BtcFrozen = 0,
                UsdtAvailable = 5000m,
                UsdtFrozen = 0,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        var path = "/api/v2/spot/account/assets";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var signature = GenerateSignature(timestamp, "GET", path, "");

        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("ACCESS-KEY", _config.ApiKey);
        request.Headers.Add("ACCESS-SIGN", signature);
        request.Headers.Add("ACCESS-TIMESTAMP", timestamp);
        request.Headers.Add("ACCESS-PASSPHRASE", _config.Passphrase);

        var response = await _http.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        decimal btcAvail = 0, btcFrozen = 0, usdtAvail = 0, usdtFrozen = 0;
        if (doc.RootElement.TryGetProperty("data", out var dataArr))
        {
            foreach (var asset in dataArr.EnumerateArray())
            {
                var coin = asset.GetProperty("coin").GetString();
                var available = decimal.Parse(asset.GetProperty("available").GetString() ?? "0");
                var frozen = decimal.Parse(asset.GetProperty("frozen").GetString() ?? "0");
                if (coin == "BTC") { btcAvail = available; btcFrozen = frozen; }
                if (coin == "USDT") { usdtAvail = available; usdtFrozen = frozen; }
            }
        }

        return new BalanceSnapshot
        {
            BtcAvailable = btcAvail, BtcFrozen = btcFrozen,
            UsdtAvailable = usdtAvail, UsdtFrozen = usdtFrozen,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private string GenerateSignature(string timestamp, string method, string path, string body)
    {
        var prehash = timestamp + method.ToUpper() + path + body;
        var keyBytes = Encoding.UTF8.GetBytes(_config.SecretKey);
        var msgBytes = Encoding.UTF8.GetBytes(prehash);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToBase64String(hmac.ComputeHash(msgBytes));
    }
}

public record PlaceOrderResult
{
    public bool Success { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public decimal FilledQty { get; init; }
    public decimal FilledPrice { get; init; }
    public decimal Fee { get; init; }
}
