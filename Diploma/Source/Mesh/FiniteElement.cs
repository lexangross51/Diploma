namespace Diploma.Source.Mesh;

public class FiniteElement
{
    public List<int> Nodes { get; }
    public List<int> Edges { get; }
    public List<int> EdgesDirect { get; }
    public int Area { get; }

    public FiniteElement(int[] nodes, int[] edges, int[] edgesDirect, int area)
        => (Nodes, Edges, EdgesDirect, Area) = (nodes.ToList(), edges.ToList(), edgesDirect.ToList(), area);
}