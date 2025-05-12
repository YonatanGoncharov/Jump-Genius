using UnityEngine;

/// <summary>
/// Represents a directional connection between two nodes in a NEAT network.
/// This class stores information about the connection's endpoints (in and out nodes),
/// its weight, whether it is currently enabled, and its unique innovation number,
/// which is crucial for tracking genetic history and determining compatibility
/// between different network structures.
/// </summary>
[System.Serializable]      
public class ConnectionGene
{
    /// <summary>
    /// The ID of the node from which this connection originates (the input node).
    /// </summary>
    public int InNode;

    /// <summary>
    /// The ID of the node to which this connection leads (the output node).
    /// </summary>
    public int OutNode;

    /// <summary>
    /// The weight of this connection, which determines the strength and sign
    /// of the signal passed from the input node to the output node.
    /// </summary>
    public float Weight;

    /// <summary>
    /// A boolean flag indicating whether this connection is currently active
    /// in the network. Disabled connections are not considered during network evaluation.
    /// This allows for the possibility of deactivating connections during evolution.
    /// </summary>
    public bool Enabled;

    /// <summary>
    /// A unique identifier for this connection across the entire evolutionary process.
    /// Innovation numbers are assigned when new connections are created and help
    /// in identifying corresponding genes in different genomes during crossover and
    /// for calculating compatibility distance.
    /// </summary>
    public int Innovation;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionGene"/> class.
    /// </summary>
    /// <param name="inNode">The ID of the input node.</param>
    /// <param name="outNode">The ID of the output node.</param>
    /// <param name="weight">The weight of the connection.</param>
    /// <param name="enabled">Whether the connection is initially enabled.</param>
    /// <param name="innovation">The unique innovation number of the connection.</param>
    public ConnectionGene(int inNode, int outNode,
                            float weight, bool enabled, int innovation)
    {
        InNode = inNode;
        OutNode = outNode;
        Weight = weight;
        Enabled = enabled;
        Innovation = innovation;
    }

    /// <summary>
    /// Creates a deep copy (clone) of this <see cref="ConnectionGene"/> object.
    /// The cloned connection gene will have the same in node, out node, weight,
    /// enabled status, and innovation number as the original but will be a new,
    /// independent object.
    /// </summary>
    /// <returns>A new <see cref="ConnectionGene"/> object that is a clone of this instance.</returns>
    public ConnectionGene Clone() =>
        new ConnectionGene(InNode, OutNode, Weight, Enabled, Innovation);
}