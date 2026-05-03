using System.Text.Json.Serialization;

namespace QuantSaaS.Infrastructure.WebSocket;

public record WsMessage
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = null!;

    [JsonPropertyName("payload")]
    public object? Payload { get; init; }
}

public record TradeCommand
{
    [JsonPropertyName("client_order_id")]
    public string ClientOrderId { get; init; } = null!;

    [JsonPropertyName("action")]
    public string Action { get; init; } = null!; // BUY | SELL

    [JsonPropertyName("engine")]
    public string Engine { get; init; } = null!; // MACRO | MICRO

    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = null!;

    [JsonPropertyName("amount_usdt")]
    public decimal? AmountUsdt { get; init; }

    [JsonPropertyName("qty_asset")]
    public decimal? QtyAsset { get; init; }

    [JsonPropertyName("lot_type")]
    public string LotType { get; init; } = null!; // DEAD_STACK | FLOATING
}

public record DeltaReport
{
    [JsonPropertyName("client_order_id")]
    public string? ClientOrderId { get; init; }

    [JsonPropertyName("balances")]
    public BalanceSnapshot Balances { get; init; } = null!;

    [JsonPropertyName("execution")]
    public ExecutionDetail? Execution { get; init; }
}

public record BalanceSnapshot
{
    [JsonPropertyName("btc_available")]
    public decimal BtcAvailable { get; init; }

    [JsonPropertyName("btc_frozen")]
    public decimal BtcFrozen { get; init; }

    [JsonPropertyName("usdt_available")]
    public decimal UsdtAvailable { get; init; }

    [JsonPropertyName("usdt_frozen")]
    public decimal UsdtFrozen { get; init; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }
}

public record ExecutionDetail
{
    [JsonPropertyName("filled_qty")]
    public decimal FilledQty { get; init; }

    [JsonPropertyName("filled_price")]
    public decimal FilledPrice { get; init; }

    [JsonPropertyName("fee")]
    public decimal Fee { get; init; }

    [JsonPropertyName("fee_asset")]
    public string FeeAsset { get; init; } = null!;

    [JsonPropertyName("status")]
    public string Status { get; init; } = null!;
}
