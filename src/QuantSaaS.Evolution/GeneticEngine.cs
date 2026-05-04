using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Evolution;

/// <summary>
/// Genetic Algorithm evolution engine.
/// Drives the full population lifecycle via the IEvolvableStrategy 8-verb interface.
/// Iron Rule: This class has ZERO knowledge of chromosome field names.
/// New strategies require only a new IEvolvableStrategy implementation, never touching this class.
/// </summary>
public sealed class GeneticEngine
{
    // ── Defaults ──────────────────────────────────────────────────────────────

    public int PopulationSize { get; init; } = 300;
    public int MaxGenerations { get; init; } = 25;
    public int ElitismCount { get; init; } = 8;
    public int TournamentSize { get; init; } = 3;

    private double _mutationProbability = 0.15;
    private double _mutationScale = 1.0;

    private const double MutationProbMax = 0.55;
    private const double MutationScaleMax = 3.0;
    private const double MutationProbRamp = 1.25;
    private const double MutationScaleRamp = 1.25;
    private const int EarlyStopPatience = 5;
    private const double EarlyStopMinDelta = 0.001;

    private const decimal FatalScore = -99999m;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs a GA epoch over the provided evaluable plan.
    /// Returns the best chromosome found along with its fitness result.
    /// </summary>
    public (Chromosome Best, FitnessResult BestFitness) RunEpoch(
        IEvolvableStrategy strategy,
        EvaluablePlan plan,
        Chromosome? eliteSeed = null,
        CancellationToken ct = default)
    {
        int seed = Environment.TickCount;
        var masterRng = new Random(seed);

        // ── Population initialisation ─────────────────────────────────────────
        var population = InitialisePopulation(strategy, plan, eliteSeed, masterRng);

        FitnessResult[] fitnesses = EvaluateAll(strategy, plan, population);
        int bestIdx = FindBest(fitnesses);
        decimal bestScore = fitnesses[bestIdx].ScoreTotal;
        int stagnantGenerations = 0;

        for (int gen = 0; gen < MaxGenerations && !ct.IsCancellationRequested; gen++)
        {
            // ── Elites ────────────────────────────────────────────────────────
            var nextPop = new Chromosome[PopulationSize];
            var eliteIndices = TopNIndices(fitnesses, ElitismCount);
            for (int i = 0; i < ElitismCount; i++)
                nextPop[i] = population[eliteIndices[i]];

            // ── Offspring ─────────────────────────────────────────────────────
            for (int i = ElitismCount; i < PopulationSize; i++)
            {
                var p1 = population[TournamentSelect(fitnesses, masterRng)];
                var p2 = population[TournamentSelect(fitnesses, masterRng)];
                var child = strategy.Crossover(p1, p2, masterRng);
                strategy.Mutate(child, _mutationProbability, _mutationScale, masterRng);
                nextPop[i] = child;
            }

            population = nextPop;
            fitnesses = EvaluateAll(strategy, plan, population);
            bestIdx = FindBest(fitnesses);

            decimal currentBest = fitnesses[bestIdx].ScoreTotal;
            if (currentBest - bestScore > (decimal)EarlyStopMinDelta)
            {
                bestScore = currentBest;
                stagnantGenerations = 0;
                _mutationProbability = 0.15;
                _mutationScale = 1.0;
            }
            else
            {
                stagnantGenerations++;
                if (stagnantGenerations >= EarlyStopPatience)
                {
                    _mutationProbability = Math.Min(MutationProbMax, _mutationProbability * MutationProbRamp);
                    _mutationScale = Math.Min(MutationScaleMax, _mutationScale * MutationScaleRamp);

                    // Early stop only when both limits hit and still no improvement
                    if (_mutationProbability >= MutationProbMax && _mutationScale >= MutationScaleMax)
                        break;
                }
            }
        }

        return (population[bestIdx], fitnesses[bestIdx]);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Chromosome[] InitialisePopulation(
        IEvolvableStrategy strategy,
        EvaluablePlan plan,
        Chromosome? eliteSeed,
        Random rng)
    {
        var pop = new Chromosome[PopulationSize];

        // Index 0: seed champion
        pop[0] = eliteSeed != null
            ? strategy.DecodeElite(strategy.EncodeResult(eliteSeed, plan.Spawn))
            : strategy.DecodeElite(null);

        int remaining = PopulationSize - 1;
        int fromElite = eliteSeed != null ? (int)(remaining * 0.10) : 0;
        int strongMutant = eliteSeed != null ? (int)(remaining * 0.40) : 0;
        int random = remaining - fromElite - strongMutant;

        int idx = 1;
        // 10%: exact elite copies
        for (int i = 0; i < fromElite; i++, idx++)
        {
            pop[idx] = strategy.DecodeElite(strategy.EncodeResult(eliteSeed!, plan.Spawn));
        }
        // 40%: elite + strong mutation
        for (int i = 0; i < strongMutant; i++, idx++)
        {
            var c = strategy.DecodeElite(strategy.EncodeResult(eliteSeed!, plan.Spawn));
            strategy.Mutate(c, 0.15, 1.5, rng);
            pop[idx] = c;
        }
        // 50%: fully random
        for (int i = 0; i < random; i++, idx++)
            pop[idx] = strategy.Sample(rng);

        return pop;
    }

    private FitnessResult[] EvaluateAll(
        IEvolvableStrategy strategy,
        EvaluablePlan plan,
        Chromosome[] population)
    {
        int workers = Math.Min(Environment.ProcessorCount, PopulationSize);
        var results = new FitnessResult[population.Length];
        var cache = new ConcurrentDictionary<string, FitnessResult>();

        Parallel.For(0, population.Length, new ParallelOptions { MaxDegreeOfParallelism = workers }, i =>
        {
            string fp = strategy.Fingerprint(population[i]);
            if (cache.TryGetValue(fp, out var cached))
            {
                results[i] = cached;
            }
            else
            {
                var r = strategy.Evaluate(plan, population[i]);
                cache[fp] = r;
                results[i] = r;
            }
        });

        return results;
    }

    private int TournamentSelect(FitnessResult[] fitnesses, Random rng)
    {
        int best = rng.Next(fitnesses.Length);
        for (int t = 1; t < TournamentSize; t++)
        {
            int challenger = rng.Next(fitnesses.Length);
            if (fitnesses[challenger].ScoreTotal > fitnesses[best].ScoreTotal)
                best = challenger;
        }
        return best;
    }

    private static int FindBest(FitnessResult[] fitnesses)
    {
        int best = 0;
        for (int i = 1; i < fitnesses.Length; i++)
            if (fitnesses[i].ScoreTotal > fitnesses[best].ScoreTotal)
                best = i;
        return best;
    }

    private static int[] TopNIndices(FitnessResult[] fitnesses, int n)
    {
        var indices = new List<int>(fitnesses.Length);
        for (int i = 0; i < fitnesses.Length; i++) indices.Add(i);
        indices.Sort((a, b) => fitnesses[b].ScoreTotal.CompareTo(fitnesses[a].ScoreTotal));
        return indices[..n].ToArray();
    }
}
