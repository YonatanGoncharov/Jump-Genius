using System.Collections.Generic;

public class Genome
{
    private const float PROBABILITY_PRETURBING = 0.9f;

    private Dictionary<int, ConnectionGene> connections;
    private Dictionary<int, Neuron> neurons;

    public Genome()
    {
        connections = new Dictionary<int, ConnectionGene>();
        neurons = new Dictionary<int, Neuron>();
    }
    public Dictionary<int, ConnectionGene> Connections
    {
        get { return connections; }
        //set { connections = value; }
    }
    public Dictionary<int, Neuron> Neurons
    {
        get { return neurons; }
        //set { neurons = value; }
    }
    public void AddNeuron(Neuron neuron)
    {
        neurons.Add(neuron.Id, neuron);
    }
    public void AddConnection(ConnectionGene con)
    {
        connections.Add(con.InnovationNumber, con);
    }
    public void Mutation()
    {
        foreach (ConnectionGene con in connections.Values)
        {
            if (UnityEngine.Random.value < PROBABILITY_PRETURBING) //change later propabilitys to be variables
            {
                con.Weight = con.Weight * (float)(UnityEngine.Random.value * 4 - 2);
            }
            else
            {
                con.Weight = (float)(UnityEngine.Random.value * 4 - 2);
            }
        }

    }
    public void AddConnectionMutation(System.Random rnd)
    {
        if (neurons.Count == 0)
        {
            return;
        }
        Neuron neuron1 = neurons[rnd.Next(neurons.Count)];
        Neuron neuron2 = neurons[rnd.Next(neurons.Count)];
        float weight = (float)rnd.NextDouble() * 2 - 1;
        bool reversed = false;
        if (neuron1.Type == Neuron.NeuronType.HIDDEN && neuron2.Type == Neuron.NeuronType.INPUT)
        {
            reversed = true;
        }
        else if (neuron1.Type == Neuron.NeuronType.OUTPUT && neuron2.Type == Neuron.NeuronType.HIDDEN)
        {
            reversed = true;
        }
        else if (neuron1.Type == Neuron.NeuronType.OUTPUT && neuron2.Type == Neuron.NeuronType.INPUT)
        {
            reversed = true;
        }
        bool connectionExists = false;
        foreach (ConnectionGene con in connections.Values)
        {
            if (con.InNeuron == neuron1.Id && con.OutNeuron == neuron2.Id)//existing connection
            {
                connectionExists = true;
                break;
            }
            else if (con.InNeuron == neuron2.Id && con.OutNeuron == neuron1.Id)//existing connection
            {
                connectionExists = true;
                break;
            }
        }
        if (connectionExists)
        {
            return;
        }
        ConnectionGene newCon = new ConnectionGene(reversed ? neuron2.Id : neuron1.Id, reversed ? neuron1.Id : neuron2.Id, weight, true, 0);
        connections.Add(newCon.InnovationNumber, newCon);
    }
    public void AddNodeMutation(System.Random rnd, InnovationGenerator ig)
    {
        if (connections.Count == 0)
        {
            return;
        }
        ConnectionGene con = connections[rnd.Next(connections.Count)];
        Neuron inNode = neurons[con.InNeuron];
        Neuron outNode = neurons[con.OutNeuron];
        con.DisableConnection();
        Neuron newNode = new Neuron(neurons.Count, Neuron.NeuronType.HIDDEN);
        ConnectionGene inToNew = new ConnectionGene(inNode.Id, newNode.Id, 1, true, ig.GetInnovation());
        ConnectionGene newToOut = new ConnectionGene(newNode.Id, outNode.Id, con.Weight, true, ig.GetInnovation());

        neurons.Add(newNode.Id, newNode);
        connections.Add(inToNew.InnovationNumber, inToNew);
        connections.Add(newToOut.InnovationNumber, newToOut);
    }
    //parent 1 is the fitter parent then parent 2
    public Genome Crossover(Genome parent1, Genome parent2)
    {
        Genome child = new Genome();
        foreach (Neuron parent1neuron in parent1.Neurons.Values)
        {
            child.AddNeuron(parent1neuron.Clone());
        }
        foreach (ConnectionGene connectionGene in parent1.Connections.Values)
        {
            if (parent2.connections.ContainsKey(connectionGene.InnovationNumber))//matching gene found
            {
                ConnectionGene childConGene = (UnityEngine.Random.value > 0.5f) ? connectionGene.Clone() : parent2.connections[connectionGene.InnovationNumber].Clone();
                child.AddConnection(childConGene);
            }
            else //disjoint or excess gene
            {
                child.AddConnection(connectionGene.Clone());
            }
        }
        return child;
    }
}
