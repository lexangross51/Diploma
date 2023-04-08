namespace Diploma.Source.Mesh;

public class Mesh
{
    public (Point2D Point, bool IsFictitious)[] Points { get; }
    public FiniteElement[] Elements { get; }
    [JsonIgnore] public DirichletCondition[] DirichletConditions { get; }
    [JsonIgnore] public (int Element, int LocalEdge)[] RemoteEdges { get; }
    [JsonIgnore] public NeumannCondition[] NeumannConditions { get; }
    [JsonIgnore] public Material[] Materials { get; }
    [JsonIgnore] public int NodesCount => Points.Length;
    [JsonIgnore] public int ElementsCount => Elements.Length;
    [JsonIgnore] public int EdgesCount => Elements[^1].EdgesIndices[^1] > Elements[^2].EdgesIndices[^1]
        ? Elements[^1].EdgesIndices[^1] + 1
        : Elements[^2].EdgesIndices[^1] + 1;

    [JsonConstructor] public Mesh(
        IEnumerable<(Point2D, bool)> points,
        IEnumerable<FiniteElement> elements,
        IEnumerable<DirichletCondition>? dirichletConditions,
        IEnumerable<(int, int)>? remoteEdgesList,
        IEnumerable<NeumannCondition>? neumannConditions,
        IEnumerable<Material>? materials)
    {
        Points = points.ToArray();
        Elements = elements.ToArray();
        DirichletConditions = dirichletConditions?.ToArray()!;
        RemoteEdges = remoteEdgesList?.ToArray()!;
        NeumannConditions = (neumannConditions ?? Array.Empty<NeumannCondition>()).ToArray();
        Materials = materials?.ToArray()!;
    }
}