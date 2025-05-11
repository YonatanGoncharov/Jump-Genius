// Assets/Scripts/Ai/Evolution/Breeder.cs

using System;
using System.Collections.Generic;

/// <summary>
/// Handles parent selection and NEAT-style crossover.
/// </summary>
public static class Breeder
{
    private static readonly Random rng = new();

    // ──────────────────────────────────────────────────────────────
    //  PARENT SELECTION (roulette wheel, fitness-proportional)
    // ──────────────────────────────────────────────────────────────
    public static Genome SelectParent(List<Genome> pool, float totalFitness)
    {
        double pick = rng.NextDouble() * totalFitness;
        double cumulative = 0.0;

        foreach (var g in pool)
        {
            cumulative += g.Fitness;
            if (cumulative >= pick) return g;
        }
        return pool[pool.Count - 1]; // fallback
    }

    // ──────────────────────────────────────────────────────────────
    //  CROSSOVER  (NEAT innovation matching)
    // ──────────────────────────────────────────────────────────────
    public static Genome Crossover(Genome a, Genome b)
    {
        // Ensure ‘a’ is fitter (or random if equal)
        if (b.Fitness > a.Fitness || (a.Fitness == b.Fitness && rng.Next(2) == 0))
            (a, b) = (b, a);

        Genome child = new();

        // copy nodes
        foreach (var n in a.Nodes.Values) child.AddNode(n.Id, n.Type);
        foreach (var n in b.Nodes.Values) child.AddNode(n.Id, n.Type);

        // match connections by innovation
        var byInnov = new Dictionary<int, (ConnectionGene fromA, ConnectionGene fromB)>();
        foreach (var cg in a.Connections)
            byInnov[cg.Innovation] = (cg, null);
        foreach (var cg in b.Connections)
        {
            if (byInnov.TryGetValue(cg.Innovation, out var pair))
                byInnov[cg.Innovation] = (pair.fromA, cg);
            else
                byInnov[cg.Innovation] = (null, cg);
        }

        foreach (var pair in byInnov.Values)
        {
            ConnectionGene chosen;
            if (pair.fromA != null && pair.fromB != null)
                chosen = rng.Next(2) == 0 ? pair.fromA.Clone() : pair.fromB.Clone();
            else if (pair.fromA != null)
                chosen = pair.fromA.Clone();
            else
                continue;
            child.Connections.Add(chosen);
        }

        // child.Nodes already correct—no Rebuild call
        return child;
    }
}
