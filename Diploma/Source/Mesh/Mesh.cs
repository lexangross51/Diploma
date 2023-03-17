using System.Drawing;

namespace Diploma.Source.Mesh;

public class Mesh
{
    public (Point2D Point, bool IsFictitious)[] Points { get; }
    public FiniteElement[] Elements { get; }
    public DirichletCondition[] DirichletConditions { get; }
    public (int Element, int LocalEdge)[] RemoteEdges { get; }
    public NeumannCondition[] NeumannConditions { get; }
    public Material[] Materials { get; }
    public int NodesCount => Points.Length;
    public int ElementsCount => Elements.Length;
    public int EdgesCount => Elements[^1].EdgesIndices[^1] > Elements[^2].EdgesIndices[^1]
        ? Elements[^1].EdgesIndices[^1] + 1
        : Elements[^2].EdgesIndices[^1] + 1;

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