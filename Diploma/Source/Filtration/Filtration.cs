namespace Diploma.Source.Filtration;

public class Filtration
{
    private readonly Mesh.Mesh _mesh;
    private readonly PhaseProperty _phaseProperty;
    private readonly FEMBuilder.FEM _fem;
    private readonly PhaseComponentsTable[] _phasesComponents;
    private readonly PhaseComponentsTable _remotePhasesComponents;
    private readonly FlowsCalculator _flowsCalculator;
    private readonly FlowsBalancer _flowsBalancer;
    private Vector _flows = default!;
    private readonly double[,] _flowsOutPhases;
    private readonly double[,] _volumeOutPhases;
    private double _deltaT;
    private List<(int, int)> _abandon;
    private double[] _saturationMaxCrit = { 0.4, 0.6 };
    private double[] _saturationMinCrit = { 0.3, 0.7 };

    public Filtration(Mesh.Mesh mesh, PhaseProperty phaseProperty, FEMBuilder.FEM fem, IBasis basis)
    {
        _mesh = mesh;
        _phaseProperty = phaseProperty;
        _fem = fem;
        var componentsTable = PhaseComponentsTable.ReadJson("Input/AreaPhasesComponents.json");
        var componentsPerWell = PhaseComponentsTable.ReadJson("Input/InjectedPhaseComponents.json");
        _remotePhasesComponents = PhaseComponentsTable.ReadJson("Input/RemotePhasesComponents.json");
        
        _phasesComponents = new PhaseComponentsTable[_mesh.Elements.Length]
                                .Select(_ => componentsTable.Clone() as PhaseComponentsTable)
                                .ToArray()!;

        foreach (var condition in _mesh.NeumannConditions)
        {
            _phasesComponents[condition.Element] = (componentsPerWell.Clone() as PhaseComponentsTable)!;
        }
        
        _flowsCalculator = new FlowsCalculator(_mesh, basis, phaseProperty);
        _flowsBalancer = new FlowsBalancer(_mesh);

        int edgesCount = _mesh.Elements[^1].Edges[^1] + 1;
        int phasesCount = _phaseProperty.Phases![0].Count;

        _flowsOutPhases = new double[edgesCount, phasesCount];
        _volumeOutPhases = new double[edgesCount, phasesCount];
        _abandon = new List<(int, int)>();
    }

    public void ModelFiltration()
    {
        _fem.Solve();
        _flows = _flowsCalculator.CalculateAverageFlows(_fem.Solution!.Value);
        _flowsBalancer.BalanceFlows(_flows);
        CalculateFlowOutPhases();
        CalculateDeltaT(0.1);
        //CalculateVolumesOutPhases();
        //CalculateNewSaturations();
    }

    private int FlowDirection(int ielem, int localEdge)
        => Math.Sign(_flows[_mesh.Elements[ielem].Edges[localEdge]]) switch
        {
            0 => 0,
            > 0 => _mesh.Elements[ielem].EdgesDirect[localEdge],
            < 0 => -_mesh.Elements[ielem].EdgesDirect[localEdge]
        };

    private bool IsWellElement(int ielem)
        => Enumerable.Any(_mesh.NeumannConditions, condition => condition.Element == ielem);

    private int AreaPhaseIndex(string phaseName)
    {
        var elementPhases = _phaseProperty.Phases![0];

        for (int i = 0; i < elementPhases.Count; i++)
        {
            if (elementPhases[i].Name == phaseName) return i;
        }

        return -1;
    }
    
    private void CalculateFlowOutPhases()
    {
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            if (IsWellElement(ielem)) continue;
            
            var edges = _mesh.Elements[ielem].Edges;
            var phases = _phaseProperty.Phases![ielem];
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
        
        // Accounting from wells
        foreach (var condition in _mesh.NeumannConditions)
        {
            var ielem = condition.Element;
            var edges = _mesh.Elements[ielem].Edges;
            var phases = _phaseProperty.Phases![ielem];
            var phasesSum = phases.Sum(phase => phase.Kappa / phase.Viscosity);

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                int globalEdge = edges[localEdge];
                
                if (FlowDirection(ielem, localEdge) == 1)
                {
                    foreach (var phase in phases)
                    {
                        int phaseIndex = AreaPhaseIndex(phase.Name);
                        double phaseFraction = phase.Kappa / (phase.Viscosity * phasesSum);
                        
                        _flowsOutPhases[globalEdge, phaseIndex] = phaseFraction * Math.Abs(_flows[globalEdge]);
                    }
                }
            }
        }
        
        // Accounting from remote boundaries
        int nx = _mesh.Elements[0].Nodes[2] - 1;
        int ny = _mesh.Elements.Length / nx;

        var remotePhases = _phaseProperty.RemoteBordersPhases!;
        var remotePhasesSum = remotePhases.Sum(phase => phase.Kappa / phase.Viscosity);
        
        // Left border
        for (int ielem = 0, j = 0; j < ny; j++, ielem += nx)
        {
            int globalEdge = _mesh.Elements[ielem].Edges[1];
            
            if (FlowDirection(ielem, 1) == 1)
            {
                foreach (var phase in remotePhases)
                {
                    int phaseIndex = AreaPhaseIndex(phase.Name);
                    double phaseFraction = phase.Kappa / (phase.Viscosity * remotePhasesSum);

                    _flowsOutPhases[globalEdge, phaseIndex] = phaseFraction * Math.Abs(_flows[globalEdge]);
                }
            }
        }
        
        // Right border
        for (int ielem = nx - 1, j = 0; j < ny; j++, ielem += nx)
        {
            int globalEdge = _mesh.Elements[ielem].Edges[2];
            
            if (FlowDirection(ielem, 2) == 1)
            {
                foreach (var phase in remotePhases)
                {
                    int phaseIndex = AreaPhaseIndex(phase.Name);
                    double phaseFraction = phase.Kappa / (phase.Viscosity * remotePhasesSum);

                    _flowsOutPhases[globalEdge, phaseIndex] = phaseFraction * Math.Abs(_flows[globalEdge]);
                }
            }
        }
        
        // Lower border
        for (int ielem = 0; ielem < nx; ielem++)
        {
            int globalEdge = _mesh.Elements[ielem].Edges[0];
            
            if (FlowDirection(ielem, 0) == 1)
            {
                foreach (var phase in remotePhases)
                {
                    int phaseIndex = AreaPhaseIndex(phase.Name);
                    double phaseFraction = phase.Kappa / (phase.Viscosity * remotePhasesSum);

                    _flowsOutPhases[globalEdge, phaseIndex] = phaseFraction * Math.Abs(_flows[globalEdge]);
                }
            }
        }
        
        // Upper border
        for (int ielem = nx * (ny - 1); ielem < nx * ny; ielem++)
        {
            int globalEdge = _mesh.Elements[ielem].Edges[3];
            
            if (FlowDirection(ielem, 3) == 1)
            {
                foreach (var phase in remotePhases)
                {
                    int phaseIndex = AreaPhaseIndex(phase.Name);
                    double phaseFraction = phase.Kappa / (phase.Viscosity * remotePhasesSum);

                    _flowsOutPhases[globalEdge, phaseIndex] = phaseFraction * Math.Abs(_flows[globalEdge]);
                }
            }
        }
    }
    
    private void CalculateDeltaT(double deltaT0)
    {
        _deltaT = deltaT0;
        var abandonH = new List<(int, int)>();

        // Forming set of abandonH and calculate deltaT
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            if (IsWellElement(ielem)) continue;

            var leftBottom = _mesh.Points[_mesh.Elements[ielem].Nodes[0]];
            var rightTop = _mesh.Points[_mesh.Elements[ielem].Nodes[^1]];
            
            var porosity = _mesh.Materials[_mesh.Elements[ielem].Area].Porosity;
            var mes = (rightTop.X - leftBottom.X) * (rightTop.Y - leftBottom.Y);
            var edges = _mesh.Elements[ielem].Edges;
            var saturations = _phaseProperty.Saturation![ielem];

            for (int iphase = 0; iphase < saturations.Count; iphase++)
            {
                if (saturations[iphase] < _saturationMaxCrit[iphase])
                {
                    abandonH.Add((ielem, iphase));
                    continue;
                }

                double sumPhaseFlowOut = 0.0;

                for (int localEdge = 0; localEdge < edges.Count; localEdge++)
                {
                    if (FlowDirection(ielem, localEdge) == 1)
                    {
                        sumPhaseFlowOut += _flowsOutPhases[edges[localEdge], iphase];
                    }
                }

                var deltaTe = mes * porosity * saturations[iphase] / sumPhaseFlowOut;

                if (deltaTe < _deltaT) _deltaT = deltaTe;
            }
        }

        // Form the set abandon for the elements of which the pushing procedure will be carried out
        foreach (var pair in abandonH)
        {
            var leftBottom = _mesh.Points[_mesh.Elements[pair.Item1].Nodes[0]];
            var rightTop = _mesh.Points[_mesh.Elements[pair.Item1].Nodes[^1]];
            
            var porosity = _mesh.Materials[_mesh.Elements[pair.Item1].Area].Porosity;
            var mes = (rightTop.X - leftBottom.X) * (rightTop.Y - leftBottom.Y);
            var edges = _mesh.Elements[pair.Item1].Edges;
            var saturations = _phaseProperty.Saturation![pair.Item1];
            
            double sumPhaseVolumeOut = 0.0;

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                if (FlowDirection(pair.Item1, localEdge) == 1)
                {
                    sumPhaseVolumeOut += _flowsOutPhases[edges[localEdge], pair.Item2];
                }
            }

            sumPhaseVolumeOut *= _deltaT;

            if (sumPhaseVolumeOut > mes * porosity * saturations[pair.Item2])
            {
                _abandon.Add(pair);
            }
        }
    }

    private void CalculateVolumesOutPhases()
    {
        
    }

    private void CalculateNewSaturations()
    {
        
    }
}