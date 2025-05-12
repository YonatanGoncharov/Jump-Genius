using System.Collections.Generic;
using System;

/// <summary>
/// Represents a species within the NEAT algorithm. A species is a group
/// of genomes that are structurally similar to each other. The purpose
/// of speciation is to protect novel and potentially beneficial innovations
/// by reducing competition between significantly different structures, allowing
/// them enough time to develop and prove their fitness.
/// </summary>
public class Species
{
    /// <summary>
    /// The representative genome of this species. New genomes are compared
    /// to this representative to determine if they belong to this species.
    /// The representative is chosen randomly from the members at the beginning
    /// of each new generation.
    /// </summary>
    public Genome Representative { get; private set; }

    /// <summary>
    /// A list of genomes that are currently members of this species.
    /// </summary>
    public List<Genome> Members { get; } = new();

    /// <summary>
    /// The highest fitness achieved by any member of this species so far.
    /// This is used to track the progress of the species over generations.
    /// </summary>
    public float BestFitness { get; private set; }

    /// <summary>
    /// The number of generations that have passed since the best fitness
    /// of this species has improved. This is used to identify stagnant
    /// species that may be culled to make way for more promising ones.
    /// </summary>
    public int AgeWithoutImprovement { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Species"/> class with
    /// the first genome that belongs to it. This genome becomes the initial
    /// representative of the new species.
    /// </summary>
    /// <param name="first">The first genome to be added to this species.</param>
    public Species(Genome first)
    {
        Representative = first;
        Members.Add(first);
        BestFitness = first.Fitness;
        AgeWithoutImprovement = 0;
    }

    /// <summary>
    /// Attempts to add a given genome to this species. The genome is added
    /// if its compatibility distance (δ) to the species' representative
    /// is less than the specified <paramref name="deltaThreshold"/>.
    /// The compatibility distance is calculated based on the number of
    /// disjoint genes, excess genes, and the average weight difference of
    /// matching genes, weighted by the coefficients <paramref name="c1"/>,
    /// <paramref name="c2"/>, and <paramref name="c3"/> respectively.
    /// </summary>
    /// <param name="g">The genome to attempt to add to the species.</param>
    /// <param name="deltaThreshold">The maximum compatibility distance
    /// allowed for a genome to be considered part of this species.</param>
    /// <param name="c1">Weighting factor for the number of excess genes.</param>
    /// <param name="c2">Weighting factor for the number of disjoint genes.</param>
    /// <param name="c3">Weighting factor for the average weight difference
    /// of matching genes.</param>
    /// <returns><c>true</c> if the genome was added to the species;
    /// otherwise, <c>false</c>.</returns>
    public bool TryAdd(Genome g, float deltaThreshold,
                        float c1, float c2, float c3)
    {
        // Calculate the compatibility distance between the species representative and the given genome.
        float delta = Genome.Compatibility(Representative, g, c1, c2, c3);
        // If the compatibility distance is greater than the threshold, the genome is not compatible with this species.
        if (delta > deltaThreshold) return false;

        // If the genome is compatible, add it to the list of members.
        Members.Add(g);
        return true;
    }

    /// <summary>
    /// Prepares the species for the next generation. This involves selecting
    /// a new random representative from the current members and clearing the
    /// membership list, as the next generation will form a new set of members.
    /// </summary>
    /// <param name="rng">A random number generator used to select the new representative.</param>
    public void ResetForNextGen(Random rng)
    {
        // Select a new representative randomly from the current members of the species.
        Representative = Members[rng.Next(Members.Count)];
        // Clear the list of members to prepare for the next generation.
        Members.Clear();
    }

    /// <summary>
    /// Updates the stagnation counter for this species based on the best fitness
    /// achieved in the current generation. If the current generation's best
    /// fitness is better than the species' all-time best fitness, the stagnation
    /// counter is reset. Otherwise, the counter is incremented. This helps in
    /// identifying species that are no longer making progress.
    /// </summary>
    /// <param name="genBest">The best fitness achieved by any genome in this
    /// species during the current generation.</param>
    public void UpdateStagnation(float genBest)
    {
        // If the best fitness in the current generation is greater than the species' best fitness so far.
        if (genBest > BestFitness)
        {
            // Update the species' best fitness.
            BestFitness = genBest;
            // Reset the age without improvement, as the species has made progress.
            AgeWithoutImprovement = 0;
        }
        else
        {
            // If the best fitness has not improved, increment the age without improvement counter.
            AgeWithoutImprovement++;
        }
    }
}