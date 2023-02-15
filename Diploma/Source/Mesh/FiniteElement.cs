namespace Diploma.Source.Mesh;

public readonly record struct Edge(int Node1, int Node2)
{
    public override string ToString()
        => "{" + Node1 + ", " + Node2 + "}";
}

public class FiniteElement
{
    public int[] Nodes { get; }
    public int[] EdgesIndices { get; }
    public Edge[] Edges { get; }
    public int Area { get; set; }

    public FiniteElement(int nodesCount)
    {
        Nodes = new int[nodesCount];
        EdgesIndices = new int[nodesCount];
        Edges = new Edge[nodesCount];
    }

    public FiniteElement(int[] nodes, int area)
    {
        Nodes = nodes;
        Area = area;
        EdgesIndices = new int[nodes.Length];
        Edges = new Edge[nodes.Length];
    }

    public override string ToString()
        => Nodes.Aggregate("", (current, node) => current + $"{node} ");
}