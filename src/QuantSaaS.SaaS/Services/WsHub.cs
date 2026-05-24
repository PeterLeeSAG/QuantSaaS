using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.WebSocket;

namespace QuantSaaS.SaaS.Services;

/// <summary>
/// WebSocket hub: manages LocalAgent connections.
/// Each user has at most one connected agent.
/// Design principle: cloud trusts only reports from agents, agents execute blindly.
/// </summary>
public class WsHub
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _connections = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JwtService _jwtService;
    private readonly ILogger<WsHub> _logger;

    public WsHub(IServiceScopeFactory scopeFactory, JwtService jwtService, ILogger<WsHub> logger)
    {
        _scopeFactory = scopeFactory;
        _jwtService = jwtService;
        _logger = logger;
    }

    public bool IsConnected(Guid userId) => _connections.ContainsKey(userId);

    public bool SendToAgent(Guid userId, TradeCommand cmd)
    {
        if (!_connections.TryGetValue(userId, out var ws) || ws.State != WebSocketState.Open)
            return false;

        var msg = JsonSerializer.Serialize(new { type = "command", payload = cmd });
        var bytes = Encoding.UTF8.GetBytes(msg);
        ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
            .ConfigureAwait(false).GetAwaiter().GetResult();
        return true;
    }

    public async Task HandleConnectionAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        Guid? userId = null;

        try
        {
            // Step 1: Wait for auth message (10 second timeout)
            using var authTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var authMsg = await ReceiveMessageAsync(ws, authTimeout.Token);
            if (authMsg == null)
            {
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Auth timeout", CancellationToken.None);
                return;
            }

            // Step 2: Validate JWT
            var doc = JsonDocument.Parse(authMsg);
            if (!doc.RootElement.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "auth")
            {
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Expected auth message", CancellationToken.None);
                return;
            }

            var token = doc.RootElement.GetProperty("payload").GetProperty("token").GetString();
            var principal = _jwtService.ValidateToken(token ?? "");
            if (principal == null)
            {
                await SendTextAsync(ws, JsonSerializer.Serialize(new { type = "auth_result", success = false }));
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid token", CancellationToken.None);
                return;
            }

            userId = Guid.Parse(principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            // Step 3: Register connection
            _connections[userId.Value] = ws;
            await SendTextAsync(ws, JsonSerializer.Serialize(new { type = "auth_result", success = true }));
            _logger.LogInformation("Agent connected for user {UserId}", userId.Value);

            // Step 4: Message loop
            using var cts = new CancellationTokenSource();
            var heartbeatTask = SendHeartbeatsAsync(ws, cts.Token);

            while (ws.State == WebSocketState.Open)
            {
                var msg = await ReceiveMessageAsync(ws, CancellationToken.None);
                if (msg == null) break;

                await HandleMessageAsync(msg, userId.Value, ws);
            }

            cts.Cancel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WsHub error for user {UserId}", userId);
        }
        finally
        {
            if (userId.HasValue) _connections.TryRemove(userId.Value, out _);
            _logger.LogInformation("Agent disconnected for user {UserId}", userId);
        }
    }

    private async Task HandleMessageAsync(string msg, Guid userId, WebSocket ws)
    {
        try
        {
            var doc = JsonDocument.Parse(msg);
            var type = doc.RootElement.GetProperty("type").GetString();

            switch (type)
            {
                case "heartbeat":
                    await SendTextAsync(ws, JsonSerializer.Serialize(new { type = "heartbeat_ack" }));
                    break;

                case "delta_report":
                    var report = JsonSerializer.Deserialize<DeltaReport>(
                        doc.RootElement.GetProperty("payload").GetRawText());
                    if (report != null) await ProcessDeltaReportAsync(report, userId);
                    await SendTextAsync(ws, JsonSerializer.Serialize(new { type = "report_ack" }));
                    break;

                case "command_ack":
                    // Agent acknowledged command receipt - nothing to do server-side
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WS message for user {UserId}", userId);
        }
    }

    private async Task ProcessDeltaReportAsync(DeltaReport report, Guid userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuantDbContext>();

        // Find the instance for this user
        var instance = await db.StrategyInstances
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Status == "RUNNING");
        if (instance == null) return;

        // Update portfolio with real balances from exchange
        var portfolio = await db.PortfolioStates.FindAsync([instance.Id]);
        if (portfolio != null)
        {
            portfolio.UsdtBalance = report.Balances.UsdtAvailable;
            portfolio.UpdatedAt = DateTime.UtcNow;
        }

        // If order was executed, update lot tracking
        if (!string.IsNullOrEmpty(report.ClientOrderId) && report.Execution != null)
        {
            var execution = await db.SpotExecutions
                .FirstOrDefaultAsync(e => e.ClientOrderId == report.ClientOrderId);

            if (execution != null)
            {
                execution.Status = report.Execution.Status == "filled" ? "filled" : "failed";
                execution.FilledAt = DateTime.UtcNow;

                if (report.Execution.Status == "filled")
                {
                    // Update portfolio BTC holdings
                    if (execution.LotType == "DEAD_STACK")
                        portfolio!.DeadBtc += report.Execution.FilledQty;
                    else
                        portfolio!.FloatBtc += report.Execution.FilledQty;

                    db.TradeRecords.Add(new TradeRecordEntity
                    {
                        InstanceId = instance.Id,
                        ClientOrderId = report.ClientOrderId,
                        Action = execution.CommandJson.Contains("\"BUY\"") ? "BUY" : "SELL",
                        Engine = execution.LotType == "DEAD_STACK" ? "MACRO" : "MICRO",
                        Symbol = instance.Symbol,
                        FilledQty = report.Execution.FilledQty,
                        FilledPrice = report.Execution.FilledPrice,
                        Fee = report.Execution.Fee
                    });
                }
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task<string?> ReceiveMessageAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var sb = new StringBuilder();
        try
        {
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
            while (!result.EndOfMessage);
            return sb.ToString();
        }
        catch (OperationCanceledException) { return null; }
        catch (WebSocketException) { return null; }
    }

    private static async Task SendTextAsync(WebSocket ws, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task SendHeartbeatsAsync(WebSocket ws, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            await Task.Delay(30000, ct);
            if (ws.State == WebSocketState.Open)
                await SendTextAsync(ws, JsonSerializer.Serialize(new { type = "heartbeat" }));
        }
    }
}
