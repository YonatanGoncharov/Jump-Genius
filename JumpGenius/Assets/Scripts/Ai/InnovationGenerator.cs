using UnityEngine;

public class InnovationGenerator
{
    private int currentInnovation = 0;
    public int GetInnovation()
    {
        return currentInnovation++;
    }
}
