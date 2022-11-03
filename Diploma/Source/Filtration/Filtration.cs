namespace Diploma.Source.Filtration;

public class Filtration
{
    private readonly Mesh.Mesh _mesh;
    private readonly IBasis _basis;
    private readonly FEMBuilder.Fem _fem;
    private readonly FlowsCalculator _flowsCalculator;
    private readonly FlowsBalancer _flowsBalancer;
    private Vector _flows = default!;

    public Filtration(Mesh.Mesh mesh, FEMBuilder.Fem fem, IBasis basis)
    {
        _mesh = mesh;
        _basis = basis;
        _fem = fem;
        _flowsCalculator = new FlowsCalculator(_mesh, _basis);
        _flowsBalancer = new FlowsBalancer(_mesh);
    }

    public void ModelFlows()
    {
        // _fem.Solve();
        // _flows = _flowsCalculator.CalculateAverageFlows(_fem.Solution!.Value);
        // _flowsBalancer.BalanceFlows(_flows);
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