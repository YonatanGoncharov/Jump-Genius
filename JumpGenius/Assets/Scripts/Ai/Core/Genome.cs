using System;
using System.Collections.Generic;
using System.Linq;         // ← NEW (needed for .ToDictionary / .Max)
using UnityEngine;

/// <summary>
/// Represents the genetic blueprint of a neural network within the NEAT algorithm.
/// A genome consists of a collection of node genes and connection genes,
/// along with a fitness score that reflects its performance in a given task.
/// This class also provides functionalities for cloning, structural manipulation,
/// serialization for saving, and calculating compatibility distance with other genomes.
/// </summary>
[Serializable]
public class Genome
{
    // ──────────────────────────────────────────────────────────────
    //  FITNESS
    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// The fitness score of this genome, typically assigned after the network
    /// represented by this genome has been evaluated in its environment or task.
    /// Higher fitness indicates better performance.
    /// </summary>
    public float Fitness { get; set; }             // set after evaluation

    // ──────────────────────────────────────────────────────────────
    //  STRUCTURE
    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// A serializable list of node genes that define the nodes present in the
    /// neural network encoded by this genome. This list is synchronized with
    /// the <see cref="Nodes"/> dictionary before serialization.
    /// </summary>
    [SerializeField] public List<NodeGene> nodeList = new();

    /// <summary>
    /// A serializable list of connection genes that define the connections
    /// between nodes in the neural network, including their weights and enabled status.
    /// This list is synchronized with the active connections before serialization.
    /// </summary>
    [SerializeField] public List<ConnectionGene> connectionList = new();

    /// <summary>
    /// A dictionary for fast lookup of node genes based on their unique ID.
    /// This allows for efficient access to node information during network evaluation
    /// and genetic operations. This field is not serialized and is rebuilt from
    /// <see cref="nodeList"/> after deserialization.
    /// </summary>
    [NonSerialized] public Dictionary<int, NodeGene> Nodes = new();

    /// <summary>
    /// Provides an active list of connection genes that are currently enabled
    /// in the neural network. This is a convenience property that returns the
    /// <see cref="connectionList"/>.
    /// </summary>
    public List<ConnectionGene> Connections => connectionList;

    /// <summary>
    /// A static counter used to assign a unique innovation number to each new
    /// connection gene created during the evolutionary process (e.g., through
    /// mutation or crossover). This helps in tracking the history of connections
    /// across generations and is crucial for determining compatibility between genomes.
    /// </summary>
    private static int innovationCounter = 0;

    /// <summary>
    /// Returns the number of output nodes in the neural network represented by this genome.
    /// This is determined by counting the node genes in the <see cref="Nodes"/> dictionary
    /// that have a <see cref="NodeType"/> of <c>Output</c>.
    /// </summary>
    public int OutputNodeCount => Nodes.Values.Count(n => n.Type == NodeType.Output);

    // ──────────────────────────────────────────────────────────────
    //  CONSTRUCTORS / CLONE
    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Initializes a new, empty <see cref="Genome"/> object.
    /// </summary>
    public Genome() { }

    /// <summary>
    /// Creates a deep copy (clone) of the current <see cref="Genome"/> object.
    /// This involves creating new instances of all node genes and connection
    /// genes, ensuring that the clone has the same structure and connection
    /// weights as the original but is an independent object.
    /// </summary>
    /// <returns>A new <see cref="Genome"/> object that is a clone of this instance.</returns>
    public Genome Clone()
    {
        Genome clone = new();

        // Clone each node gene and add it to the new genome's node dictionary.
        foreach (var node in Nodes.Values)
            clone.Nodes[node.Id] = new NodeGene(node.Id, node.Type);

        // Clone each connection gene and add it to the new genome's connection list.
        foreach (var conn in Connections)
            clone.Connections.Add(conn.Clone());

        return clone;
    }

    // ──────────────────────────────────────────────────────────────
    //  MANIPULATION
    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Adds a new node gene to this genome with the specified ID and type.
    /// If a node with the given ID already exists, this operation has no effect.
    /// </summary>
    /// <param name="id">The unique ID of the new node.</param>
    /// <param name="type">The type of the new node (e.g., Input, Output, Hidden, Bias).</param>
    public void AddNode(int id, NodeType type)
    {
        // Only add the node if it doesn't already exist in the Nodes dictionary.
        if (!Nodes.ContainsKey(id))
            Nodes[id] = new NodeGene(id, type);
    }

    /// <summary>
    /// Adds a new connection gene to this genome between the specified input and
    /// output nodes, with the given weight. The new connection is initially enabled
    /// and is assigned a unique innovation number.
    /// </summary>
    /// <param name="inNode">The ID of the input node of the new connection.</param>
    /// <param name="outNode">The ID of the output node of the new connection.</param>
    /// <param name="weight">The weight of the new connection.</param>
    public void AddConnection(int inNode, int outNode, float weight)
    {
        // Assign a new unique innovation number to this connection.
        int innovation = innovationCounter++;
        // Create a new connection gene with the given parameters and add it to the Connections list.
        Connections.Add(new ConnectionGene(inNode, outNode, weight, true, innovation));
    }

    // ──────────────────────────────────────────────────────────────
    //  SERIALISATION FOR “Save Champion”
    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Converts this <see cref="Genome"/> object into a JSON string.
    /// Before serialization, it synchronizes the <see cref="nodeList"/> with
    /// the <see cref="Nodes"/> dictionary and creates a new list from the
    /// active <see cref="Connections"/> to ensure all structural information is included.
    /// </summary>
    /// <returns>A JSON string representing this genome.</returns>
    public string ToJson()
    {
        // Synchronize the serializable nodeList with the current state of the Nodes dictionary.
        nodeList = Nodes.Values.ToList();
        // Create a new list from the active Connections for serialization.
        connectionList = new List<ConnectionGene>(Connections);
        // Use Unity's JsonUtility to convert the Genome object to a formatted JSON string.
        return JsonUtility.ToJson(this, true);
    }

    /// <summary>
    /// Creates a <see cref="Genome"/> object from a JSON string. After deserialization,
    /// it calls <see cref="Rebuild"/> to reconstruct the <see cref="Nodes"/> dictionary
    /// for efficient access.
    /// </summary>
    /// <param name="json">The JSON string representing a <see cref="Genome"/>.</param>
    /// <returns>A <see cref="Genome"/> object deserialized from the JSON string.</returns>
    public static Genome FromJson(string json)
    {
        // Use Unity's JsonUtility to deserialize the JSON string into a new Genome object.
        Genome g = JsonUtility.FromJson<Genome>(json);
        // Rebuild the Nodes dictionary from the deserialized nodeList.
        g.Rebuild();
        return g;
    }

    /// <summary>
    /// Rebuilds the <see cref="Nodes"/> dictionary from the <see cref="nodeList"/>.
    /// This method is typically called after deserialization to provide fast lookup
    /// of node genes by their ID.
    /// </summary>
    public void Rebuild() => Nodes = nodeList.ToDictionary(n => n.Id, n => n);

    /// <summary>
    /// Calculates the NEAT compatibility distance (δ) between this genome (genome A)
    /// and another genome (genome B). The compatibility distance is a measure of how
    /// structurally different two genomes are and is used for speciation.
    /// The formula for δ is: (c₁·E + c₂·D) / L + c₃·W̄, where:
    /// - E is the number of excess genes (genes present in one genome but beyond the last matching gene).
    /// - D is the number of disjoint genes (genes present in both genomes but with different innovation numbers).
    /// - L is the length of the longer genome (number of connections).
    /// - W̄ is the average weight difference of matching genes.
    /// - c₁, c₂, and c₃ are coefficients that adjust the importance of excess genes,
    ///   disjoint genes, and weight differences, respectively.
    /// </summary>
    /// <param name="a">The first genome to compare (this instance).</param>
    /// <param name="b">The second genome to compare.</param>
    /// <param name="c1">Coefficient for excess genes (default: 1.0f).</param>
    /// <param name="c2">Coefficient for disjoint genes (default: 1.0f).</param>
    /// <param name="c3">Coefficient for average weight difference (default: 0.4f).</param>
    /// <returns>The compatibility distance between the two genomes.</returns>
    public static float Compatibility(Genome a, Genome b,
                                        float c1 = 1f, float c2 = 1f, float c3 = 0.4f)
    {
        // Create dictionaries for quick lookup of connections by their innovation number for both genomes.
        var dictA = a.Connections.ToDictionary(c => c.Innovation);
        var dictB = b.Connections.ToDictionary(c => c.Innovation);

        // Find the highest innovation number in each genome to identify excess genes.
        int maxInA = dictA.Count > 0 ? dictA.Keys.Max() : 0;
        int maxInB = dictB.Count > 0 ? dictB.Keys.Max() : 0;
        int max = Math.Max(maxInA, maxInB);

        int excess = 0, disjoint = 0, matches = 0;
        float wDiffSum = 0f;

        // Iterate through the innovation numbers up to the maximum found in either genome.
        for (int i = 0; i <= max; i++)
        {
            // Check if a connection with the current innovation number exists in each genome.
            bool hasA = dictA.TryGetValue(i, out var ca);
            bool hasB = dictB.TryGetValue(i, out var cb);

            // If the innovation number is present in both genomes, it's a matching gene.
            if (hasA && hasB)
            {
                matches++;
                // Accumulate the absolute difference in weights for matching connections.
                wDiffSum += Mathf.Abs(ca.Weight - cb.Weight);
            }
            // If the innovation number is present in one genome but not the other, it's either disjoint or excess.
            else if (hasA || hasB)
            {
                // A gene is excess if its innovation number is greater than the highest innovation number of the *other* genome.
                bool isExcess = (i > maxInA) || (i > maxInB);
                // Otherwise, it's a disjoint gene (innovation number within the range of both genomes but not matching).
                if (isExcess) excess++; else disjoint++;
            }
        }

        // L is the number of connections in the larger genome.
        int L = Math.Max(dictA.Count, dictB.Count);
        // Avoid division by zero if both genomes have no connections.
        if (L == 0) L = 1;
        // Calculate the average weight difference of matching connections.
        float wBar = matches > 0 ? wDiffSum / matches : 0f;

        // Calculate and return the compatibility distance using the NEAT formula.
        return (c1 * excess + c2 * disjoint) / L + c3 * wBar;
    }
}