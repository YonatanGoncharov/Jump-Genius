using UnityEngine;

public class Neuron
{
    public enum NeuronType
    {
        INPUT,
        HIDDEN,
        OUTPUT
    }
    private NeuronType type;
    private int id;
    //getters and setters

    public Neuron(int id, NeuronType type)
    {
        this.id = id;
        this.type = type;
    }

    public NeuronType Type
    {
        get { return type; }
        set { type = value; }
    }
    public int Id
    {
        get { return id; }
        set { id = value; }
    }
    public Neuron Clone()
    {
        return new Neuron(id, type);
    }
}
