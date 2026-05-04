using System;
using System.Diagnostics;

namespace QuantSaaS.Core.Attributes;

/// <summary>
/// Marks a class as a pure strategy implementation.
/// Iron Rule: Step() must be a pure function – no I/O, no timers, no DB, no DateTime.Now.
/// Same implementation is called for both backtest and live trading.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StrategyPurityAttribute : Attribute
{
    public bool EnforceAtRuntime { get; }

    public StrategyPurityAttribute(bool enforceAtRuntime = true)
    {
        EnforceAtRuntime = enforceAtRuntime;
    }
}

/// <summary>
/// Debug-mode guard to detect I/O calls inside strategy Step().
/// </summary>
public static class PurityGuard
{
    [Conditional("DEBUG")]
    public static void VerifyNoIOInCallStack()
    {
        var stack = new StackTrace(2, false);
        foreach (var frame in stack.GetFrames())
        {
            var method = frame.GetMethod();
            var dt = method?.DeclaringType?.FullName;
            if (dt == null) continue;
            if (dt.StartsWith("System.Net") || dt.StartsWith("System.Data") ||
                dt.StartsWith("System.IO") || dt.Contains("HttpClient") ||
                dt.Contains("SqlConnection") || dt.Contains("EntityFrameworkCore"))
            {
                throw new InvalidOperationException(
                    $"Strategy purity violation: I/O detected in call stack at {dt}.{method!.Name}");
            }
        }
    }
}
