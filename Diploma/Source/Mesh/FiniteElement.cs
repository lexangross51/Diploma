namespace Diploma.Source.Mesh;

public readonly record struct Edge(int Node1, int Node2)
{
    public override string ToString()
        => "{" + Node1 + ", " + Node2 + "}";
}
public class FiniteElement
{
    public int[] Nodes { get; }
    [JsonIgnore] public int[] EdgesIndices { get; }
    [JsonIgnore] public Edge[] Edges { get; }
    [JsonIgnore] public int Area { get; set; }

    public FiniteElement(int nodesCount)
    {
        Nodes = new int[nodesCount];
        EdgesIndices = new int[nodesCount];
        Edges = new Edge[nodesCount];
    }

    [JsonConstructor]
    public FiniteElement(int[] nodes, int area)
    {
        Nodes = nodes;
        Area = area;
        EdgesIndices = new int[nodes.Length];
        Edges = new Edge[nodes.Length];
    }

    public override string ToString()
    {
        string element = $"Area: {Area}\nNodes: ";
        element = Nodes.Aggregate(element, (current, node) => current + $" {node}  ");
        element += "\nEdges indices: ";
        element = EdgesIndices.Aggregate(element, (current, edge) => current + $" {edge}  ");
        element += "\nEdges: ";
        element = Edges.Aggregate(element, (current, edge) => current + $"{edge}  ");

        return element;
    }
}