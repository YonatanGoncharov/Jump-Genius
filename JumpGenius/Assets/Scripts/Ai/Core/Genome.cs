using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Represents the structure (DNA) of a neural network in NEAT.
/// Contains a list of nodes and connections that define how the network is built.
/// Also stores the fitness score assigned after evaluation.
/// </summary>
[System.Serializable]
public class Genome
{
    // ===================== FITNESS =====================

    /// <summary>
    /// The fitness score of this genome, assigned during evaluation.
    /// Higher fitness = better agent.
    /// </summary>
    public float Fitness { get; set; }

    // ===================== NETWORK STRUCTURE =====================


    [SerializeField] public List<NodeGene> nodeList = new();
    [SerializeField] public List<ConnectionGene> connectionList = new();


    [NonSerialized] public Dictionary<int, NodeGene> Nodes = new();
    public List<ConnectionGene> Connections => connectionList;


    // ===================== INNOVATION TRACKING =====================

    /// <summary>
    /// Simple counter used to assign unique innovation numbers to new connections.
    /// Ensures historical markings for crossover/mutation tracking.
    /// </summary>
    private static int innovationCounter = 0;



    public int OutputNodeCount => Nodes.Values.Count(n => n.Type == NodeType.Output);

    // ===================== CONSTRUCTORS =====================

    /// <summary>
    /// Default constructor. Creates an empty genome.
    /// </summary>
    public Genome() { }

    /// <summary>
    /// Deep copies a genome: duplicates all nodes and connections.
    /// </summary>
    public Genome Clone()
    {
        Genome clone = new();

        // Copy all node genes
        foreach (var node in Nodes.Values)
            clone.Nodes[node.Id] = new NodeGene(node.Id, node.Type);

        // Copy all connection genes
        foreach (var conn in Connections)
            clone.Connections.Add(conn.Clone());

        return clone;
    }

    // ===================== GENE MANIPULATION =====================

    /// <summary>
    /// Adds a new node to the genome if it doesn't already exist.
    /// </summary>
    public void AddNode(int id, NodeType type)
    {
        if (!Nodes.ContainsKey(id))
            Nodes[id] = new NodeGene(id, type);
    }

    /// <summary>
    /// Adds a new connection between two nodes with a specified weight.
    /// Assigns a new innovation number to this connection.
    /// </summary>
    public void AddConnection(int inNode, int outNode, float weight)
    {
        int innovation = innovationCounter++; // Assign unique innovation ID
        ConnectionGene connection = new(inNode, outNode, weight, true, innovation);
        Connections.Add(connection);
    }
    public string ToJson()
    {
        nodeList = Nodes.Values.ToList();      // copy nodes
        connectionList = new List<ConnectionGene>(Connections);  // ← copy edges
        return JsonUtility.ToJson(this, true);
    }


    public static Genome FromJson(string json)
    {
        Genome genome = JsonUtility.FromJson<Genome>(json);
        genome.Rebuild();
        return genome;
    }

    public void Rebuild()
    {
        Nodes = nodeList.ToDictionary(n => n.Id, n => n);
    }




}
