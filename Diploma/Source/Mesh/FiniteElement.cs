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
}