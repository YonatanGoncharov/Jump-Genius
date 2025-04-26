using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central authority that organises <see cref="LevelStage"/> instances, keeps track
/// of the player’s current stage, and positions the main camera so it always frames
/// the active stage during play or AI training.
///
/// It implements a lightweight singleton pattern via <see cref="instance"/> for
/// convenient global access. Stages are discovered automatically at runtime by
/// scanning the scene for objects tagged <c>"Stage"</c>.
/// </summary>
public class LevelManager : MonoBehaviour
{
    // ---------------------------------------------------------------------
    // Singleton
    // ---------------------------------------------------------------------

    /// <summary>
    /// Singleton reference so other scripts can access <see cref="LevelManager"/>
    /// without burying coupling in <c>FindObjectByType</c> calls.
    /// </summary>
    public static LevelManager instance;

    // ---------------------------------------------------------------------
    // Public state
    // ---------------------------------------------------------------------

    /// <summary>
    /// Ordered list of stages; index 0 is the lowest (starting) stage and the list
    /// is sorted ascending by world-space Y so comparisons are easy.
    /// </summary>
    public List<LevelStage> levels;

    /// <summary>
    /// Index of the stage the camera currently targets. Changed by
    /// <see cref="GoToNextStage"/>, <see cref="GoToPreviousStage"/> or during AI
    /// evaluation when agents climb.
    /// </summary>
    private int currentStageIndex = 0;

    /// <summary>
    /// Camera that the manager repositions. If left <c>null</c> the first enabled
    /// <see cref="Camera.main"/> is used during <see cref="Start"/>.
    /// </summary>
    public Camera mainCamera;

    /// <summary>
    /// Vertical offset applied so the camera is centred slightly above the bottom
    /// of a stage rather than directly on the stage’s origin.
    /// </summary>
    public float cameraYOffset = 5f;

    /// <summary>
    /// Cached Y-coordinate that the camera was last moved to. Stored so the value
    /// can be reused in frame-to-frame camera updates without recomputing.
    /// </summary>
    private float startingYPosition;

    // ---------------------------------------------------------------------
    // MonoBehaviour life-cycle
    // ---------------------------------------------------------------------

    private void Awake()
    {
        // Basic singleton guard: if another instance exists destroy this one.
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
        // Resolve the camera if the inspector reference is empty.
        if (mainCamera == null)
            mainCamera = Camera.main;

        MoveCameraToStage(); // Frame the starting stage.
        Debug.Log($"Loaded {levels.Count} levels");
    }

    private void Update()
    {
        // During NEAT training move the camera dynamically to the highest stage
        // any agent has reached so we can observe learning progress.
        if (GameManager.instance.IsTraining && GameManager.instance.neatManager != null)
        {
            int highestStage = 0;

            foreach (GameObject agent in GameManager.instance.neatManager.GetAgents())
            {
                if (agent == null) continue;

                AgentController controller = agent.GetComponent<AgentController>();
                if (controller == null) continue;

                highestStage = Mathf.Max(highestStage, controller.BestStageReached);
            }

            MoveCameraToStage(highestStage);
        }
    }

    // ---------------------------------------------------------------------
    // Stage helpers
    // ---------------------------------------------------------------------

    /// <summary>
    /// Returns the <see cref="LevelStage"/> object that represents the stage the
    /// camera currently targets, or <c>null</c> if indices are out of bounds.
    /// </summary>
    public LevelStage GetCurrentStage()
    {
        if (levels == null || levels.Count == 0 || currentStageIndex < 0 || currentStageIndex >= levels.Count)
            return null;

        return levels[currentStageIndex];
    }

    /// <summary>
    /// Advances to the next stage (if one exists) and repositions the camera.
    /// Intended mainly for debugging where manual stage navigation is useful.
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
    /// Moves back to the previous stage (if one exists) and repositions the camera.
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
    /// Populates <see cref="levels"/> by finding all objects tagged <c>"Stage"</c>,
    /// ordering them by Y position, then extracting child objects tagged
    /// <c>"Platform"</c> into each stage’s <c>platforms</c> list.
    /// </summary>
    public void LoadStagesFromScene()
    {
        // Find all stage roots.
        GameObject[] stageObjects = GameObject.FindGameObjectsWithTag("Stage");

        // Sort ascending by Y so index order matches vertical layout.
        System.Array.Sort(stageObjects, (a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

        levels = new List<LevelStage>();

        foreach (GameObject stageObj in stageObjects)
        {
            LevelStage newStage = new LevelStage
            {
                platforms = new List<Platform>(),
                stageObject = stageObj
            };

            // Collect all child platforms.
            foreach (Transform child in stageObj.transform)
            {
                if (!child.CompareTag("Platform")) continue;

                Platform platform = child.GetComponent<Platform>();
                if (platform != null)
                    newStage.platforms.Add(platform);
            }

            levels.Add(newStage);
        }
    }

    // ---------------------------------------------------------------------
    // Camera control
    // ---------------------------------------------------------------------

    /// <summary>
    /// Repositions the camera so its Y aligns with the bottom of the specified
    /// stage plus <see cref="cameraYOffset"/>.
    /// </summary>
    /// <param name="targetStageIndex">Index of the stage to frame. If -1 the
    /// current stage is used.</param>
    public void MoveCameraToStage(int targetStageIndex = -1)
    {
        if (targetStageIndex == -1)
            targetStageIndex = currentStageIndex;

        if (targetStageIndex < 0 || targetStageIndex >= levels.Count)
            return; // Index out of range—nothing to do.

        float stageBottomY = levels[targetStageIndex].stageObject.transform.position.y + cameraYOffset;
        startingYPosition = stageBottomY;

        Vector3 cameraPosition = new Vector3(mainCamera.transform.position.x,
                                             startingYPosition,
                                             mainCamera.transform.position.z);
        mainCamera.transform.position = cameraPosition;
    }

    /// <summary>
    /// Moves the camera vertically to follow <paramref name="playerPosition"/> while
    /// maintaining its current X/Z. Useful when the player climbs within a single
    /// stage.
    /// </summary>
    public void UpdateCameraPosition(Vector3 playerPosition)
    {
        if (mainCamera == null) return;

        Vector3 cameraPosition = new Vector3(mainCamera.transform.position.x,
                                             playerPosition.y,
                                             mainCamera.transform.position.z);
        mainCamera.transform.position = cameraPosition;
    }

    // ---------------------------------------------------------------------
    // Query helpers
    // ---------------------------------------------------------------------

    /// <summary>
    /// Returns the world-space Y coordinate of the active stage’s origin.
    /// </summary>
    public float GetCurrentLevelHeight() => levels[currentStageIndex].stageObject.transform.position.y;

    /// <summary>
    /// Returns the index of the stage the provided <paramref name="agent"/> belongs
    /// to. Currently just a passthrough to <see cref="currentStageIndex"/>, but left
    /// here for future expansion where agents might occupy different stages.
    /// </summary>
    public int GetCurrentStageIndex(Transform agent) => currentStageIndex;

    /// <summary>
    /// Finds the stage index whose vertical range contains <paramref name="worldY"/>.
    /// </summary>
    public int GetStageIndexByY(float worldY)
    {
        for (int i = 0; i < levels.Count; i++)
        {
            float stageY = levels[i].stageObject.transform.position.y;

            bool isLast = (i == levels.Count - 1);
            float nextY = isLast ? float.PositiveInfinity : levels[i + 1].stageObject.transform.position.y;

            if (worldY >= stageY && worldY < nextY)
                return i;
        }

        return 0; // Default to first stage if none matched (shouldn’t happen).
    }

    // ---------------------------------------------------------------------
    // Debug utilities
    // ---------------------------------------------------------------------

    /// <summary>
    /// Logs the Y coordinate of every platform in every stage. Handy for verifying
    /// scene setup in the editor.
    /// </summary>
    public void DebugPrintAllPlatformHeights()
    {
        Debug.Log("PLATFORM HEIGHTS BY STAGE:");

        for (int i = 0; i < levels.Count; i++)
        {
            LevelStage stage = levels[i];
            Debug.Log($"Stage {i}:");

            foreach (Platform platform in stage.platforms)
            {
                float height = platform.transform.position.y;
                Debug.Log($"   ↳ Platform Y: {height:F2}");
            }
        }
    }
}