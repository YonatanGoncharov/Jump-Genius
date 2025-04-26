using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Represents a single vertical "stage" or floor in the game.
/// Each LevelStage groups together multiple platforms and links to its corresponding GameObject in the scene.
/// </summary>
public class LevelStage
{
    /// <summary>
    /// List of all platforms in this stage.
    /// Used for physics checks, interactions, or future optimizations like spatial partitioning.
    /// </summary>
    public List<Platform> platforms = new List<Platform>();

    /// <summary>
    /// The GameObject in the scene that represents this stage visually.
    /// Typically contains all platform children as children of this object.
    /// </summary>
    public GameObject stageObject;

    /// <summary>
    /// Adds a platform to this stage’s platform list.
    /// Called during stage loading in LevelManager.
    /// </summary>
    public void AddPlatform(Platform platform)
    {
        platforms.Add(platform);
    }
}
