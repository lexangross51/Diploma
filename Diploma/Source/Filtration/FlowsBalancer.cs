namespace Diploma.Source.Filtration;

public class FlowsBalancer
{
    private readonly Mesh.Mesh _mesh;
    private readonly SparseMatrix _globalMatrix;
    private readonly Vector _globalVector;
    private readonly Vector _deltaQ;
    private readonly bool[] _flowsNulls;
    private readonly double[] _beta;
    private readonly double[] _alpha;
    private readonly DirectSolver _solver;
    private readonly IterativeSolver _solve = new LOS(100, 1E-20);
    private readonly double _maxImbalance;
    private readonly int _maxBalanceIters;
    private readonly int[,] _edgesDirect;

    public FlowsBalancer(Mesh.Mesh mesh, int[,] edgesDirect)
    {
        _mesh = mesh;
        _edgesDirect = edgesDirect;
        
        _maxBalanceIters = 100;
        _maxImbalance = 1E-07;
        
        PortraitBuilder.PortraitByEdges(_mesh, out int[] ig, out int[] jg);
        _globalMatrix = new SparseMatrix(ig.Length - 1, jg.Length)
        {
            Ig = ig,
            Jg = jg
        };

        _globalVector = new Vector(ig.Length - 1);
        _deltaQ = new Vector(ig.Length - 1);
        _flowsNulls = new bool[ig.Length - 1];

        _beta = new double[_mesh.Elements.Length];
        _alpha = new double[_mesh.Elements[^1].EdgesIndices[^1] + 1];

        _solver = new LUSolver();
    }
    
    public void BalanceFlows(Vector flows)
    {
        Array.Fill(_flowsNulls, false, 0, _flowsNulls.Length);
        Array.Fill(_alpha, 0.0, 0, _alpha.Length);
        Array.Fill(_beta, 1E-05, 0, _beta.Length);

        for (int i = 0; i < flows.Length; i++)
        {
            if (Math.Abs(flows[i]) < 1E-10)
            {
                _flowsNulls[i] = true;
            }
        }
        
        bool isBalanced = true;
        int iteration = 0;
        double maxFlow = MaxFlow(flows);

        // It's possible the predetermined level of imbalance has already been reached
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            if (ElementImbalance(ielem, flows) / maxFlow > _maxImbalance)
            {
                isBalanced = false;
            }
        }
        
        #region Print imbalances

        using (var sw = new StreamWriter("Output/imbalances.txt"))
        {
            for (int i = 0; i < _mesh.Elements.Length; i++)
            {
                var imbalance = ElementImbalance(i, flows);
                var strImb = i + ": " + imbalance;

                if (IsWellElement(i)) strImb += "(Near-well)";
                
                sw.WriteLine(strImb);
            }
        }

        #endregion

        CalculateAlpha(flows);

        while (!isBalanced && iteration < _maxBalanceIters)
        {
            AssemblyGlobalMatrix();
            AssemblyGlobalVector(flows);
            FixWellsFlows();
            //_globalMatrix.PrintDense("GlobalMatrix.txt");
            // _solver.SetSystem(_globalMatrix, _globalVector);
            // _solver.Compute();
            _solve.SetSystem(_globalMatrix, _globalVector);
            _solve.Compute();
            Vector.Copy(_solve.Solution!, _deltaQ);

            bool isNullImbalance = true;

            for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
            {
                double imbalance = ElementImbalance(ielem, flows);

                if (imbalance / maxFlow > _maxImbalance)
                {
                    _beta[ielem] *= 10.0;
                    isNullImbalance = false;
                }
            }

            #region Print imbalances

            using (var sw = new StreamWriter("Output/imbalances.txt"))
            {
                for (int i = 0; i < _mesh.Elements.Length; i++)
                {
                    var imbalance = ElementImbalance(i, flows);
                    var strImb = i + ": " + imbalance;

                    if (IsWellElement(i)) strImb += "(Near-well)";
                
                    sw.WriteLine(strImb);
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

    private void ElementsByEdge(int globalEdge, out (int elem1, int elem2) elements)
    {
        int counter = 0;
        elements = (-1, -1);

        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var edges = _mesh.Elements[ielem].EdgesIndices;
            
            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                if (edges[localEdge] == globalEdge)
                {
                    counter++;

                    if (counter == 1) elements.elem1 = ielem;
                    if (counter == 2) elements.elem2 = ielem;

                    break;
                }
            }

            if (counter == 2) break;
        }
    }

    private double ElementImbalance(int ielem, Vector flows)
    {
        var edges = _mesh.Elements[ielem].EdgesIndices;
        double imbalance = 0.0;

        for (int localEdge = 0; localEdge < 4; localEdge++)
        {
            int globalEdge = edges[localEdge];
            int flowDirection = FlowDirection(flows[globalEdge], ielem, localEdge);

            imbalance += flowDirection * Math.Abs(flows[globalEdge]) + _edgesDirect[ielem, localEdge] * _deltaQ[globalEdge];
        }

        return Math.Abs(imbalance);
    }
    
    private void CalculateAlpha(Vector flows)
    {
        int edgesCount = _alpha.Length;

        for (int globalEdge = 0; globalEdge < edgesCount; globalEdge++)
        {
            ElementsByEdge(globalEdge, out var elements);

            // Edge belongs to only 1 element
            if (elements.elem2 == -1)
            {
                double elementImbalance = ElementImbalance(elements.elem1, flows);
                _alpha[globalEdge] = elementImbalance < 1E-14 ? 1.0 : 1.0 / elementImbalance;
            }
            // Edge belongs to 2 elements
            else
            {
                double elementImbalance1 = ElementImbalance(elements.elem1, flows);
                double elementImbalance2 = ElementImbalance(elements.elem2, flows);

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

    private int FlowDirection(double flow, int ielem, int iedge)
        => Math.Sign(flow) switch
        {
            0 => 0,
            > 0 => _edgesDirect[ielem, iedge],
            _ => -_edgesDirect[ielem, iedge]
        };

    private bool IsWellElement(int ielem)
        => _mesh.NeumannConditions.Any(condition => condition.Element == ielem);

    private double MaxFlow(Vector flows)
    {
        double max = 0.0;

        foreach (var element in _mesh.Elements)
        {
            var edges = element.EdgesIndices;

            for (int j = 0; j < 4; j++)
            {
                if (Math.Abs(flows[edges[j]]) > max) max = Math.Abs(flows[edges[j]]);
            }
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
            var edges = _mesh.Elements[ielem].EdgesIndices;

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    double value = _beta[ielem] * _edgesDirect[ielem, i] * _edgesDirect[ielem, j];
                    
                    AddToGlobal(edges[i], edges[j], value);
                }
            }
        }

        for (int i = 0; i < _alpha.Length; i++)
        {
            if (!_flowsNulls[i])
            {
                _globalMatrix.Di[i] += _alpha[i];
            }
        }
    }

    private void AssemblyGlobalVector(Vector flows)
    {
        _globalVector.Fill();
        
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var edges = _mesh.Elements[ielem].EdgesIndices;
            double imbalance = 0.0;

            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                imbalance += _edgesDirect[ielem, localEdge] * flows[edges[localEdge]];
            }

            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                _globalVector[edges[localEdge]] -= _beta[ielem] * _edgesDirect[ielem, localEdge] * imbalance;
            }
        }
    }

    private void FixWellsFlows()
    {
        foreach (var (ielem, iedge, _) in _mesh.NeumannConditions)
        {
            var edges = _mesh.Elements[ielem].EdgesIndices;
            int globalEdge = edges[iedge];
        
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

        for (int globalEdge = 0; globalEdge < _flowsNulls.Length; globalEdge++)
        {
            if (_flowsNulls[globalEdge])
            {
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
            var edges = _mesh.Elements[i].EdgesIndices;
            
            string dirs = string.Empty;
            
            for (int j = 0; j < 4; j++)
            {
                dirs += FlowDirection(flows[edges[j]], i, j) + "   ";
            }

            if (IsWellElement(i)) dirs += $"NB - {i}(Near-well)\n";
            else dirs += $"NB - {i}\n";
            
            for (int j = 0; j < 4; j++)
            {
                dirs += FlowDirection(tmpFlows[edges[j]], i, j) + "   ";
            }

            dirs += "B\n";
            dirs += "------------------------------";
            
            sw.WriteLine(dirs);
        }
    }
}