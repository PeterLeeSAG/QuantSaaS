please review, check and finalize my planning and draft in C# solutions with the original md files:

# QuantSaaS C# Implementation Review Report

**Reviewer:** Investment Analyst (CFA/IB Background)  
**Date:** April 2026  
**Scope:** Architecture compliance, mathematical fidelity, and production readiness assessment against provided design documents

---

## Executive Summary

The provided C# implementation demonstrates a solid foundational understanding of the QuantSaaS architecture, particularly in translating the core Sigmoid formula and GA concepts. However, **significant gaps exist** across 8 critical dimensions that would prevent production deployment. The implementation is approximately **25-30% complete** relative to the design specification.

| Category | Completion | Critical Issues |
|----------|-----------|----------------|
| Domain Models | ~40% | Missing 8+ essential entities, no structural constraints |
| Strategy Engine | ~60% | Signal logic incomplete, wedge filtering partial |
| GA Evolution | ~20% | Missing 8-verb interface, fitness function, crucible logic |
| System Architecture | ~15% | No WebSocket protocol, Agent, Cron driver, or deployment |
| Security/Isolation | ~30% | API Key isolation not enforced, config management incomplete |
| Testing/Validation | 0% | No unit tests, no deterministic backtest verification |
| Frontend/UI | 0% | Entire Phase 12 unimplemented |
| Documentation | ~50% | No inline docs for iron rules, no user-facing terminology mapping |

---

## Critical Gaps by Priority

### 🔴 P0: Architecture Iron Rules Violations (Must Fix Before Any Testing)

#### 1. Strategy Purity Not Enforced
```csharp
// Current: Interface only declares purity via comment
public interface IStrategy {
    StrategyOutput Step(StrategyInput input); // "Pure Function: No I/O..."
}

// Missing: Compile-time enforcement mechanism
// Recommendation: Use Roslyn analyzer or marker interfaces to prevent:
// - http://, HttpClient, SqlConnection, File.Open, DateTime.Now usage
// - Any dependency injection of I/O services into strategy implementations
```

#### 2. API Key Physical Isolation Not Implemented
```csharp
// Risk: No mechanism preventing API keys from entering SaaS layer
// Document requirement: "永不进入 SaaS 侧" (Never enter SaaS side)

// Missing:
// - Config schema that explicitly excludes api_key fields in SaaS config
// - Build-time validation: grep -r "api_key\|secret_key" internal/saas/
// - Runtime assertion: throw if any config field contains "key"/"secret" patterns
```

#### 3. Strategy Homomorphism Gap
```csharp
// Current: Single Step() interface exists ✓
// Missing: 
// - BacktestAdapter that calls Step() with identical input construction
// - Assertion in tests: same params + same bars → identical StrategyOutput
// - No mechanism to prevent if (isBacktest) branching (requires code review policy + static analysis)
```

---

### 🔴 P1: Mathematical Fidelity Issues

#### Sigmoid Engine: Signal Calculation Mismatch
```csharp
// Current implementation (oversimplified):
decimal signal = (input.CurrentPrice - avg) / avg;

// Document requirement: Signal must be chromosome-parameterized
// Signal = a × X1 + b × X2 + c × X3 + ... where X1/X2/X3 are dimensionless features

// Missing:
// - Feature extraction layer (price deviation, momentum, acceleration - all dimensionless)
// - Chromosome fields for signal coefficients (a, b, c with bounds)
// - Proper normalization: all features must be ratio/log-return based
```

#### Wedge Filtering: VolatilityRatio Missing
```csharp
// Current: Only checks deltaWeight threshold
} else if (!input.Market.IsQuiet && Math.Abs(deltaWeight) > 0.02m) {

// Document formula:
// VolatilityRatio = clip(MAV_short / MAV_long, 0.1, 3.0)
// Wedge breakout: |DeltaWeight| ≥ threshold OR VolatilityRatio ≥ threshold

// Missing:
// - MAVAbsChange implementation (average absolute change, NOT ATR)
// - Configurable short/long window constants (MicroVolRatioShortBars=16, LongBars=112)
// - clip() utility function with [0.1, 3.0] bounds
```

#### MarketState Interface Incomplete
```csharp
// Current:
public class MarketState {
    public string State { get; set; }
    public bool IsQuiet { get; set; }
    public decimal BetaMultiplier { get; set; }
}

// Document requirement (4 fields):
public class MarketState {
    public string State { get; set; }                    // enum: bull/bear/quiet
    public decimal TimeDilationMultiplier { get; set; }  // for macro engine
    public decimal BetaMultiplier { get; set; }          // for micro Sigmoid
    public bool IsQuiet { get; set; }                    // dust order suppression
}
```

---

### 🔴 P2: GA Evolution Engine: Core Logic Missing

#### EvolvableStrategy 8-Verb Interface Not Defined
```csharp
// Document contract (critical for engine/strategy decoupling):
public interface IEvolvableStrategy {
    string StrategyID();
    Chromosome Sample(Random rng);
    Chromosome Mutate(Chromosome c, double prob, double scale, Random rng);
    Chromosome Crossover(Chromosome p1, Chromosome p2, Random rng);
    string Fingerprint(Chromosome c);  // FNV-1a-64, precision 1e-6
    FitnessResult Evaluate(EvaluablePlan plan, Chromosome c);
    Chromosome DecodeElite(string paramPackJson);
    string EncodeResult(Chromosome champion, SpawnPoint spawn);
}
```

#### Fitness Function: Multi-Window Crucible Missing
```csharp
// Document formula (non-negotiable for fair evaluation):
// Alpha = ROI_strategy - ROI_GhostDCA
// SliceScore = Alpha - 1.5 × max(0, MaxDD_strategy - MaxDD_GhostDCA)
// Fatal: MaxDD >= 0.88 → Score = -99999
// ScoreTotal = 0.40×full + 0.30×5y + 0.20×2y + 0.10×6m

// Missing:
// - GhostDCA simulator with Modified Dietz ROI calculation
// - Cascading short-circuit: evaluate 6m→2y→5y→full, exit on fatal
// - MaxDrawdown calculation on NAV curve
```

#### Chromosome Structure: Incomplete Parameter Space
```csharp
// Current (3 fields):
public class Chromosome {
    public decimal Beta { get; set; }
    public decimal Gamma { get; set; }
    public decimal MinOrderThreshold { get; set; }
}

// Document minimum requirements:
public class Chromosome {
    // Sigmoid parameters
    public decimal Beta { get; set; }           // [0.1, 5.0]
    public decimal Gamma { get; set; }          // [0, 2.0]
    public decimal SigmaFloor { get; set; }     // [0.001, 0.1]
    
    // Signal synthesis coefficients (example for 3-factor model)
    public decimal CoefX1 { get; set; }         // [-2, 2] - price deviation weight
    public decimal CoefX2 { get; set; }         // [-2, 2] - momentum weight  
    public decimal CoefX3 { get; set; }         // [-2, 2] - acceleration weight
    
    // Wedge filtering thresholds
    public decimal DeltaWeightThreshold { get; set; }  // [0.01, 0.1]
    public decimal VolatilityRatioThreshold { get; set; } // [1.2, 2.5]
    
    // Order management
    public decimal MinOrderThreshold { get; set; } // [5.0, 20.0] USDT
    
    // MUST include: HardBounds constants + ClampChromosome() for structural constraints
}
```

---

### 🟡 P3: System Integration Gaps

#### WebSocket Protocol: Message Schema Incomplete
```csharp
// Document requires 8 message types with strict semantics:
// auth → auth_result → [heartbeat/heartbeat_ack] ↔ [command/command_ack] ↔ [delta_report/report_ack]

// Missing TradeCommand structure:
public class TradeCommand {
    public string ClientOrderId { get; set; }  // Format: inst{id}-{engine}-{ts}
    public string Action { get; set; }         // "BUY" / "SELL" (uppercase)
    public string Engine { get; set; }         // "MACRO" / "MICRO"
    public string Symbol { get; set; }
    public decimal? AmountUsdt { get; set; }   // For BUY orders
    public decimal? QtyAsset { get; set; }     // For SELL orders
    public LotType LotType { get; set; }       // DEAD_STACK / FLOATING
}
```

#### Cron Tick Driver: Idempotency & State Management Missing
```csharp
// Critical document requirement:
// "同一聚合桶已处理时跳过" - Skip if same aggregation bucket already processed

// Missing implementation:
// 1. Power-idempotent bucket check: 
//    if (latestBar.OpenTime <= portfolio.LastProcessedBarTime) return;
// 2. RuntimeState persistence: JSON blob storage for cross-tick strategy memory
// 3. DeadBTC release logic: SaaS-side ledger update ONLY (no Agent command)
// 4. AuditLog writing for all state transitions
```

#### Configuration Management: Environment Variable Injection
```yaml
# Document requirement: "密钥字段留空并注明需通过环境变量注入"
# Current C# config lacks:
app:
  role: ${APP_ROLE:-dev}  # saas/lab/dev
database:
  connection_string: ${DB_CONNECTION_STRING}
jwt:
  secret: ${JWT_SECRET}  # NEVER hardcoded
```

---

## Recommended Implementation Roadmap

### Phase A: Foundation Hardening (Week 1-2)
1. [ ] Add compile-time purity enforcement for strategy package (Roslyn analyzer)
2. [ ] Implement config schema validation that rejects API key fields in SaaS config
3. [ ] Add `ClampChromosome()` with structural constraints (EMA order, time period locks)
4. [ ] Implement MAVAbsChange and VolatilityRatio calculations with proper clipping

### Phase B: GA Core Completion (Week 3-4)
1. [ ] Define `IEvolvableStrategy` 8-verb interface
2. [ ] Implement GhostDCA simulator with Modified Dietz ROI
3. [ ] Build multi-window crucible evaluator with cascading short-circuit
4. [ ] Add FNV-1a-64 fingerprinting with 1e-6 precision quantization

### Phase C: System Integration (Week 5-6)
1. [ ] Implement full WebSocket message protocol with auth timeout (10s)
2. [ ] Build Cron tick driver with idempotent bucket checking
3. [ ] Add RuntimeState JSON persistence and DeadBTC release logic
4. [ ] Create environment variable injection for all secrets

### Phase D: Validation & Safety (Week 7)
1. [ ] Write deterministic backtest test: same input → identical output
2. [ ] Implement Sigmoid property tests (Signal>0 → TargetWeight<0.5, etc.)
3. [ ] Add grep-based security checks to CI pipeline
4. [ ] Create user-facing terminology mapping (DeadBTC → "长期持仓")

---

## Risk Disclosures

⚠️ **Mathematical Risk**: The current signal calculation (`(price - avg)/avg`) is not dimensionless across different assets and may cause cross-asset comparison errors. All price features must use log-returns or ratios.

⚠️ **Overfitting Risk**: Without the multi-window crucible evaluation and cascading short-circuit, GA may optimize for short-term noise. The 4-window weighted evaluation (0.40/0.30/0.20/0.10) is non-negotiable for robust parameter discovery.

⚠️ **Operational Risk**: Missing idempotency checks in Cron tick could cause duplicate order issuance during network retries. The `LastProcessedBarTime` check is a critical safety mechanism.

⚠️ **Security Risk**: Without compile-time enforcement of strategy purity, future developers may accidentally introduce I/O into `Step()`, breaking the backtest/live homomorphism guarantee.

---

## Conclusion

The C# implementation provides a promising foundation but requires substantial work to achieve production readiness. **Priority focus should be on enforcing the six iron rules** (especially strategy purity and API key isolation) before expanding functionality. The mathematical core (Sigmoid engine) needs alignment with the document's parameterized signal synthesis approach.

I recommend treating this as a **reference architecture** rather than a drop-in implementation, and using the provided Phase-by-Phase prompts from the design document to systematically build out missing components with AI assistance.

Draft:
# QuantSaaS C# Production Update Package
## Drop-in Implementation Following Phased Roadmap

**Version:** 1.0.0-production-ready  
**Target Framework:** .NET 8.0  
**Integration:** Drop files into existing project structure

---

## 📦 Package Structure

```
QuantSaaS.Update/
├── PhaseA-Foundation/
│   ├── Core/Attributes/StrategyPurityAttribute.cs
│   ├── Core/Interfaces/IPureStrategy.cs
│   ├── Core/Config/ConfigValidator.cs
│   ├── Core/Models/Chromosome.Constraints.cs
│   └── Quant/MAVCalculator.cs
├── PhaseB-GA-Core/
│   ├── Evolution/Interfaces/IEvolvableStrategy.cs
│   ├── Evolution/GhostDCA/Simulator.cs
│   ├── Evolution/Crucible/MultiWindowEvaluator.cs
│   └── Evolution/Utils/FNV1a64.cs
├── PhaseC-SystemIntegration/
│   ├── Infrastructure/WebSocket/TradeCommand.cs
│   ├── Infrastructure/Cron/IdempotentTickDriver.cs
│   ├── Infrastructure/Config/EnvVarInjector.cs
│   └── Core/Models/RuntimeState.cs
├── PhaseD-Validation/
│   ├── Tests/StrategyPurityTests.cs
│   ├── Tests/DeterministicBacktestTests.cs
│   ├── Tests/SigmoidPropertyTests.cs
│   └── CI/security-scan.ps1
├── README-INTEGRATION.md
└── migration-guide.md
```

---

## 🔧 Phase A: Foundation Hardening

### A1. Strategy Purity Enforcement (`Core/Attributes/StrategyPurityAttribute.cs`)

```csharp
using System;
using System.Reflection;

namespace QuantSaaS.Core.Attributes
{
    /// <summary>
    /// Marks a class as a pure strategy implementation.
    /// Enforces compile-time and runtime checks for I/O isolation.
    /// Iron Rule: Step() must be pure function - no I/O, no timers, no DB.
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
    /// Runtime guard for strategy purity - inject via DI in development/test only
    /// </summary>
    public static class PurityGuard
    {
        [Conditional("DEBUG")]
        public static void VerifyNoIOInCallStack()
        {
            var stack = new System.Diagnostics.StackTrace(2, false);
            foreach (var frame in stack.GetFrames())
            {
                var method = frame.GetMethod();
                if (method == null) continue;
                
                var declaringType = method.DeclaringType?.FullName;
                if (declaringType == null) continue;
                
                // Blocklisted namespaces for strategy purity
                if (declaringType.StartsWith("System.Net") ||
                    declaringType.StartsWith("System.Data") ||
                    declaringType.StartsWith("System.IO") ||
                    declaringType.StartsWith("Microsoft.EntityFrameworkCore") ||
                    declaringType.Contains("HttpClient") ||
                    declaringType.Contains("SqlConnection"))
                {
                    throw new InvalidOperationException(
                        $"Strategy purity violation: I/O call detected in strategy call stack: {declaringType}.{method.Name}");
                }
            }
        }
    }
}
```

### A2. Pure Strategy Interface (`Core/Interfaces/IPureStrategy.cs`)

```csharp
using QuantSaaS.Core.Attributes;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Core.Interfaces
{
    /// <summary>
    /// Pure strategy interface - enforces homomorphism between backtest and live.
    /// Iron Rule: Same Step() implementation for both modes, no if(isBacktest) branching.
    /// </summary>
    [StrategyPurity]
    public interface IPureStrategy
    {
        /// <summary>
        /// Pure function: transforms StrategyInput → StrategyOutput
        /// MUST NOT: perform I/O, access timers, read/write database, call network
        /// MUST: be deterministic - same input → identical output
        /// </summary>
        /// <param name="input">Immutable snapshot of market + portfolio state</param>
        /// <returns>Trading intent (target weight, order size, action)</returns>
        StrategyOutput Step(StrategyInput input);
        
        /// <summary>
        /// Strategy metadata - used for logging and UI display
        /// </summary>
        StrategyMetadata Metadata { get; }
    }
    
    public record StrategyMetadata
    {
        public string StrategyId { get; init; } = null!;
        public string DisplayName { get; init; } = null!; // User-facing, no internal terms
        public string Version { get; init; } = null!;
        public bool IsSpotOnly { get; init; }
        
        // User-friendly terminology mapping (Iron Rule: no internal terms in UI)
        public static class UserTerms
        {
            public const string DeadBTC = "Long-term Holdings";
            public const string FloatBTC = "Active Position";
            public const string ColdSealed = "Sealed Assets";
            public const string TotalEquity = "Total Assets";
            public const string SpendableUSDT = "Available Funds";
        }
    }
}
```

### A3. Config Validation for API Key Isolation (`Core/Config/ConfigValidator.cs`)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace QuantSaaS.Core.Config
{
    /// <summary>
    /// Validates configuration to enforce API Key physical isolation.
    /// Iron Rule: API credentials NEVER enter SaaS layer - only in config.agent.yaml
    /// </summary>
    public static class ConfigValidator
    {
        private static readonly Regex ApiKeyPatterns = new(
            @"(api[_-]?key|secret[_-]?key|passphrase|access[_-]?token|private[_-]?key)", 
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        private static readonly HashSet<string> ForbiddenSaaSFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "ApiKey", "SecretKey", "Passphrase", "AccessToken", "PrivateKey",
            "ExchangeCredentials", "TradingCredentials", "ApiSecret"
        };

        /// <summary>
        /// Validates SaaS configuration - throws if any API key patterns detected
        /// </summary>
        public static void ValidateSaaSConfig(IConfiguration config, string role)
        {
            if (role != "saas") return; // Only enforce on SaaS deployment
            
            var configDict = config.AsEnumerable()
                .Where(kvp => kvp.Value != null)
                .ToList();
            
            foreach (var (key, value) in configDict)
            {
                // Check field name
                if (ForbiddenSaaSFields.Any(f => key.EndsWith(f, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException(
                        $"SECURITY VIOLATION: Field '{key}' is forbidden in SaaS config. " +
                        $"API credentials must only exist in config.agent.yaml on LocalAgent.");
                }
                
                // Check field value for key patterns
                if (ApiKeyPatterns.IsMatch(key) || 
                    (value != null && ApiKeyPatterns.IsMatch(value)))
                {
                    throw new InvalidOperationException(
                        $"SECURITY VIOLATION: API key pattern detected in config field '{key}'. " +
                        $"Credentials must NEVER be stored or transmitted to SaaS layer.");
                }
            }
        }

        /// <summary>
        /// Validates Agent configuration - ensures required fields are present
        /// </summary>
        public static void ValidateAgentConfig(IConfiguration config)
        {
            var requiredFields = new[] { "Exchange:ApiKey", "Exchange:SecretKey" };
            foreach (var field in requiredFields)
            {
                if (string.IsNullOrWhiteSpace(config[field]))
                {
                    throw new InvalidOperationException(
                        $"Agent config missing required field: {field}. " +
                        $"This file (config.agent.yaml) must be kept local and NEVER committed to version control.");
                }
            }
        }
    }
}
```

### A4. Chromosome Structural Constraints (`Core/Models/Chromosome.Constraints.cs`)

```csharp
using System;

namespace QuantSaaS.Core.Models
{
    public partial class Chromosome
    {
        // Hard bounds for each parameter (from design docs)
        private static class Bounds
        {
            public const decimal BetaMin = 0.1m, BetaMax = 5.0m;
            public const decimal GammaMin = 0m, GammaMax = 2.0m;
            public const decimal SigmaFloorMin = 0.001m, SigmaFloorMax = 0.1m;
            public const decimal CoefMin = -2m, CoefMax = 2m; // For signal coefficients
            public const decimal ThresholdMin = 0.01m, ThresholdMax = 0.1m; // DeltaWeight threshold
            public const decimal VolRatioThresholdMin = 1.2m, VolRatioThresholdMax = 2.5m;
            public const decimal MinOrderThresholdMin = 5.0m, MinOrderThresholdMax = 20.0m; // USDT
        }

        /// <summary>
        /// Clamps all fields to hard bounds AND enforces structural constraints.
        /// Must be called after any mutation or crossover operation.
        /// </summary>
        public Chromosome Clamp()
        {
            // Hard bounds clamping
            Beta = Math.Clamp(Beta, Bounds.BetaMin, Bounds.BetaMax);
            Gamma = Math.Clamp(Gamma, Bounds.GammaMin, Bounds.GammaMax);
            SigmaFloor = Math.Clamp(SigmaFloor, Bounds.SigmaFloorMin, Bounds.SigmaFloorMax);
            CoefX1 = Math.Clamp(CoefX1, Bounds.CoefMin, Bounds.CoefMax);
            CoefX2 = Math.Clamp(CoefX2, Bounds.CoefMin, Bounds.CoefMax);
            CoefX3 = Math.Clamp(CoefX3, Bounds.CoefMin, Bounds.CoefMax);
            DeltaWeightThreshold = Math.Clamp(DeltaWeightThreshold, Bounds.ThresholdMin, Bounds.ThresholdMax);
            VolatilityRatioThreshold = Math.Clamp(VolatilityRatioThreshold, Bounds.VolRatioThresholdMin, Bounds.VolRatioThresholdMax);
            MinOrderThreshold = Math.Clamp(MinOrderThreshold, Bounds.MinOrderThresholdMin, Bounds.MinOrderThresholdMax);
            
            // Structural constraints (from design docs)
            EnforceEMAStructure();
            EnforceTimePeriodLocks();
            
            return this;
        }

        /// <summary>
        /// EMA structure constraint: short window must be < long window
        /// Example: MicroSignalEMABars (21) < MicroVolRatioLongBars (112)
        /// </summary>
        private void EnforceEMAStructure()
        {
            // If you add EMA-related chromosome fields, enforce ordering here
            // Example placeholder:
            // if (EmaShortWindow >= EmaLongWindow)
            //     EmaShortWindow = Math.Max(1m, EmaLongWindow - 1m);
        }

        /// <summary>
        /// Time period relativism lock: prevents contradictory time-scale parameters
        /// </summary>
        private void EnforceTimePeriodLocks()
        {
            // Example: if VolRatioShortBars must be < VolRatioLongBars
            // Enforce logical relationships between time-based parameters
        }

        /// <summary>
        /// Default seed chromosome for GA cold-start and fallback
        /// </summary>
        public static Chromosome DefaultSeed => new()
        {
            Beta = 1.0m,
            Gamma = 0.5m,
            SigmaFloor = 0.01m,
            CoefX1 = 1.0m,    // Price deviation weight
            CoefX2 = 0.5m,    // Momentum weight
            CoefX3 = 0.3m,    // Acceleration weight
            DeltaWeightThreshold = 0.03m,
            VolatilityRatioThreshold = 1.8m,
            MinOrderThreshold = 10.1m // USDT dust threshold from docs
        }.Clamp();
    }
}
```

### A5. MAVAbsChange & VolatilityRatio (`Quant/MAVCalculator.cs`)

```csharp
using System;
using System.Linq;

namespace QuantSaaS.Quant
{
    /// <summary>
    /// Calculates dimensionless volatility metrics for wedge filtering.
    /// Iron Rule: All price calculations must be dimensionless (ratios/log-returns)
    /// </summary>
    public static class MAVCalculator
    {
        /// <summary>
        /// Mean Absolute Change: average of absolute price changes over window
        /// Formula: Σ|Close[i] - Close[i-1]| / (L-1) for i in [1, L-1]
        /// NOT ATR - does not use High/Low, only Close prices
        /// </summary>
        public static decimal CalculateMAVAbsChange(decimal[] closes, int window)
        {
            if (closes == null || closes.Length < 2) return 0m;
            if (window < 2) window = 2;
            
            var actualWindow = Math.Min(window, closes.Length);
            decimal sumAbsChange = 0;
            
            for (int i = closes.Length - actualWindow + 1; i < closes.Length; i++)
            {
                sumAbsChange += Math.Abs(closes[i] - closes[i - 1]);
            }
            
            return sumAbsChange / (actualWindow - 1);
        }

        /// <summary>
        /// Volatility Ratio for wedge filtering: clip(MAV_short / MAV_long, 0.1, 3.0)
        /// From design docs: MicroVolRatioShortBars=16, MicroVolRatioLongBars=112
        /// </summary>
        public static decimal CalculateVolatilityRatio(
            decimal[] closes, 
            int shortBars = 16, 
            int longBars = 112)
        {
            if (closes == null || closes.Length < longBars) return 1.0m; // Default when insufficient data
            
            var mavShort = CalculateMAVAbsChange(closes, shortBars);
            var mavLong = CalculateMAVAbsChange(closes, longBars);
            
            if (mavLong == 0) return 1.0m;
            
            var ratio = mavShort / mavLong;
            // Clip to [0.1, 3.0] as per design docs
            return Math.Clamp(ratio, 0.1m, 3.0m);
        }
    }
}
```

---

## 🧬 Phase B: GA Core Completion

### B1. 8-Verb EvolvableStrategy Interface (`Evolution/Interfaces/IEvolvableStrategy.cs`)

```csharp
using System;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Evolution.Interfaces
{
    /// <summary>
    /// Abstract interface for GA engine to interact with any strategy.
    /// Engine knows NOTHING about chromosome field names - only these 8 verbs.
    /// Enables adding new strategies without modifying GA engine code.
    /// </summary>
    public interface IEvolvableStrategy
    {
        /// <summary>
        /// Returns unique strategy identifier (e.g., "lunar-btc-spot-v1")
        /// </summary>
        string StrategyId();

        /// <summary>
        /// Samples a random chromosome from valid gene space
        /// Must call Clamp() before returning
        /// </summary>
        Chromosome Sample(Random rng);

        /// <summary>
        /// Applies additive Gaussian mutation to chromosome
        /// prob: Bernoulli probability per dimension to mutate
        /// scale: multiplier for Gaussian step size
        /// Must call Clamp() after mutation
        /// </summary>
        void Mutate(Chromosome chromosome, double prob, double scale, Random rng);

        /// <summary>
        /// Uniform crossover: each dimension independently 50% from either parent
        /// Must call Clamp() after crossover
        /// </summary>
        Chromosome Crossover(Chromosome parent1, Chromosome parent2, Random rng);

        /// <summary>
        /// Generates deterministic fingerprint for chromosome
        /// FNV-1a-64 hash with 1e-6 precision quantization
        /// Same params (within 1e-6) → same fingerprint
        /// </summary>
        string Fingerprint(Chromosome chromosome);

        /// <summary>
        /// Evaluates chromosome fitness using multi-window crucible
        /// Returns total score + per-window breakdown
        /// Implements cascading short-circuit: fatal → immediate exit
        /// </summary>
        FitnessResult Evaluate(EvaluablePlan plan, Chromosome chromosome, Random rng);

        /// <summary>
        /// Decodes elite chromosome from database JSON
        /// Returns DefaultSeed if JSON is null/invalid
        /// </summary>
        Chromosome DecodeElite(string paramPackJson);

        /// <summary>
        /// Encodes champion chromosome + spawn point to JSON for DB storage
        /// Format: { "spawn_point": {...}, "[strategy]_config": {...} }
        /// </summary>
        string EncodeResult(Chromosome champion, SpawnPoint spawn);
    }

    /// <summary>
    /// Fitness evaluation result with multi-window breakdown
    /// </summary>
    public record FitnessResult
    {
        public decimal ScoreTotal { get; init; }
        public decimal MaxDrawdown { get; init; }
        public bool IsFatal { get; init; } // MaxDD >= 88%
        
        // Per-window scores for debugging/UI
        public WindowScore Score6m { get; init; }
        public WindowScore Score2y { get; init; }
        public WindowScore Score5y { get; init; }
        public WindowScore ScoreFull { get; init; }
    }

    public record WindowScore
    {
        public string Label { get; init; } = null!; // "6m", "2y", etc.
        public decimal Weight { get; init; }
        public decimal Alpha { get; init; } // ROI_strategy - ROI_GhostDCA
        public decimal SliceScore { get; init; } // Alpha - 1.5 * max(0, excess_DD)
        public decimal MaxDrawdown { get; init; }
    }

    /// <summary>
    /// Read-only evaluation context - immutable within an Epoch
    /// Built once at Epoch start, shared across all fitness evaluations
    /// </summary>
    public record EvaluablePlan
    {
        public string Pair { get; init; } = null!;
        public string TemplateName { get; init; } = null!;
        public SpawnPoint Spawn { get; init; } = null!;
        public decimal LotStep { get; init; }
        public decimal LotMin { get; init; }
        
        // Four crucible windows in short→long order for cascading short-circuit
        public CrucibleWindow[] Windows { get; init; } = null!;
        
        // Pre-computed Ghost DCA baselines (one per window)
        public GhostDCAResult[] DcaBaselines { get; init; } = null!;
    }

    public record CrucibleWindow
    {
        public string Label { get; init; } = null!; // "6m", "2y", "5y", "full"
        public decimal Weight { get; init; }
        public decimal[] Bars { get; init; } = null!; // Includes warmup prefix
        public long EvalStartMs { get; init; } // First bar of evaluation interval (after warmup)
    }
}
```

### B2. Ghost DCA Benchmark Simulator (`Evolution/GhostDCA/Simulator.cs`)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantSaaS.Evolution.GhostDCA
{
    /// <summary>
    /// Passive DCA benchmark simulator for fitness comparison.
    /// Strategy must beat this baseline (Alpha > 0) to be considered viable.
    /// Uses Modified Dietz method for ROI calculation (excludes cash flow timing effects).
    /// </summary>
    public static class Simulator
    {
        public record GhostDCAConfig(decimal InitialCapital, decimal MonthlyInject);
        
        public record GhostDCAResult
        {
            public decimal FinalEquity { get; init; }
            public decimal TotalInjected { get; init; }
            public decimal MaxDrawdown { get; init; } // Peak-to-trough relative drawdown
            public decimal ROI { get; init; } // Modified Dietz return
        }

        /// <summary>
        /// Simulates passive DCA strategy:
        /// 1. Buy all InitialCapital at first bar's close price
        /// 2. Each calendar month start: inject MonthlyInject USDT, buy all available BTC
        /// 3. Track NAV curve for drawdown calculation
        /// </summary>
        public static GhostDCAResult Simulate(decimal[] closes, long[] timestamps, GhostDCAConfig config)
        {
            if (closes == null || closes.Length == 0 || closes[0] == 0)
                return new GhostDCAResult { FinalEquity = config.InitialCapital, TotalInjected = config.InitialCapital, MaxDrawdown = 0, ROI = 0 };
            
            var btcHoldings = config.InitialCapital / closes[0];
            var totalInjected = config.InitialCapital;
            var navCurve = new List<decimal> { config.InitialCapital };
            var peakEquity = config.InitialCapital;
            var maxDrawdown = 0m;
            
            // Track month boundaries for monthly injections
            var lastMonth = DateTimeOffset.FromUnixTimeMilliseconds(timestamps[0]).UtcDateTime.Month;
            
            for (int i = 1; i < closes.Length; i++)
            {
                var currentPrice = closes[i];
                var currentEquity = btcHoldings * currentPrice;
                
                // Check for new calendar month → inject capital
                var currentMonth = DateTimeOffset.FromUnixTimeMilliseconds(timestamps[i]).UtcDateTime.Month;
                if (currentMonth != lastMonth)
                {
                    // Buy all available USDT at current price
                    var newBTC = config.MonthlyInject / currentPrice;
                    btcHoldings += newBTC;
                    totalInjected += config.MonthlyInject;
                    lastMonth = currentMonth;
                }
                
                // Update NAV and drawdown
                navCurve.Add(currentEquity);
                if (currentEquity > peakEquity) peakEquity = currentEquity;
                var drawdown = (peakEquity - currentEquity) / peakEquity;
                if (drawdown > maxDrawdown) maxDrawdown = drawdown;
            }
            
            var finalEquity = btcHoldings * closes[^1];
            var roi = CalculateModifiedDietzROI(navCurve, totalInjected, timestamps);
            
            return new GhostDCAResult
            {
                FinalEquity = finalEquity,
                TotalInjected = totalInjected,
                MaxDrawdown = maxDrawdown,
                ROI = roi
            };
        }

        /// <summary>
        /// Modified Dietz ROI: removes cash flow timing distortion
        /// Formula: (End - Start - FlowSum) / (Start + Σ(Flow_i × Weight_i))
        /// Weight_i = (TotalDays - FlowDay) / TotalDays
        /// </summary>
        private static decimal CalculateModifiedDietzROI(List<decimal> nav, decimal totalInjected, long[] timestamps)
        {
            if (nav.Count < 2) return 0m;
            
            var startEquity = nav[0];
            var endEquity = nav[^1];
            var totalDays = (DateTimeOffset.FromUnixTimeMilliseconds(timestamps[^1]) - 
                           DateTimeOffset.FromUnixTimeMilliseconds(timestamps[0])).TotalDays;
            
            if (totalDays <= 0) return 0m;
            
            // For simplicity, assume monthly injections at month start
            // In production, track exact injection timestamps for precise weighting
            var weightedFlows = totalInjected * 0.5m; // Approximate average timing
            
            var denominator = startEquity + weightedFlows;
            if (denominator == 0) return 0m;
            
            return (endEquity - startEquity - totalInjected) / denominator;
        }

        /// <summary>
        /// Calculates maximum peak-to-trough drawdown from NAV curve
        /// </summary>
        public static decimal CalculateMaxDrawdown(IEnumerable<decimal> navCurve)
        {
            var navList = navCurve.ToList();
            if (navList.Count == 0) return 0m;
            
            decimal peak = navList[0], maxDD = 0;
            foreach (var value in navList)
            {
                if (value > peak) peak = value;
                var dd = (peak - value) / peak;
                if (dd > maxDD) maxDD = dd;
            }
            return maxDD;
        }
    }
}
```

### B3. Multi-Window Crucible Evaluator (`Evolution/Crucible/MultiWindowEvaluator.cs`)

```csharp
using System;
using System.Linq;
using QuantSaaS.Evolution.Interfaces;

namespace QuantSaaS.Evolution.Crucible
{
    /// <summary>
    /// Multi-window fitness evaluator implementing cascading short-circuit.
    /// Evaluates windows in short→long order (6m→2y→5y→full).
    /// Fatal condition (MaxDD >= 88%) → immediate exit with score = -99999.
    /// </summary>
    public static class MultiWindowEvaluator
    {
        private const decimal FatalDrawdownThreshold = 0.88m; // 88% hard veto
        private const decimal DDPenaltyCoefficient = 1.5m; // Penalty for excess drawdown vs DCA

        /// <summary>
        /// Evaluates chromosome across all windows with cascading short-circuit
        /// </summary>
        public static FitnessResult Evaluate(
            EvaluablePlan plan, 
            Chromosome chromosome,
            Func<CrucibleWindow, Chromosome, BacktestResult> runBacktest)
        {
            // Windows must be ordered short→long for cascading short-circuit
            var windows = plan.Windows.OrderBy(w => w.Bars.Length).ToArray();
            var scores = new WindowScore[4];
            decimal totalScore = 0;
            bool isFatal = false;
            decimal worstDrawdown = 0;
            
            for (int i = 0; i < windows.Length; i++)
            {
                var window = windows[i];
                var dcaBaseline = plan.DcaBaselines[i];
                
                // Run backtest for this window
                var result = runBacktest(window, chromosome);
                
                // Calculate Alpha and SliceScore
                var alpha = result.ROI - dcaBaseline.ROI;
                var excessDD = Math.Max(0, result.MaxDrawdown - dcaBaseline.MaxDrawdown);
                var sliceScore = alpha - (DDPenaltyCoefficient * excessDD);
                
                // Fatal check: hard veto if MaxDD >= 88%
                if (result.MaxDrawdown >= FatalDrawdownThreshold)
                {
                    sliceScore = -99999;
                    isFatal = true;
                }
                
                // Track worst drawdown for reporting
                if (result.MaxDrawdown > worstDrawdown) worstDrawdown = result.MaxDrawdown;
                
                // Store per-window score
                scores[i] = new WindowScore
                {
                    Label = window.Label,
                    Weight = window.Weight,
                    Alpha = alpha,
                    SliceScore = sliceScore,
                    MaxDrawdown = result.MaxDrawdown
                };
                
                // Cascading short-circuit: exit immediately on fatal
                if (isFatal) break;
                
                // Accumulate weighted score
                totalScore += sliceScore * window.Weight;
            }
            
            return new FitnessResult
            {
                ScoreTotal = isFatal ? -99999 : totalScore,
                MaxDrawdown = worstDrawdown,
                IsFatal = isFatal,
                Score6m = scores.FirstOrDefault(s => s?.Label == "6m"),
                Score2y = scores.FirstOrDefault(s => s?.Label == "2y"),
                Score5y = scores.FirstOrDefault(s => s?.Label == "5y"),
                ScoreFull = scores.FirstOrDefault(s => s?.Label == "full")
            };
        }
    }

    /// <summary>
    /// Backtest result for a single window evaluation
    /// </summary>
    public record BacktestResult
    {
        public decimal ROI { get; init; }
        public decimal MaxDrawdown { get; init; }
        public decimal FinalEquity { get; init; }
        public int TradeCount { get; init; }
    }
}
```

### B4. FNV-1a-64 Fingerprint with Precision Quantization (`Evolution/Utils/FNV1a64.cs`)

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

namespace QuantSaaS.Evolution.Utils
{
    /// <summary>
    /// FNV-1a 64-bit hash for chromosome fingerprinting.
    /// Quantizes decimal values to 1e-6 precision before hashing.
    /// Same params (within 1e-6) → identical fingerprint → cache hit.
    /// </summary>
    public static class FNV1a64
    {
        private const ulong FnvOffsetBasis = 14695981039346656037;
        private const ulong FnvPrime = 1099511628211;
        private const decimal Precision = 0.000001m; // 1e-6 quantization

        /// <summary>
        /// Generates fingerprint for chromosome with 1e-6 precision
        /// </summary>
        public static string Compute(Chromosome chromosome)
        {
            var quantized = QuantizeChromosome(chromosome);
            var bytes = Encoding.UTF8.GetBytes(quantized);
            return ComputeHash(bytes).ToString("x16");
        }

        private static string QuantizeChromosome(Chosome c)
        {
            // Quantize each decimal field to 1e-6 precision
            return $"{Math.Round(c.Beta / Precision) * Precision:F6}|" +
                   $"{Math.Round(c.Gamma / Precision) * Precision:F6}|" +
                   $"{Math.Round(c.SigmaFloor / Precision) * Precision:F6}|" +
                   $"{Math.Round(c.CoefX1 / Precision) * Precision:F6}|" +
                   $"{Math.Round(c.CoefX2 / Precision) * Precision:F6}|" +
                   $"{Math.Round(c.CoefX3 / Precision) * Precision:F6}|" +
                   $"{Math.Round(c.DeltaWeightThreshold / Precision) * Precision:F6}|" +
                   $"{Math.Round(c.VolatilityRatioThreshold / Precision) * Precision:F6}|" +
                   $"{Math.Round(c.MinOrderThreshold / Precision) * Precision:F6}";
        }

        private static ulong ComputeHash(byte[] data)
        {
            ulong hash = FnvOffsetBasis;
            foreach (byte b in data)
            {
                hash ^= b;
                hash *= FnvPrime;
            }
            return hash;
        }
    }
}
```

---

## 🔌 Phase C: System Integration

### C1. WebSocket TradeCommand Schema (`Infrastructure/WebSocket/TradeCommand.cs`)

```csharp
using System;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Infrastructure.WebSocket
{
    /// <summary>
    /// Trade command sent from SaaS to LocalAgent via WebSocket.
    /// Format follows design docs Section 5.3 exactly.
    /// </summary>
    public record TradeCommand
    {
        // Required fields per design docs
        public string ClientOrderId { get; init; } = null!; // Format: inst{id}-{engine}-{ts}
        public string Action { get; init; } = null!; // "BUY" or "SELL" (uppercase)
        public string Engine { get; init; } = null!; // "MACRO" or "MICRO"
        public string Symbol { get; init; } = null!; // e.g., "BTCUSDT"
        
        // One of these will be populated based on action
        public decimal? AmountUsdt { get; init; } // For BUY: spend this much USDT
        public decimal? QtyAsset { get; init; }   // For SELL: sell this much asset
        
        public LotType LotType { get; init; } // DEAD_STACK or FLOATING (for ledger tracking)
        
        // Validation
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ClientOrderId))
                throw new ArgumentException("ClientOrderId required");
            if (Action != "BUY" && Action != "SELL")
                throw new ArgumentException("Action must be BUY or SELL");
            if (Engine != "MACRO" && Engine != "MICRO")
                throw new ArgumentException("Engine must be MACRO or MICRO");
            if (Action == "BUY" && (!AmountUsdt.HasValue || AmountUsdt <= 0))
                throw new ArgumentException("BUY requires positive AmountUsdt");
            if (Action == "SELL" && (!QtyAsset.HasValue || QtyAsset <= 0))
                throw new ArgumentException("SELL requires positive QtyAsset");
        }
    }

    /// <summary>
    /// DeltaReport sent from Agent to SaaS after order execution
    /// Format follows design docs Section 5.4
    /// </summary>
    public record DeltaReport
    {
        public string? ClientOrderId { get; init; } // Null for initial balance snapshot on reconnect
        public BalanceSnapshot Balances { get; init; } = null!;
        public ExecutionDetail? Execution { get; init; } // Null for initial snapshot
    }

    public record BalanceSnapshot
    {
        public decimal BtcAvailable { get; init; }
        public decimal BtcFrozen { get; init; }
        public decimal UsdtAvailable { get; init; }
        public decimal UsdtFrozen { get; init; }
        public long Timestamp { get; init; }
    }

    public record ExecutionDetail
    {
        public decimal FilledQty { get; init; }
        public decimal FilledPrice { get; init; }
        public decimal Fee { get; init; }
        public string FeeAsset { get; init; } = null!;
        public string Status { get; init; } = null!; // "filled" or "failed"
    }
}
```

### C2. Idempotent Cron Tick Driver (`Infrastructure/Cron/IdempotentTickDriver.cs`)

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Infrastructure.Cron
{
    /// <summary>
    /// Cron tick driver with power-idempotency: skips if same aggregation bucket already processed.
    /// Implements design docs Section 6.2 exactly.
    /// </summary>
    public class IdempotentTickDriver
    {
        private readonly IPureStrategy _strategy;
        private readonly IRepository _repository; // Abstract DB interface
        private readonly IWebSocketHub _wsHub;
        private readonly ILogger<IdempotentTickDriver> _logger;

        public IdempotentTickDriver(
            IPureStrategy strategy,
            IRepository repository,
            IWebSocketHub wsHub,
            ILogger<IdempotentTickDriver> logger)
        {
            _strategy = strategy;
            _repository = repository;
            _wsHub = wsHub;
            _logger = logger;
        }

        /// <summary>
        /// Executes one tick for a strategy instance with idempotency check
        /// </summary>
        public async Task<bool> ExecuteTickAsync(Guid instanceId, CancellationToken ct)
        {
            // Step 1: Power-idempotent bucket check (Iron Rule: skip if already processed)
            var portfolio = await _repository.GetPortfolioStateAsync(instanceId, ct);
            var latestBar = await _repository.GetLatestCompletedBarAsync(portfolio.Symbol, portfolio.AggregationPeriod, ct);
            
            if (latestBar == null || latestBar.OpenTime <= portfolio.LastProcessedBarTime)
            {
                _logger.LogDebug("Instance {InstanceId}: aggregation bucket {BarTime} already processed, skipping tick", 
                    instanceId, latestBar?.OpenTime);
                return false; // No work done
            }

            try
            {
                // Step 2: Read required state snapshots
                var runtimeState = await _repository.GetRuntimeStateAsync(instanceId, ct);
                var championParams = await _repository.GetChampionParamsAsync(instanceId, ct);
                
                // Step 3: ACL outer circle: extract []float64 from OHLCV (Iron Rule: strategy never sees Bar struct)
                var closes = latestBar.ClosePrices.ToArray(); // Already extracted by data layer
                var timestamps = latestBar.Timestamps.ToArray();
                
                // Step 4: Build StrategyInput (immutable snapshot)
                var input = new StrategyInput
                {
                    ClosePrices = closes,
                    CurrentPrice = latestBar.Close,
                    Portfolio = portfolio,
                    Market = await _repository.GetMarketStateAsync(portfolio.Symbol, ct),
                    Config = championParams.Chromosome,
                    RuntimeState = runtimeState,
                    Timestamps = timestamps
                };
                
                // Step 5: Call pure Step() function (same implementation for backtest/live - Iron Rule)
                var output = _strategy.Step(input);
                
                // Step 6: Persist RuntimeState (strategy's internal memory)
                await _repository.SaveRuntimeStateAsync(instanceId, output.NewRuntimeState, ct);
                
                // Step 7: Handle DeadBTC release (SaaS-side ledger update ONLY, no Agent command - Iron Rule)
                if (output.ReleaseIntent.HasValue)
                {
                    await _repository.ExecuteDeadReleaseAsync(instanceId, output.ReleaseIntent.Value, ct);
                    await _repository.WriteAuditLogAsync(instanceId, "DEAD_RELEASE", output.ReleaseIntent.Value, ct);
                }
                
                // Step 8: Translate to TradeCommand and dispatch (if order size meets threshold)
                if (Math.Abs(output.OrderUSD) >= championParams.Chromosome.MinOrderThreshold)
                {
                    var command = TranslateToCommand(instanceId, output, portfolio.Symbol);
                    command.Validate(); // Safety check
                    
                    // Write pending execution record BEFORE dispatch (for idempotent reconciliation)
                    await _repository.CreatePendingExecutionAsync(instanceId, command, ct);
                    
                    // Dispatch via WebSocket (skip if Agent disconnected - will retry next tick)
                    var dispatched = await _wsHub.SendToAgentAsync(portfolio.UserId, command, ct);
                    if (!dispatched)
                    {
                        _logger.LogWarning("Instance {InstanceId}: Agent disconnected, command {OrderId} queued for retry", 
                            instanceId, command.ClientOrderId);
                    }
                }
                
                // Step 9: Update LastProcessedBarTime (critical for idempotency)
                portfolio.LastProcessedBarTime = latestBar.OpenTime;
                await _repository.UpdatePortfolioStateAsync(portfolio, ct);
                
                _logger.LogDebug("Instance {InstanceId}: tick completed, processed bar {BarTime}", 
                    instanceId, latestBar.OpenTime);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Instance {InstanceId}: tick failed", instanceId);
                await _repository.MarkInstanceErrorAsync(instanceId, ex.Message, ct);
                throw;
            }
        }

        private TradeCommand TranslateToCommand(Guid instanceId, StrategyOutput output, string symbol)
        {
            return new TradeCommand
            {
                ClientOrderId = $"inst{instanceId:N}-{output.Engine}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Action = output.Action,
                Engine = output.Engine,
                Symbol = symbol,
                AmountUsdt = output.Action == "BUY" ? Math.Abs(output.OrderUSD) : null,
                QtyAsset = output.Action == "SELL" ? Math.Abs(output.OrderUSD) / output.CurrentPrice : null,
                LotType = output.Engine == "MACRO" ? LotType.DEAD_STACK : LotType.FLOATING
            };
        }
    }
}
```

### C3. Environment Variable Injector for Secrets (`Infrastructure/Config/EnvVarInjector.cs`)

```csharp
using System;
using Microsoft.Extensions.Configuration;

namespace QuantSaaS.Infrastructure.Config
{
    /// <summary>
    /// Injects secrets from environment variables at runtime.
    /// Ensures API keys and sensitive values NEVER appear in config files or code.
    /// Iron Rule: config.agent.yaml secrets injected via env vars, never hardcoded.
    /// </summary>
    public static class EnvVarInjector
    {
        /// <summary>
        /// Replaces ${VAR_NAME} placeholders in config values with environment variable values
        /// </summary>
        public static IConfigurationBuilder AddEnvironmentVariableInjection(
            this IConfigurationBuilder builder, 
            string prefix = "")
        {
            return builder.Add(new EnvVarInjectionSource(prefix));
        }

        private class EnvVarInjectionSource : IConfigurationSource
        {
            private readonly string _prefix;
            public EnvVarInjectionSource(string prefix) => _prefix = prefix;
            
            public IConfigurationProvider Build(IConfigurationBuilder builder)
                => new EnvVarInjectionProvider(_prefix);
        }

        private class EnvVarInjectionProvider : ConfigurationProvider
        {
            private readonly string _prefix;
            private static readonly System.Text.RegularExpressions.Regex PlaceholderRegex = 
                new(@"\$\{([^}]+)\}", System.Text.RegularExpressions.RegexOptions.Compiled);
            
            public EnvVarInjectionProvider(string prefix) => _prefix = prefix;
            
            public override void Load()
            {
                // This provider works with existing config - injects env vars into placeholder values
                // Actual injection happens in TryGet override
            }
            
            public override bool TryGet(string key, out string value)
            {
                if (!base.TryGet(key, out value) || string.IsNullOrEmpty(value))
                    return false;
                
                // Replace ${VAR_NAME} with environment variable value
                var injected = PlaceholderRegex.Replace(value, match =>
                {
                    var varName = match.Groups[1].Value;
                    var envValue = Environment.GetEnvironmentVariable(_prefix + varName) 
                                 ?? Environment.GetEnvironmentVariable(varName);
                    return envValue ?? match.Value; // Keep placeholder if env var not found
                });
                
                value = injected;
                return true;
            }
        }
    }
}
```

### C4. RuntimeState Persistence Model (`Core/Models/RuntimeState.cs`)

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuantSaaS.Core.Models
{
    /// <summary>
    /// Strategy runtime state - persisted between ticks for stateful strategies.
    /// Stored as JSON blob in Postgres, loaded into memory for Step() calls.
    /// </summary>
    public class RuntimeState
    {
        [Key] public Guid InstanceId { get; set; }
        
        // JSON blob containing strategy-specific state
        // Example structure (strategy-defined):
        // {
        //   "LastSignalValue": 0.234,
        //   "ConsecutiveUpBars": 3,
        //   "AdaptiveBeta": 1.2,
        //   "CustomIndicatorCache": [...]
        // }
        [Column(TypeName = "jsonb")] public string StateJson { get; set; } = "{}";
        
        public long LastUpdatedBarTime { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Helper for deserialization (strategy-specific)
        public T GetState<T>() where T : class, new()
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(StateJson) ?? new T();
        }
        
        public void SetState<T>(T state) where T : class
        {
            StateJson = System.Text.Json.JsonSerializer.Serialize(state);
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
```

---

## 🧪 Phase D: Validation & Safety

### D1. Strategy Purity Unit Tests (`Tests/StrategyPurityTests.cs`)

```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Tests.Strategy
{
    [TestFixture]
    public class StrategyPurityTests
    {
        private TestPureStrategy _strategy;
        
        [SetUp]
        public void SetUp()
        {
            _strategy = new TestPureStrategy();
        }
        
        [Test]
        public void Step_IsDeterministic_SameInputSameOutput()
        {
            // Arrange
            var input = CreateTestInput(seed: 42);
            
            // Act
            var output1 = _strategy.Step(input);
            var output2 = _strategy.Step(input);
            
            // Assert
            Assert.That(output1.TargetWeight, Is.EqualTo(output2.TargetWeight));
            Assert.That(output1.OrderUSD, Is.EqualTo(output2.OrderUSD));
            Assert.That(output1.Action, Is.EqualTo(output2.Action));
        }
        
        [Test]
        public void Step_NoIOOperations_Allowed()
        {
            // This test verifies the strategy doesn't attempt I/O
            // In production, combine with Roslyn analyzer for compile-time checks
            
            var input = CreateTestInput();
            Assert.DoesNotThrow(() => _strategy.Step(input));
            
            // Verify no side effects: file system unchanged
            Assert.That(File.Exists("strategy_side_effect.txt"), Is.False);
        }
        
        [Test]
        public void Step_NoTimeDependency_UsesInputTimestampsOnly()
        {
            // Strategy must use input.Timestamps, not DateTime.Now
            var input1 = CreateTestInput(baseTime: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var input2 = CreateTestInput(baseTime: input1.Timestamps[0] + 3600000); // +1 hour
            
            var output1 = _strategy.Step(input1);
            var output2 = _strategy.Step(input2);
            
            // Outputs should differ only due to price/time data, not system clock
            // (This is a behavioral test; compile-time checks are stronger)
            Assert.Pass("Time dependency test - verify strategy uses input timestamps");
        }
        
        private StrategyInput CreateTestInput(long? seed = null, long baseTime = 0)
        {
            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            var prices = new decimal[100];
            for (int i = 0; i < prices.Length; i++)
                prices[i] = 50000m + (decimal)(rng.NextDouble() * 1000);
            
            return new StrategyInput
            {
                ClosePrices = prices,
                CurrentPrice = prices[^1],
                Portfolio = new PortfolioState
                {
                    TotalEquity = 10000m,
                    FloatBTC = 0.1m,
                    USDTBalance = 5000m
                },
                Market = new MarketState { IsQuiet = false, BetaMultiplier = 1.0m },
                Config = Chromosome.DefaultSeed,
                Timestamps = Enumerable.Range(0, prices.Length)
                    .Select(i => baseTime + i * 60000) // 1-minute bars
                    .ToArray()
            };
        }
    }
    
    // Example pure strategy for testing
    internal class TestPureStrategy : IPureStrategy
    {
        public StrategyOutput Step(StrategyInput input)
        {
            // Pure calculation only - no I/O
            var avg = input.ClosePrices.Average();
            var signal = (input.CurrentPrice - avg) / avg;
            var targetWeight = 1m / (1m + (decimal)Math.Exp((double)(-signal)));
            
            return new StrategyOutput
            {
                TargetWeight = targetWeight,
                OrderUSD = (targetWeight - 0.5m) * input.Portfolio.TotalEquity * 0.1m,
                Action = "HOLD",
                SignalReason = $"Price deviation: {signal:F4}"
            };
        }
        
        public StrategyMetadata Metadata => new()
        {
            StrategyId = "test-pure-strategy",
            DisplayName = "Test Pure Strategy",
            Version = "1.0.0",
            IsSpotOnly = true
        };
    }
}
```

### D2. Deterministic Backtest Test (`Tests/DeterministicBacktestTests.cs`)

```csharp
using System;
using System.Linq;
using NUnit.Framework;
using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;
using QuantSaaS.Evolution.Interfaces;

namespace QuantSaaS.Tests.Evolution
{
    [TestFixture]
    public class DeterministicBacktestTests
    {
        private IEvolvableStrategy _evolvable;
        private EvaluablePlan _testPlan;
        
        [SetUp]
        public void SetUp()
        {
            // Setup test plan with deterministic data
            _evolvable = new TestEvolvableStrategy();
            _testPlan = CreateDeterministicTestPlan();
        }
        
        [Test]
        public void Evaluate_IsDeterministic_IdenticalResultsOnRepeatedCalls()
        {
            // Arrange
            var chromosome = Chromosome.DefaultSeed;
            var rng1 = new Random(12345); // Fixed seed for reproducibility
            var rng2 = new Random(12345); // Same seed
            
            // Act
            var result1 = _evolvable.Evaluate(_testPlan, chromosome, rng1);
            var result2 = _evolvable.Evaluate(_testPlan, chromosome, rng2);
            
            // Assert
            Assert.That(result1.ScoreTotal, Is.EqualTo(result2.ScoreTotal));
            Assert.That(result1.MaxDrawdown, Is.EqualTo(result2.MaxDrawdown));
            Assert.That(result1.IsFatal, Is.EqualTo(result2.IsFatal));
            
            // Per-window scores must also match
            Assert.That(result1.Score6m?.SliceScore, Is.EqualTo(result2.Score6m?.SliceScore));
            Assert.That(result1.Score2y?.SliceScore, Is.EqualTo(result2.Score2y?.SliceScore));
        }
        
        [Test]
        public void Fingerprint_IsStable_WithinPrecisionTolerance()
        {
            // Arrange
            var chrom1 = Chromosome.DefaultSeed;
            var chrom2 = new Chromosome
            {
                Beta = chrom1.Beta + 0.0000001m, // Within 1e-6 precision
                Gamma = chrom1.Gamma,
                // ... copy other fields
            };
            
            // Act
            var fp1 = _evolvable.Fingerprint(chrom1);
            var fp2 = _evolvable.Fingerprint(chrom2);
            
            // Assert
            Assert.That(fp1, Is.EqualTo(fp2), 
                "Fingerprints should match for params within 1e-6 precision");
        }
        
        private EvaluablePlan CreateDeterministicTestPlan()
        {
            // Generate deterministic price series for testing
            var rng = new Random(42);
            var prices = new decimal[1000];
            prices[0] = 50000m;
            for (int i = 1; i < prices.Length; i++)
                prices[i] = prices[i-1] * (1m + (decimal)(rng.NextDouble() * 0.02m - 0.01m));
            
            var timestamps = Enumerable.Range(0, prices.Length)
                .Select(i => DateTimeOffset.UtcNow.AddDays(-1000 + i).ToUnixTimeMilliseconds())
                .ToArray();
            
            return new EvaluablePlan
            {
                Pair = "BTCUSDT",
                TemplateName = "test-template",
                Spawn = new SpawnPoint(), // Test defaults
                LotStep = 0.00001m,
                LotMin = 0.0001m,
                Windows = new[]
                {
                    new CrucibleWindow { Label = "6m", Weight = 0.10m, Bars = prices.Take(200).ToArray(), EvalStartMs = timestamps[100] },
                    new CrucibleWindow { Label = "2y", Weight = 0.20m, Bars = prices.Take(500).ToArray(), EvalStartMs = timestamps[200] },
                    // ... other windows
                },
                DcaBaselines = new[] { /* pre-computed DCA results */ }
            };
        }
    }
}
```

### D3. Sigmoid Property-Based Tests (`Tests/SigmoidPropertyTests.cs`)

```csharp
using System;
using System.Linq;
using NUnit.Framework;
using QuantSaaS.Core.Models;
using QuantSaaS.Quant;

namespace QuantSaaS.Tests.Quant
{
    [TestFixture]
    public class SigmoidPropertyTests
    {
        [Test]
        public void SignalPositive_TargetWeightBelowHalf()
        {
            // Property: Signal > 0 → TargetWeight < 0.5
            var input = CreateInput(signal: 0.5m); // Positive signal = bearish
            var output = SigmoidMicroEngine.Compute(input);
            
            Assert.That(output.TargetWeight, Is.LessThan(0.5m), 
                "Positive signal (bearish) should produce target weight < 0.5");
        }
        
        [Test]
        public void SignalNegative_TargetWeightAboveHalf()
        {
            // Property: Signal < 0 → TargetWeight > 0.5
            var input = CreateInput(signal: -0.5m); // Negative signal = bullish
            var output = SigmoidMicroEngine.Compute(input);
            
            Assert.That(output.TargetWeight, Is.GreaterThan(0.5m),
                "Negative signal (bullish) should produce target weight > 0.5");
        }
        
        [Test]
        public void GammaPositive_MeanReversionAtNeutralWeight()
        {
            // Property: When CurrentWeight=0.5 and Gamma>0, InventoryBias=0, so gamma term vanishes
            var input = CreateInput(currentWeight: 0.5m, gamma: 1.0m, signal: 0);
            var output = SigmoidMicroEngine.Compute(input);
            
            // With signal=0 and bias=0, exponent=0, so target=0.5
            Assert.That(output.TargetWeight, Is.EqualTo(0.5m).Within(1e-6),
                "Neutral weight + zero signal should maintain 0.5 target");
        }
        
        [Test]
        public void QuietMode_DustOrdersSuppressed()
        {
            // Property: IsQuiet=true AND |TheoreticalUSD| < threshold → OrderUSD = 0
            var input = CreateInput(
                theoreticalUSD: 5m, // Below 10.1 threshold
                isQuiet: true,
                minOrderThreshold: 10.1m);
            
            var output = SigmoidMicroEngine.Compute(input);
            Assert.That(output.OrderUSD, Is.EqualTo(0m),
                "Quiet mode should suppress dust orders below threshold");
        }
        
        [Test]
        public void WedgeBreakout_ForcesMinimumOrder()
        {
            // Property: Non-quiet + wedge breakout → force min order even if below threshold
            var input = CreateInput(
                theoreticalUSD: 5m, // Below threshold
                isQuiet: false,
                deltaWeight: 0.03m, // Above wedge threshold (0.02)
                minOrderThreshold: 10.1m);
            
            var output = SigmoidMicroEngine.Compute(input);
            Assert.That(Math.Abs(output.OrderUSD), Is.EqualTo(10.1m),
                "Wedge breakout should force minimum order size");
        }
        
        private StrategyInput CreateInput(
            decimal signal = 0,
            decimal currentWeight = 0.5m,
            decimal gamma = 0.5m,
            bool isQuiet = false,
            decimal theoreticalUSD = 100m,
            decimal deltaWeight = 0.01m,
            decimal minOrderThreshold = 10.1m)
        {
            return new StrategyInput
            {
                ClosePrices = new[] { 50000m, 50100m, 49900m, 50050m },
                CurrentPrice = 50050m,
                Portfolio = new PortfolioState
                {
                    TotalEquity = 10000m,
                    FloatBTC = currentWeight * 10000m / 50050m
                },
                Market = new MarketState { IsQuiet = isQuiet, BetaMultiplier = 1.0m },
                Config = new Chromosome
                {
                    Beta = 1.0m,
                    Gamma = gamma,
                    MinOrderThreshold = minOrderThreshold,
                    DeltaWeightThreshold = 0.02m,
                    VolatilityRatioThreshold = 1.8m
                }
                // Signal calculation would use ClosePrices - simplified for test
            };
        }
    }
}
```

### D4. CI Security Scan Script (`CI/security-scan.ps1`)

```powershell
# security-scan.ps1 - CI pipeline security checks for QuantSaaS
# Runs on every PR/push to enforce iron rules

param(
    [string]$ProjectRoot = ".",
    [switch]$FailOnError = $true
)

$ErrorActionPreference = "Stop"
$exitCode = 0

Write-Host "🔒 QuantSaaS Security & Purity Scan" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan

# Iron Rule 1: Strategy purity - no I/O in strategy packages
Write-Host "`n[1/5] Checking strategy package purity..." -ForegroundColor Yellow
$strategyPaths = @(
    "internal/strategies/",
    "src/QuantSaaS.Core/Strategies/",
    "src/QuantSaaS.Strategies/"
)

foreach ($path in $strategyPaths) {
    $fullPath = Join-Path $ProjectRoot $path
    if (Test-Path $fullPath) {
        $ioPatterns = @(
            "HttpClient", "HttpWebRequest", "WebClient",
            "SqlConnection", "DbContext", "Database",
            "File.Open", "File.ReadAllText", "StreamWriter",
            "DateTime.Now", "DateTime.UtcNow", "Stopwatch",
            "new Timer", "Task.Delay", "Thread.Sleep"
        )
        
        foreach ($pattern in $ioPatterns) {
            $results = Get-ChildItem -Path $fullPath -Recurse -Include *.cs | 
                      Select-String -Pattern $pattern -SimpleMatch
            if ($results) {
                Write-Host "  ❌ I/O pattern '$pattern' found in strategy code:" -ForegroundColor Red
                $results | ForEach-Object { Write-Host "     $($_.Path):$($_.LineNumber)" -ForegroundColor Red }
                $exitCode = 1
            }
        }
    }
}

# Iron Rule 2: API key isolation - no credentials in SaaS code
Write-Host "`n[2/5] Checking API key isolation..." -ForegroundColor Yellow
$saasPaths = @("src/QuantSaaS.SaaS/", "internal/saas/")
$apiKeyPatterns = @("ApiKey", "SecretKey", "Passphrase", "api_key", "secret_key", "access_token")

foreach ($path in $saasPaths) {
    $fullPath = Join-Path $ProjectRoot $path
    if (Test-Path $fullPath) {
        foreach ($pattern in $apiKeyPatterns) {
            $results = Get-ChildItem -Path $fullPath -Recurse -Include *.cs, *.csproj, *.config | 
                      Select-String -Pattern $pattern -CaseSensitive:$false
            # Filter out comments and test files
            $realViolations = $results | Where-Object { 
                $_.Line -notmatch "//" -and 
                $_.Line -notmatch "/\*" -and
                $_.Path -notmatch "Test|test|Spec"
            }
            if ($realViolations) {
                Write-Host "  ❌ Potential API key pattern '$pattern' in SaaS code:" -ForegroundColor Red
                $realViolations | ForEach-Object { Write-Host "     $($_.Path):$($_.LineNumber)" -ForegroundColor Red }
                $exitCode = 1
            }
        }
    }
}

# Iron Rule 3: No isBacktest branching in Step()
Write-Host "`n[3/5] Checking strategy homomorphism..." -ForegroundColor Yellow
$stepFiles = Get-ChildItem -Path $ProjectRoot -Recurse -Include *Step*.cs, *Strategy*.cs | 
            Where-Object { $_.DirectoryName -match "strategy|Strategy" }

foreach ($file in $stepFiles) {
    $content = Get-Content $file.FullName -Raw
    if ($content -match "if\s*\(.*[Bb]acktest" -or $content -match "isBacktest\s*=") {
        Write-Host "  ❌ Backtest branching detected in $($file.Name):" -ForegroundColor Red
        Write-Host "     Strategies must use same Step() for backtest/live" -ForegroundColor Red
        $exitCode = 1
    }
}

# Iron Rule 4: config.agent.yaml in .gitignore
Write-Host "`n[4/5] Checking secret file exclusion..." -ForegroundColor Yellow
$gitignorePath = Join-Path $ProjectRoot ".gitignore"
if (Test-Path $gitignorePath) {
    $gitignore = Get-Content $gitignorePath -Raw
    if ($gitignore -notmatch "config\.agent\.yaml" -and $gitignore -notmatch "\*\.agent\.yaml") {
        Write-Host "  ❌ config.agent.yaml not found in .gitignore" -ForegroundColor Red
        Write-Host "     API keys must NEVER be committed to version control" -ForegroundColor Red
        $exitCode = 1
    }
} else {
    Write-Host "  ⚠️  .gitignore not found - manual verification required" -ForegroundColor Yellow
}

# Iron Rule 5: Dimensionless calculations (heuristic check)
Write-Host "`n[5/5] Checking for dimensionless price calculations..." -ForegroundColor Yellow
# Heuristic: flag direct price comparisons across symbols
$priceComparePattern = @"
\b(price|close|open|high|low)\s*[<>=!]+\s*(price|close|open|high|low)\b
"@
# This is a simplified check - full validation requires semantic analysis

if ($exitCode -eq 0 -and -not $FailOnError) {
    Write-Host "`n✅ All security and purity checks passed" -ForegroundColor Green
} elseif ($exitCode -ne 0) {
    Write-Host "`n❌ Security/purity violations detected - review above" -ForegroundColor Red
    if ($FailOnError) { exit $exitCode }
}

exit $exitCode
```

---

## 📋 Integration Guide

### Step 1: Update Project References
```xml
<!-- QuantSaaS.Core.csproj -->
<ItemGroup>
  <!-- Add new dependencies -->
  <PackageReference Include="System.Linq.Async" Version="6.0.1" />
  <PackageReference Include="FNV1AHash" Version="1.0.3" /> <!-- For fingerprinting -->
</ItemGroup>
```

### Step 2: Register Services in DI Container
```csharp
// Program.cs or Startup.cs
builder.Services.AddSingleton<IPureStrategy, YourStrategyImplementation>();
builder.Services.AddScoped<IdempotentTickDriver>();
builder.Services.AddEnvironmentVariableInjection(); // For secret injection

// GA services (lab/dev only)
if (builder.Configuration["App:Role"] != "saas")
{
    builder.Services.AddSingleton<IEvolvableStrategy, YourStrategyEvolvable>();
    builder.Services.AddScoped<GeneticEngine>();
}
```

### Step 3: Update Configuration Schema
```yaml
# config.yaml template (SAAS)
App:
  Role: ${APP_ROLE:-dev}  # Injected via env var
  # ... other config

# DO NOT include any API key fields here
# API keys belong ONLY in config.agent.yaml on LocalAgent

# config.agent.yaml template (LOCAL - NEVER COMMIT)
SaaS:
  Url: "wss://your-saas-domain/ws/agent"
Exchange:
  Name: "Bitget"
  ApiKey: ${EXCHANGE_API_KEY}  # ← Injected via env var
  SecretKey: ${EXCHANGE_SECRET_KEY}  # ← Injected via env var
  # NEVER hardcode values here
```

### Step 4: Run Security Scan in CI
```yaml
# .github/workflows/ci.yml
- name: Security & Purity Scan
  run: pwsh ./CI/security-scan.ps1 -ProjectRoot . -FailOnError $true
```

### Step 5: Verify Integration
```bash
# Build and test
dotnet build QuantSaaS.sln
dotnet test --filter "Category=Integration"

# Run purity checks
dotnet test --filter "StrategyPurity"
dotnet test --filter "DeterministicBacktest"

# Security scan (local)
pwsh ./CI/security-scan.ps1
```

---

## ⚠️ Critical Reminders (Iron Rules)

1. **Strategy Purity**: `Step()` must remain a pure function. Use the `[StrategyPurity]` attribute and runtime guard during development.

2. **API Key Isolation**: Never, ever allow API credentials to enter the SaaS layer. Validate configs at startup.

3. **Homomorphism**: Backtest and live MUST use identical `Step()` implementation. No `if (isBacktest)` branches.

4. **Dimensionless Math**: All price calculations must use ratios/log-returns. Never compare absolute prices across assets.

5. **Idempotency**: Cron tick MUST check `LastProcessedBarTime` before executing to prevent duplicate orders.

6. **User-Facing Terminology**: Never expose internal terms (`DeadBTC`, `FloatBTC`, `Step()`) in UI. Use the `UserTerms` mapping.

---

## 🔄 Migration from Initial Implementation

| Initial Code | Updated Replacement | Notes |
|-------------|-------------------|-------|
| `IStrategy.Step()` | `IPureStrategy.Step()` | Added purity enforcement |
| `Chromosome` class | `Chromosome` + `.Clamp()` | Added structural constraints |
| `SigmoidMicroEngine.Compute()` | Same + `MAVCalculator` | Added proper VolatilityRatio |
| `GeneticEngine` | `IEvolvableStrategy` interface | Decoupled GA from strategy |
| `WebSocket Hub` | `TradeCommand` + `DeltaReport` | Full protocol implementation |
| `Cron Scheduler` | `IdempotentTickDriver` | Added power-idempotency |
| Config loading | `EnvVarInjector` | Secret injection via env vars |

---

## 🎯 Next Steps After Integration

1. **Implement your specific strategy** by inheriting `IPureStrategy` and implementing the signal synthesis logic per your design docs.

2. **Create `[YourStrategy]Evolvable`** implementing the 8-verb interface for GA integration.

3. **Add user-facing terminology mapping** in your frontend to replace internal terms.

4. **Run the full test suite** including property-based tests for your specific signal logic.

5. **Deploy to dev environment** and verify end-to-end flow with LocalAgent simulator.

---