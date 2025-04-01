using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Util
{   
    public static float CompatibilityDistance(Genome genome1, Genome genome2)
    {
        return 0f;
    }
    public static int CountMatchingGenes(Genome genome1, Genome genome2)
    {
        int matchingGenes = 0;

        List<int> nodekeys1 = new List<int>(genome1.Connections.Keys);
        nodekeys1.Sort();
        List<int> nodekeys2 = new List<int>(genome2.Connections.Keys);
        nodekeys2.Sort();

        int highestInnovation1 = nodekeys1[nodekeys1.Count - 1];
        int highestInnovation2 = nodekeys2[nodekeys2.Count - 1];
        int indicies = Math.Max(highestInnovation1, highestInnovation2);

        for (int i = 0; i < indicies; i++)
        {
            Neuron nu1 = genome1.Neurons[i];
            Neuron nu2 = genome2.Neurons[i];
            if (nu1 != null && nu2 != null)
            {
                matchingGenes++;
            }
        }
        List<int> conKeys1 = new List<int>(genome1.Connections.Keys);
        List<int> conKeys2 = new List<int>(genome2.Connections.Keys);

        highestInnovation1 = conKeys1[conKeys1.Count - 1];
        highestInnovation2 = conKeys2[conKeys2.Count - 1];

        indicies = Math.Max(highestInnovation1, highestInnovation2);
        for (int i = 0; i < indicies; i++)
        {
            ConnectionGene con1 = genome1.Connections[i];
            ConnectionGene con2 = genome2.Connections[i];
            if (con1 != null && con2 != null)
            {
                matchingGenes++;
            }
        }
        return matchingGenes;
    }
}
