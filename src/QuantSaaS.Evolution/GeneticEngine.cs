using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using QuantSaaS.Core.Models;
using QuantSaaS.Quant;

namespace QuantSaaS.Evolution;

public class EpochConfig
{
    public int PopSize { get; set; } = 300;
    public int MaxGenerations { get; set; } = 25;
    public SpawnPoint? SpawnPointOverride { get; set; }
    public IReadOnlyList<Chromosome>? PreviousElites { get; set; }
    public Action<int, double, double, double>? OnProgress { get; set; }
    public bool TestMode { get; set; } = false;
}

public record EpochResult
{
    public Chromosome ChampionChromosome { get; init; } = null!;
    public SpawnPoint SpawnPoint { get; init; } = null!;
    public string ParamPackJson { get; init; } = null!;
    public double ScoreTotal { get; init; }
    public decimal MaxDrawdown { get; init; }
    public WindowScore[]? WindowScores { get; init; }
}

/// <summary>
/// GA Evolution Engine. Knows nothing about chromosome fields - uses 8-verb interface.
/// Full lifecycle: elite init → concurrent evaluation → tournament selection →
/// uniform crossover → additive Gaussian mutation → elite preservation → mutation ramp.
/// </summary>
public class GeneticEngine
{
    private readonly IEvolvableStrategy _strategy;
    private readonly ILogger<GeneticEngine>? _logger;

    public int PopulationSize { get; set; } = 300;
    public int MaxGenerations { get; set; } = 25;
    public int EliteCount { get; set; } = 8;
    public int TournamentSize { get; set; } = 3;
    public double MutationProbability { get; set; } = 0.15;
    public double MutationScale { get; set; } = 1.0;
    public double MutationProbMax { get; set; } = 0.55;
    public double MutationScaleMax { get; set; } = 3.0;
    public double RampFactor { get; set; } = 1.25;
    public int EarlyStopPatience { get; set; } = 5;
    public double EarlyStopMinDelta { get; set; } = 0.001;

    public GeneticEngine(IEvolvableStrategy strategy, ILogger<GeneticEngine>? logger = null)
    {
        _strategy = strategy;
        _logger = logger;
    }

    public async Task<EpochResult> RunEpochAsync(EvaluablePlan plan, EpochConfig config,
        CancellationToken ct = default)
    {
        if (config.TestMode) { PopulationSize = 10; MaxGenerations = 3; }

        var spawn = config.SpawnPointOverride ?? SpawnPoint.Default;

        // Step 1: Initialize population with elite seeding
        var population = InitializePopulation(config.PreviousElites);

        // Step 2: Initial evaluation
        var cache = new ConcurrentDictionary<string, FitnessResult>();
        var fitness = await EvaluatePopulationAsync(population, plan, cache, ct);

        double bestScore = fitness.Max();
        int patience = 0;
        double mutProb = MutationProbability;
        double mutScale = MutationScale;

        for (int gen = 0; gen < MaxGenerations; gen++)
        {
            ct.ThrowIfCancellationRequested();

            // Sort population by fitness descending
            var sorted = population.Zip(fitness, (c, f) => (c, f))
                .OrderByDescending(x => x.f).ToList();

            double currentBest = sorted[0].f;
            double improvement = currentBest - bestScore;

            if (improvement <= EarlyStopMinDelta)
            {
                patience++;
                if (patience >= EarlyStopPatience)
                {
                    if (mutProb < MutationProbMax || mutScale < MutationScaleMax)
                    {
                        mutProb = Math.Min(MutationProbMax, mutProb * RampFactor);
                        mutScale = Math.Min(MutationScaleMax, mutScale * RampFactor);
                        _logger?.LogInformation("Gen {Gen}: Mutation ramp → Prob={P:F3}, Scale={S:F3}", gen, mutProb, mutScale);
                        patience = 0;
                    }
                    else
                    {
                        _logger?.LogInformation("Gen {Gen}: Early stop (both caps reached)", gen);
                        break;
                    }
                }
            }
            else
            {
                patience = 0;
                bestScore = currentBest;
            }

            config.OnProgress?.Invoke(gen, currentBest, mutProb, mutScale);

            // Build next generation
            var nextGen = new Chromosome[PopulationSize];
            // Elite preservation: top EliteCount go unchanged
            for (int i = 0; i < Math.Min(EliteCount, sorted.Count); i++)
                nextGen[i] = sorted[i].c.DeepClone();

            // Fill rest with tournament → crossover → mutate
            var rng = new Random();
            for (int i = EliteCount; i < PopulationSize; i++)
            {
                var p1 = TournamentSelect(sorted, rng);
                var p2 = TournamentSelect(sorted, rng);
                var child = _strategy.Crossover(p1, p2, rng);
                _strategy.Mutate(child, mutProb, mutScale, rng);
                nextGen[i] = child;
            }

            population = nextGen;
            fitness = await EvaluatePopulationAsync(population, plan, cache, ct);
        }

        // Deliver champion
        int champIdx = fitness.Select((f, i) => (f, i)).OrderByDescending(x => x.f).First().i;
        var champion = population[champIdx];
        var champFitness = cache.Values
            .OrderByDescending(f => f.ScoreTotal)
            .FirstOrDefault() ?? new FitnessResult();

        return new EpochResult
        {
            ChampionChromosome = champion,
            SpawnPoint = spawn,
            ParamPackJson = _strategy.EncodeResult(champion, spawn),
            ScoreTotal = fitness[champIdx],
            MaxDrawdown = champFitness.MaxDrawdown,
            WindowScores = champFitness.WindowScores
        };
    }

    private Chromosome[] InitializePopulation(IReadOnlyList<Chromosome>? elites)
    {
        var rng = new Random();
        var pop = new Chromosome[PopulationSize];

        if (elites != null && elites.Count > 0)
        {
            pop[0] = elites[0].DeepClone(); // Index 0 = current champion
            int remaining = PopulationSize - 1;
            int copyCount = (int)Math.Round(remaining * 0.10);
            int reinforceCount = (int)Math.Round(remaining * 0.40);

            int idx = 1;
            for (int i = 0; i < copyCount && idx < PopulationSize; i++)
                pop[idx++] = elites[rng.Next(elites.Count)].DeepClone();

            for (int i = 0; i < reinforceCount && idx < PopulationSize; i++)
            {
                var baseGene = elites[rng.Next(elites.Count)].DeepClone();
                _strategy.Mutate(baseGene, 0.15, 1.5, rng); // Fixed init mutation
                pop[idx++] = baseGene;
            }

            while (idx < PopulationSize)
                pop[idx++] = _strategy.Sample(rng);
        }
        else
        {
            pop[0] = Chromosome.DefaultSeed;
            for (int i = 1; i < PopulationSize; i++)
                pop[i] = _strategy.Sample(rng);
        }

        return pop;
    }

    private async Task<double[]> EvaluatePopulationAsync(
        Chromosome[] population,
        EvaluablePlan plan,
        ConcurrentDictionary<string, FitnessResult> cache,
        CancellationToken ct)
    {
        var fitness = new double[population.Length];
        int workers = Math.Min(Environment.ProcessorCount, population.Length);
        var semaphore = new SemaphoreSlim(workers);
        var tasks = new Task[population.Length];

        for (int i = 0; i < population.Length; i++)
        {
            int idx = i;
            var gene = population[i];
            tasks[i] = Task.Run(() =>
            {
                semaphore.Wait(ct);
                try
                {
                    var fp = _strategy.Fingerprint(gene);
                    if (!cache.TryGetValue(fp, out var result))
                    {
                        result = _strategy.Evaluate(plan, gene);
                        cache.TryAdd(fp, result);
                    }
                    fitness[idx] = result.ScoreTotal;
                }
                finally { semaphore.Release(); }
            }, ct);
        }

        await Task.WhenAll(tasks);
        return fitness;
    }

    private Chromosome TournamentSelect(List<(Chromosome c, double f)> sorted, Random rng)
    {
        Chromosome? best = null;
        double bestScore = double.MinValue;
        var used = new HashSet<int>();

        int attempts = 0;
        while (used.Count < TournamentSize && attempts < TournamentSize * 3)
        {
            attempts++;
            int idx = rng.Next(sorted.Count);
            if (used.Add(idx) && sorted[idx].f > bestScore)
            {
                bestScore = sorted[idx].f;
                best = sorted[idx].c;
            }
        }
        return best ?? sorted[0].c;
    }
}
