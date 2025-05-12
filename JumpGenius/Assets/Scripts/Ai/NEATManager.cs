using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(FitnessEvaluator))]
public class NEATManager : MonoBehaviour
{
    // ─── Inspector tunables ───
    [Tooltip("The number of agents in each generation.")]
    public int populationSize = 150;
    [Tooltip("The number of input nodes for the neural network.")]
    public int inputCount = 6;  // 4 sensors + frontBlocked (example)
    [Tooltip("The number of output nodes for the neural network.")]
    public int outputCount = 2;

    // ─── Runtime structures ───
    private Population population;
    private FitnessEvaluator evaluator;
    private readonly List<GameObject> agents = new();

    private int currentGeneration;
    /// <summary>The current generation number of the training process.</summary>
    public int CurrentGeneration => currentGeneration;

    // ★ track how high any agent has ever gotten ★
    /// <summary>The highest level stage any agent has reached during training.</summary>
    public int HighestReachedStage { get; private set; }

    private Vector2 nextSpawnPoint;
    private Genome pioneerGenome;
    private bool checkpointQueued;
    private int moveBonusLevel = 0;

    /// <summary>The best fitness achieved in the most recent generation.</summary>
    public float LastBestFitness { get; private set; }
    /// <summary>The highest fitness ever achieved by any agent across all generations.</summary>
    public float AllTimeBestFitness { get; private set; }
    /// <summary>A history of the best fitness values for each generation.</summary>
    public List<float> bestFitnessHistory = new();

    // Save path for the best performing genome
    private string bestPath;

    // Prefab and spawn point assigned by GameManager
    [HideInInspector] public GameObject agentPrefab;
    [HideInInspector] public Transform spawnPoint;

    // ✅ Shared reference to the current agent's movement controller for external synchronization
    /// <summary>A static reference to the movement controller of the first agent spawned in the current generation.</summary>
    public static PlayerMovementController CurrentAgentMovement { get; private set; }

    private void Awake()
    {
        // Construct the file path to save the best genome
        bestPath = Path.Combine(Application.persistentDataPath, "BestGenome.json");
    }

    /// <summary>Initializes the NEAT training process.</summary>
    public void BeginTraining()
    {
        // Get the FitnessEvaluator component
        evaluator = GetComponent<FitnessEvaluator>();
        // Initialize the population with the specified size and network structure
        population = new Population(populationSize, inputCount, outputCount);

        // Reset training statistics
        currentGeneration = 1;
        HighestReachedStage = 0;
        checkpointQueued = false;
        LastBestFitness = 0f;
        AllTimeBestFitness = 0f;
        bestFitnessHistory.Clear();

        // Start the process of spawning the first generation of agents
        StartCoroutine(SpawnGeneration());
    }

    /// <summary>Spawns a new generation of agents based on the current population.</summary>
    private IEnumerator SpawnGeneration()
    {
        // Destroy all existing agents from the previous generation
        ClearAgents();
        yield return null; // Wait one frame to ensure agents are fully destroyed before spawning

        // Determine the spawn point for the new generation
        Vector2 spawn2D = checkpointQueued ? nextSpawnPoint : (Vector2)spawnPoint.position;
        checkpointQueued = false; // Reset the checkpoint flag

        // If a pioneer genome exists (from a checkpoint), use it for the first agent
        if (pioneerGenome != null)
        {
            population.Genomes[0] = pioneerGenome.Clone();
            pioneerGenome = null;
        }

        // Get the layer for the agents
        int agentLayer = LayerMask.NameToLayer("Agent");
        float z = spawnPoint.position.z;

        // Instantiate an agent for each genome in the population
        foreach (var g in population.Genomes)
        {
            var obj = Instantiate(agentPrefab, new Vector3(spawn2D.x, spawn2D.y, z), Quaternion.identity);
            obj.layer = agentLayer;
            // Ensure all child objects of the agent are also on the agent layer
            foreach (Transform t in obj.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = agentLayer;

            // Initialize the agent with its genome and the allowed number of moves
            obj.GetComponent<AgentController>().Init(g, GetCurrentMoveLimit());
            agents.Add(obj);

            // ✅ Register the movement controller of the first agent spawned
            if (CurrentAgentMovement == null)
                CurrentAgentMovement = obj.GetComponent<PlayerMovementController>();
        }

        Debug.Log($"🔄 Generation {currentGeneration} spawned at {spawn2D}");
    }

    private void Update()
    {
        // If there are no agents, do nothing
        if (agents.Count == 0) return;
        // If all agents are either null or dead, trigger the evolution process
        if (agents.All(a => a == null || a.GetComponent<AgentController>().IsDead))
            Evolve();
    }

    /// <summary>Evaluates the fitness of the current population and evolves to the next generation.</summary>
    private void Evolve()
    {
        // Evaluate the fitness of each genome based on the performance of its corresponding agent
        population.EvaluateFitness(gen =>
        {
            // Find the agent associated with the current genome
            var inst = agents.FirstOrDefault(a => a && a.GetComponent<AgentController>().Genome == gen);
            // If an agent is found, evaluate its fitness; otherwise, return 0
            return inst ? evaluator.EvaluateAgent(inst.GetComponent<AgentController>()) : 0f;
        });

        // Get the best fitness of the current generation
        float genBest = population.Genomes.Max(g => g.Fitness);
        LastBestFitness = genBest;
        // Add the best fitness to the history
        bestFitnessHistory.Add(genBest);

        // If the current generation's best fitness is better than the all-time best
        if (genBest > AllTimeBestFitness)
        {
            AllTimeBestFitness = genBest;
            // Find the genome with the best fitness in the current generation
            var best = population.Genomes.First(g => g.Fitness == genBest);
            // Save the best genome to a JSON file
            File.WriteAllText(bestPath, best.ToJson());
            Debug.Log($"💾 New best saved ({genBest:F2})");
        }

        // Perform the evolutionary steps to create the next generation
        population.Evolve();
        currentGeneration++;
        if (currentGeneration % 10 == 0)
            moveBonusLevel++;

        // Spawn the next generation of agents
        StartCoroutine(SpawnGeneration());
    }

    /// <summary>Updates the highest reached stage if a new high score is achieved.</summary>
    /// <param name="stage">The index of the stage reached by an agent.</param>
    public void OnStageChanged(int stage)
    {
        if (stage > HighestReachedStage)
        {
            HighestReachedStage = stage;
            moveBonusLevel = 0;
        }
            
    }

    /// <summary>Registers a checkpoint with a specific stage, spawn position, and a pioneer genome.</summary>
    /// <param name="stage">The stage index of the checkpoint.</param>
    /// <param name="spawnPos">The position where the next generation should spawn.</param>
    /// <param name="pioneer">The genome of a potentially successful agent to prioritize in the next generation.</param>
    public void RegisterCheckpoint(int stage, Vector2 spawnPos, Genome pioneer)
    {
        if (stage > HighestReachedStage)
            HighestReachedStage = stage;

        nextSpawnPoint = spawnPos;
        pioneerGenome = pioneer.Clone();
        checkpointQueued = true;
    }

    /// <summary>Calculates the current move limit for agents based on the generation number.</summary>
    /// <returns>The maximum number of moves allowed for agents in the current generation.</returns>
    public int GetCurrentMoveLimit()
    {
        return 5 + moveBonusLevel * 5;
    }

    /// <summary>Checks if all currently active agents are dead.</summary>
    /// <returns>True if all agents are dead or null, false otherwise.</returns>
    private bool AllAgentsDead() => agents.All(a => !a || a.GetComponent<AgentController>().IsDead);

    /// <summary>Destroys all currently active agents and clears the agent list.</summary>
    private void ClearAgents()
    {
        foreach (var a in agents) if (a) Destroy(a);
        agents.Clear();

        // ❗ Clear the static reference to the current agent's movement controller
        CurrentAgentMovement = null;
    }

    /// <summary>Returns the list of currently active agent GameObjects.</summary>
    /// <returns>A List of GameObject representing the active agents.</returns>
    public List<GameObject> GetAgents() => agents;
}