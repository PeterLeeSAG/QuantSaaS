using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuantSaaS.Core.Models;
using QuantSaaS.Infrastructure.WebSocket;

namespace QuantSaaS.Agent;

/// <summary>
/// LocalAgent WebSocket client with auto-reconnect (exponential backoff 1s → 5min).
/// Iron Rule: No strategy code here. Just execute commands and report deltas.
/// </summary>
public class AgentWsClient : IHostedService
{
    private readonly AgentConfig _config;
    private readonly BitgetExchange _exchange;
    private readonly ILogger<AgentWsClient> _logger;
    private CancellationTokenSource _cts = new();
    private Task? _mainLoop;

    public AgentWsClient(AgentConfig config, BitgetExchange exchange, ILogger<AgentWsClient> logger)
    {
        _config = config;
        _exchange = exchange;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _mainLoop = RunMainLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _cts.Cancel();
        if (_mainLoop != null) await _mainLoop.WaitAsync(ct);
    }

    private async Task RunMainLoopAsync(CancellationToken ct)
    {
        int retrySeconds = 1;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Step 1: Get JWT from SaaS REST API
                var token = await GetJwtAsync(ct);
                if (token == null)
                {
                    _logger.LogWarning("Login failed, retrying in {S}s", retrySeconds);
                    await Task.Delay(retrySeconds * 1000, ct);
                    retrySeconds = Math.Min(retrySeconds * 2, 300);
                    continue;
                }

                // Step 2: Connect WebSocket
                using var ws = new ClientWebSocket();
                var wsUrl = _config.SaaSUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "/ws/agent";
                await ws.ConnectAsync(new Uri(wsUrl), ct);

                // Step 3: Send auth
                await SendAsync(ws, new { type = "auth", payload = new { token } }, ct);

                // Step 4: Wait for auth_result
                var authResp = await ReceiveAsync(ws, ct);
                if (authResp == null || !authResp.Contains("\"success\":true"))
                {
                    _logger.LogWarning("Auth rejected by server");
                    retrySeconds = Math.Min(retrySeconds * 2, 300);
                    continue;
                }

                _logger.LogInformation("Agent connected and authenticated");
                retrySeconds = 1; // Reset on success

                // Step 5: Initial balance snapshot
                var balances = await _exchange.GetBalancesAsync();
                await SendAsync(ws, new
                {
                    type = "delta_report",
                    payload = new DeltaReport { Balances = ToAssetBalances(balances) }
                }, ct);

                // Step 6: Message loop
                var heartbeatTask = SendHeartbeatsAsync(ws, ct);
                await MessageLoopAsync(ws, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent connection error, retrying in {S}s", retrySeconds);
                await Task.Delay(retrySeconds * 1000, ct);
                retrySeconds = Math.Min(retrySeconds * 2, 300);
            }
        }
    }

    private async Task MessageLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var msg = await ReceiveAsync(ws, ct);
            if (msg == null) break;

            try
            {
                var doc = JsonDocument.Parse(msg);
                var type = doc.RootElement.GetProperty("type").GetString();

                if (type == "command")
                {
                    var cmd = JsonSerializer.Deserialize<TradeCommand>(
                        doc.RootElement.GetProperty("payload").GetRawText());
                    if (cmd == null) continue;

                    // Immediately acknowledge command receipt
                    await SendAsync(ws, new { type = "command_ack", payload = new { cmd.ClientOrderId } }, ct);

                    // Execute asynchronously
                    _ = ExecuteCommandAsync(ws, cmd, ct);
                }
                else if (type == "heartbeat")
                {
                    await SendAsync(ws, new { type = "heartbeat_ack" }, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message: {Msg}", msg);
            }
        }
    }

    private async Task ExecuteCommandAsync(ClientWebSocket ws, TradeCommand cmd, CancellationToken ct)
    {
        try
        {
            var result = await _exchange.PlaceOrderAsync(cmd);
            var balances = await _exchange.GetBalancesAsync();

            var report = new DeltaReport
            {
                ClientOrderId = cmd.ClientOrderId,
                Balances = ToAssetBalances(balances),
                Execution = new ExecutionDetail
                {
                    FilledQty = result.FilledQty,
                    FilledPrice = result.FilledPrice,
                    Fee = result.Fee,
                    Status = result.Success ? "filled" : "failed",
                    TimestampUtc = DateTime.UtcNow
                }
            };

            await SendAsync(ws, new { type = "delta_report", payload = report }, ct);
            _logger.LogInformation("Order {Id}: {Status}", cmd.ClientOrderId, result.Success ? "filled" : "failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order execution failed for {Id}", cmd.ClientOrderId);
        }
    }

    private async Task<string?> GetJwtAsync(CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient();
            var resp = await http.PostAsJsonAsync(
                $"{_config.SaaSUrl}/api/v1/auth/login",
                new { email = _config.Email, password = _config.Password }, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(ct);
            return doc?.RootElement.GetProperty("token").GetString();
        }
        catch { return null; }
    }

    private static async Task SendAsync(ClientWebSocket ws, object msg, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    private static async Task<string?> ReceiveAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buf = new byte[4096];
        var sb = new StringBuilder();
        try
        {
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                sb.Append(Encoding.UTF8.GetString(buf, 0, result.Count));
            }
            while (!result.EndOfMessage);
            return sb.ToString();
        }
        catch { return null; }
    }

    private static async Task SendHeartbeatsAsync(ClientWebSocket ws, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            await Task.Delay(30000, ct);
            if (ws.State == WebSocketState.Open)
                await SendAsync(ws, new { type = "heartbeat" }, ct);
        }
    }

    private static IReadOnlyList<AssetBalance> ToAssetBalances(BalanceSnapshot snap)
    {
        return new[]
        {
            new AssetBalance { Asset = "BTC",  Available = snap.BtcAvailable,  Frozen = snap.BtcFrozen },
            new AssetBalance { Asset = "USDT", Available = snap.UsdtAvailable, Frozen = snap.UsdtFrozen }
        };
    }
}
