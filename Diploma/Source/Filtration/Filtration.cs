namespace Diploma.Source.Filtration;

public class Filtration
{
    private readonly Mesh.Mesh _mesh;
    private readonly double[] _elementsSquares;
    private readonly PhaseProperty _phaseProperty;
    private readonly FEMBuilder.FEM _fem;
    private readonly FlowsCalculator _flowsCalculator;
    private readonly Vector _flows;
    private readonly int[,] _edgesDirect;
    private readonly double[,] _flowsOutPhases;
    private readonly double[,] _volumeOutPhases;
    private double _deltaT;
    private readonly List<(int Element, int PhaseIndex)> _abandon;
    private readonly HashSet<(int Element, int PhaseIndex)> _abandonH;
    private readonly List<HashSet<int>> _iMissed;
    private readonly double[] _saturationMaxCrit = { 0.01, 0.01 };
    private readonly double[] _saturationMinCrit = { 0.0001, 0.0001 };
    private int _timeStart, _timeEnd, _timeMoment;
    private readonly string _path;
    private readonly List<double> _oilProduced;
    private readonly List<double> _waterProduced;
    private readonly List<double> _waterInjected;
    private readonly List<double> _oilSaturationPerWell;
    private readonly Point2D[] _normals;
    public readonly List<double> Times;

    public Filtration(Mesh.Mesh mesh, PhaseProperty phaseProperty, FEMBuilder.FEM fem, IBasis basis, string path)
    {
        _path = path;
        _mesh = mesh;
        _phaseProperty = phaseProperty;
        _fem = fem;
        _oilProduced = new List<double> { 0.0 };
        _waterProduced = new List<double> { 0.0 };
        _waterInjected = new List<double> { 0.0 };
        _oilSaturationPerWell = new List<double> { 0.8 };
        Times = new List<double>() { 0.0 };
        _flows = new Vector(_mesh.EdgesCount);

        int edgesCount = _mesh.EdgesCount;
        int phasesCount = _phaseProperty.Phases![0].Count;

        _flowsOutPhases = new double[edgesCount, phasesCount];
        _volumeOutPhases = new double[edgesCount, phasesCount];
        _abandon = new List<(int, int)>();
        _abandonH = new HashSet<(int Element, int PhaseIndex)>();
        _iMissed = new List<HashSet<int>>();

        // Calculate normals and fix directions for general normals
        _normals = new Point2D[edgesCount];
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
                double n1 = p2.X - p1.X;
                double n2 = p2.Y - p1.Y;
                double norm = Math.Sqrt(n1 * n1 + n2 * n2);

                if (isUsed[globalIdx])
                {
                    _edgesDirect[ielem, iedge] = -1;
                }
                else
                {
                    isUsed[globalIdx] = true;
                    _normals[globalIdx] = new Point2D(n2 / norm, -n1 / norm);
                    _edgesDirect[ielem, iedge] = 1;
                }
            }
        }

        _flowsCalculator = new FlowsCalculator(_mesh, basis, phaseProperty, _normals, _edgesDirect);

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

    private bool IsWellEdge(int globalEdge)
    {
        foreach (var (ielem, iedge, _) in _mesh.NeumannConditions)
        {
            var edge = _mesh.Elements[ielem].EdgesIndices[iedge];
            if (edge == globalEdge) return true;
        }

        return false;
    }

    public void ModelFiltration(int timeStart, int timeEnd, int deltaTime)
    {
        _timeStart = timeStart;
        _timeEnd = timeEnd;

        double time = 0.0;
        double totalTime = 0.0;

        var sw = Stopwatch.StartNew();
        for (_timeMoment = _timeStart; totalTime < _timeEnd * 86400.0; _timeMoment += deltaTime)
        {
            _oilProduced.Add(0.0);
            _waterProduced.Add(0.0);
            _waterInjected.Add(0.0);
            //_oilSaturationPerWell.Add(0.0);

            _fem.Solve();
            _flows.Fill();
            _flowsCalculator.CalculateAverageFlows(_fem.Solution!, _flows);
            DataWriter.WritePressure(_path, $"Pressure{_timeMoment}.txt", _fem.Solution!, _mesh);
            DataWriter.WriteSaturation(_path, $"Saturation{_timeMoment}.txt", _mesh, _phaseProperty.Saturation!);

            //if (time >= deltaTime * 86400)
            //{
            //    _oilProduced.Add(0.0);
            //    _waterProduced.Add(0.0);
            //    _waterInjected.Add(0.0);
            //}

            while (time < deltaTime * 86400.0)
            {
                //if (Math.Abs(time) > 1E-14)
                //{
                //    totalTime += time;
                //    times.Add(totalTime / 86400.0);
                //    time -= deltaTime * 86400.0;
                //}

                ClearData();
                CalculateFlowOutPhases();
                CalculateDeltaT(deltaTime * 86400);
                CalculateVolumesOutPhases();
                CalculateNewSaturations();
                time += _deltaT;
            }

            //foreach (var (ielem, _, _) in _mesh.NeumannConditions)
            //{
            //    _oilSaturationPerWell[^1] += _phaseProperty.Saturation![ielem][1];
            //}
            //_oilSaturationPerWell[^1] /= 8;

            totalTime += time;

            if (totalTime < _timeEnd * 86400.0)
            {
                Times.Add(totalTime / 86400);
            }
            else
            {
                Times.Add(_timeEnd);
            }

            time -= deltaTime * 86400.0;
        }
        //times.Add((totalTime + time) / 86400.0);
        DataWriter.WritePressure(_path, $"Pressure{_timeMoment}.txt", _fem.Solution!, _mesh);
        DataWriter.WriteSaturation(_path, $"Saturation{_timeMoment}.txt", _mesh, _phaseProperty.Saturation!);
        sw.Stop();

        using var timeWriter = new StreamWriter($"{_path}/TimeModelingSeconds.txt");
        timeWriter.WriteLine(sw.Elapsed.Seconds);
        timeWriter.Close();

        //var satSw = new StreamWriter($"{_path}/OilSaturationPerf.txt");
        //for (int i = 0; i < _oilSaturationPerWell.Count; i++)
        //{
        //    satSw.WriteLine($"{Times[i].ToString(CultureInfo.InvariantCulture)} {_oilSaturationPerWell[i].ToString(CultureInfo.InvariantCulture)}");
        //}
        //satSw.Close();

        var op = new StreamWriter($"{_path}/OilProduced.txt");
        double total = 0.0;
        for (int i = 0; i < _oilProduced.Count; i++)
        {
            total += _oilProduced[i];
            op.WriteLine($"{Times[i].ToString(CultureInfo.InvariantCulture)} {total.ToString(CultureInfo.InvariantCulture)}");
        }
        op.Close();

        var wp = new StreamWriter($"{_path}/WaterProduced.txt");
        total = 0.0;
        for (int i = 0; i < _waterProduced.Count; i++)
        {
            total += _waterProduced[i];
            wp.WriteLine($"{Times[i].ToString(CultureInfo.InvariantCulture)} {total.ToString(CultureInfo.InvariantCulture)}");
        }
        wp.Close();

        var wi = new StreamWriter($"{_path}/WaterInjected.txt");
        total = 0.0;
        for (int i = 0; i < _waterInjected.Count; i++)
        {
            total += _waterInjected[i];
            wi.WriteLine($"{Times[i].ToString(CultureInfo.InvariantCulture)} {total.ToString(CultureInfo.InvariantCulture)}");
        }
        wi.Close();
    }

    private void WriteFlowsTest()
    {
        var isUsed = new bool[_mesh.EdgesCount];
        var exact = new double[_mesh.EdgesCount];

        System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
        using var sw = new StreamWriter("4.csv");
        foreach (var element in _mesh.Elements)
        {
            var edges = element.Edges;
            var iedges = element.EdgesIndices;

            for (int i = 0; i < 4; i++)
            {
                int g = iedges[i];
                var n = _normals[g];
                var p1 = _mesh.Points[edges[i].Node1].Point;
                var p2 = _mesh.Points[edges[i].Node2].Point;
                double l = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));

                if (isUsed[g]) continue;
                isUsed[g] = true;
                if (i is 1 or 2) exact[g] = -l * n.X;
            }
        }

        for (int i = 0; i < exact.Length; i++)
        {
            if (i == 0) sw.WriteLine("$i$, $V^{'*}$, $V^{'}$");
            double flow = Math.Abs(_flows[i]) < 1E-14 ? 0.0 : _flows[i];
            sw.WriteLine($"{i + 1}, {exact[i]}, {flow}");
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
                        _flowsOutPhases[globalEdge, phaseIndex] = phaseFraction * _flows[globalEdge];
                    }
                }
            }
        }
    }

    private void CalculateDeltaT(double deltaT0)
    {
        _deltaT = deltaT0;
        _abandonH.Clear();

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
                    _abandonH.Add((ielem, iphase));
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

        // Form the set abandon for the elements of which the pushing procedure will be carried out
        //using var sww = new StreamWriter("Output/CheckMixEnough.txt");
        //sww.WriteLine($"{"Element", 7} {"Phase", 5} {"Out volume", 14} {"Exist volume", 14}");

        foreach (var (ielem, iphase) in _abandonH)
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
                //sww.WriteLine($"{ielem, 7} {iphase, 5} {phaseVolumeOut:F14} {existingVolume:F14}");
            }
        }
    }

    private void CalculateVolumesOutPhases()
    {
       _iMissed.Clear();

        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            _iMissed.Add(new HashSet<int>());
        }

        int phasesCount = _phaseProperty.Phases![0].Count;
        double[] phaseVolumes = new double[phasesCount];    // Volumes of the ejected phases
        double[] phasesFractions = new double[phasesCount];
        double[] newVolumes = new double[_volumeOutPhases.GetLength(0)];

        // Forming set _iMissed which will contain phases for which the ejection procedure will be performed
        foreach (var (ielem, iphase) in _abandon)
        {
            _iMissed[ielem].Add(iphase);
        }

        // Or iphase saturation on ielem < min critical saturation
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var saturations = _phaseProperty.Saturation![ielem];

            for (int iphase = 0; iphase < saturations.Count; iphase++)
            {
                if (saturations[iphase] < _saturationMinCrit[iphase])
                {
                    _iMissed[ielem].Add(iphase);
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
            if (_iMissed[ielem].Count == 0)
            {
                for (int localEdge = 0; localEdge < 4; localEdge++)
                {
                    int globalEdge = edges[localEdge];

                    if (FlowDirection(_flows[globalEdge], ielem, localEdge) == 1)
                    {
                        for (int iphase = 0; iphase < phases.Count; iphase++)
                        {
                            _volumeOutPhases[globalEdge, iphase] = Math.Abs(_flowsOutPhases[globalEdge, iphase]) * _deltaT;
                        }

                        if (IsWellEdge(globalEdge))
                        {
                            for (int iphase = 0; iphase < saturations.Count; iphase++)
                            {
                                if (iphase == 0) _waterProduced[^1] += _volumeOutPhases[globalEdge, iphase];
                                if (iphase == 1) _oilProduced[^1] += _volumeOutPhases[globalEdge, iphase];
                            }
                        }
                    }
                }

                continue;
            }

            foreach (var iphase in _iMissed[ielem])
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
                if (!_iMissed[ielem].Contains(iphase))
                {
                    notMissedPhases += phasesFractions[iphase];
                }
            }

            // New fraction of phases that will not be ejected
            for (int iphase = 0; iphase < phases.Count; iphase++)
            {
                if (!_iMissed[ielem].Contains(iphase))
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
                        if (_iMissed[ielem].Contains(iphase))
                        {
                            _volumeOutPhases[globalEdge, iphase] = Math.Abs(_flows[globalEdge] / flowOut) * phaseVolumes[iphase];
                        }
                        else
                        {
                            _volumeOutPhases[globalEdge, iphase] = phasesFractions[iphase] * newVolumes[globalEdge];
                        }
                    }

                    if (IsWellEdge(globalEdge))
                    {
                        for (int iphase = 0; iphase < saturations.Count; iphase++)
                        {
                            if (iphase == 0) _waterProduced[^1] += _volumeOutPhases[globalEdge, iphase];
                            if (iphase == 1) _oilProduced[^1] += _volumeOutPhases[globalEdge, iphase];
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
                    _waterInjected[^1] += _volumeOutPhases[globalEdge, phaseIndex];
                }
            }
        }
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