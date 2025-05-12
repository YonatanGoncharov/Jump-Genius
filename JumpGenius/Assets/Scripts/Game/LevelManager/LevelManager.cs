// Assets/Scripts/LevelManager.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the different stages of the level, tracks the current stage,
/// and controls the main camera's position to follow the active part of the level.
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager instance;

    [Tooltip("A list of LevelStage objects defining the level's structure.")]
    public List<LevelStage> levels;
    private int currentStageIndex = 0;

    /// <summary>
    /// Provides read-only access to the current stage index.
    /// </summary>
    public int CurrentStageIndex => currentStageIndex;

    [Tooltip("The main camera in the scene.")]
    public Camera mainCamera;
    [Tooltip("Vertical offset to apply to the camera's Y position relative to the stage.")]
    public float cameraYOffset = 5f;
    private float startingYPosition;

    private void Awake()
    {
        // Singleton pattern to ensure only one LevelManager exists
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        // Load the level stages by finding GameObjects tagged as "Stage" in the scene
        LoadStagesFromScene();
    }

    private void Start()
    {
        // Ensure the main camera is assigned
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Initially position the camera based on the starting stage
        MoveCameraToStage();
        Debug.Log($"Loaded {levels.Count} levels");
    }

    private void Update()
    {
        // During training, if GameManager and NEATManager are available,
        // move the camera to the highest stage reached by any agent.
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

    /// <summary>
    /// Returns the LevelStage object for the current stage index.
    /// Returns null if the level list is invalid or the index is out of bounds.
    /// </summary>
    public LevelStage GetCurrentStage()
    {
        if (levels == null || levels.Count == 0 ||
            currentStageIndex < 0 || currentStageIndex >= levels.Count)
            return null;
        return levels[currentStageIndex];
    }

    /// <summary>
    /// Advances to the next stage in the level sequence, if available, and moves the camera.
    /// </summary>
    public void GoToNextStage()
    {
        if (currentStageIndex < levels.Count - 1)
        {
            currentStageIndex++;
            MoveCameraToStage();
        }
    }

    /// <summary>
    /// Moves to the previous stage in the level sequence, if available, and moves the camera.
    /// </summary>
    public void GoToPreviousStage()
    {
        if (currentStageIndex > 0)
        {
            currentStageIndex--;
            MoveCameraToStage();
        }
    }

    /// <summary>
    /// Finds all GameObjects tagged as "Stage" in the scene, sorts them by their Y position,
    /// and creates LevelStage objects for each, including their child "Platform" objects.
    /// </summary>
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

    /// <summary>
    /// Moves the main camera to focus on the specified stage index.
    /// If no index is provided, it defaults to the current stage index.
    /// </summary>
    /// <param name="targetStageIndex">The index of the stage to move the camera to.</param>
    public void MoveCameraToStage(int targetStageIndex = -1)
    {
        if (targetStageIndex == -1)
            targetStageIndex = currentStageIndex;

        if (targetStageIndex < 0 || targetStageIndex >= levels.Count)
            return;

        currentStageIndex = targetStageIndex;
        // Calculate the desired Y position for the camera based on the stage's bottom and the Y offset
        float stageBottomY = levels[currentStageIndex]
                                    .stageObject.transform.position.y + cameraYOffset;
        startingYPosition = stageBottomY;

        // Update the camera's position, keeping its X and Z the same
        mainCamera.transform.position = new Vector3(
            mainCamera.transform.position.x,
            startingYPosition,
            mainCamera.transform.position.z
        );
    }

    /// <summary>
    /// Updates the camera's Y position to follow the player's (or agent's) Y position.
    /// </summary>
    /// <param name="playerPosition">The current world position of the player or agent.</param>
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
    /// Returns the world-space Y coordinate of the bottom of the currently active stage.
    /// </summary>
    /// <returns>The Y position of the current level's origin.</returns>
    public float GetCurrentLevelHeight()
        => levels[currentStageIndex].stageObject.transform.position.y;

    /// <summary>
    /// Returns the current stage index. Primarily for legacy compatibility.
    /// </summary>
    /// <param name="agent">The transform of the agent (not currently used in this implementation).</param>
    /// <returns>The index of the current level stage.</returns>
    public int GetCurrentStageIndex(Transform agent)
        => currentStageIndex;

    /// <summary>
    /// Determines the stage index that contains a given world Y coordinate.
    /// Iterates through the stages and checks if the Y coordinate falls within the vertical bounds of each stage.
    /// </summary>
    /// <param name="worldY">The world Y coordinate to check.</param>
    /// <returns>The index of the stage containing the world Y, or 0 if none is found.</returns>
    public int GetStageIndexByY(float worldY)
    {
        for (int i = 0; i < levels.Count; i++)
        {
            float y = levels[i].stageObject.transform.position.y;
            // Determine the Y coordinate of the next stage to define the upper bound
            float nextY = (i == levels.Count - 1)
                ? float.PositiveInfinity
                : levels[i + 1].stageObject.transform.position.y;
            // If the worldY is within the current stage's vertical range, return its index
            if (worldY >= y && worldY < nextY)
                return i;
        }
        return 0; // Default to the first stage if no match is found
    }

    /// <summary>
    /// Logs the Y position of all platforms within each level stage for debugging purposes.
    /// </summary>
    public void DebugPrintAllPlatformHeights()
    {
        Debug.Log("PLATFORM HEIGHTS BY STAGE:");
        for (int i = 0; i < levels.Count; i++)
        {
            Debug.Log($"Stage {i}:");
            foreach (var platform in levels[i].platforms)
            {
                Debug.Log($"    ↳ Platform Y: {platform.transform.position.y:F2}");
            }
        }
    }
    /// <summary>
    /// Returns the world-space position of the GameObject tagged as "Door".
    /// This is likely the exit point of the level.
    /// </summary>
    /// <returns>The Vector2 position of the exit door, or Vector2.zero if no door is found.</returns>
    public Vector2 GetExitPosition()
    {
        var door = GameObject.FindGameObjectWithTag("Door");
        return door ? (Vector2)door.transform.position : Vector2.zero;
    }
}