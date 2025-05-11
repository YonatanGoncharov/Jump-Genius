using UnityEngine;

public class FitnessEvaluator : MonoBehaviour
{
    [Header("Rewards")]
    public float stageReward = 200f;
    public float heightReward = 1f;
    public float verticalDistReward = 5f;
    public float platformReward = 10f;
    public float airTimeReward = 0.5f;
    public float exitBonus = 500f;

    [Header("Efficiency")]
    public float efficiencyBonus = 30f;
    public float movePenalty = 5f;

    [Header("Penalties")]
    public float fallPenalty = 100f;
    public float idlePenalty = 1f;
    public float wallPushPenalty = 50f;

    [Header("Overall Scale"), Range(0f, 1f)]
    public float scoreScale = 0.1f;

    /// <summary>
    /// Called by NEATManager.EvaluateFitness(genome => ...) passing in each AgentController.
    /// </summary>
    public float EvaluateAgent(AgentController a)
    {
        float f = 0f;

        // 1) Stage & height
        f += a.BestStageReached * stageReward;
        f += a.BestPlatformHeight * heightReward;

        // 2) Vertical jump height
        f += a.BestYDistance * verticalDistReward;

        // 3) Platforms visited
        f += a.PlatformsVisitedCount * platformReward;

        // 4) Air-time bonus
        f += a.AirTime * airTimeReward;

        // 5) Exit bonus
        if (a.HasExited)
            f += exitBonus;

        // 6) Efficiency: bonus & per-move penalty
        if (a.MoveCount > 0)
            f += efficiencyBonus / a.MoveCount;
        f -= a.MoveCount * movePenalty;

        // 7) Other penalties
        f -= a.Falls * fallPenalty;
        f -= a.GroundIdleTime * idlePenalty;
        f -= a.WallPushTime * wallPushPenalty;

        // 8) Final scale & clamp
        f *= scoreScale;
        return Mathf.Max(0f, f);
    }
}
