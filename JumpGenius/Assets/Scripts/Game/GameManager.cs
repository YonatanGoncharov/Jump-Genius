using System.IO;
using UnityEngine;

[RequireComponent(typeof(NEATManager))]
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Prefab & Spawn")]
    [Tooltip("The prefab of the agent to be spawned during training and replay.")]
    public GameObject agentPrefab;
    [Tooltip("The transform representing the initial spawn point for agents.")]
    public Transform spawnPoint;

    [Header("UI Canvases")]
    [Tooltip("The Canvas object for the main menu UI.")]
    [SerializeField] private Canvas mainMenuCanvas;
    [Tooltip("The Canvas object for the AI statistics UI (e.g., graph).")]
    [SerializeField] private Canvas aiStatsCanvas;

    /// <summary>The NEATManager component responsible for handling the neural network evolution.</summary>
    public NEATManager neatManager { get; private set; }

    /// <summary>A flag indicating whether the game is currently in training mode.</summary>
    public bool IsTraining { get; private set; }

    private void Awake()
    {
        // Singleton pattern to ensure only one GameManager instance exists
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        // Prevent the GameManager from being destroyed when loading new scenes
        DontDestroyOnLoad(gameObject);

        // Get the NEATManager component attached to this GameObject
        neatManager = GetComponent<NEATManager>();
        // Initially disable the NEATManager until training is started
        neatManager.enabled = false;

        // Pass the agent prefab and spawn point to the NEATManager
        neatManager.agentPrefab = agentPrefab;
        neatManager.spawnPoint = spawnPoint;

        // Initially hide the AI statistics canvas
        if (aiStatsCanvas) aiStatsCanvas.gameObject.SetActive(false);
    }

    /// <summary>Starts the NEAT training process.</summary>
    public void UI_Train()
    {
        // Set the training flag to true
        IsTraining = true;
        // Enable the NEATManager component
        neatManager.enabled = true;
        // Begin the training process in the NEATManager
        neatManager.BeginTraining();

        // Hide the main menu UI
        if (mainMenuCanvas) mainMenuCanvas.gameObject.SetActive(false);
        // Show the AI statistics UI
        if (aiStatsCanvas) aiStatsCanvas.gameObject.SetActive(true);

        Debug.Log("Training mode started");
    }

    /// <summary>Spawns the agent with the best genome saved from previous training (if any).</summary>
    public void UI_Replay()
    {
        // Construct the path to the saved best genome file
        string path = Path.Combine(Application.persistentDataPath, "BestGenome.json");
        // Check if the file exists
        if (!File.Exists(path))
        {
            Debug.LogWarning("No saved best genome to replay!");
            return;
        }

        // Set the training flag to false
        IsTraining = false;
        // Disable the NEATManager as we are just replaying a saved agent
        neatManager.enabled = false;
        // Load and spawn the agent with the best genome
        LoadBestAgent();

        // Hide the main menu UI
        if (mainMenuCanvas) mainMenuCanvas.gameObject.SetActive(false);
        // Hide the AI statistics UI during replay
        if (aiStatsCanvas) aiStatsCanvas.gameObject.SetActive(false);

        Debug.Log("Replay-Best mode started");
    }

    /// <summary>Loads the best performing genome from file and spawns an agent with that genome.</summary>
    private void LoadBestAgent()
    {
        // Construct the path to the saved best genome file
        string path = Path.Combine(Application.persistentDataPath, "BestGenome.json");
        // Check if the file exists
        if (!File.Exists(path)) return;

        // Read the JSON content of the file and deserialize it into a Genome object
        Genome best = Genome.FromJson(File.ReadAllText(path));
        // Basic validation to ensure the loaded genome has at least 2 output nodes
        if (best.OutputNodeCount < 2)
        {
            Debug.LogError("Best genome invalid (needs ≥2 outputs).");
            return;
        }

        // Instantiate the agent prefab at the specified spawn point
        var obj = Instantiate(agentPrefab, spawnPoint.position, Quaternion.identity);
        // Get the AgentController component of the spawned agent
        var ctrl = obj.GetComponent<AgentController>();
        // Initialize the agent with the loaded best genome and the current move limit (for consistency)
        ctrl.Init(best, neatManager.GetCurrentMoveLimit());
        // Enable replay mode on the AgentController
        ctrl.SetReplayMode(true);
    }

    /// <summary>Hides both the main menu and AI statistics canvases.</summary>
    public void HideMenus()
    {
        if (mainMenuCanvas) mainMenuCanvas.gameObject.SetActive(false);
        if (aiStatsCanvas) aiStatsCanvas.gameObject.SetActive(false);
    }
}