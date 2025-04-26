using UnityEngine;

public class FitnessEvaluator : MonoBehaviour
{
    public float EvaluateAgent(AgentController a)
    {
        float f = 0f;

        // core rewards
        f += a.BestPlatformHeight * 2f;
        f += a.BestStageReached * 20f;

        // penalties
        f -= a.Falls * 20f;
        f -= a.WallPushTime * 5f;
        f -= a.GroundIdleTime * 5f;

        // small efficiency bonus
        if (a.MoveCount > 0)
            f += Mathf.Clamp(10f / a.MoveCount, 0f, 5f);

        return Mathf.Max(0f, f);
    }
}
