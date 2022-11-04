namespace Diploma.Source.Filtration;

public class Filtration
{
    private readonly Mesh.Mesh _mesh;
    private readonly PhaseProperty _phaseProperty;
    private readonly FEMBuilder.Fem _fem;
    private readonly PhaseComponentsTable[] _phaseComponents;
    private readonly FlowsCalculator _flowsCalculator;
    private readonly FlowsBalancer _flowsBalancer;
    private Vector _flows = default!;

    public Filtration(Mesh.Mesh mesh, PhaseProperty phaseProperty, FEMBuilder.Fem fem, IBasis basis)
    {
        _mesh = mesh;
        _phaseProperty = phaseProperty;
        _fem = fem;
        var componentsTable = PhaseComponentsTable.ReadJson("Input/PrimaryComponents.json");
        _phaseComponents = new PhaseComponentsTable[_mesh.Elements.Length]
                                .Select(_ => componentsTable.Clone() as PhaseComponentsTable)
                                .ToArray()!;
        _flowsCalculator = new FlowsCalculator(_mesh, basis);
        _flowsBalancer = new FlowsBalancer(_mesh);
    }

    public void ModelFlows()
    {
        _fem.Solve();
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