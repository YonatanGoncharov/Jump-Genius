using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager instance;

    public List<LevelStage> levels; // List of stages
    private int currentStageIndex = 0; // Current stage index
    public Camera mainCamera; // Reference to the camera
    public float cameraYOffset = 5f; // To add some offset, if needed

    private float startingYPosition; // The Y position of the bottom of the camera

    void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Start()
    {
        LoadStagesFromScene();
        if (mainCamera == null)
        {
            mainCamera = Camera.main; // Automatically assign the main camera if not assigned
        }
        MoveCameraToStage(); // Move camera to the starting stage

        // Log the number of levels
        Debug.Log("Loaded " + levels.Count + " levels");
    }

    // Returns the current stage
    public LevelStage GetCurrentStage()
    {
        if (levels == null || levels.Count == 0 || currentStageIndex < 0 || currentStageIndex >= levels.Count)
            return null;

        return levels[currentStageIndex];
    }

    // Go to the next stage
    public void GoToNextStage()
      {
        if (currentStageIndex < levels.Count - 1)
        {
            currentStageIndex++;
            MoveCameraToStage(); // Move the camera when transitioning to the next stage
        }
    }

    // Go to the previous stage
    public void GoToPreviousStage()
    {
        if (currentStageIndex > 0)
        {
            currentStageIndex--;
            MoveCameraToStage(); // Move the camera when falling back to the previous stage
        }
    }

    // Loads all stages from the scene by finding the objects with tag "Stage"
    public void LoadStagesFromScene()
    {
        GameObject[] stageObjects = GameObject.FindGameObjectsWithTag("Stage");

        System.Array.Sort(stageObjects, (a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

        levels = new List<LevelStage>();

        foreach (GameObject stageObj in stageObjects)
        {
            LevelStage newStage = new LevelStage
            {
                platforms = new List<Platform>(),
                stageObject = stageObj // Store the reference
            };

            foreach (Transform child in stageObj.transform)
            {
                if (child.CompareTag("Platform"))
                {
                    Platform platform = child.GetComponent<Platform>();
                    if (platform != null)
                    {
                        newStage.platforms.Add(platform);
                    }
                }
            }

            levels.Add(newStage);
        }
    }


    // Moves the camera to the current stage while keeping the bottom of the camera aligned with the stage's bottom
    private void MoveCameraToStage()
    {
        LevelStage currentStage = GetCurrentStage();
        if (currentStage != null && mainCamera != null)
        {
            // Find the lowest Y position of the platforms in the current stage
            float stageBottomY = GetCurrentLevelHeight() + cameraYOffset;
            // Calculate the starting Y position for the bottom of the camera
            startingYPosition = stageBottomY;

            // Set the camera's position: align the bottom of the camera with the starting Y position of the stage
            Vector3 cameraPosition = new Vector3(mainCamera.transform.position.x, startingYPosition, mainCamera.transform.position.z);
            mainCamera.transform.position = cameraPosition;
        }
    }

    // Updates camera position as the player moves
    public void UpdateCameraPosition(Vector3 playerPosition)
    {
        if (mainCamera != null)
        {
            // Maintain the Y position of the camera based on the stage and player's movement
            Vector3 cameraPosition = new Vector3(mainCamera.transform.position.x, playerPosition.y, mainCamera.transform.position.z);
            mainCamera.transform.position = cameraPosition;
        }
    }

    // Returns the current level height based on the current stage index
    public float GetCurrentLevelHeight()
    {
        return currentStageIndex * mainCamera.orthographicSize * 2;
    }   
} 
