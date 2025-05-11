using System.IO;
using UnityEngine;

[RequireComponent(typeof(NEATManager))]
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Prefab & Spawn")]
    public GameObject agentPrefab;
    public Transform spawnPoint;

    [Header("UI Canvases")]
    [SerializeField] private Canvas mainMenuCanvas;
    [SerializeField] private Canvas aiStatsCanvas;

    public NEATManager neatManager { get; private set; }

    public bool IsTraining { get; private set; }

    private void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);

        neatManager = GetComponent<NEATManager>();
        neatManager.enabled = false;

        neatManager.agentPrefab = agentPrefab;
        neatManager.spawnPoint = spawnPoint;

        if (aiStatsCanvas) aiStatsCanvas.gameObject.SetActive(false);
    }

    /// <summary>Start NEAT training.</summary>
    public void UI_Train()
    {
        IsTraining = true;
        neatManager.enabled = true;
        neatManager.BeginTraining();

        if (mainMenuCanvas) mainMenuCanvas.gameObject.SetActive(false);
        if (aiStatsCanvas) aiStatsCanvas.gameObject.SetActive(true);

        Debug.Log("🔬 Training mode started");
    }

    /// <summary>Spawn the saved champion genome (if any).</summary>
    public void UI_Replay()
    {
        string path = Path.Combine(Application.persistentDataPath, "BestGenome.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning("▶️  No saved best genome to replay!");
            return;
        }

        IsTraining = false;
        neatManager.enabled = false;
        LoadBestAgent();

        if (mainMenuCanvas) mainMenuCanvas.gameObject.SetActive(false);
        if (aiStatsCanvas) aiStatsCanvas.gameObject.SetActive(false);

        Debug.Log("▶️  Replay-Best mode started");
    }

    private void LoadBestAgent()
    {
        string path = Path.Combine(Application.persistentDataPath, "BestGenome.json");
        if (!File.Exists(path)) return;

        Genome best = Genome.FromJson(File.ReadAllText(path));
        if (best.OutputNodeCount < 2)
        {
            Debug.LogError("Best genome invalid (needs ≥2 outputs).");
            return;
        }

        var obj = Instantiate(agentPrefab, spawnPoint.position, Quaternion.identity);
        var ctrl = obj.GetComponent<AgentController>();
        ctrl.Init(best, neatManager.GetCurrentMoveLimit());
        ctrl.SetReplayMode(true);
    }
    public void HideMenus()
    {
        if (mainMenuCanvas) mainMenuCanvas.gameObject.SetActive(false);
        if (aiStatsCanvas) aiStatsCanvas.gameObject.SetActive(false);
    }
}
