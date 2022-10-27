namespace Diploma.Source.Filtration;

public class Filtration
{
    private readonly Mesh.Mesh _mesh;
    private readonly IBasis _basis;
    private readonly FEMBuilder.Fem _fem;
    
    public double MaxNonBalance { get; set; }
    public int MaxBalanceIters { get; set; }

    public Filtration(Mesh.Mesh mesh, FEMBuilder.Fem fem, IBasis basis)
        => (_mesh, _basis, _fem) = (mesh, basis, fem);

    public void ModelFlows()
    {
        
    }

    private void CalculateAverageFlows()
    {
        
    }

    private void BalanceFlows()
    {
        
    }

    private void CalculateDeltaT(double deltaT0)
    {
        
    }

    private void CalculateVolumes()
    {
        
    }

    private void CalculateNewSaturations()
    {
        
    }
}