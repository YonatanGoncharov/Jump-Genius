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

    // Best / checkpoint bookkeeping
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

    // ────────────────────────────────────────
    private void Awake()
    {
        bestPath = Path.Combine(Application.persistentDataPath, "BestGenome.json");
    }

    // ────────────────────────────────────────
    public void BeginTraining()
    {
        evaluator = GetComponent<FitnessEvaluator>();
        population = new Population(populationSize, inputCount, outputCount);

        currentGeneration = 1;
        HighestReachedStage = 0;
        checkpointQueued = false;
        LastBestFitness = 0;
        AllTimeBestFitness = 0;
        bestFitnessHistory.Clear();

        StartCoroutine(SpawnGeneration());
    }

    // ───── Spawn entire generation (all agents in one frame) ─────
    private IEnumerator SpawnGeneration()
    {
        ClearAgents();
        yield return null;

        Vector2 spawn2D = checkpointQueued ? nextSpawnPoint : (Vector2)spawnPoint.position;
        checkpointQueued = false;

        // pioneer genome …
        if (pioneerGenome != null)
        {
            population.Genomes[0] = pioneerGenome.Clone();
            pioneerGenome = null;
        }

        int agentLayer = LayerMask.NameToLayer("Agent");
        float z = spawnPoint.position.z;          // keep original Z

        foreach (var g in population.Genomes)
        {
            Vector3 pos3 = new Vector3(spawn2D.x, spawn2D.y, z);   // ← use correct z
            GameObject obj = Instantiate(agentPrefab, pos3, Quaternion.identity);

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
        if (AllAgentsDead()) Evolve();
    }

    // ───────── Evolution cycle ─────────
    private void Evolve()
    {
        population.EvaluateFitness(gen =>
        {
            var a = agents.FirstOrDefault(o => o && o.GetComponent<AgentController>().Genome == gen);
            return a ? evaluator.EvaluateAgent(a.GetComponent<AgentController>()) : 0f;
        });

        float genBest = population.Genomes.Max(g => g.Fitness);
        LastBestFitness = genBest;
        bestFitnessHistory.Add(genBest);

        if (genBest > AllTimeBestFitness)
        {
            AllTimeBestFitness = genBest;
            Genome best = population.Genomes.First(g => g.Fitness == genBest);
            File.WriteAllText(bestPath, best.ToJson());
            Debug.Log($"💾 Saved best genome ({genBest:F1})");
        }

        population.Evolve();
        currentGeneration++;
        StartCoroutine(SpawnGeneration());
    }

    // ───────── Checkpoint API (called by AgentController) ─────────
    public void RegisterCheckpoint(int stage, Vector2 spawnPos, Genome pioneer)
    {
        HighestReachedStage = stage;
        nextSpawnPoint = spawnPos;
        pioneerGenome = pioneer.Clone();
        checkpointQueued = true;
        Debug.Log($"🏁 Checkpoint stage {stage} at {spawnPos}");
    }

    // keep UI/other scripts informed
    public void OnStageChanged(int stage)
    {
        if (stage > HighestReachedStage) HighestReachedStage = stage;
    }

    // current move allowance per agent
    public int GetCurrentMoveLimit()
    {
        int bonus = (CurrentGeneration / 10) * 5;  // +5 every 10 generations
        return 5 + bonus;
    }

    // ───────── Helpers ─────────
    private bool AllAgentsDead() => agents.All(a => !a || a.GetComponent<AgentController>().IsDead);
    private void ClearAgents() { foreach (var a in agents) if (a) Destroy(a); agents.Clear(); }
    public List<GameObject> GetAgents() => agents;
}
