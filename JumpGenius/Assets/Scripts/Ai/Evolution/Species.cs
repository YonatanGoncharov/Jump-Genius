using System.Collections.Generic;
using System;

/// <summary>
/// A NEAT species: protects structurally similar genomes so that
/// new innovations survive long enough to prove themselves.
/// </summary>
public class Species
{
    public Genome Representative { get; private set; }
    public List<Genome> Members { get; } = new();

    public float BestFitness { get; private set; }
    public int AgeWithoutImprovement { get; private set; }

    public Species(Genome first)
    {
        Representative = first;
        Members.Add(first);
        BestFitness = first.Fitness;
        AgeWithoutImprovement = 0;
    }

    /// <summary>Adds a genome if δ &lt; threshold.</summary>
    public bool TryAdd(Genome g, float deltaThreshold,
                       float c1, float c2, float c3)
    {
        float delta = Genome.Compatibility(Representative, g, c1, c2, c3);
        if (delta > deltaThreshold) return false;

        Members.Add(g);
        return true;
    }

    /// <summary>Pick a new representative and clear membership.</summary>
    public void ResetForNextGen(Random rng)
    {
        Representative = Members[rng.Next(Members.Count)];
        Members.Clear();
    }

    /// <summary>Track progress to cull stagnant species.</summary>
    public void UpdateStagnation(float genBest)
    {
        if (genBest > BestFitness)
        {
            BestFitness = genBest;
            AgeWithoutImprovement = 0;
        }
        else
            AgeWithoutImprovement++;
    }
}
