using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuantSaaS.Core.Interfaces;

/// <summary>
/// Corporate action feed interface.
/// Provides splits, dividends, and spinoffs for equity back-adjustment and NAV accounting.
/// </summary>
public interface ICorporateActionFeed
{
    /// <summary>
    /// Returns all corporate actions for <paramref name="symbol"/> in [from, to].
    /// </summary>
    Task<IReadOnlyList<CorporateAction>> GetActionsAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
}

public record CorporateAction
{
    public string Symbol { get; init; } = string.Empty;
    public CorporateActionType Type { get; init; }
    public DateTime ExDateUtc { get; init; }

    /// <summary>
    /// For splits: ratio (e.g., 2.0 = 2-for-1 forward split).
    /// For dividends: per-share amount in the instrument's quote currency.
    /// </summary>
    public decimal Amount { get; init; }
}

public enum CorporateActionType
{
    Split,
    Dividend,
    Spinoff,
}
