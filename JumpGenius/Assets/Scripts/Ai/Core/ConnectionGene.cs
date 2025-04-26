using UnityEngine;

/// <summary>
/// Represents a directional connection between two nodes in a NEAT network.
/// </summary>
[System.Serializable]                 //  ← ADD THIS LINE
public class ConnectionGene
{
    public int InNode;
    public int OutNode;
    public float Weight;
    public bool Enabled;
    public int Innovation;

    public ConnectionGene(int inNode, int outNode,
                          float weight, bool enabled, int innovation)
    {
        InNode = inNode;
        OutNode = outNode;
        Weight = weight;
        Enabled = enabled;
        Innovation = innovation;
    }

    public ConnectionGene Clone() =>
        new ConnectionGene(InNode, OutNode, Weight, Enabled, Innovation);
}
