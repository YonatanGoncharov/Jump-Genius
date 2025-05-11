using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(FitnessEvaluator))]
public class NEATManager : MonoBehaviour
{
    // ─── Inspector tunables ───
    public int populationSize = 150;
    public int inputCount = 5;   // 4 sensors + frontBlocked
    public int outputCount = 2;

    // ─── Runtime structures ───
    private Population population;
    private FitnessEvaluator evaluator;
    private readonly List<GameObject> agents = new();

    private int currentGeneration;
    public int CurrentGeneration => currentGeneration;

    // ★ track how high any agent has ever gotten ★
    public int HighestReachedStage { get; private set; }

    private Vector2 nextSpawnPoint;
    private Genome pioneerGenome;
    private bool checkpointQueued;

    public float LastBestFitness { get; private set; }
    public float AllTimeBestFitness { get; private set; }
    public List<float> bestFitnessHistory = new();

    // Save path
    private string bestPath;

    // Prefab & spawn assigned by GameManager
    [HideInInspector] public GameObject agentPrefab;
    [HideInInspector] public Transform spawnPoint;

    private void Awake()
    {
        bestPath = Path.Combine(Application.persistentDataPath, "BestGenome.json");
    }

    public void BeginTraining()
    {
        evaluator = GetComponent<FitnessEvaluator>();
        population = new Population(populationSize, inputCount, outputCount);

        currentGeneration = 1;
        HighestReachedStage = 0;
        checkpointQueued = false;
        LastBestFitness = 0f;
        AllTimeBestFitness = 0f;
        bestFitnessHistory.Clear();

        StartCoroutine(SpawnGeneration());
    }

    private IEnumerator SpawnGeneration()
    {
        ClearAgents();
        yield return null;

        Vector2 spawn2D = checkpointQueued ? nextSpawnPoint : (Vector2)spawnPoint.position;
        checkpointQueued = false;

        if (pioneerGenome != null)
        {
            population.Genomes[0] = pioneerGenome.Clone();
            pioneerGenome = null;
        }

        int agentLayer = LayerMask.NameToLayer("Agent");
        float z = spawnPoint.position.z;

        foreach (var g in population.Genomes)
        {
            var obj = Instantiate(agentPrefab, new Vector3(spawn2D.x, spawn2D.y, z), Quaternion.identity);
            obj.layer = agentLayer;
            foreach (Transform t in obj.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = agentLayer;
            obj.GetComponent<AgentController>().Init(g, GetCurrentMoveLimit());
            agents.Add(obj);
        }

        Debug.Log($"🔄 Generation {currentGeneration} spawned at {spawn2D}");
    }

    private void Update()
    {
        if (agents.Count == 0) return;
        if (agents.All(a => a == null || a.GetComponent<AgentController>().IsDead))
            Evolve();
    }

    private void Evolve()
    {
        population.EvaluateFitness(gen =>
        {
            var inst = agents.FirstOrDefault(a => a && a.GetComponent<AgentController>().Genome == gen);
            return inst ? evaluator.EvaluateAgent(inst.GetComponent<AgentController>()) : 0f;
        });

        float genBest = population.Genomes.Max(g => g.Fitness);
        LastBestFitness = genBest;
        bestFitnessHistory.Add(genBest);

        if (genBest > AllTimeBestFitness)
        {
            AllTimeBestFitness = genBest;
            var best = population.Genomes.First(g => g.Fitness == genBest);
            File.WriteAllText(bestPath, best.ToJson());
            Debug.Log($"💾 New best saved ({genBest:F2})");
        }

        population.Evolve();
        currentGeneration++;
        StartCoroutine(SpawnGeneration());
    }

    /// <summary>
    /// Called by AgentController when an individual agent reaches a new stage.
    /// </summary>
    public void OnStageChanged(int stage)
    {
        if (stage > HighestReachedStage)
            HighestReachedStage = stage;
    }

    /// <summary>
    /// Called by AgentController when checkpointing (to carry over the “pioneer”).
    /// </summary>
    public void RegisterCheckpoint(int stage, Vector2 spawnPos, Genome pioneer)
    {
        if (stage > HighestReachedStage)
            HighestReachedStage = stage;

        nextSpawnPoint = spawnPos;
        pioneerGenome = pioneer.Clone();
        checkpointQueued = true;
    }

    /// <summary>How many moves each agent gets this gen.</summary>
    public int GetCurrentMoveLimit()
    {
        int bonus = (CurrentGeneration / 10) * 5;  // +5 every 10 gens
        return 5 + bonus;
    }

    private bool AllAgentsDead() => agents.All(a => !a || a.GetComponent<AgentController>().IsDead);

    private void ClearAgents()
    {
        foreach (var a in agents) if (a) Destroy(a);
        agents.Clear();
    }

    public List<GameObject> GetAgents() => agents;
}
