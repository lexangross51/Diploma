namespace Diploma.Source.Mesh;

public readonly record struct Edge(int Node1, int Node2);

public class FiniteElement
{
    public List<int> Nodes { get; }
    public List<int> EdgesIndices { get; }
    public List<Edge> Edges { get; }
    public List<int> EdgesDirect { get; set; }
    public int Area { get; set; }
    public bool IsFictitious { get; set; }

    public FiniteElement(int[] nodes, int area)
    {
        Nodes = nodes.ToList();
        Area = area;
        IsFictitious = false;
        EdgesIndices = new List<int>(4);
        Edges = new List<Edge>(4);
        EdgesDirect = new List<int>(4);
    }

    public override string ToString()
    {
        string element = $"Area: {Area}\n";
        element += "Nodes: ";
        element = Nodes.Aggregate(element, (current, node) => current + $" {node}  ");
        element += "\n";
        element += "Edges indices: ";
        element = EdgesIndices.Aggregate(element, (current, edge) => current + $" {edge}  ");
        // element += "\n";
        // element += "Edges direct: ";
        // element = EdgesDirect.Aggregate(element, (current, edgeDir) => current + $" {edgeDir}  ");
        // element += "\n";
        // element += "Edges: ";
        // element = Edges.Aggregate(element, (current, edge) => current + $"{edge}");
        //
        return element;
    }
}