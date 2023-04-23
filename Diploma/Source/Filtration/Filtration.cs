namespace Diploma.Source.Filtration;

public class Filtration
{
    private readonly Mesh.Mesh _mesh;
    private readonly double[] _elementsSquares;
    private readonly PhaseProperty _phaseProperty;
    private readonly FEMBuilder.FEM _fem;
    private readonly FlowsCalculator _flowsCalculator;
    private Vector _flows = default!;
    private readonly int[,] _edgesDirect;
    private readonly double[,] _flowsOutPhases;
    private readonly double[,] _volumeOutPhases;
    private double _deltaT;
    private readonly List<(int Element, int PhaseIndex)> _abandon;
    private readonly double[] _saturationMaxCrit = { 0.05, 0.05 };
    private readonly double[] _saturationMinCrit = { 0.01, 0.01 };
    private int _timeStart, _timeEnd, _timeMoment;
    //private double _time;
    private readonly string _path; 
    
    public Filtration(Mesh.Mesh mesh, PhaseProperty phaseProperty, FEMBuilder.FEM fem, IBasis basis, string path)
    {
        _path = path;
        _mesh = mesh;
        _phaseProperty = phaseProperty;
        _fem = fem;

        int edgesCount = _mesh.EdgesCount;
        int phasesCount = _phaseProperty.Phases![0].Count;
    
        _flowsOutPhases = new double[edgesCount, phasesCount];
        _volumeOutPhases = new double[edgesCount, phasesCount];
        _abandon = new List<(int, int)>();

        // Calculate normals and fix directions for general normals
        Point2D[] normals = new Point2D[edgesCount];
        _edgesDirect = new int[_mesh.Elements.Length, 4];
        
        bool[] isUsed = new bool[edgesCount];

        for (var ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var element = _mesh.Elements[ielem];
            for (int iedge = 0; iedge < 4; iedge++)
            {
                int globalIdx = element.EdgesIndices[iedge];
                var edge = element.Edges[iedge];
                var p1 = _mesh.Points[edge.Node1].Point;
                var p2 = _mesh.Points[edge.Node2].Point;
                double n1 = -(p2.Y - p1.Y);
                double n2 = p2.X - p1.X;
                double norm = Math.Sqrt(n1 * n1 + n2 * n2);

                if (isUsed[globalIdx])
                {
                    _edgesDirect[ielem, iedge] = -1;
                }
                else
                {
                    isUsed[globalIdx] = true;
                    normals[globalIdx] = new Point2D(n1 / norm, n2 / norm);
                    _edgesDirect[ielem, iedge] = 1;
                }
            }
        }
        
        _flowsCalculator = new FlowsCalculator(_mesh, basis, phaseProperty, normals);
        
        // Calculate elements squares
        _elementsSquares = new double[_mesh.Elements.Length];

        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var nodes = _mesh.Elements[ielem].Nodes;
            var leftBottom = _mesh.Points[nodes[0]].Point;
            var rightBottom = _mesh.Points[nodes[1]].Point;
            var leftTop = _mesh.Points[nodes[2]].Point;
            var rightTop = _mesh.Points[nodes[3]].Point;

            _elementsSquares[ielem] = Quadrilateral.Square(leftBottom, rightBottom, leftTop, rightTop);
        }
    }

    private void ClearData()
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
    }
    
    public void ModelFiltration(int timeStart, int timeEnd)
    {
        _timeStart = timeStart;
        _timeEnd = timeEnd;

        for (_timeMoment = _timeStart; _timeMoment < _timeEnd; _timeMoment++)
        {
            ClearData();
            
            _fem.Solve();

            if (_timeMoment % 10 == 0)
            {
                DataWriter.WritePressure(_path, $"Pressure{_timeMoment}.txt", _fem.Solution!);
                DataWriter.WriteSaturation(_path, $"Saturation{_timeMoment}.txt", _mesh, _phaseProperty.Saturation!);
            }
    
            _flows = _flowsCalculator.CalculateAverageFlows(_fem.Solution!);
            CalculateFlowOutPhases();
            CalculateDeltaT(100.0);
            CalculateVolumesOutPhases();
            // DataWriter.WriteFlows(_path, $"Flows{_timeMoment}.txt", _mesh, _flowsOutPhases);
            // DataWriter.WriteVolumes(_path, $"Volumes{_timeMoment}.txt", _mesh, _volumeOutPhases);
            CalculateNewSaturations();
        }
    }
    
    private int FlowDirection(double flow, int ielem, int localEdge)
        => Math.Sign(flow) switch
        {
            0 => 0,
            > 0 => _edgesDirect[ielem, localEdge],
            < 0 => -_edgesDirect[ielem, localEdge]
        };

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
            var edges = _mesh.Elements[ielem].EdgesIndices;
            var phases = _phaseProperty.Phases![ielem];
            double phasesSum = phases.Sum(phase => phase.Kappa / phase.Viscosity);
    
            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                int globalEdge = edges[localEdge];
                
                if (FlowDirection(_flows[globalEdge], ielem, localEdge) == 1)
                {
                    for (int iphase = 0; iphase < phases.Count; iphase++)
                    {
                        double alphaM = phases[iphase].Kappa / (phases[iphase].Viscosity * phasesSum);
    
                        // НУЖНО ЛИ ИСПОЛЬЗОВАТЬ МОДУЛЬ ??????????????????????????????????????????????
                        //_flowsOutPhases[globalEdge, iphase] = alphaM * Math.Abs(_flows[globalEdge]);
                        _flowsOutPhases[globalEdge, iphase] = alphaM * _flows[globalEdge];
                    }
                }
            }
        }
        
        // Accounting from wells
        if (_phaseProperty.InjectedPhases is not null)
        {
            double injectedPhasesSum = _phaseProperty.InjectedPhases.Sum(phase => phase.Kappa / phase.Viscosity);
            
            foreach (var (ielem, iedge, _) in _mesh.NeumannConditions)
            {
                int globalEdge = _mesh.Elements[ielem].EdgesIndices[iedge];

                if (FlowDirection(_flows[globalEdge], ielem, iedge) == -1)
                {
                    foreach (var phase in _phaseProperty.InjectedPhases)
                    {
                        int phaseIndex = AreaPhaseIndex(phase.Name);
                        double phaseFraction = phase.Kappa / (phase.Viscosity * injectedPhasesSum);

                        // НУЖНО ЛИ ИСПОЛЬЗОВАТЬ МОДУЛЬ ??????????????????????????????????????????????
                        //_flowsOutPhases[globalEdge, phaseIndex] = phaseFraction * Math.Abs(_flows[globalEdge]);
                        _flowsOutPhases[globalEdge, phaseIndex] = phaseFraction * _flows[globalEdge];
                    }
                }
            }
        }
        
        // Accounting from remote boundaries
        if (_phaseProperty.RemoteBordersPhases is not null)
        {
            var remotePhasesSum = _phaseProperty.RemoteBordersPhases.Sum(phase => phase.Kappa / phase.Viscosity);

            foreach (var (ielem, iedge) in _mesh.RemoteEdges)
            {
                int globalEdge = _mesh.Elements[ielem].EdgesIndices[iedge];

                if (FlowDirection(_flows[globalEdge], ielem, iedge) == -1)
                {
                    foreach (var phase in _phaseProperty.RemoteBordersPhases)
                    {
                        int phaseIndex = AreaPhaseIndex(phase.Name);
                        double phaseFraction = phase.Kappa / (phase.Viscosity * remotePhasesSum);

                        // НУЖНО ЛИ ИСПОЛЬЗОВАТЬ МОДУЛЬ ??????????????????????????????????????????????
                        //_flowsOutPhases[globalEdge, phaseIndex] = phaseFraction * Math.Abs(_flows[globalEdge]);
                        _flowsOutPhases[globalEdge, phaseIndex] = phaseFraction * _flows[globalEdge];
                    }
                }
            }
        }
    }
    
    private void CalculateDeltaT(double deltaT0)
    {
        _deltaT = deltaT0;
        
        var abandonH = new HashSet<(int Element, int PhaseIndex)>();
    
        // Forming set of abandonH and calculate deltaT
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var porosity = _mesh.Materials[_mesh.Elements[ielem].Area].Porosity;
            var mes = _elementsSquares[ielem];
            var edges = _mesh.Elements[ielem].EdgesIndices;
            var saturations = _phaseProperty.Saturation![ielem];
    
            for (int iphase = 0; iphase < saturations.Count; iphase++)
            {
                if (saturations[iphase] < _saturationMaxCrit[iphase])
                {
                    abandonH.Add((ielem, iphase));
                    continue;
                }

                // Total outgoing phase flow from the element
                double totalPhaseFlowOut = 0.0;

                for (int localEdge = 0; localEdge < 4; localEdge++)
                {
                    int globalEdge = edges[localEdge];

                    if (FlowDirection(_flows[globalEdge], ielem, localEdge) == 1)
                    {
                        totalPhaseFlowOut += _flowsOutPhases[globalEdge, iphase];
                    }
                }

                double deltaTe = mes * porosity * saturations[iphase] / Math.Abs(totalPhaseFlowOut);
    
                if (deltaTe < _deltaT) _deltaT = deltaTe;
            }
        }

        #region Delta time decrease

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

        #endregion
    
        // Form the set abandon for the elements of which the pushing procedure will be carried out
        using var sww = new StreamWriter("Output/CheckMixEnough.txt");
        sww.WriteLine($"{"Element", 7} {"Phase", 5} {"Out volume", 14} {"Exist volume", 14}");
        
        foreach (var (ielem, iphase) in abandonH)
        {
            var porosity = _mesh.Materials[_mesh.Elements[ielem].Area].Porosity;
            var mes = _elementsSquares[ielem];
            var edges = _mesh.Elements[ielem].EdgesIndices;
            var saturations = _phaseProperty.Saturation![ielem];
        
            double totalPhaseFlowOut = 0.0;

            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                int globalEdge = edges[localEdge];

                if (FlowDirection(_flows[globalEdge], ielem, localEdge) == 1)
                {
                    totalPhaseFlowOut += _flowsOutPhases[globalEdge, iphase];
                }
            }

            double phaseVolumeOut = Math.Abs(totalPhaseFlowOut) * _deltaT;
            double existingVolume = mes * porosity * saturations[iphase]; 
        
            if (phaseVolumeOut > existingVolume)
            {
                _abandon.Add((ielem, iphase));
                sww.WriteLine($"{ielem, 7} {iphase, 5} {phaseVolumeOut:F14} {existingVolume:F14}");
            }
        }

        // _time += _deltaT;
        // string timeStr = $"{_timeMoment}: {_time}";
        // Debug.Print(timeStr);
    }
    
    private void CalculateVolumesOutPhases()
    {
        List<HashSet<int>> iMissed = new();
        
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            iMissed.Add(new HashSet<int>());
        }

        int phasesCount = _phaseProperty.Phases![0].Count;
        double[] phaseVolumes = new double[phasesCount];    // Volumes of the ejected phases
        double[] phasesFractions = new double[phasesCount];
        double[] newVolumes = new double[_volumeOutPhases.GetLength(0)];
    
        // Forming set iMissed which will contain phases for which the ejection procedure will be performed
        foreach (var (ielem, iphase) in _abandon)
        {
            iMissed[ielem].Add(iphase);
        }
    
        // Or iphase saturation on ielem < min critical saturation
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var saturations = _phaseProperty.Saturation![ielem];
        
            for (int iphase = 0; iphase < saturations.Count; iphase++)
            {
                if (saturations[iphase] < _saturationMinCrit[iphase])
                {
                    iMissed[ielem].Add(iphase);
                }
            }
        }
        
        // Calculate out phases volumes
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            Array.Fill(phaseVolumes, 0.0, 0, phaseVolumes.Length);

            var mes = _elementsSquares[ielem];
            var edges = _mesh.Elements[ielem].EdgesIndices;
            var porosity = _mesh.Materials[_mesh.Elements[ielem].Area].Porosity;
            var phases = _phaseProperty.Phases![ielem];
            var saturations = _phaseProperty.Saturation![ielem];
            double phasesSum = phases.Sum(phase => phase.Kappa / phase.Viscosity);
    
            // If the list is empty for an element, then we calculate the volumes arising from the element
            if (iMissed[ielem].Count == 0)
            {
                for (int localEdge = 0; localEdge < 4; localEdge++)
                {
                    int globalEdge = edges[localEdge];
                    
                    if (FlowDirection(_flows[globalEdge], ielem, localEdge) == 1)
                    {
                        for (int iphase = 0; iphase < saturations.Count; iphase++)
                        {
                            _volumeOutPhases[globalEdge, iphase] = Math.Abs(_flowsOutPhases[globalEdge, iphase]) * _deltaT;
                        }
                    }
                }
                
                continue;
            }
    
            foreach (var iphase in iMissed[ielem])
            {
                phaseVolumes[iphase] = mes * porosity * saturations[iphase];
            }
    
            for (int iphase = 0; iphase < phasesCount; iphase++)
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
            double flowOut = 0.0;

            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                int globalEdge = _mesh.Elements[ielem].EdgesIndices[localEdge];

                if (FlowDirection(_flows[globalEdge], ielem, localEdge) == 1)
                {
                    flowOut += _flows[globalEdge];
                }
            }

            // Total ejection volume
            double volumeMissed = phaseVolumes.Sum();
            
            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                var globalEdge = edges[localEdge];
                
                if (FlowDirection(_flows[globalEdge], ielem, localEdge) == 1)
                {
                    // НУЖНО ЛИ ИСПОЛЬЗОВАТЬ МОДУЛЬ ??????????????????????????????????????????????
                    //newVolumes[globalEdge] = Math.Abs(_flows[globalEdge]) * _deltaT - _flows[globalEdge] / flowOut * volumeMissed;
                    newVolumes[globalEdge] = Math.Abs(_flows[globalEdge]) * _deltaT - Math.Abs(_flows[globalEdge] / flowOut) * volumeMissed;
                }
            }
    
            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                var globalEdge = edges[localEdge];
                
                if (FlowDirection(_flows[globalEdge], ielem, localEdge) == 1)
                {
                    for (int iphase = 0; iphase < phases.Count; iphase++)
                    {
                        if (iMissed[ielem].Contains(iphase))
                        {
                            // НУЖНО ЛИ ИСПОЛЬЗОВАТЬ МОДУЛЬ ??????????????????????????????????????????????
                            //_volumeOutPhases[globalEdge, iphase] = Math.Abs(_flows[globalEdge]) / flowOut * phaseVolumes[iphase];
                            _volumeOutPhases[globalEdge, iphase] = Math.Abs(_flows[globalEdge] / flowOut) * phaseVolumes[iphase];
                        }
                        else
                        {
                            _volumeOutPhases[globalEdge, iphase] = phasesFractions[iphase] * newVolumes[globalEdge];
                        }
                    }
                }
            }
        }
        
        foreach (var (ielem, iedge) in _mesh.RemoteEdges)
        {
            int globalEdge = _mesh.Elements[ielem].EdgesIndices[iedge];
        
            if (FlowDirection(_flows[globalEdge], ielem, iedge) == -1)
            {
                foreach (var phaseIndex in _phaseProperty.RemoteBordersPhases!.Select(phase => AreaPhaseIndex(phase.Name)))
                {
                    _volumeOutPhases[globalEdge, phaseIndex] = Math.Abs(_flowsOutPhases[globalEdge, phaseIndex]) * _deltaT;
                }
            }
        }

        foreach (var (ielem, iedge, _) in _mesh.NeumannConditions)
        {
            int globalEdge = _mesh.Elements[ielem].EdgesIndices[iedge];

            if (FlowDirection(_flows[globalEdge], ielem, iedge) == -1)
            {
                foreach (var phaseIndex in _phaseProperty.InjectedPhases!.Select(phase => AreaPhaseIndex(phase.Name)))
                {
                    _volumeOutPhases[globalEdge, phaseIndex] = Math.Abs(_flowsOutPhases[globalEdge, phaseIndex]) * _deltaT;
                }
            }
        }
        
        // using var sw = new StreamWriter("Output/CheckVolumeSign.txt");
        //
        // for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        // {
        //     var edges = _mesh.Elements[ielem].EdgesIndices;
        //     var saturations = _phaseProperty.Saturation![ielem];
        //
        //     for (int localEdge = 0; localEdge < 4; localEdge++)
        //     {
        //         for (int iphase = 0; iphase < saturations.Count; iphase++)
        //         {
        //             if (_volumeOutPhases[edges[localEdge], iphase] < 0 &&
        //                 Math.Abs(_volumeOutPhases[edges[localEdge], iphase]) > 1E-15)
        //             {
        //                 sw.WriteLine($"{ielem}: {localEdge}, {iphase}, {_volumeOutPhases[edges[localEdge], iphase]}");
        //             }
        //         }
        //     }
        // }
    }
    
    private void CalculateNewSaturations()
    {
        double[] phasesVolumes = new double[_phaseProperty.Phases![0].Count];
        
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            Array.Fill(phasesVolumes, 0.0, 0, phasesVolumes.Length);

            double porosity = _mesh.Materials[_mesh.Elements[ielem].Area].Porosity;
            double mes = _elementsSquares[ielem];
            var saturations = _phaseProperty.Saturation![ielem];

            for (int iphase = 0; iphase < saturations.Count; iphase++)
            {
                // Current phase volume in the element
                phasesVolumes[iphase] = mes * porosity * saturations[iphase];
    
                double inVolume = 0.0, outVolume = 0.0;
    
                // Count the leaked and flowed volumes of the phase
                for (int localEdge = 0; localEdge < 4; localEdge++)
                {
                    int globalEdge = _mesh.Elements[ielem].EdgesIndices[localEdge];
                    
                    if (FlowDirection(_flows[globalEdge], ielem, localEdge) == 1)
                    {
                        outVolume += _volumeOutPhases[globalEdge, iphase];
                    }
                    else if (FlowDirection(_flows[globalEdge], ielem, localEdge) == -1)
                    {
                        inVolume += _volumeOutPhases[globalEdge, iphase];
                    }
                }
                
                // Calculate the new phase volume in the element
                phasesVolumes[iphase] = phasesVolumes[iphase] + inVolume - outVolume;
            }

            double phasesSum = phasesVolumes.Sum();
    
            for (int iphase = 0; iphase < saturations.Count; iphase++)
            {
                saturations[iphase] = phasesVolumes[iphase] / phasesSum;

                if (Math.Abs(saturations[iphase]) < 1E-10)
                {
                    saturations[iphase] = 0.0;
                }
            }
        }
        
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var saturations = _phaseProperty.Saturation![ielem];
            var phases = _phaseProperty.Phases[ielem];

            for (int iphase = 0; iphase < phases.Count; iphase++)
            {
                var phase = phases[iphase];
                phase.Kappa = Phase.KappaDependence(saturations[iphase]);
                phases[iphase] = phase;
            }
        }
    }
}