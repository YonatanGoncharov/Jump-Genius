using UnityEngine;

public class ConnectionGene
{
    private int inNeuron;
    private int outNeuron;
    private float weight;
    private bool expressed;
    private int innovationNumber;

    public ConnectionGene(int inNeuron, int outNeuron, float weight, bool expressed, int innovationNumber)
    {
        this.inNeuron = inNeuron;
        this.outNeuron = outNeuron;
        this.weight = weight;
        this.expressed = expressed;
        this.innovationNumber = innovationNumber;
    }
    //getters and setters
    public int InNeuron
    {
        get { return inNeuron; }
        set { inNeuron = value; }
    }
    public int OutNeuron
    {
        get { return outNeuron; }
        set { outNeuron = value; }
    }
    public float Weight
    {
        get { return weight; }
        set { weight = value; }
    }
    public bool Expressed
    {
        get { return expressed; }
        set { expressed = value; }
    }
    public int InnovationNumber
    {
        get { return innovationNumber; }
        set { innovationNumber = value; }
    }
    public void DisableConnection()
    {
        expressed = false;
    }
    public ConnectionGene Clone()
    {
        return new ConnectionGene(inNeuron, outNeuron, weight, expressed, innovationNumber);
    }

}
