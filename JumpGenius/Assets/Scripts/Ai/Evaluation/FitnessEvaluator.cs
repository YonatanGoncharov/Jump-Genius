using UnityEngine;

/// <summary>
/// Computes the fitness score for an agent based on height, stage, exploration, and penalties.
/// </summary>
public class FitnessEvaluator : MonoBehaviour
{
    [Header("Rewards")]
    public float stageReward = 300f;
    public float platformTopReward = 100f;
    public float verticalGainReward = 20f;
    public float airTimeReward = 0.5f;

    [Header("Penalties")]
    public float fallPenalty = 100f;
    public float idlePenalty = 100f;
    public float sideClingPenalty = 50f;
    public float movePenalty = 8f;

    [Header("Efficiency")]
    public float efficiencyBonus = 20f;

    [Header("Overall Scale"), Range(0f, 1f)]
    public float scoreScale = 0.001f;

    /// <summary>
    /// Called by NEATManager to evaluate a single agent's fitness.
    /// </summary>
    public float EvaluateAgent(AgentController a)
    {
        float f = 0f;

        // 1) Reward based on standing platform height
        f += a.BestPlatformHeight * platformTopReward;

        // 2) Reward stage progress
        f += a.BestStageReached * stageReward;

        // 3) Reward overall Y-distance gained from spawn
        f += a.BestYDistance * verticalGainReward;

        // 4) Reward exploration (more platforms visited)
        f += a.PlatformsVisitedCount * 5f;

        // 5) Reward time in air (encourages jumping)
        f += a.AirTime * airTimeReward;

        // 6) Efficiency bonuses
        if (a.MoveCount > 0)
            f += efficiencyBonus / a.MoveCount;
        f -= a.MoveCount * movePenalty;

        // 7) Penalties
        f -= a.Falls * fallPenalty;
        f -= a.GroundIdleTime * idlePenalty;
        f -= a.SideClingTime * sideClingPenalty;

        // 8) Bonus for finishing
        if (a.HasExited)
            f += 10000f;

        f *= scoreScale;
        return Mathf.Max(0f, f);
    }
}
