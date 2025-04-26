using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static class that handles mutation operations in the NEAT algorithm.
/// This includes changing weights, adding new connections, and splitting connections with new nodes.
/// </summary>
public static class Mutator
{
    // Random number generator used for selection and probability checks
    private static System.Random rng = new();

    // ===================== WEIGHT MUTATION =====================

    /// <summary>
    /// Randomly perturbs or reassigns the weights of all enabled connections in a genome.
    /// </summary>
    /// <param name="genome">The genome whose weights will be mutated.</param>
    /// <param name="perturbChance">Probability of slightly changing a weight instead of reassigning it.</param>
    /// <param name="stepSize">Amount by which to perturb weight values.</param>
    public static void MutateWeights(Genome genome, float perturbChance = 0.9f, float stepSize = 0.1f)
    {
        foreach (var conn in genome.Connections)
        {
            if (rng.NextDouble() < perturbChance)
            {
                // Slightly tweak the existing weight
                conn.Weight += UnityEngine.Random.Range(-stepSize, stepSize);
            }
            else
            {
                // Completely reassign weight to a new random value
                conn.Weight = UnityEngine.Random.Range(-1f, 1f);
            }
        }
    }

    // ===================== ADD CONNECTION MUTATION =====================

    /// <summary>
    /// Attempts to create a new connection between two unconnected nodes in the genome.
    /// Ensures connections are valid and not duplicated.
    /// </summary>
    public static void AddConnectionMutation(Genome genome)
    {
        var nodes = new List<NodeGene>(genome.Nodes.Values); // Get all nodes in the genome
        if (nodes.Count < 2) return; // Need at least 2 nodes to connect

        // Try up to 100 times to find a valid node pair
        for (int tries = 0; tries < 100; tries++)
        {
            var a = nodes[rng.Next(nodes.Count)];
            var b = nodes[rng.Next(nodes.Count)];

            // Prevent invalid connections:
            if (a.Type == NodeType.Output && b.Type == NodeType.Output) continue; // No output → output
            if (a.Type == NodeType.Input && b.Type == NodeType.Input) continue;   // No input → input
            if (a.Id == b.Id) continue;                                           // No self-connections

            // Decide direction: inputs always feed forward to outputs
            var inNode = a.Type == NodeType.Output ? b.Id : a.Id;
            var outNode = a.Type == NodeType.Output ? a.Id : b.Id;

            // Avoid duplicate connections
            if (genome.Connections.Exists(c => c.InNode == inNode && c.OutNode == outNode))
                continue;

            // Create a new connection with a random weight
            float weight = UnityEngine.Random.Range(-1f, 1f);
            genome.AddConnection(inNode, outNode, weight);
            break; // Stop after successfully adding one connection
        }
    }

    // ===================== ADD NODE MUTATION =====================

    /// <summary>
    /// Splits an existing connection by disabling it and inserting a new hidden node between.
    /// Adds two new connections to replace the original one.
    /// </summary>
    public static void AddNodeMutation(Genome genome)
    {
        if (genome.Connections.Count == 0) return; // No connections to split

        // Select a random connection to split
        var conn = genome.Connections[rng.Next(genome.Connections.Count)];
        if (!conn.Enabled) return; // Only split active connections

        // Disable the original connection
        conn.Enabled = false;

        // Create a new node with a new ID
        int newNodeId = genome.Nodes.Count + 1;
        genome.AddNode(newNodeId, NodeType.Hidden);

        // Add two new connections:
        // 1. From original input to new node (weight = 1)
        // 2. From new node to original output (same weight as original)
        genome.AddConnection(conn.InNode, newNodeId, 1f);
        genome.AddConnection(newNodeId, conn.OutNode, conn.Weight);
    }
}
