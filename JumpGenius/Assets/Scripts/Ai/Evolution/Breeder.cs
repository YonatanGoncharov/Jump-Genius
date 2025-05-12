// Assets/Scripts/Ai/Evolution/Breeder.cs

using System;
using System.Collections.Generic;

/// <summary>
/// Provides static methods for selecting parent genomes and performing
/// crossover between them to produce offspring genomes in the NEAT algorithm.
/// </summary>
public static class Breeder
{
    private static readonly Random rng = new();

    // ──────────────────────────────────────────────────────────────
    //  PARENT SELECTION (roulette wheel, fitness-proportional)
    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Selects a parent genome from a given pool based on its fitness, using
    /// a roulette wheel selection mechanism. Genomes with higher fitness have
    /// a greater probability of being selected.
    /// </summary>
    /// <param name="pool">The list of genomes to select from.</param>
    /// <param name="totalFitness">The sum of the fitness values of all genomes in the pool.</param>
    /// <returns>The selected parent genome.</returns>
    public static Genome SelectParent(List<Genome> pool, float totalFitness)
    {
        // Generate a random "pick" value within the range of the total fitness.
        double pick = rng.NextDouble() * totalFitness;
        double cumulative = 0.0;

        // Iterate through the pool of genomes, accumulating their fitness.
        // The first genome whose cumulative fitness exceeds the pick value is selected.
        foreach (var g in pool)
        {
            cumulative += g.Fitness;
            if (cumulative >= pick) return g;
        }
        // Fallback: if no genome was selected (due to floating-point inaccuracies),
        // return the last genome in the pool.
        return pool[pool.Count - 1];
    }

    // ──────────────────────────────────────────────────────────────
    //  CROSSOVER  (NEAT innovation matching)
    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Performs crossover between two parent genomes (a and b) to create a child genome.
    /// This method implements the NEAT crossover mechanism, which aligns connection
    /// genes based on their innovation numbers. The child inherits genes from the
    /// fitter parent for matching genes, and randomly from either parent for disjoint
    /// or excess genes (though excess are typically only inherited from the fitter parent).
    /// </summary>
    /// <param name="a">The first parent genome.</param>
    /// <param name="b">The second parent genome.</param>
    /// <returns>The resulting child genome after crossover.</returns>
    public static Genome Crossover(Genome a, Genome b)
    {
        // Ensure that genome 'a' is the fitter parent. If 'b' is fitter, or if they have
        // equal fitness and a random chance occurs, swap them. This biases the child
        // to inherit more from the fitter parent.
        if (b.Fitness > a.Fitness || (a.Fitness == b.Fitness && rng.Next(2) == 0))
            (a, b) = (b, a);

        Genome child = new();

        // Copy all the nodes from both parents into the child genome. Note that if both
        // parents have a node with the same ID, it will only be added once due to the
        // dictionary behavior in Genome.AddNode().
        foreach (var n in a.Nodes.Values) child.AddNode(n.Id, n.Type);
        foreach (var n in b.Nodes.Values) child.AddNode(n.Id, n.Type);

        // Create a dictionary to store the connection genes from both parents, keyed by their
        // innovation numbers. This allows for efficient matching of corresponding connections.
        var byInnov = new Dictionary<int, (ConnectionGene fromA, ConnectionGene fromB)>();
        foreach (var cg in a.Connections)
            byInnov[cg.Innovation] = (cg, null); // Initialize with connection from 'a'

        foreach (var cg in b.Connections)
        {
            if (byInnov.TryGetValue(cg.Innovation, out var pair))
                byInnov[cg.Innovation] = (pair.fromA, cg); // Add matching connection from 'b'
            else
                byInnov[cg.Innovation] = (null, cg); // Connection only in 'b'
        }

        // Iterate through the matched connection genes and decide which ones to inherit
        // into the child genome.
        foreach (var pair in byInnov.Values)
        {
            ConnectionGene chosen;
            // If the innovation number exists in both parents (matching genes), randomly
            // choose the connection gene from either parent.
            if (pair.fromA != null && pair.fromB != null)
                chosen = rng.Next(2) == 0 ? pair.fromA.Clone() : pair.fromB.Clone();
            // If the innovation number only exists in the fitter parent ('a'), inherit it.
            // This is how excess and disjoint genes from the fitter parent are passed on.
            else if (pair.fromA != null)
                chosen = pair.fromA.Clone();
            // If the innovation number only exists in the less fit parent ('b'), we typically
            // don't inherit these excess/disjoint genes unless a specific strategy dictates otherwise.
            // In this implementation, we simply skip them.
            else
                continue; // Skip genes only present in the less fit parent ('b')

            child.Connections.Add(chosen);
        }

        // The child's Nodes dictionary is already populated correctly during the node copying phase.
        // Therefore, no explicit Rebuild() call is needed here.
        return child;
    }
}