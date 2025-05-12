using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A feed-forward neural network constructed from a Genome. This class is
/// responsible for evaluating the network by propagating input signals
/// through its connections and nodes, using topological sorting to ensure
/// that nodes are updated in the correct order.
/// </summary>
public class NeuralNetwork
{
    // ===================== STRUCTURES =====================

    private Dictionary<int, Node> nodes = new();       // Dictionary of all nodes, accessible by their unique ID.
    private List<Connection> connections = new();         // List of active connections (edges) between nodes.

    /// <summary>
    /// Constructor: Builds a neural network from a given Genome.
    /// This constructor takes a Genome, which represents the genetic blueprint
    /// of the network, and uses it to create the corresponding network structure
    /// consisting of nodes and connections.
    /// </summary>
    /// <param name="genome">The Genome from which to build the neural network.</param>
    public NeuralNetwork(Genome genome)
    {
        // === CREATE NODES ===
        // Iterate through the node genes in the genome and create corresponding Node objects.
        foreach (var nodeGene in genome.Nodes.Values)
        {
            nodes[nodeGene.Id] = new Node(nodeGene.Type);
        }

        // === CREATE CONNECTIONS ===
        // Iterate through the connection genes in the genome and create corresponding
        // Connection objects, but only for connections that are enabled.
        foreach (var connGene in genome.Connections)
        {
            if (connGene.Enabled)
            {
                // Only create connections for enabled connection genes.
                connections.Add(new Connection
                {
                    In = connGene.InNode,
                    Out = connGene.OutNode,
                    Weight = connGene.Weight
                });
            }
        }

        // === TOPOLOGICAL SORT ===
        // Perform a topological sort of the nodes to determine the correct order
        // in which they should be evaluated during the feed-forward process.
        TopologicalSort();
    }

    private List<int> sortedNodeIds = new(); // Ordered list of node IDs, representing the evaluation order.

    /// <summary>
    /// Performs a topological sort of the nodes in the network. This is crucial
    /// for ensuring that nodes are evaluated in the correct order, i.e., that
    /// no node's output is calculated before all of its inputs have been processed.
    /// This method uses a recursive approach to traverse the network and order the nodes.
    /// </summary>
    private void TopologicalSort()
    {
        var visited = new HashSet<int>(); // Keeps track of nodes that have already been visited.
        sortedNodeIds.Clear();            // Clear any previous sorting results.

        // Local recursive function to visit and order nodes.
        void Visit(int nodeId)
        {
            // If the node has already been visited, there's no need to process it again.
            if (visited.Contains(nodeId)) return;
            visited.Add(nodeId); // Mark the current node as visited.

            // Visit all nodes that this node outputs to (i.e., its children).
            foreach (var conn in connections)
            {
                if (conn.In == nodeId)
                    Visit(conn.Out); // Recursively visit the output node.
            }

            // Add the node to the sorted list *after* all its children have been visited.
            // This ensures that a node is added to the list only after all nodes
            // that provide input to it have been processed.
            sortedNodeIds.Add(nodeId);
        }

        // Start the topological sort by visiting all nodes in the network.
        foreach (var nodeId in nodes.Keys)
            Visit(nodeId);

        // Reverse the sorted list to obtain the correct input-to-output order.
        // The nodes are added in a reverse post-order during the recursion, so reversing
        // the list gives the correct topological order.
        sortedNodeIds.Reverse();
    }

    // ===================== EVALUATION =====================

    /// <summary>
    /// Feeds input values into the neural network and propagates them through
    /// the network's connections and nodes to produce the output values.
    /// This method implements the feed-forward process using the order of
    /// node evaluation determined by the topological sort.
    /// </summary>
    /// <param name="input">An array of float values representing the input to the network.</param>
    /// <returns>An array of float values representing the output of the network.</returns>
    public float[] FeedForward(float[] input)
    {
        int inputIndex = 0;

        // === STEP 1: Assign input values to the input nodes ===
        foreach (var node in nodes)
        {
            if (node.Value.Type == NodeType.Input)
            {
                // Assign the next input value from the input array to the current input node.
                node.Value.Value = input[inputIndex++];
            }
            else
            {
                // For non-input nodes (hidden, output, and bias), reset their values to zero.
                // This ensures that the node's value is calculated correctly for the current feed-forward pass.
                node.Value.Value = 0f;
            }
        }

        // === STEP 2: Process nodes in topological order ===
        // Iterate through the nodes in the order determined by the topological sort.
        foreach (var nodeId in sortedNodeIds)
        {
            var node = nodes[nodeId]; // Get the Node object for the current node ID.

            // For each incoming connection to the current node, sum the weighted input from the source node.
            foreach (var conn in connections)
            {
                if (conn.Out == nodeId)
                {
                    node.Value += nodes[conn.In].Value * conn.Weight;
                }
            }

            // Apply the activation function (tanh) to the node's value, but only for non-input nodes.
            // Input nodes typically don't have an activation function applied to them.
            if (node.Type != NodeType.Input)
            {
                node.Value = System.MathF.Tanh(node.Value); // Use hyperbolic tangent as the activation function.
            }
        }

        // === STEP 3: Collect the output values from the output nodes ===
        // Select the nodes that are output nodes, order them by their ID for consistency,
        // and extract their values into an array.
        var output = nodes
            .Where(kvp => kvp.Value.Type == NodeType.Output) // Filter for output nodes.
            .OrderBy(kvp => kvp.Key)                        // Order the output nodes by their IDs.
            .Select(kvp => kvp.Value.Value)                  // Select the output value of each output node.
            .ToArray();                                    // Convert the selected values to an array.

        return output; // Return the array of output values.
    }

    // ===================== INTERNAL NODE STRUCTURE =====================

    /// <summary>
    /// Represents a single node in the neural network. Each node has a type
    /// (input, hidden, output, or bias) and a value, which represents its
    /// current activation level.
    /// </summary>
    private class Node
    {
        public NodeType Type;    // The type of the node.
        public float Value;     // The current value (activation) of the node.

        /// <summary>
        /// Constructor for the Node class.
        /// </summary>
        /// <param name="type">The type of the node.</param>
        public Node(NodeType type)
        {
            Type = type;
            Value = 0f; // Initialize the node's value to zero.
        }
    }

    /// <summary>
    /// Represents a single connection (edge) between two nodes in the neural network.
    /// Each connection has a source node (In), a destination node (Out), and a weight.
    /// </summary>
    private class Connection
    {
        public int In;      // The ID of the source node.
        public int Out;     // The ID of the destination node.
        public float Weight;  // The weight of the connection.
    }
}