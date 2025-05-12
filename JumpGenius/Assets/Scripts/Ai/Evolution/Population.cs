using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

/// <summary>
/// Manages the NEAT (NeuroEvolution of Augmenting Topologies) population,
/// handling key evolutionary processes such as speciation, reproduction,
/// and the preservation of high-performing individuals in a Hall of Fame.
/// This class orchestrates the complete evolutionary cycle, including fitness
/// sharing within species and dynamic adjustments to mutation rates.
/// </summary>
public class Population
{
    /// <summary>
    /// The current list of genomes within the population. Each genome represents
    /// a neural network's blueprint.
    /// </summary>
    public List<Genome> Genomes { get; private set; } = new();

    private readonly int inputCount;
    private readonly int outputCount;
    private readonly int populationSize;

    // ───── Speciation Parameters ─────
    private float deltaThreshold = 3.0f;
    private const float targetSpeciesCount = 10f;
    private const float deltaChangeSpeed = 0.1f;
    private const float minDelta = 1.5f;
    private const float maxDelta = 6.0f;
    private const float c1 = 1f, c2 = 1f, c3 = 0.4f;
    private const int stagnationLimit = 15;

    // ───── Mutation Parameters ─────
    private float addConnProb = 0.5f;
    private float addNodeProb = 0.2f;
    private const float minConnProb = 0.2f, maxConnProb = 0.8f;
    private const float minNodeProb = 0.05f, maxNodeProb = 0.4f;

    private List<Species> species = new();
    private readonly System.Random rng = new();
    private float lastBestFitness = 0f;
    private float allTimeBestFitness = 0f;
    private int stagnationCounter = 0;
    private int generationCounter = 0;

    private readonly string hallOfFamePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="Population"/> class.
    /// This constructor sets up the population with a specified size,
    /// input node count, and output node count. It also initializes the
    /// genomes within the population and prepares the Hall of Fame directory.
    /// </summary>
    /// <param name="populationSize">The desired size of the population (number of genomes).</param>
    /// <param name="inputCount">The number of input nodes for the neural networks.</param>
    /// <param name="outputCount">The number of output nodes for the neural networks.</param>
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

    /// <summary>
    /// Initializes the population by creating the initial set of genomes.
    /// Each genome starts with a basic structure consisting of input nodes,
    /// a bias node, and output nodes, along with a few initial random connections.
    /// </summary>
    private void InitializePopulation()
    {
        Genomes.Clear();

        for (int i = 0; i < populationSize; i++)
        {
            var g = new Genome();

            // Create input nodes
            for (int n = 0; n < inputCount; n++)
                g.AddNode(n, NodeType.Input);

            // Add a bias node
            g.AddNode(inputCount, NodeType.Bias);

            // Create output nodes
            for (int n = 0; n < outputCount; n++)
                g.AddNode(inputCount + 1 + n, NodeType.Output);

            // Add initial random connections and mutate their weights
            Mutator.AddConnectionMutation(g);
            Mutator.AddConnectionMutation(g);
            Mutator.MutateWeights(g);

            Genomes.Add(g);
        }
    }

    /// <summary>
    /// Evaluates the fitness of each genome in the population using a provided
    /// fitness evaluation function. The resulting fitness score is stored within
    /// each <see cref="Genome"/> object.
    /// </summary>
    /// <param name="fitnessFunc">A function that takes a <see cref="Genome"/>
    /// as input and returns its fitness score (a float value).</param>
    public void EvaluateFitness(Func<Genome, float> fitnessFunc)
    {
        foreach (var g in Genomes)
        {
            // Assign fitness score to each genome using the provided function
            g.Fitness = fitnessFunc(g);
        }
    }

    /// <summary>
    /// Executes one generation of the evolutionary process. This includes
    /// speciation, fitness sharing, dynamic adjustment of the speciation
    /// threshold, culling of stagnant species, sorting of genomes within
    /// species, adjustment of mutation rates based on progress, and breeding
    /// the next generation of genomes.
    /// </summary>
    public void Evolve()
    {
        // Group genomes into species based on compatibility
        Speciate();

        // Apply fitness sharing to adjust fitness based on species size
        ShareFitness();

        // Dynamically adjust the threshold for species separation
        AdjustDeltaThreshold();

        // Remove species that have not shown improvement over a certain number of generations
        CullStagnantSpecies();

        // Sort genomes within each species by their adjusted fitness in descending order
        SortSpeciesMembers();

        // Adjust the probabilities of adding new connections and nodes based on recent fitness progress
        AdjustMutationRates();

        // Create the next generation of genomes through selection, crossover, and mutation
        var nextGen = BreedNextGeneration();
        Genomes = nextGen;
        generationCounter++;
    }

    /// <summary>
    /// Groups the current population of genomes into species based on their
    /// genetic compatibility. Compatibility is determined by comparing the
    /// structural differences between genomes using a defined distance metric
    /// and the current <see cref="deltaThreshold"/>.
    /// </summary>
    private void Speciate()
    {
        // Clear the members of each existing species to prepare for the new generation
        foreach (var s in species) s.ResetForNextGen(rng);

        // Iterate through each genome in the population and attempt to place it into a species
        foreach (var g in Genomes)
        {
            bool placed = false;
            foreach (var s in species)
            {
                // Check if the current genome is compatible with the representative genome of the species
                if (s.TryAdd(g, deltaThreshold, c1, c2, c3))
                {
                    placed = true;
                    break;
                }
            }
            // If the genome is not compatible with any existing species, create a new species with this genome as its representative
            if (!placed)
                species.Add(new Species(g));
        }

        // Remove any species that ended up with no members in the current generation
        species.RemoveAll(s => s.Members.Count == 0);
    }

    /// <summary>
    /// Implements fitness sharing, where the fitness of each genome is divided
    /// by the number of members in its species. This mechanism prevents any
    /// single species from dominating the population and encourages the exploration
    /// of diverse solutions.
    /// </summary>
    private void ShareFitness()
    {
        // Iterate through each species
        foreach (var s in species)
            // For each genome within the species, divide its fitness by the number of members in the species
            foreach (var g in s.Members)
                g.Fitness /= s.Members.Count;
    }

    /// <summary>
    /// Dynamically adjusts the <see cref="deltaThreshold"/>, which controls the
    /// level of compatibility required for two genomes to be considered part of
    /// the same species. The threshold is adjusted based on whether the current
    /// number of species is above or below the <see cref="targetSpeciesCount"/>.
    /// </summary>
    private void AdjustDeltaThreshold()
    {
        // If the number of species is less than the target, decrease the threshold to encourage more speciation
        if (species.Count < targetSpeciesCount) deltaThreshold -= deltaChangeSpeed;
        // If the number of species is greater than the target, increase the threshold to reduce the number of species
        else if (species.Count > targetSpeciesCount) deltaThreshold += deltaChangeSpeed;

        // Ensure the delta threshold stays within the predefined minimum and maximum bounds
        deltaThreshold = Mathf.Clamp(deltaThreshold, minDelta, maxDelta);
    }

    /// <summary>
    /// Identifies and potentially removes species that have not shown significant
    /// improvement in fitness over a specified number of generations
    /// (<see cref="stagnationLimit"/>). The champion (highest fitness genome)
    /// of each species is tracked for improvement.
    /// </summary>
    private void CullStagnantSpecies()
    {
        for (int i = 0; i < species.Count; i++)
        {
            var s = species[i];
            // Find the highest fitness among the members of the current species
            float best = s.Members.Max(m => m.Fitness);

            // If the current best fitness is an improvement over the species' previous best, save the champion
            if (best > s.BestFitness)
                SaveChampion(s.Members[0], i, generationCounter);

            // Update the stagnation counter for the species based on the current best fitness
            s.UpdateStagnation(best);

            // If the species has not improved for a prolonged period, remove it from the population
            if (s.AgeWithoutImprovement >= stagnationLimit)
                species.RemoveAt(i--); // Decrement i because the list size has changed
        }
    }

    /// <summary>
    /// Sorts the genomes within each species based on their adjusted fitness
    /// in descending order. This is crucial for selection during the breeding process,
    /// where fitter individuals are more likely to be chosen as parents.
    /// </summary>
    private void SortSpeciesMembers()
    {
        // Iterate through each species
        foreach (var s in species)
            // Sort the members of the species by fitness in descending order
            s.Members.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));
    }

    /// <summary>
    /// Adjusts the probabilities of adding new connections (<see cref="addConnProb"/>)
    /// and new nodes (<see cref="addNodeProb"/>) based on the recent progress
    /// in the population's fitness. If the fitness has stagnated, the mutation
    /// rates are slightly increased to encourage exploration. If there's improvement,
    /// the rates are slightly decreased to maintain stability.
    /// </summary>
    private void AdjustMutationRates()
    {
        // Get the highest fitness among all genomes in the current population
        float currentBest = Genomes.Max(g => g.Fitness);
        // Calculate the change in the best fitness since the last generation
        float fitnessChange = currentBest - lastBestFitness;

        // If the fitness improvement is below a threshold, consider it stagnation
        if (fitnessChange < 0.01f)
        {
            stagnationCounter++;
            // If stagnation persists for a certain number of generations, increase mutation probabilities
            if (stagnationCounter >= 5)
            {
                // Slightly increase the probability of adding a new connection
                addConnProb += 0.05f;
                // Slightly increase the probability of adding a new node
                addNodeProb += 0.02f;
                stagnationCounter = 0; // Reset the stagnation counter
            }
        }
        else
        {
            // If there's improvement in fitness, slightly reduce the mutation probabilities
            addConnProb -= 0.03f;
            addNodeProb -= 0.01f;
            stagnationCounter = 0; // Reset the stagnation counter
        }

        // Clamp the mutation probabilities within their allowed ranges
        addConnProb = Mathf.Clamp(addConnProb, minConnProb, maxConnProb);
        addNodeProb = Mathf.Clamp(addNodeProb, minNodeProb, maxNodeProb);
        lastBestFitness = currentBest; // Update the last best fitness
    }

    /// <summary>
    /// Creates the next generation of genomes by selecting parents from the current
    /// species based on their fitness, performing crossover to produce offspring,
    /// and then applying mutations to these offspring. Elitism is used to ensure
    /// that the best genome from each species survives to the next generation.
    /// </summary>
    /// <returns>A new list of <see cref="Genome"/> objects representing the next generation.</returns>
    private List<Genome> BreedNextGeneration()
    {
        List<Genome> nextGen = new();
        // Calculate the total adjusted fitness of all genomes across all species
        float totalAdjustedFitness = species.Sum(s => s.Members.Sum(m => m.Fitness));

        // Implement elitism: keep the highest-performing genome from each species
        foreach (var s in species)
            nextGen.Add(s.Members[0].Clone());

        // Fill the rest of the next generation through selection and reproduction
        while (nextGen.Count < populationSize && species.Count > 0)
        {
            // Select a species based on a probability proportional to its total adjusted fitness
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

            // Select two parents from the chosen species based on their fitness
            float poolFitness = chosen.Members.Sum(m => m.Fitness);
            Genome p1 = Breeder.SelectParent(chosen.Members, poolFitness);
            Genome p2 = Breeder.SelectParent(chosen.Members, poolFitness);
            // Perform crossover between the selected parents to create a child genome
            Genome child = Breeder.Crossover(p1, p2);

            // Apply mutations to the child genome
            Mutator.MutateWeights(child);
            if (UnityEngine.Random.value < addConnProb)
                Mutator.AddConnectionMutation(child);
            if (UnityEngine.Random.value < addNodeProb)
                Mutator.AddNodeMutation(child);

            nextGen.Add(child);
        }

        // If not enough children were generated (e.g., due to few species), clone random genomes to fill the population
        while (nextGen.Count < populationSize)
            nextGen.Add(Genomes[UnityEngine.Random.Range(0, Genomes.Count)].Clone());

        return nextGen;
    }

    /// <summary>
    /// Saves the champion genome of a species (the one with the highest fitness)
    /// to the Hall of Fame. Each saved champion is stored in a JSON file with a
    /// filename indicating its species ID and the generation number. If the
    /// champion's fitness surpasses the all-time best fitness, it is also saved
    /// as the new best champion.
    /// </summary>
    /// <param name="g">The champion <see cref="Genome"/> to be saved.</param>
    /// <param name="speciesId">The ID of the species the champion belongs to.</param>
    /// <param name="generation">The current generation number.</param>
    private void SaveChampion(Genome g, int speciesId, int generation)
    {
        string filename = $"Species_{speciesId}_Gen_{generation}.json";
        string fullPath = Path.Combine(hallOfFamePath, filename);
        File.WriteAllText(fullPath, g.ToJson());
        Debug.Log($"💾 Saved champion: {filename}");

        // Check if the current champion's fitness is better than the all-time best
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