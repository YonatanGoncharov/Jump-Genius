using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A feed-forward neural network built from a Genome.
/// This class evaluates the network using topological sorting to ensure correct node update order.
/// </summary>
public class NeuralNetwork
{
    // ===================== STRUCTURES =====================

    private Dictionary<int, Node> nodes = new();       // Dictionary of all nodes (by ID)
    private List<Connection> connections = new();      // List of active connections (edges)

    /// <summary>
    /// Constructor: builds a neural network from a given Genome.
    /// </summary>
    public NeuralNetwork(Genome genome)
    {
        // === CREATE NODES ===
        foreach (var nodeGene in genome.Nodes.Values)
        {
            nodes[nodeGene.Id] = new Node(nodeGene.Type);
        }

        // === CREATE CONNECTIONS ===
        foreach (var connGene in genome.Connections)
        {
            if (connGene.Enabled)
            {
                // Only use enabled connections (disabled ones are skipped)
                connections.Add(new Connection
                {
                    In = connGene.InNode,
                    Out = connGene.OutNode,
                    Weight = connGene.Weight
                });
            }
        }

        // === TOPOLOGICAL SORT ===
        // Determine the order in which nodes must be evaluated (from inputs to outputs)
        TopologicalSort();
    }

    private List<int> sortedNodeIds = new(); // Ordered list of node IDs to process during evaluation

    /// <summary>
    /// Recursively performs topological sorting of nodes to respect data flow.
    /// Ensures no output is calculated before all its inputs are ready.
    /// </summary>
    private void TopologicalSort()
    {
        var visited = new HashSet<int>(); // Keeps track of visited nodes
        sortedNodeIds.Clear();

        // Local recursive function
        void Visit(int nodeId)
        {
            if (visited.Contains(nodeId)) return;
            visited.Add(nodeId);

            // Visit all nodes this node outputs to
            foreach (var conn in connections)
            {
                if (conn.In == nodeId)
                    Visit(conn.Out);
            }

            // Add node after its children have been visited
            sortedNodeIds.Add(nodeId);
        }

        // Visit all nodes in the network
        foreach (var nodeId in nodes.Keys)
            Visit(nodeId);

        // Reverse the list to get input → output order
        sortedNodeIds.Reverse();
    }

    // ===================== EVALUATION =====================

    /// <summary>
    /// Feeds input values through the network and returns the output values.
    /// </summary>
    public float[] FeedForward(float[] input)
    {
        int inputIndex = 0;

        // === STEP 1: Assign input values ===
        foreach (var node in nodes)
        {
            if (node.Value.Type == NodeType.Input)
            {
                // Assign next input value
                node.Value.Value = input[inputIndex++];
            }
            else
            {
                // Clear value for hidden/output/bias nodes
                node.Value.Value = 0f;
            }
        }

        // === STEP 2: Process nodes in topological order ===
        foreach (var nodeId in sortedNodeIds)
        {
            var node = nodes[nodeId];

            // For each incoming connection, sum the weighted input
            foreach (var conn in connections)
            {
                if (conn.Out == nodeId)
                {
                    node.Value += nodes[conn.In].Value * conn.Weight;
                }
            }

            // Apply activation function to all non-input nodes
            if (node.Type != NodeType.Input)
            {
                node.Value = System.MathF.Tanh(node.Value); // Use tanh as activation function
            }
        }

        // === STEP 3: Collect outputs ===
        var output = nodes
            .Where(kvp => kvp.Value.Type == NodeType.Output)     // Only get output nodes
            .OrderBy(kvp => kvp.Key)                             // Sort outputs by ID for consistency
            .Select(kvp => kvp.Value.Value)                      // Extract final value
            .ToArray();

        return output;
    }

    // ===================== INTERNAL NODE STRUCTURE =====================

    /// <summary>
    /// Represents a single node in the network (input, hidden, output, or bias).
    /// </summary>
    private class Node
    {
        public NodeType Type;    // What type of node it is
        public float Value;      // Current accumulated value (output after activation)

        public Node(NodeType type)
        {
            Type = type;
            Value = 0f;
        }
    }

    /// <summary>
    /// Represents a single directional connection (edge) between two nodes.
    /// </summary>
    private class Connection
    {
        public int In;           // Source node ID
        public int Out;          // Destination node ID
        public float Weight;     // Weight of the connection
    }
}
