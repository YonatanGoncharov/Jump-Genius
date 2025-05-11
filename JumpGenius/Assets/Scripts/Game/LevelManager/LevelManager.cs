// Assets/Scripts/LevelManager.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central authority that organises LevelStage instances, keeps track
/// of the player’s current stage, and positions the main camera.
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager instance;

    public List<LevelStage> levels;
    private int currentStageIndex = 0;

    /// <summary>
    /// Exposes the current stage index for external scripts.
    /// </summary>
    public int CurrentStageIndex => currentStageIndex;

    public Camera mainCamera;
    public float cameraYOffset = 5f;
    private float startingYPosition;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadStagesFromScene();
    }

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        MoveCameraToStage();
        Debug.Log($"Loaded {levels.Count} levels");
    }

    private void Update()
    {
        if (GameManager.instance.IsTraining && GameManager.instance.neatManager != null)
        {
            int highestStage = 0;
            foreach (GameObject agent in GameManager.instance.neatManager.GetAgents())
            {
                if (agent == null) continue;
                var ctrl = agent.GetComponent<AgentController>();
                if (ctrl == null) continue;
                highestStage = Mathf.Max(highestStage, ctrl.BestStageReached);
            }
            MoveCameraToStage(highestStage);
        }
    }

    public LevelStage GetCurrentStage()
    {
        if (levels == null || levels.Count == 0 ||
            currentStageIndex < 0 || currentStageIndex >= levels.Count)
            return null;
        return levels[currentStageIndex];
    }

    public void GoToNextStage()
    {
        if (currentStageIndex < levels.Count - 1)
        {
            currentStageIndex++;
            MoveCameraToStage();
        }
    }

    public void GoToPreviousStage()
    {
        if (currentStageIndex > 0)
        {
            currentStageIndex--;
            MoveCameraToStage();
        }
    }

    public void LoadStagesFromScene()
    {
        var stageObjects = GameObject.FindGameObjectsWithTag("Stage");
        System.Array.Sort(stageObjects,
            (a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

        levels = new List<LevelStage>();
        foreach (var stageObj in stageObjects)
        {
            var newStage = new LevelStage
            {
                platforms = new List<Platform>(),
                stageObject = stageObj
            };
            foreach (Transform child in stageObj.transform)
            {
                if (!child.CompareTag("Platform")) continue;
                var platform = child.GetComponent<Platform>();
                if (platform != null)
                    newStage.platforms.Add(platform);
            }
            levels.Add(newStage);
        }
    }

    public void MoveCameraToStage(int targetStageIndex = -1)
    {
        if (targetStageIndex == -1)
            targetStageIndex = currentStageIndex;

        if (targetStageIndex < 0 || targetStageIndex >= levels.Count)
            return;

        currentStageIndex = targetStageIndex;
        float stageBottomY = levels[currentStageIndex]
                             .stageObject.transform.position.y + cameraYOffset;
        startingYPosition = stageBottomY;

        mainCamera.transform.position = new Vector3(
            mainCamera.transform.position.x,
            startingYPosition,
            mainCamera.transform.position.z
        );
    }

    public void UpdateCameraPosition(Vector3 playerPosition)
    {
        if (mainCamera == null) return;
        mainCamera.transform.position = new Vector3(
            mainCamera.transform.position.x,
            playerPosition.y,
            mainCamera.transform.position.z
        );
    }

    /// <summary>
    /// Returns the world-space Y of the active stage origin.
    /// </summary>
    public float GetCurrentLevelHeight()
        => levels[currentStageIndex].stageObject.transform.position.y;

    /// <summary>
    /// Returns the current stage index (for legacy callers).
    /// </summary>
    public int GetCurrentStageIndex(Transform agent)
        => currentStageIndex;

    /// <summary>
    /// Finds the stage index containing a given worldY.
    /// </summary>
    public int GetStageIndexByY(float worldY)
    {
        for (int i = 0; i < levels.Count; i++)
        {
            float y = levels[i].stageObject.transform.position.y;
            float nextY = (i == levels.Count - 1)
                ? float.PositiveInfinity
                : levels[i + 1].stageObject.transform.position.y;
            if (worldY >= y && worldY < nextY)
                return i;
        }
        return 0;
    }

    public void DebugPrintAllPlatformHeights()
    {
        Debug.Log("PLATFORM HEIGHTS BY STAGE:");
        for (int i = 0; i < levels.Count; i++)
        {
            Debug.Log($"Stage {i}:");
            foreach (var platform in levels[i].platforms)
            {
                Debug.Log($"   ↳ Platform Y: {platform.transform.position.y:F2}");
            }
        }
    }
    /// <summary>
    /// Return the world‐space position of the exit door (tagged "Door").
    /// </summary>
    public Vector2 GetExitPosition()
    {
        var door = GameObject.FindGameObjectWithTag("Door");
        return door ? (Vector2)door.transform.position : Vector2.zero;
    }
}
