namespace Diploma.Source.Interfaces;

public interface IMeshBuilder
{
    (IEnumerable<Point2D>, IEnumerable<FiniteElement>) CreatePointsAndElements();
    IEnumerable<DirichletCondition> CreateDirichlet();
    IEnumerable<NeumannCondition>? CreateNeumann();
    IEnumerable<Material> CreateMaterials();
}