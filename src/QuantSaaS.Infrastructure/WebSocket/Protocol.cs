using QuantSaaS.Core.Models;

namespace QuantSaaS.Infrastructure.WebSocket;

/// <summary>
/// WebSocket message type identifiers.
/// Protocol flow: auth → auth_result → [heartbeat ↔ heartbeat_ack] ↔ [command → command_ack → delta_report → report_ack]
/// </summary>
public static class WsMessageType
{
    public const string Auth = "auth";
    public const string AuthResult = "auth_result";
    public const string Heartbeat = "heartbeat";
    public const string HeartbeatAck = "heartbeat_ack";
    public const string Command = "command";
    public const string CommandAck = "command_ack";
    public const string DeltaReport = "delta_report";
    public const string ReportAck = "report_ack";
}

/// <summary>Base message envelope.</summary>
public record WsMessage
{
    public string Type { get; init; } = string.Empty;
    public long TimestampMs { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

public record AuthMessage : WsMessage
{
    public string Token { get; init; } = string.Empty;
    public string InstanceId { get; init; } = string.Empty;
}

public record AuthResultMessage : WsMessage
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public record HeartbeatMessage : WsMessage
{
    public string InstanceId { get; init; } = string.Empty;
}

public record CommandMessage : WsMessage
{
    public TradeCommand Command { get; init; } = null!;
}

public record CommandAckMessage : WsMessage
{
    public string ClientOrderId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;  // "accepted" | "rejected"
    public string? RefusalReason { get; init; }
}

public record DeltaReportMessage : WsMessage
{
    public DeltaReport Report { get; init; } = null!;
}

public record ReportAckMessage : WsMessage
{
    public string ReportId { get; init; } = string.Empty;
}
