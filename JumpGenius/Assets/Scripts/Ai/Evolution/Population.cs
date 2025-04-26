using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The Population class manages a group of Genomes (neural networks)
/// It handles initialization, fitness evaluation, and evolution.
/// </summary>
public class Population
{
    // ===================== PUBLIC =====================

    /// <summary>
    /// The current list of Genomes in the population.
    /// Each genome represents the neural network for one agent.
    /// </summary>
    public List<Genome> Genomes { get; private set; } = new();

    // ===================== PRIVATE CONFIG =====================

    private int inputCount;       // Number of input neurons in each genome
    private int outputCount;      // Number of output neurons in each genome
    private int populationSize;   // Total number of agents in the population

    // ===================== CONSTRUCTOR =====================

    /// <summary>
    /// Creates a new Population with given input/output sizes and total agent count.
    /// </summary>
    public Population(int populationSize, int inputCount, int outputCount)
    {
        this.populationSize = populationSize;
        this.inputCount = inputCount;
        this.outputCount = outputCount;

        InitializePopulation(); // Create the first generation
    }

    // ===================== INITIALIZATION =====================

    /// <summary>
    /// Fills the population with randomly initialized Genomes.
    /// </summary>
    private void InitializePopulation()
    {
        Genomes.Clear(); // Start fresh

        for (int i = 0; i < populationSize; i++)
        {
            Genome genome = new Genome();

            // Add input nodes (IDs: 0 to inputCount - 1)
            for (int j = 0; j < inputCount; j++)
                genome.AddNode(j, NodeType.Input);

            // Add 1 bias node (ID: inputCount)
            genome.AddNode(inputCount, NodeType.Bias);

            // Add output nodes (IDs: inputCount + 1 to inputCount + outputCount)
            for (int j = 0; j < outputCount; j++)
                genome.AddNode(inputCount + 1 + j, NodeType.Output);

            // Random initial network connections
            Mutator.AddConnectionMutation(genome); // Connect random nodes
            Mutator.AddConnectionMutation(genome); // Add another connection
            Mutator.MutateWeights(genome);         // Tweak connection weights

            // Add the new genome to the population
            Genomes.Add(genome);
        }
    }

    // ===================== FITNESS EVALUATION =====================

    /// <summary>
    /// Calculates fitness for each genome by passing it to a fitness function.
    /// The fitnessFunction parameter is a lambda that returns a float.
    /// </summary>
    public void EvaluateFitness(Func<Genome, float> fitnessFunction)
    {
        foreach (var genome in Genomes)
        {
            genome.Fitness = fitnessFunction(genome); // Call evaluator (from NEATManager)
        }
    }

    // ===================== EVOLUTION =====================

    /// <summary>
    /// Selects the best genomes, clones and mutates them to form a new generation.
    /// </summary>
    public void Evolve()
    {
        // Sort genomes by fitness in descending order (best first)
        Genomes = Genomes.OrderByDescending(g => g.Fitness).ToList();

        int survivors = populationSize / 4; // Top 25% will be cloned
        List<Genome> newGenomes = new();

        // Keep top survivors unchanged (elitism)
        for (int i = 0; i < survivors; i++)
        {
            newGenomes.Add(Genomes[i].Clone());
        }

        // Fill the rest of the population by mutating children from survivors
        while (newGenomes.Count < populationSize)
        {
            // Select a random parent from the top survivors
            Genome parent = Genomes[UnityEngine.Random.Range(0, survivors)];
            Genome child = parent.Clone(); // Copy parent's genome

            // Mutate weights slightly
            Mutator.MutateWeights(child);

            // 50% chance to add a new connection
            if (UnityEngine.Random.value < 0.5f)
                Mutator.AddConnectionMutation(child);

            // 20% chance to split a connection and add a new node
            if (UnityEngine.Random.value < 0.2f)
                Mutator.AddNodeMutation(child);

            // Add the new child to the next generation
            newGenomes.Add(child);
        }

        // Replace current generation with the new one
        Genomes = newGenomes;
    }
}
