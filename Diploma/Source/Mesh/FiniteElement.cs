namespace Diploma.Source.Mesh;

public class FiniteElement
{
    public List<int> Nodes { get; }
    public List<int> Edges { get; }
    public List<int> EdgesDirect { get; set; }
    public int Area { get; set; }

    public FiniteElement(int[] nodes, int area)
    {
        Nodes = nodes.ToList();
        Area = area;
        Edges = new List<int>(4);
        EdgesDirect = new List<int>(4);
    }
}