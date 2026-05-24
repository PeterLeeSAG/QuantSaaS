namespace QuantSaaS.Core.Models;

/// <summary>
/// UI-facing terminology mapping. Never expose internal terms in user interfaces.
/// </summary>
public static class UserTerms
{
    public const string DeadBtc = "Long-term Holdings";
    public const string FloatBtc = "Active Position";
    public const string ColdSealed = "Sealed Assets";
    public const string TotalEquity = "Total Assets";
    public const string SpendableUsdt = "Available Funds";
    public const string Running = "Running";
    public const string Stopped = "Paused";
    public const string Error = "Error";
    public const string Challenger = "Candidate Parameters";
    public const string Champion = "Current Best Parameters";
    public const string Retired = "Archived";
    public const string EvolutionTask = "Parameter Optimisation";
    public const string StepTrigger = "Strategy Decision";
    public const string Backtest = "Historical Simulation";
    public const string MarketState = "Market Environment";
    public const string Agent = "Execution Client";
    public const string CronTick = "Scheduled Decision";
}
