namespace Diploma.Source.Mesh;

public class Mesh
{
    public ImmutableArray<Point2D> Points { get; }
    public ImmutableArray<FiniteElement> Elements { get; }
    public ImmutableArray<Material> Materials { get; }
    public ImmutableArray<DirichletCondition> DirichletConditions { get; }
    public ImmutableArray<NeumannCondition> NeumannConditions { get; }

    public Mesh(
        (IEnumerable<Point2D> points, IEnumerable<FiniteElement> elements) grid,
        IEnumerable<DirichletCondition> dirichletConditions,
        IEnumerable<NeumannCondition>? neumannConditions,
        IEnumerable<Material> materials)
    {
        Points = grid.points.ToImmutableArray();
        Elements = grid.elements.ToImmutableArray();
        DirichletConditions = dirichletConditions.ToImmutableArray();
        NeumannConditions = (neumannConditions ?? Array.Empty<NeumannCondition>()).ToImmutableArray();
        Materials = materials.ToImmutableArray();
    }
}