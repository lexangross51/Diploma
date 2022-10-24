namespace Diploma.Source.Interfaces;

public interface IMeshBuilder
{
    IEnumerable<Point2D> CreatePoints();
    IEnumerable<FiniteElement> CreateElements();
    IEnumerable<DirichletCondition> CreateDirichlet();
    IEnumerable<NeumannCondition> CreateNeumann();
    IEnumerable<Material> CreateMaterials();
    IEnumerable<double>? CreateProperties();
    IEnumerable<double> CreateViscosities();
}