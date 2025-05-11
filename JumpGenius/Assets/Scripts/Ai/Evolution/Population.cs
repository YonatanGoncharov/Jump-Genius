using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

/// <summary>
/// Manages NEAT population: speciation, evolution, and Hall of Fame saving.
/// Now also saves the BEST overall champion separately.
/// </summary>
public class Population
{
    // ─── Public access ───
    public List<Genome> Genomes { get; private set; } = new();

    // ─── Configuration ───
    private readonly int inputCount;
    private readonly int outputCount;
    private readonly int populationSize;

    // Speciation
    private float deltaThreshold = 3.0f;
    private const float targetSpeciesCount = 10f;
    private const float deltaChangeSpeed = 0.1f;
    private const float minDelta = 1.5f;
    private const float maxDelta = 6.0f;
    private const float c1 = 1f, c2 = 1f, c3 = 0.4f;
    private const int stagnationLimit = 15;

    // Mutation Rates
    private float addConnProb = 0.5f;
    private float addNodeProb = 0.2f;
    private const float minConnProb = 0.2f, maxConnProb = 0.8f;
    private const float minNodeProb = 0.05f, maxNodeProb = 0.4f;

    // Internal
    private List<Species> species = new();
    private readonly System.Random rng = new();
    private float lastBestFitness = 0f;
    private float allTimeBestFitness = 0f;  // ← ADDED to track global best
    private int stagnationCounter = 0;
    private int generationCounter = 0;

    // Hall of Fame
    private readonly string hallOfFamePath;

    // ──────────────────────────────────────────────────────────────
    public Population(int populationSize, int inputCount, int outputCount)
    {
        this.populationSize = populationSize;
        this.inputCount = inputCount;
        this.outputCount = outputCount;

        InitializePopulation();

        hallOfFamePath = Path.Combine(Application.persistentDataPath, "HallOfFame");
        if (!Directory.Exists(hallOfFamePath))
            Directory.CreateDirectory(hallOfFamePath);
    }

    private void InitializePopulation()
    {
        Genomes.Clear();

        for (int i = 0; i < populationSize; i++)
        {
            var g = new Genome();
            for (int n = 0; n < inputCount; n++) g.AddNode(n, NodeType.Input);
            g.AddNode(inputCount, NodeType.Bias);
            for (int n = 0; n < outputCount; n++) g.AddNode(inputCount + 1 + n, NodeType.Output);

            Mutator.AddConnectionMutation(g);
            Mutator.AddConnectionMutation(g);
            Mutator.MutateWeights(g);

            Genomes.Add(g);
        }
    }

    public void EvaluateFitness(Func<Genome, float> fitnessFunc)
    {
        foreach (var g in Genomes)
            g.Fitness = fitnessFunc(g);
    }

    public void Evolve()
    {
        Speciate();
        ShareFitness();
        AdjustDeltaThreshold();

        CullStagnantSpecies();
        SortSpeciesMembers();
        AdjustMutationRates();

        var nextGen = BreedNextGeneration();

        Genomes = nextGen;
        generationCounter++;
    }

    // ──────────────────────────────────────────────────────────────
    private void Speciate()
    {
        foreach (var s in species) s.ResetForNextGen(rng);
        foreach (var g in Genomes)
        {
            bool placed = false;
            foreach (var s in species)
                if (s.TryAdd(g, deltaThreshold, c1, c2, c3)) { placed = true; break; }
            if (!placed) species.Add(new Species(g));
        }
        species.RemoveAll(s => s.Members.Count == 0);
    }

    private void ShareFitness()
    {
        foreach (var s in species)
            foreach (var g in s.Members)
                g.Fitness /= s.Members.Count;
    }

    private void AdjustDeltaThreshold()
    {
        if (species.Count < targetSpeciesCount) deltaThreshold -= deltaChangeSpeed;
        else if (species.Count > targetSpeciesCount) deltaThreshold += deltaChangeSpeed;

        deltaThreshold = Mathf.Clamp(deltaThreshold, minDelta, maxDelta);

        Debug.Log($"δ threshold adjusted: {deltaThreshold:F2}, species count: {species.Count}");
    }

    private void CullStagnantSpecies()
    {
        for (int i = 0; i < species.Count; i++)
        {
            var s = species[i];
            float best = s.Members.Max(m => m.Fitness);

            if (best > s.BestFitness)
            {
                SaveChampion(s.Members[0], i, generationCounter);
            }

            s.UpdateStagnation(best);

            if (s.AgeWithoutImprovement >= stagnationLimit)
                species.RemoveAt(i--);
        }
    }

    private void SortSpeciesMembers()
    {
        foreach (var s in species)
            s.Members.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));
    }

    private void AdjustMutationRates()
    {
        float currentBest = Genomes.Max(g => g.Fitness);
        float fitnessChange = currentBest - lastBestFitness;

        if (fitnessChange < 0.01f)
        {
            stagnationCounter++;
            if (stagnationCounter >= 5)
            {
                addConnProb += 0.05f;
                addNodeProb += 0.02f;
                stagnationCounter = 0;
            }
        }
        else
        {
            addConnProb -= 0.03f;
            addNodeProb -= 0.01f;
            stagnationCounter = 0;
        }

        addConnProb = Mathf.Clamp(addConnProb, minConnProb, maxConnProb);
        addNodeProb = Mathf.Clamp(addNodeProb, minNodeProb, maxNodeProb);

        lastBestFitness = currentBest;

        Debug.Log($"Mutation rates: addConn={addConnProb:F2}, addNode={addNodeProb:F2}");
    }

    private List<Genome> BreedNextGeneration()
    {
        List<Genome> nextGen = new();
        float totalAdjustedFitness = species.Sum(s => s.Members.Sum(m => m.Fitness));

        foreach (var s in species)
            nextGen.Add(s.Members[0].Clone());

        while (nextGen.Count < populationSize && species.Count > 0)
        {
            float pick = UnityEngine.Random.value * totalAdjustedFitness;
            float cumulativeFitness = 0f;
            Species chosen = species[0];

            foreach (var s in species)
            {
                cumulativeFitness += s.Members.Sum(m => m.Fitness);
                if (cumulativeFitness >= pick)
                {
                    chosen = s;
                    break;
                }
            }

            float poolFitness = chosen.Members.Sum(m => m.Fitness);
            Genome p1 = Breeder.SelectParent(chosen.Members, poolFitness);
            Genome p2 = Breeder.SelectParent(chosen.Members, poolFitness);

            Genome child = Breeder.Crossover(p1, p2);

            Mutator.MutateWeights(child);

            if (UnityEngine.Random.value < addConnProb)
                Mutator.AddConnectionMutation(child);

            if (UnityEngine.Random.value < addNodeProb)
                Mutator.AddNodeMutation(child);

            nextGen.Add(child);
        }

        while (nextGen.Count < populationSize)
            nextGen.Add(Genomes[UnityEngine.Random.Range(0, Genomes.Count)].Clone());

        return nextGen;
    }

    private void SaveChampion(Genome g, int speciesId, int generation)
    {
        string filename = $"Species_{speciesId}_Gen_{generation}.json";
        string fullPath = Path.Combine(hallOfFamePath, filename);
        File.WriteAllText(fullPath, g.ToJson());
        Debug.Log($"💾 Saved champion: {filename}");

        if (g.Fitness > allTimeBestFitness)
        {
            allTimeBestFitness = g.Fitness;
            string bestFilename = $"BEST_Species_{speciesId}_Gen_{generation}.json";
            string bestPath = Path.Combine(hallOfFamePath, bestFilename);
            File.WriteAllText(bestPath, g.ToJson());
            Debug.Log($"🏆 New BEST champion saved: {bestFilename}");
        }
    }
}
