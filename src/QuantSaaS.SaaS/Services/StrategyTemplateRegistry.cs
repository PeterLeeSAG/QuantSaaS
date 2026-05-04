using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;

namespace QuantSaaS.SaaS.Services;

/// <summary>
/// In-memory registry of all strategy templates.
/// Validates that an instance's instrument matches its template's asset class.
/// </summary>
public sealed class StrategyTemplateRegistry
{
    private readonly ConcurrentDictionary<string, TemplateEntry> _templates = new();

    public void Register(string strategyId, AssetClass assetClass, IPureStrategy factory)
    {
        _templates[strategyId] = new TemplateEntry(strategyId, assetClass, factory);
    }

    /// <summary>Validates that the instrument is compatible with the strategy template.</summary>
    public ValidationResult Validate(string strategyId, Instrument instrument)
    {
        if (!_templates.TryGetValue(strategyId, out var entry))
            return ValidationResult.Fail($"Unknown strategy: {strategyId}");

        if (entry.AssetClass != instrument.AssetClass)
            return ValidationResult.Fail(
                $"Strategy '{strategyId}' is for {entry.AssetClass} but instrument '{instrument.Symbol}' " +
                $"is a {instrument.AssetClass}.");

        return ValidationResult.Ok();
    }

    public bool TryGet(string strategyId, out TemplateEntry? entry)
        => _templates.TryGetValue(strategyId, out entry);

    public IEnumerable<TemplateEntry> All() => _templates.Values;

    public sealed class TemplateEntry
    {
        public string StrategyId { get; }
        public AssetClass AssetClass { get; }
        public IPureStrategy Strategy { get; }

        public TemplateEntry(string strategyId, AssetClass assetClass, IPureStrategy strategy)
        {
            StrategyId = strategyId;
            AssetClass = assetClass;
            Strategy = strategy;
        }
    }
}

public record ValidationResult
{
    public bool IsValid { get; init; }
    public string? Error { get; init; }

    public static ValidationResult Ok() => new() { IsValid = true };
    public static ValidationResult Fail(string error) => new() { IsValid = false, Error = error };
}
