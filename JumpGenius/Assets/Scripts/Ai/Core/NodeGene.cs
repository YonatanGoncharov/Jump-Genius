using System;
using UnityEngine;

/// <summary>
/// Represents a single node (neuron) in a genome's neural network.
/// Each node has an ID, a type (Input, Hidden, Output, Bias), and a value that is calculated during evaluation.
/// </summary>
public enum NodeType
{
    Input,   // Receives data from the environment (e.g., velocity, wind)
    Hidden,  // Intermediate neuron used in more complex networks
    Output,  // Produces final decisions (e.g., moveX, jump)
    Bias     // Constant-value input node (usually always outputs 1)
}

/// <summary>
/// NodeGene holds the metadata for a single node in the genome.
/// During network evaluation, it temporarily stores the neuron's current value.
/// </summary>
[Serializable]
public class NodeGene
{
    // ========== IDENTITY ==========

    /// <summary>
    /// Unique identifier for this node within the genome.
    /// Used to connect it via ConnectionGenes.
    /// </summary>
    public int Id;

    /// <summary>
    /// Type of the node (Input, Hidden, Output, Bias).
    /// Determines how the node behaves in the network.
    /// </summary>
    public NodeType Type;

    // ========== EVALUATION STATE ==========

    /// <summary>
    /// Temporary value assigned during feed-forward evaluation.
    /// This is reset before every evaluation and updated based on incoming signals.
    /// </summary>
    public float Value;

    // ========== CONSTRUCTOR ==========

    /// <summary>
    /// Creates a new node with the given ID and type.
    /// </summary>
    public NodeGene(int id, NodeType type)
    {
        Id = id;
        Type = type;
        Value = 0f; // Initialized to 0 — gets filled during feedforward
    }
}
