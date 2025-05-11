using System;
using System.Collections.Generic;
using System.Linq;          // ← NEW (needed for .ToDictionary / .Max)
using UnityEngine;

/// <summary>
/// The DNA of a NEAT network: node genes + connection genes + fitness.
/// </summary>
[Serializable]
public class Genome
{
    // ──────────────────────────────────────────────────────────────
    //  FITNESS
    // ──────────────────────────────────────────────────────────────
    public float Fitness { get; set; }                 // set after evaluation

    // ──────────────────────────────────────────────────────────────
    //  STRUCTURE
    // ──────────────────────────────────────────────────────────────
    [SerializeField] public List<NodeGene> nodeList = new();
    [SerializeField] public List<ConnectionGene> connectionList = new();

    /// <summary>Fast lookup: node ID → node gene.</summary>
    [NonSerialized] public Dictionary<int, NodeGene> Nodes = new();

    /// <summary>Active list of connections (edges).</summary>
    public List<ConnectionGene> Connections => connectionList;

    // innovation counter for brand-new connections
    private static int innovationCounter = 0;

    public int OutputNodeCount => Nodes.Values.Count(n => n.Type == NodeType.Output);

    // ──────────────────────────────────────────────────────────────
    //  CONSTRUCTORS / CLONE
    // ──────────────────────────────────────────────────────────────
    public Genome() { }

    public Genome Clone()
    {
        Genome clone = new();

        foreach (var node in Nodes.Values)
            clone.Nodes[node.Id] = new NodeGene(node.Id, node.Type);

        foreach (var conn in Connections)
            clone.Connections.Add(conn.Clone());

        return clone;
    }

    // ──────────────────────────────────────────────────────────────
    //  MANIPULATION
    // ──────────────────────────────────────────────────────────────
    public void AddNode(int id, NodeType type)
    {
        if (!Nodes.ContainsKey(id))
            Nodes[id] = new NodeGene(id, type);
    }

    public void AddConnection(int inNode, int outNode, float weight)
    {
        int innovation = innovationCounter++;
        Connections.Add(new ConnectionGene(inNode, outNode, weight, true, innovation));
    }

    // ──────────────────────────────────────────────────────────────
    //  SERIALISATION FOR “Save Champion”
    // ──────────────────────────────────────────────────────────────
    public string ToJson()
    {
        nodeList = Nodes.Values.ToList();               // sync
        connectionList = new List<ConnectionGene>(Connections);
        return JsonUtility.ToJson(this, true);
    }

    public static Genome FromJson(string json)
    {
        Genome g = JsonUtility.FromJson<Genome>(json);
        g.Rebuild();
        return g;
    }

    public void Rebuild() => Nodes = nodeList.ToDictionary(n => n.Id, n => n);

    // ──────────────────────────────────────────────────────────────
    //  ★ NEW ★  COMPATIBILITY DISTANCE  (for speciation)
    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// NEAT compatibility distance δ.  
    /// δ = (c₁·E + c₂·D) / L + c₃·W̄  
    /// E = excess genes, D = disjoint genes, L = max(|A|,|B|),  
    /// W̄ = average weight difference of matching genes.
    /// </summary>
    public static float Compatibility(Genome a, Genome b,
                                      float c1 = 1f, float c2 = 1f, float c3 = 0.4f)
    {
        var dictA = a.Connections.ToDictionary(c => c.Innovation);
        var dictB = b.Connections.ToDictionary(c => c.Innovation);

        int maxInA = dictA.Count > 0 ? dictA.Keys.Max() : 0;
        int maxInB = dictB.Count > 0 ? dictB.Keys.Max() : 0;
        int max = Math.Max(maxInA, maxInB);

        int excess = 0, disjoint = 0, matches = 0;
        float wDiffSum = 0f;

        for (int i = 0; i <= max; i++)
        {
            bool hasA = dictA.TryGetValue(i, out var ca);
            bool hasB = dictB.TryGetValue(i, out var cb);

            if (hasA && hasB)
            {
                matches++;
                wDiffSum += Mathf.Abs(ca.Weight - cb.Weight);
            }
            else if (hasA || hasB)
            {
                bool isExcess = (i > maxInA) || (i > maxInB);
                if (isExcess) excess++; else disjoint++;
            }
        }

        int L = Math.Max(dictA.Count, dictB.Count);
        if (L == 0) L = 1;                 // avoid /0
        float wBar = matches > 0 ? wDiffSum / matches : 0f;

        return (c1 * excess + c2 * disjoint) / L + c3 * wBar;
    }
}
