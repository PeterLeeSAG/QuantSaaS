using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace QuantSaaS.Core.Config;

/// <summary>
/// Validates configuration to enforce API Key physical isolation.
/// Iron Rule: API credentials NEVER enter the SaaS layer – only stored in config.agent.yaml on LocalAgent.
/// </summary>
public static class ConfigValidator
{
    private static readonly Regex ApiKeyPattern = new(
        @"(api[_\-]?key|secret[_\-]?key|passphrase|access[_\-]?token|private[_\-]?key)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> ForbiddenSaaSFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApiKey", "SecretKey", "Passphrase", "AccessToken", "PrivateKey",
        "ExchangeCredentials", "TradingCredentials", "ApiSecret", "BrokerApiKey",
    };

    /// <summary>
    /// Validates SaaS-role configuration. Throws on any API key pattern found.
    /// </summary>
    public static void ValidateSaaSConfig(IConfiguration config, string role)
    {
        if (role != "saas") return;

        foreach (var (key, value) in config.AsEnumerable().Where(x => x.Value != null))
        {
            if (ForbiddenSaaSFields.Any(f => key.EndsWith(f, StringComparison.OrdinalIgnoreCase)) ||
                ApiKeyPattern.IsMatch(key))
            {
                throw new InvalidOperationException(
                    $"SECURITY VIOLATION: Field '{key}' is forbidden in SaaS config. " +
                    "API credentials must only exist in config.agent.yaml on LocalAgent.");
            }

            if (value != null && ApiKeyPattern.IsMatch(value))
            {
                throw new InvalidOperationException(
                    $"SECURITY VIOLATION: API key pattern detected in value of config field '{key}'. " +
                    "Credentials must NEVER be stored or transmitted to the SaaS layer.");
            }
        }
    }

    /// <summary>
    /// Validates Agent configuration – ensures required broker credentials are present locally.
    /// </summary>
    public static void ValidateAgentConfig(IConfiguration config)
    {
        var required = new[] { "Broker:ApiKey", "Broker:SecretKey" };
        foreach (var field in required)
        {
            if (string.IsNullOrWhiteSpace(config[field]))
                throw new InvalidOperationException(
                    $"Agent config missing required field: {field}. " +
                    "This file must be kept local and NEVER committed to version control.");
        }
    }
}
