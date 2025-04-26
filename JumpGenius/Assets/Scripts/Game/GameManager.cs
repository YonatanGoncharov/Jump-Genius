using System.IO;
using UnityEngine;

/// <summary>
/// Top-level coordinator.  Idle at launch; UI decides whether to
/// start NEAT training or replay the champion genome.
/// </summary>
[RequireComponent(typeof(NEATManager))]
public class GameManager : MonoBehaviour
{
    // ───── Singleton ─────
    public static GameManager instance;

    // ───── Inspector links ─────
    [Header("Prefab & Spawn")]
    public GameObject agentPrefab;      // AI agent prefab
    public Transform spawnPoint;       // where agents appear

    [Header("UI Canvases")]
    [SerializeField] private Canvas mainMenuCanvas;   // drag MainMenuCanvas
    [SerializeField] private Canvas aiStatsCanvas;    // drag AiStats canvas

    // ───── Runtime refs ─────
    public NEATManager neatManager { get; private set; }

    /// <summary>true while NEAT training is running</summary>
    public bool IsTraining { get; private set; }

    // ──────────────────────────────────────────
    private void Awake()
    {
        // singleton guard
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);

        // cache NEATManager & keep it disabled until user clicks
        neatManager = GetComponent<NEATManager>();
        neatManager.enabled = false;

        // pass prefab & spawn to manager
        neatManager.agentPrefab = agentPrefab;
        neatManager.spawnPoint = spawnPoint;

        // ensure AiStats starts hidden (will be shown when Train begins)
        if (aiStatsCanvas) aiStatsCanvas.gameObject.SetActive(false);
    }

    // ──────────────────────────────────────────
    //  UI Button hooks (called from MenuUI)
    // ──────────────────────────────────────────

    /// <summary>Start NEAT training from the UI.</summary>
    public void UI_Train()
    {
        IsTraining = true;
        neatManager.enabled = true;
        neatManager.BeginTraining();

        if (mainMenuCanvas) mainMenuCanvas.gameObject.SetActive(false);
        if (aiStatsCanvas) aiStatsCanvas.gameObject.SetActive(true);

        Debug.Log("🔬 Training mode started");
    }

    /// <summary>Spawn the saved champion genome from the UI.</summary>
    public void UI_Replay()
    {
        IsTraining = false;
        neatManager.enabled = false;
        LoadBestAgent();

        if (mainMenuCanvas) mainMenuCanvas.gameObject.SetActive(false);
        if (aiStatsCanvas) aiStatsCanvas.gameObject.SetActive(false);

        Debug.Log("▶️  Replay-Best mode started");
    }

    // ──────────────────────────────────────────
    //  Champion-loading helper
    // ──────────────────────────────────────────
    private void LoadBestAgent()
    {
        string path = Path.Combine(Application.persistentDataPath, "BestGenome.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning($"No saved best genome at {path}");
            return;
        }

        Genome best = Genome.FromJson(File.ReadAllText(path));
        if (best.OutputNodeCount < 2)
        {
            Debug.LogError("Best genome invalid (needs ≥2 outputs).");
            return;
        }

        GameObject obj = Instantiate(agentPrefab, spawnPoint.position, Quaternion.identity);
        var ctrl = obj.GetComponent<AgentController>();

        ctrl.Init(best, neatManager.GetCurrentMoveLimit());
        ctrl.SetReplayMode(true);               // invincible champion
    }
}
