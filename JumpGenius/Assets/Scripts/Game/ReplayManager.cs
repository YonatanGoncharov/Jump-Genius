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
        championsFolder = Path.Combine(Application.persistentDataPath, "HallOfFame");
    }

    private void Start()
    {
        // Use the new API to locate the GameManager if the singleton isn't set yet
        gm = GameManager.instance
             ?? Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
            Debug.LogError("GameManager not found in scene! Make sure it’s in the hierarchy.");
    }

    public void OpenReplayFile()
    {
        var paths = StandaloneFileBrowser.OpenFilePanel(
            "Select Champion JSON",
            championsFolder,
            "json",
            false
        );

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            LoadChampion(paths[0]);
    }

    public void ReplayBestChampion()
    {
        var all = Directory.GetFiles(championsFolder, "BEST_*.json");
        if (all.Length == 0)
        {
            Debug.LogWarning("No BEST_*.json found in HallOfFame folder.");
            return;
        }
        LoadChampion(all[0]);
    }

    private void LoadChampion(string fullPath)
    {
        gm.HideMenus(); 

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"File not found: {fullPath}");
            return;
        }

        string json = File.ReadAllText(fullPath);
        Genome g = Genome.FromJson(json);

        // Destroy any existing agents
        foreach (var obj in gm.neatManager.GetAgents())
            if (obj) Destroy(obj);

        // Spawn single replay agent
        Vector3 pos = gm.spawnPoint.position;
        GameObject agent = Instantiate(gm.agentPrefab, pos, Quaternion.identity);
        var ctrl = agent.GetComponent<AgentController>();
        ctrl.Init(g, gm.neatManager.GetCurrentMoveLimit());
        ctrl.SetReplayMode(true);

        Debug.Log($"▶️ Replaying champion from: {Path.GetFileName(fullPath)}");
    }


}
