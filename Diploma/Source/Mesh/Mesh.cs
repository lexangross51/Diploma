namespace Diploma.Source.Mesh;

public class Mesh
{
    public (Point2D Point, bool IsFictitious)[] Points { get; }
    public FiniteElement[] Elements { get; }
    public DirichletCondition[] DirichletConditions { get; }
    public (int Element, int LocalEdge)[] RemoteEdges { get; }
    public NeumannCondition[] NeumannConditions { get; }
    public Material[] Materials { get; }

    public Mesh(
        IEnumerable<(Point2D, bool)> points,
        IEnumerable<FiniteElement> elements,
        IEnumerable<DirichletCondition> dirichletConditions,
        IEnumerable<(int, int)> remoteEdgesList,
        IEnumerable<NeumannCondition>? neumannConditions,
        IEnumerable<Material> materials)
    {
        Points = points.ToArray();
        Elements = elements.ToArray();
        DirichletConditions = dirichletConditions.ToArray();
        RemoteEdges = remoteEdgesList.ToArray();
        NeumannConditions = (neumannConditions ?? Array.Empty<NeumannCondition>()).ToArray();
        Materials = materials.ToArray();
    }
}