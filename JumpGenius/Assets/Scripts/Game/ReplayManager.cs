// Assets/Scripts/Game/ReplayManager.cs

using System.IO;
using UnityEngine;
using SFB;  // StandaloneFileBrowser

public class ReplayManager : MonoBehaviour
{
    private string championsFolder;
    private GameManager gm;


    private void Awake()
    {
        // Define the folder where champion genomes are saved
        championsFolder = Path.Combine(Application.persistentDataPath, "HallOfFame");
    }

    private void Start()
    {
        // Find the GameManager instance in the scene, using the singleton if available,
        // or a direct search if the singleton hasn't been initialized yet.
        gm = GameManager.instance
            ?? Object.FindFirstObjectByType<GameManager>();
        // Log an error if the GameManager couldn't be found
        if (gm == null)
            Debug.LogError("GameManager not found in scene! Make sure it’s in the hierarchy.");
    }

    /// <summary>
    /// Opens a file browser dialog to allow the user to select a champion genome JSON file for replay.
    /// </summary>
    public void OpenReplayFile()
    {
        // Open a file browser panel with specified title, default directory, file extension filter, and single file selection
        var paths = StandaloneFileBrowser.OpenFilePanel(
            "Select Champion JSON",
            championsFolder,
            "json",
            false
        );

        // If a file path was selected and it's not empty, load the champion from that file
        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            LoadChampion(paths[0]);
    }

    /// <summary>
    /// Attempts to replay the most recently saved "BEST_*.json" champion genome from the HallOfFame folder.
    /// </summary>
    public void ReplayBestChampion()
    {
        // Get all files in the champions folder that match the "BEST_*.json" pattern
        var all = Directory.GetFiles(championsFolder, "BEST_*.json");
        // If no such files are found, log a warning and exit
        if (all.Length == 0)
        {
            Debug.LogWarning("No BEST_*.json found in HallOfFame folder.");
            return;
        }
        // Load the champion from the first "BEST_*.json" file found (assuming it's the latest)
        LoadChampion(all[0]);
    }

    /// <summary>
    /// Loads a champion genome from the specified JSON file path and spawns an agent to replay its actions.
    /// </summary>
    /// <param name="fullPath">The full path to the champion genome JSON file.</param>
    private void LoadChampion(string fullPath)
    {
        // Hide any active menus before starting the replay
        gm.HideMenus();

        // Check if the specified file exists
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"File not found: {fullPath}");
            return;
        }

        // Read the JSON content from the file
        string json = File.ReadAllText(fullPath);
        // Deserialize the JSON into a Genome object
        Genome g = Genome.FromJson(json);

        // Destroy any existing agents currently in the scene
        foreach (var obj in gm.neatManager.GetAgents())
            if (obj) Destroy(obj);

        // Spawn a single agent at the defined spawn point
        Vector3 pos = gm.spawnPoint.position;
        GameObject agent = Instantiate(gm.agentPrefab, pos, Quaternion.identity);
        // Get the AgentController component of the spawned agent
        var ctrl = agent.GetComponent<AgentController>();
        // Initialize the agent with the loaded genome and the current move limit
        ctrl.Init(g, gm.neatManager.GetCurrentMoveLimit());
        // Set the agent to replay mode, disabling AI control
        ctrl.SetReplayMode(true);

        Debug.Log($"Replaying champion from: {Path.GetFileName(fullPath)}");
    }
}