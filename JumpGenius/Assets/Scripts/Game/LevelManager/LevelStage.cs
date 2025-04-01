using UnityEngine;
using System.Collections.Generic;

public class LevelStage
{
    public List<Platform> platforms = new List<Platform>();
    public GameObject stageObject;
    // public Transform stageTransform; // Reference to the Transform of the stage


    public void AddPlatform(Platform platform)
    {
        platforms.Add(platform);
    }
}
