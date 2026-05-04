using System;
using System.Collections.Generic;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Infrastructure.Providers;

/// <summary>
/// Registry that maps an instrument to its appropriate data provider.
/// Provider selection is resolved at runtime from Instrument.Exchange;
/// strategy code never knows which provider is active.
/// </summary>
public sealed class MarketDataProviderRegistry
{
    private readonly Dictionary<string, IMarketDataProvider> _byProvider = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<AssetClass, IMarketDataProvider> _byAssetClass = new();

    /// <summary>Registers a provider by its ID and optional default asset class.</summary>
    public void Register(IMarketDataProvider provider, AssetClass? defaultForAssetClass = null)
    {
        _byProvider[provider.ProviderId] = provider;
        if (defaultForAssetClass.HasValue)
            _byAssetClass[defaultForAssetClass.Value] = provider;
    }

    /// <summary>
    /// Resolves the data provider for the given instrument.
    /// Priority: exchange name → asset class default → first registered.
    /// </summary>
    public IMarketDataProvider Resolve(Instrument instrument)
    {
        // Try matching by exchange name
        if (_byProvider.TryGetValue(instrument.Exchange, out var byExchange))
            return byExchange;

        // Try matching by asset class
        if (_byAssetClass.TryGetValue(instrument.AssetClass, out var byClass))
            return byClass;

        // Fallback: first registered
        foreach (var p in _byProvider.Values)
            return p;

        throw new InvalidOperationException(
            $"No data provider registered for instrument {instrument.Symbol} " +
            $"(exchange: {instrument.Exchange}, assetClass: {instrument.AssetClass}).");
    }
}
