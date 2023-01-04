namespace Diploma.Source.Filtration;

public class FlowsBalancer
{
    private readonly Mesh.Mesh _mesh;
    private readonly SparseMatrix _globalMatrix;
    private readonly Vector _globalVector;
    private readonly double[] _deltaQ;
    private readonly double[] _beta;
    private readonly double[] _alpha;
    private readonly DirectSolver _solver;
    private readonly double _maxImbalance;
    private readonly int _maxBalanceIters;
    
    public FlowsBalancer(Mesh.Mesh mesh)
    {
        _mesh = mesh;
        
        _maxBalanceIters = 100;
        _maxImbalance = 1E-10;
        
        PortraitBuilder.PortraitByEdges(_mesh, out int[] ig, out int[] jg);
        _globalMatrix = new SparseMatrix(ig.Length - 1, jg.Length)
        {
            Ig = ig,
            Jg = jg
        };

        _globalVector = new Vector(ig.Length - 1);
        _deltaQ = new double[ig.Length - 1];

        _beta = new double[_mesh.Elements.Length].Select(_ => 1E-05).ToArray();
        _alpha = new double[_mesh.Elements[^1].Edges[^1] + 1];

        _solver = new LUSolver();
    }
    
    public void BalanceFlows(Vector flows)
    {
        Array.Fill(_alpha, 0.0, 0, _alpha.Length);
        Array.Fill(_beta, 1E-05, 0, _beta.Length);
        
        bool isBalanced = true;
        int iteration = 0;
        double maxFlow = MaxFlow(flows);
        
        // It's possible the predetermined level of imbalance has already been reached
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            if (IsWellElement(ielem)) continue;

            if (ElementImbalance(ielem, flows) / maxFlow > _maxImbalance)
            {
                isBalanced = false;
            }
        }

        CalculateAlpha(flows);
        
        #region Print imbalances

        using (var sw = new StreamWriter("Output/imbalances.txt"))
        {
            for (int i = 0; i < _mesh.Elements.Length; i++)
            {
                var imbalance = ElementImbalance(i, flows);
                var strImb = i + ": " + imbalance;

                if (IsWellElement(i)) strImb += "(Well)";
                
                sw.WriteLine(strImb);
            }
        }

        #endregion

        while (!isBalanced && iteration < _maxBalanceIters)
        {
            AssemblyGlobalMatrix();
            AssemblyGlobalVector(flows);
            FixWellsFlows(flows);
            _solver.SetSystem(_globalMatrix, _globalVector);
            _solver.Compute();
            Array.Copy(_solver.Solution!.ToArray(), _deltaQ, _deltaQ.Length);

            bool isNullImbalance = true;

            for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
            {
                if (IsWellElement(ielem)) continue;

                double imbalance = ElementImbalance(ielem, flows);

                if (imbalance / maxFlow > _maxImbalance)
                {
                    _beta[ielem] *= 10.0;
                    isNullImbalance = false;
                }
            }

            #region Print imbalances

            using(var sww = new StreamWriter("Output/imbalances.txt"))
            {
                for (int i = 0; i < _mesh.Elements.Length; i++)
                {
                    var imbalance = ElementImbalance(i, flows);
                    var strImb = i + ": " + imbalance;
                    
                    if (IsWellElement(i)) strImb += "(Well)";
                    
                    sww.WriteLine(strImb);
                }
            }

            #endregion

            if (isNullImbalance) isBalanced = true;
            iteration++;
        }

        if (!isBalanced) return;
        
        CheckFlowsDirection(flows);

        for (int i = 0; i < _deltaQ.Length; i++)
        {
            flows[i] += _deltaQ[i];
        }
    }

    private void ElementsByEdge(int globalEdge, out List<int> elements)
    {
        elements = new List<int>();
        int counter = 0;
        bool flag = false;

        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var edges = _mesh.Elements[ielem].Edges;
            
            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                if (edges[localEdge] == globalEdge)
                {
                    elements.Add(ielem);
                    counter++;

                    if (localEdge is 0 or 1) flag = true;

                    break;
                }
            }

            if (flag) break;
            if (counter == 2) break;
        }
    }

    private double ElementImbalance(int ielem, Vector flows)
    {
        if (IsWellElement(ielem)) return 0.0;

        var edges = _mesh.Elements[ielem].Edges;
        var edgesDirect = _mesh.Elements[ielem].EdgesDirect;
        double imbalance = 0.0;

        for (int localEdge = 0; localEdge < edges.Count; localEdge++)
        {
            int globalEdge = edges[localEdge];
            int flowDirection = FlowDirection(flows, ielem, localEdge);

            imbalance += flowDirection * Math.Abs(flows[globalEdge]) + edgesDirect[localEdge] * _deltaQ[globalEdge];
        }

        return Math.Abs(imbalance);
    }
    
    private void CalculateAlpha(Vector flows)
    {
        int edgesCount = _alpha.Length;

        for (int globalEdge = 0; globalEdge < edgesCount; globalEdge++)
        {
            ElementsByEdge(globalEdge, out var elements);

            if (elements.Count == 1)
            {
                double elementImbalance = ElementImbalance(elements[0], flows);
                _alpha[globalEdge] = elementImbalance < 1E-14 ? 1.0 : 1.0 / elementImbalance;
            }
            else
            {
                double elementImbalance1 = ElementImbalance(elements[0], flows);
                double elementImbalance2 = ElementImbalance(elements[1], flows);

                _alpha[globalEdge] = Math.Abs(elementImbalance1) switch
                {
                    < 1E-14 when Math.Abs(elementImbalance2) > 1E-14 => 1.0 / elementImbalance2,
                    < 1E-14 when Math.Abs(elementImbalance2) < 1E-14 => 1.0,
                    > 1E-14 when Math.Abs(elementImbalance2) < 1E-14 => 1.0 / elementImbalance1,
                    _ => (elementImbalance1 + elementImbalance2) / (2 * elementImbalance1 * elementImbalance2)
                };
            }
        }
    }

    private int FlowDirection(Vector flows, int ielem, int iedge)
    {
        int globalEdge = _mesh.Elements[ielem].Edges[iedge];
        double flow = Math.Abs(flows[globalEdge]) < 1E-14 ? 0.0 : flows[globalEdge]; 

        return flow switch
        {
            0.0 => 0,
            > 0 => _mesh.Elements[ielem].EdgesDirect[iedge],
            _ => -_mesh.Elements[ielem].EdgesDirect[iedge]
        };
    }

    private double MaxFlow(Vector flows)
    {
        double max = 0.0;

        foreach (var element in _mesh.Elements)
        {
            var edges = element.Edges;

            max = edges.Select(globalEdge => Math.Abs(flows[globalEdge])).Prepend(max).Max();
        }

        return max;
    }

    private void AddToGlobal(int i, int j, double value)
    {
        if (i == j)
        {
            _globalMatrix.Di[i] += value;
            return;
        }

        if (i < j)
        {
            for (int idx = _globalMatrix.Ig[j]; idx < _globalMatrix.Ig[j + 1]; idx++)
            {
                if (_globalMatrix.Jg[idx] == i)
                {
                    _globalMatrix.GGu[idx] += value;
                    return;
                }
            }
        }
        else
        {
            for (int idx = _globalMatrix.Ig[i]; idx < _globalMatrix.Ig[i + 1]; idx++)
            {
                if (_globalMatrix.Jg[idx] == j)
                {
                    _globalMatrix.GGl[idx] += value;
                    return;
                }
            }
        }
    }
    
    private void AssemblyGlobalMatrix()
    {
        _globalMatrix.Clear();

        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var edges = _mesh.Elements[ielem].Edges;
            var edgesDirect = _mesh.Elements[ielem].EdgesDirect;

            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = 0; j < edges.Count; j++)
                {
                    double value = _beta[ielem] * edgesDirect[i] * edgesDirect[j];
                    AddToGlobal(edges[i], edges[j], value);
                }
            }
        }

        for (int i = 0; i < _alpha.Length; i++)
        {
            _globalMatrix.Di[i] += _alpha[i];
        }
    }

    private void AssemblyGlobalVector(Vector flows)
    {
        _globalVector.Clear();
        
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var edges = _mesh.Elements[ielem].Edges;
            var edgesDirect = _mesh.Elements[ielem].EdgesDirect;
            double imbalance = 0.0;

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                imbalance += edgesDirect[localEdge] * flows[edges[localEdge]];
            }

            if (IsWellElement(ielem)) imbalance = 0.0;

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                _globalVector[edges[localEdge]] -= _beta[ielem] * edgesDirect[localEdge] * imbalance;
            }
        }
    }

    private void FixWellsFlows(Vector flows)
    {
        foreach (var (ielem, theta) in _mesh.NeumannConditions)
        {
            var edges = _mesh.Elements[ielem].Edges;
            var edgesDirect = _mesh.Elements[ielem].EdgesDirect;

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                int globalEdge = edges[localEdge];
                
                flows[globalEdge] = theta * edgesDirect[localEdge];
                _globalVector[globalEdge] = 0.0;

                for (int k = _globalMatrix.Ig[globalEdge]; k < _globalMatrix.Ig[globalEdge + 1]; k++) 
                {
                    _globalMatrix.GGl[k] = 0.0;
                }

                for (int k = globalEdge + 1; k < _globalMatrix.Size; k++)
                {
                    for (int j = _globalMatrix.Ig[k]; j < _globalMatrix.Ig[k + 1]; j++)
                    {
                        if (_globalMatrix.Jg[j] == globalEdge)
                        {
                            _globalMatrix.GGu[j] = 0.0;
                        }
                    }
                }
            }
        }
    }

    private bool IsWellElement(int ielem)
        => Enumerable.Any(_mesh.NeumannConditions, condition => condition.Element == ielem);

    private void CheckFlowsDirection(Vector flows)
    {
        using var sw = new StreamWriter("Output/CheckDirection.txt");

        Vector tmpFlows = new(flows.Length);
        Vector.Copy(flows, tmpFlows);
        
        for (int i = 0; i < _deltaQ.Length; i++)
        {
            tmpFlows[i] += _deltaQ[i];
        }
        
        for (int i = 0; i < _mesh.Elements.Length; i++)
        {
            string dirs = string.Empty;
            
            for (int j = 0; j < 4; j++)
            {
                dirs += FlowDirection(flows, i, j) + "   ";
            }

            if (IsWellElement(i)) dirs += $"NB - {i}(Well)\n";
            else dirs += $"NB - {i}\n";
            
            for (int j = 0; j < 4; j++)
            {
                dirs += FlowDirection(tmpFlows, i, j) + "   ";
            }

            dirs += "B\n";
            dirs += "------------------------------";
            
            sw.WriteLine(dirs);
        }
    }
}