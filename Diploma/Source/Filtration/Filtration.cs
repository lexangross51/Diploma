using System.Configuration;
using System.Windows;
using Vector = Diploma.Source.MathClasses.Vector;

namespace Diploma.Source.Filtration;

public class Filtration
{
    private readonly Mesh.Mesh _mesh;
    private readonly PhaseProperty _phaseProperty;
    private readonly FEMBuilder.FEM _fem;
    private readonly PhaseComponentsTable[] _phaseComponents;
    private readonly PhaseComponentsTable _remoteBordersPhases;
    private readonly FlowsCalculator _flowsCalculator;
    private readonly FlowsBalancer _flowsBalancer;
    private Vector _flows = default!;
    private readonly double[,] _flowsOutPhases;

    public Filtration(Mesh.Mesh mesh, PhaseProperty phaseProperty, FEMBuilder.FEM fem, IBasis basis)
    {
        _mesh = mesh;
        _phaseProperty = phaseProperty;
        _fem = fem;
        var componentsTable = PhaseComponentsTable.ReadJson("Input/PrimaryComponents.json");
        _phaseComponents = new PhaseComponentsTable[_mesh.Elements.Length]
                                .Select(_ => componentsTable.Clone() as PhaseComponentsTable)
                                .ToArray()!;
        _remoteBordersPhases = PhaseComponentsTable.ReadJson("Input/RemoteBordersPhases.json");

        var componentsPerWell = PhaseComponentsTable.ReadJson("Input/InjectedPhases.json");

        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            if (IsWellElement(ielem))
            {
                _phaseComponents[ielem] = (componentsPerWell.Clone() as PhaseComponentsTable)!;   
            }
        }
        
        _flowsCalculator = new FlowsCalculator(_mesh, basis, phaseProperty);
        _flowsBalancer = new FlowsBalancer(_mesh);

        int edgesCount = _mesh.Elements[^1].Edges[^1] + 1;
        int phasesCount = _phaseProperty.Phases[0].Count;

        _flowsOutPhases = new double[edgesCount, phasesCount];
    }

    public void ModelFlows()
    {
        _fem.Solve();
        _flows = _flowsCalculator.CalculateAverageFlows(_fem.Solution!.Value);
        _flowsBalancer.BalanceFlows(_flows);
        //CalculateFlowOutPhases();
    }

    private int FlowDirection(int ielem, int localEdge)
    {
        int globalEdge = _mesh.Elements[ielem].Edges[localEdge];

        return Math.Sign(_flows[globalEdge]) switch
        {
            0 => 0,
            > 0 => _mesh.Elements[ielem].EdgesDirect[localEdge],
            < 0 => -_mesh.Elements[ielem].EdgesDirect[localEdge]
        };
    }

    private bool IsWellElement(int ielem)
        => Enumerable.Any(_mesh.NeumannConditions, condition => condition.Element == ielem);
    
    private void CalculateFlowOutPhases()
    {
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            if (IsWellElement(ielem)) continue;
            
            var edges = _mesh.Elements[ielem].Edges;
            var phases = _phaseProperty.Phases[ielem];
            double phasesSum = phases.Sum(phase => phase.Kappa / phase.Viscosity);

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                int globalEdge = edges[localEdge];
                
                if (FlowDirection(ielem, localEdge) == 1)
                {
                    for (int iphase = 0; iphase < phases.Count; iphase++)
                    {
                        double phaseFraction = phases[iphase].Kappa / (phases[iphase].Viscosity * phasesSum);

                        _flowsOutPhases[globalEdge, iphase] = phaseFraction * Math.Abs(_flows[globalEdge]);
                    }
                }
            }
        }
        
        // Учет от скважин
        foreach (var condition in _mesh.NeumannConditions)
        {
            var ielem = condition.Element;
            var edges = _mesh.Elements[ielem].Edges;
            var phases = _phaseProperty.Phases[ielem];
            var phasesSum = phases.Sum(phase => phase.Kappa / phase.Viscosity);

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                int globalEdge = edges[localEdge];
                
                if (FlowDirection(ielem, localEdge) == 1)
                {
                    for (int iphase = 0; iphase < _phaseComponents[ielem].Rows; iphase++)
                    {
                        double phaseFraction = phases[iphase].Kappa / (phases[iphase].Viscosity * phasesSum);

                        _flowsOutPhases[globalEdge, iphase] = phaseFraction * Math.Abs(_flows[globalEdge]);
                    }
                }
            }
        }
        
        // Учет от удаленных границ
        // Самая левая граница
        int nx = _mesh.Elements[0].Nodes[2] - 1;
        int ny = _mesh.Elements.Length / nx;

        // for (int ielem = 0, j = 0; j < ny; j++, ielem += nx)
        // {
        //     var phasesSum = phases.Sum(phase => phase.Kappa / phase.Viscosity);
        //     int globalEdge = _mesh.Elements[ielem].Edges[1];
        //     
        //     if (FlowDirection(ielem, 1) == 1)
        //     {
        //         for (int iphase = 0; iphase < _phaseComponents[ielem].Rows; iphase++)
        //         {
        //             double phaseFraction = phases[iphase].Kappa / (phases[iphase].Viscosity * phasesSum);
        //
        //             _flowsOutPhases[globalEdge, iphase] = phaseFraction * Math.Abs(_flows[globalEdge]);
        //         }
        //     }
        // }
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