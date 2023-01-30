namespace Diploma.Source.Filtration;

public class Filtration
{
    private readonly Mesh.Mesh _mesh;
    private readonly PhaseProperty _phaseProperty;
    private readonly FEMBuilder.FEM _fem;
    //private readonly PhaseComponentsTable[] _phasesComponents;
    //private readonly PhaseComponentsTable _remotePhasesComponents;
    private readonly FlowsCalculator _flowsCalculator;
    //private readonly FlowsBalancer _flowsBalancer;
    private Vector _flows = default!;
    private readonly double[,] _flowsOutPhases;
    private readonly double[,] _volumeOutPhases;
    private double _deltaT;
    private readonly List<(int, int)> _abandon;
    private readonly double[] _saturationMaxCrit = { 0.4, 0.4 };
    private readonly double[] _saturationMinCrit = { 0.01, 0.01 };
    private int _timeStart, _timeEnd;

    public Filtration(Mesh.Mesh mesh, PhaseProperty phaseProperty, FEMBuilder.FEM fem, IBasis basis)
    {
        _mesh = mesh;
        _phaseProperty = phaseProperty;
        _fem = fem;
        // var componentsTable = PhaseComponentsTable.ReadJson("Input/AreaPhasesComponents.json");
        // var componentsPerWell = PhaseComponentsTable.ReadJson("Input/InjectedPhaseComponents.json");
        //_remotePhasesComponents = PhaseComponentsTable.ReadJson("Input/RemotePhasesComponents.json");
        
        // _phasesComponents = new PhaseComponentsTable[_mesh.Elements.Length]
        //                         .Select(_ => componentsTable.Clone() as PhaseComponentsTable)
        //                         .ToArray()!;

        // foreach (var condition in _mesh.NeumannConditions)
        // {
        //     _phasesComponents[condition.Element] = (componentsPerWell.Clone() as PhaseComponentsTable)!;
        // }
        
        _flowsCalculator = new FlowsCalculator(_mesh, basis, phaseProperty);
        //_flowsBalancer = new FlowsBalancer(_mesh);

        int edgesCount = _mesh.Elements[^1].EdgesIndices[^1] + 1;
        int phasesCount = _phaseProperty.Phases![0].Count;

        _flowsOutPhases = new double[edgesCount, phasesCount];
        _volumeOutPhases = new double[edgesCount, phasesCount];
        _abandon = new List<(int, int)>();
    }

    public void ModelFiltration(int timeStart, int timeEnd)
    {
        _timeStart = timeStart;
        _timeEnd = timeEnd;

        for (int timeMoment = _timeStart; timeMoment < _timeEnd; timeMoment++)
        {
            _abandon.Clear();

            for (int i = 0; i < _flowsOutPhases.GetLength(0); i++)
            {
                for (int j = 0; j < _flowsOutPhases.GetLength(1); j++)
                {
                    _flowsOutPhases[i, j] = 0.0;
                    _volumeOutPhases[i, j] = 0.0;
                }
            }

            _fem.Solve();
            
            DataWriter.WritePressure($"Pressure{timeMoment}.txt", _fem.Solution!);
            DataWriter.WriteSaturation($"Saturation{timeMoment}.txt", _mesh, _phaseProperty.Saturation!);

            _flows = _flowsCalculator.CalculateAverageFlows(_fem.Solution!);
            //_flowsBalancer.BalanceFlows(_flows);
            CalculateFlowOutPhases();
            CalculateDeltaT(0.1);
            CalculateVolumesOutPhases();
            CalculateNewSaturations();
        }
    }

    private int FlowDirection(int ielem, int localEdge)
        => Math.Sign(_flows[_mesh.Elements[ielem].EdgesIndices[localEdge]]) switch
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
            
            var edges = _mesh.Elements[ielem].EdgesIndices;
            var phases = _phaseProperty.Phases![ielem];
            double phasesSum = phases.Sum(phase => phase.Kappa / phase.Viscosity);

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                int globalEdge = edges[localEdge];
                
                if (FlowDirection(ielem, localEdge) == 1)
                {
                    for (int iphase = 0; iphase < phases.Count; iphase++)
                    {
                        double alphaM = phases[iphase].Kappa / (phases[iphase].Viscosity * phasesSum);

                        _flowsOutPhases[globalEdge, iphase] = alphaM * Math.Abs(_flows[globalEdge]);
                    }
                }
            }
        }
        
        // Accounting from wells
        foreach (var condition in _mesh.NeumannConditions)
        {
            var ielem = condition.Element;
            var edges = _mesh.Elements[ielem].EdgesIndices;
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
            int globalEdge = _mesh.Elements[ielem].EdgesIndices[1];
            
            if (FlowDirection(ielem, 1) == -1)
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
            int globalEdge = _mesh.Elements[ielem].EdgesIndices[2];
            
            if (FlowDirection(ielem, 2) == -1)
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
            int globalEdge = _mesh.Elements[ielem].EdgesIndices[0];
            
            if (FlowDirection(ielem, 0) == -1)
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
            int globalEdge = _mesh.Elements[ielem].EdgesIndices[3];
            
            if (FlowDirection(ielem, 3) == -1)
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
        var abandonH2 = new List<(int, int)>();

        // Forming set of abandonH and calculate deltaT
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            if (IsWellElement(ielem)) continue;

            var leftBottom = _mesh.Points[_mesh.Elements[ielem].Nodes[0]].Point;
            var rightTop = _mesh.Points[_mesh.Elements[ielem].Nodes[^1]].Point;
            
            var porosity = _mesh.Materials[_mesh.Elements[ielem].Area].Porosity;
            var mes = (rightTop.X - leftBottom.X) * (rightTop.Y - leftBottom.Y);
            var edges = _mesh.Elements[ielem].EdgesIndices;
            var saturations = _phaseProperty.Saturation![ielem];

            for (int iphase = 0; iphase < saturations.Count; iphase++)
            {
                if (saturations[iphase] < _saturationMinCrit[iphase])
                {
                    abandonH2.Add((ielem, iphase));
                    continue;
                }
                
                if (saturations[iphase] < _saturationMaxCrit[iphase])
                {
                    abandonH.Add((ielem, iphase));
                    continue;
                }

                double sumPhaseFlowOut = edges.Where((_, localEdge) => FlowDirection(ielem, localEdge) == 1)
                    .Sum(t => _flowsOutPhases[t, iphase]);

                var deltaTe = mes * porosity * saturations[iphase] / sumPhaseFlowOut;

                if (deltaTe < _deltaT) _deltaT = deltaTe;
            }
        }

        // // Check if there is enough mix at the selected delta time
        // bool isOptimalDeltaT = false;
        //
        // using var sw = new StreamWriter("Output/CheckMixEnough.txt");
        //
        // while (!isOptimalDeltaT)
        // {
        //     sw.WriteLine($"Delta time: {_deltaT}");
        //     isOptimalDeltaT = true;
        //     int totalCount = 0;
        //
        //     for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        //     {
        //         if (IsWellElement(ielem)) continue;
        //
        //         var leftBottom = _mesh.Points[_mesh.Elements[ielem].Nodes[0]];
        //         var rightTop = _mesh.Points[_mesh.Elements[ielem].Nodes[^1]];
        //
        //         var porosity = _mesh.Materials[_mesh.Elements[ielem].Area].Porosity;
        //         var mes = (rightTop.X - leftBottom.X) * (rightTop.Y - leftBottom.Y);
        //         var saturations = _phaseProperty.Saturation![ielem];
        //         var edges = _mesh.Elements[ielem].Edges;
        //
        //         for (int iphase = 0; iphase < saturations.Count; iphase++)
        //         {
        //             double phaseVolume = mes * porosity * saturations[iphase];
        //             double volumeOut = edges.Where((t, localEdge) => FlowDirection(ielem, localEdge) == 1)
        //                 .Sum(t => _flowsOutPhases[t, iphase]);
        //
        //             if (phaseVolume < volumeOut * _deltaT)
        //             {
        //                 isOptimalDeltaT = false;                                
        //                 totalCount++;                                           
        //                 sw.WriteLine($"{ielem}: {volumeOut * _deltaT}, {phaseVolume}");
        //             }
        //         }
        //     }
        //     
        //     sw.WriteLine($"Total count: {totalCount}");
        //     sw.WriteLine("--------------------------------------------------");
        //     sw.WriteLine("--------------------------------------------------");
        //
        //     if (!isOptimalDeltaT) _deltaT /= 2.0;
        // }

        // Form the set abandon for the elements of which the pushing procedure will be carried ou
        using var sww = new StreamWriter("Output/CheckMixEnough.txt");
        
        foreach (var pair in abandonH)
        {
            var (ielem, iphase) = pair;
            var leftBottom = _mesh.Points[_mesh.Elements[ielem].Nodes[0]].Point;
            var rightTop = _mesh.Points[_mesh.Elements[ielem].Nodes[^1]].Point;
            
            var porosity = _mesh.Materials[_mesh.Elements[ielem].Area].Porosity;
            var mes = (rightTop.X - leftBottom.X) * (rightTop.Y - leftBottom.Y);
            var edges = _mesh.Elements[ielem].EdgesIndices;
            var saturations = _phaseProperty.Saturation![ielem];
        
            double phaseVolumeOut = edges.Where((_, localEdge) => FlowDirection(ielem, localEdge) == 1)
                .Sum(t => _flowsOutPhases[t, iphase]);
            phaseVolumeOut *= _deltaT;
        
            if (phaseVolumeOut > mes * porosity * saturations[iphase])
            {
                _abandon.Add(pair);
                sww.WriteLine($"{ielem}: {iphase}, {phaseVolumeOut}, {mes * porosity * saturations[iphase]}");
            }
        }

        foreach (var pair in abandonH2)
        {
            _abandon.Add(pair);
        }
    }

    private void CalculateVolumesOutPhases()
    {
        List<HashSet<int>> iMissed = new();
        
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            iMissed.Add(new HashSet<int>());
        }
        
        double[] phaseVolumes = new double[_phaseProperty.Phases![0].Count];
        double[] phasesFractions = new double[_phaseProperty.Phases![0].Count];
        double[] newVolumes = new double[_volumeOutPhases.GetLength(0)];

        // Forming set iMissed which will contain phases for which the ejection procedure
        // will be performed
        // Pair (ielem, iphase) in abandon
        foreach (var pair in _abandon)
        {
            iMissed[pair.Item1].Add(pair.Item2);
        }

        // // or iphase saturation on ielem < min critical saturation
        // for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        // {
        //     if (IsWellElement(ielem)) continue;
        //     
        //     var saturations = _phaseProperty.Saturation![ielem];
        //
        //     for (int iphase = 0; iphase < saturations.Count; iphase++)
        //     {
        //         if (saturations[iphase] < _saturationMinCrit[iphase])
        //         {
        //             iMissed[ielem].Add(iphase);
        //         }
        //     }
        // }
        
        // Calculate out phases volumes
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            Array.Fill(phaseVolumes, 0.0, 0, phaseVolumes.Length);
            
            if (IsWellElement(ielem)) continue;
            
            var leftBottom = _mesh.Points[_mesh.Elements[ielem].Nodes[0]].Point;
            var rightTop = _mesh.Points[_mesh.Elements[ielem].Nodes[^1]].Point;
            
            var mes = (rightTop.X - leftBottom.X) * (rightTop.Y - leftBottom.Y);
            var edges = _mesh.Elements[ielem].EdgesIndices;
            var porosity = _mesh.Materials[_mesh.Elements[ielem].Area].Porosity;
            var phases = _phaseProperty.Phases![ielem];
            var saturations = _phaseProperty.Saturation![ielem];
            double phasesSum = phases.Sum(phase => phase.Kappa / phase.Viscosity);

            // If the list is empty for an element, then we calculate the volumes arising from the element
            if (iMissed[ielem].Count == 0)
            {
                for (int localEdge = 0; localEdge < edges.Count; localEdge++)
                {
                    int globalEdge = edges[localEdge];
                    
                    if (FlowDirection(ielem, localEdge) == 1)
                    {
                        for (int iphase = 0; iphase < saturations.Count; iphase++)
                        {
                            _volumeOutPhases[globalEdge, iphase] = _flowsOutPhases[globalEdge, iphase] * _deltaT;
                        }
                    }
                }
                
                continue;
            }

            foreach (var iphase in iMissed[ielem])
            {
                phaseVolumes[iphase] = mes * porosity * saturations[iphase];
            }

            for (int iphase = 0; iphase < phases.Count; iphase++)
            {
                phasesFractions[iphase] = phases[iphase].Kappa / (phases[iphase].Viscosity * phasesSum);
            }

            // Total fraction of phases that will not be ejected
            double notMissedPhases = 0.0;
            
            for (int iphase = 0; iphase < phases.Count; iphase++)
            {
                if (!iMissed[ielem].Contains(iphase))
                {
                    notMissedPhases += phasesFractions[iphase];
                }
            } 

            // New fraction of phases that will not be ejected
            for (int iphase = 0; iphase < phases.Count; iphase++)
            {
                if (!iMissed[ielem].Contains(iphase))
                {
                    phasesFractions[iphase] /= notMissedPhases;
                }
            }
            
            // Total outgoing flow of the mixture
            double flowOut = edges.Where((_, localEdge) => FlowDirection(ielem, localEdge) == 1)
                .Sum(t => Math.Abs(_flows[t]));

            // Total ejection volume
            double volumeMissed = 0.0;

            for (int iphase = 0; iphase < phases.Count; iphase++)
            {
                volumeMissed += phaseVolumes[iphase];
            }

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                var globalEdge = edges[localEdge];
                
                if (FlowDirection(ielem, localEdge) == 1)
                {
                    newVolumes[globalEdge] = Math.Abs(_flows[globalEdge]) * _deltaT -
                                             Math.Abs(_flows[globalEdge]) / flowOut * volumeMissed;
                }
            }

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                var globalEdge = edges[localEdge];
                
                if (FlowDirection(ielem, localEdge) == 1)
                {
                    for (int iphase = 0; iphase < phases.Count; iphase++)
                    {
                        if (iMissed[ielem].Contains(iphase))
                        {
                            _volumeOutPhases[globalEdge, iphase] =
                                Math.Abs(_flows[globalEdge]) / flowOut * phaseVolumes[iphase];
                        }
                        else
                        {
                            _volumeOutPhases[globalEdge, iphase] = phasesFractions[iphase] * newVolumes[globalEdge];
                        }
                    }
                }
            }
        }


        using var sw = new StreamWriter("Output/CheckVolumeSign.txt");
        
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            if (IsWellElement(ielem)) continue;

            var edges = _mesh.Elements[ielem].EdgesIndices;
            var saturations = _phaseProperty.Saturation![ielem];

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                for (int iphase = 0; iphase < saturations.Count; iphase++)
                {
                    if (_volumeOutPhases[edges[localEdge], iphase] < 0 &&
                        Math.Abs(_volumeOutPhases[edges[localEdge], iphase]) > 1E-15)
                    {
                        sw.WriteLine($"{ielem}: {localEdge}, {iphase}, {_volumeOutPhases[edges[localEdge], iphase]}");
                    }
                }
            }
        }
    }

    private void CalculateNewSaturations()
    {
        double[] phasesVolumes = new double[_phaseProperty.Phases![0].Count];
        
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            if (IsWellElement(ielem)) continue;
            
            Array.Fill(phasesVolumes, 0.0, 0, phasesVolumes.Length);
            
            var leftBottom = _mesh.Points[_mesh.Elements[ielem].Nodes[0]].Point;
            var rightTop = _mesh.Points[_mesh.Elements[ielem].Nodes[^1]].Point;
            
            double porosity = _mesh.Materials[_mesh.Elements[ielem].Area].Porosity;
            double mes = (rightTop.X - leftBottom.X) * (rightTop.Y - leftBottom.Y);
            
            var edges = _mesh.Elements[ielem].EdgesIndices;
            var saturations = _phaseProperty.Saturation![ielem];
            //var componentsTable = _phasesComponents[ielem];

            double phasesSum = 0.0;
            
            for (int iphase = 0; iphase < saturations.Count; iphase++)
            {
                // Current phase volume in the element
                phasesVolumes[iphase] = mes * porosity * saturations[iphase];

                double inVolume = 0.0, outVolume = 0.0;

                // Count the leaked and flowed volumes of the phase
                for (int localEdge = 0; localEdge < edges.Count; localEdge++)
                {
                    if (FlowDirection(ielem, localEdge) == 1)
                    {
                        outVolume += _volumeOutPhases[edges[localEdge], iphase];
                    }
                    else if (FlowDirection(ielem, localEdge) == -1)
                    {
                        inVolume += _volumeOutPhases[edges[localEdge], iphase];
                    }
                }
                
                // Calculate the new phase volume in the element
                phasesVolumes[iphase] = phasesVolumes[iphase] + inVolume - outVolume;
                phasesSum += phasesVolumes[iphase];
            }

            for (int iphase = 0; iphase < saturations.Count; iphase++)
            {
                saturations[iphase] = phasesVolumes[iphase] / phasesSum;
            }
        }
    }
}