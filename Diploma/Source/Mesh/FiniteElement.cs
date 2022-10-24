namespace Diploma.Source.Mesh;

public class FiniteElement
{
    public List<int> Nodes { get; }
    public List<int> Edges { get; set; }
    public List<int> EdgesDirect { get; set; }
    public int Area { get; }

    public FiniteElement(int[] nodes, int area)
    {
        Nodes = nodes.ToList();
        Area = area;
        Edges = new();
        EdgesDirect = new();
    }
}