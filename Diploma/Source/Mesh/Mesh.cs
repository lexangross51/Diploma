namespace Diploma.Source.Mesh;

public class Mesh
{
    public ImmutableArray<(Point2D Point, bool IsFictitious)> Points { get; }
    public ImmutableArray<FiniteElement> Elements { get; }
    public ImmutableArray<DirichletCondition> DirichletConditions { get; }
    public ImmutableArray<int> RemoteEdges { get; }
    public ImmutableArray<NeumannCondition> NeumannConditions { get; }
    public ImmutableArray<Material> Materials { get; }

    public Mesh(
        IEnumerable<(Point2D, bool)> points, 
        IEnumerable<FiniteElement> elements,
        IEnumerable<DirichletCondition> dirichletConditions,
        IEnumerable<int> remoteEdgesList,
        IEnumerable<NeumannCondition>? neumannConditions,
        IEnumerable<Material> materials)
    {
        Points = points.ToImmutableArray();
        Elements = elements.ToImmutableArray();
        DirichletConditions = dirichletConditions.ToImmutableArray();
        RemoteEdges = remoteEdgesList.ToImmutableArray();
        NeumannConditions = (neumannConditions ?? Array.Empty<NeumannCondition>()).ToImmutableArray();
        Materials = materials.ToImmutableArray();
    }
}