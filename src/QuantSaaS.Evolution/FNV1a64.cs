using System;
using System.Collections.Generic;

namespace QuantSaaS.Evolution;

/// <summary>
/// FNV-1a-64 hash for chromosome fingerprinting.
/// Chromosomes with field values within 1e-6 of each other hash to the same value,
/// enabling the Genome fingerprint cache to skip duplicate evaluations.
/// </summary>
public static class FNV1a64
{
    private const ulong FNVPrime = 1099511628211UL;
    private const ulong FNVOffset = 14695981039346656037UL;

    /// <summary>
    /// Hashes a sequence of decimal values at 1e-6 precision.
    /// </summary>
    public static string Hash(IEnumerable<decimal> values)
    {
        ulong hash = FNVOffset;

        foreach (var v in values)
        {
            // Quantize to 1e-6 precision to make near-identical values hash equally
            long quantized = (long)Math.Round((double)v * 1_000_000.0);
            byte[] bytes = BitConverter.GetBytes(quantized);

            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= FNVPrime;
            }
        }

        return hash.ToString("X16");
    }
}
