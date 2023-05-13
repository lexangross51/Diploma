#define DEBUGGIN

namespace Diploma.Source.Filtration;

public class FlowsBalancer
{
    private readonly Mesh.Mesh _mesh;
    private readonly SparseMatrix _globalMatrix;
    private readonly double[] _globalVector;
    private readonly double[] _deltaQ;
    private readonly bool[] _flowsNulls;
    private readonly double[] _beta;
    private readonly double[] _alpha;
    private const double MaxImbalance = 1E-08;
    private const int MaxBalanceIters = 100;
    private readonly int[,] _edgesDirect;
    private readonly Dictionary<int, List<int>> _elementsByEdge;

    public FlowsBalancer(Mesh.Mesh mesh, int[,] edgesDirect)
    {
        _mesh = mesh;
        _edgesDirect = edgesDirect;

        PortraitBuilder.PortraitByEdges(_mesh, out int[] ig, out int[] jg);
        _globalMatrix = new SparseMatrix(ig.Length - 1, jg.Length)
        {
            Ig = ig,
            Jg = jg
        };

        _globalVector = new double[ig.Length - 1];
        _deltaQ = new double[ig.Length - 1];
        _flowsNulls = new bool[ig.Length - 1];

        _beta = new double[_mesh.ElementsCount];
        _alpha = new double[_mesh.EdgesCount];
        Array.Fill(_beta, 1e+10);

        _elementsByEdge = new Dictionary<int, List<int>>();

        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            for (int iedge = 0; iedge < 4; iedge++)
            {
                int globalEdge = _mesh.Elements[ielem].EdgesIndices[iedge];

                if (!_elementsByEdge.Keys.Contains(globalEdge))
                {
                    _elementsByEdge.Add(globalEdge, new List<int>());
                }
                _elementsByEdge[globalEdge].Add(ielem);
            }
        }
    }

    private void FixNullsFlows(Vector flows)
    {
        Array.Fill(_flowsNulls, false);

        for (int i = 0; i < flows.Length; i++)
        {
            if (Math.Abs(flows[i]) < 1E-14)
            {
                _flowsNulls[i] = true;
                flows[i] = 0.0;
            }
        }
    }

    private double ElementImbalance(int ielem, Vector flows)
    {
        var edges = _mesh.Elements[ielem].EdgesIndices;
        double imbalance = 0.0;

        for (int localEdge = 0; localEdge < 4; localEdge++)
        {
            int globalEdge = edges[localEdge];
            imbalance += _edgesDirect[ielem, localEdge] * (flows[globalEdge] + _deltaQ[globalEdge]);
        }

        return Math.Abs(imbalance);
    }

    private void CalculateAlpha(Vector flows)
    {
        int edgesCount = _mesh.EdgesCount;

        for (int globalEdge = 0; globalEdge < edgesCount; globalEdge++)
        {
            if (_flowsNulls[globalEdge]) continue;

            // Edge belongs to only 1 element
            if (_elementsByEdge[globalEdge].Count == 1)
            {
                int ielem = _elementsByEdge[globalEdge][0];
                double elementImbalance = ElementImbalance(ielem, flows);
                //_alpha[globalEdge] = elementImbalance < 1E-14 ? 1.0 : 1.0 / elementImbalance;
                _alpha[globalEdge] = 1.0 / elementImbalance;
            }
            // Edge belongs to 2 elements
            else
            {
                int ielem1 = _elementsByEdge[globalEdge][0];
                int ielem2 = _elementsByEdge[globalEdge][1];
                double elementImbalance1 = ElementImbalance(ielem1, flows);
                double elementImbalance2 = ElementImbalance(ielem2, flows);

                // _alpha[globalEdge] = Math.Abs(elementImbalance1) switch
                // {
                //     < MaxImbalance when elementImbalance2 > MaxImbalance => 1.0 / elementImbalance2,
                //     < MaxImbalance when elementImbalance2 < MaxImbalance => 1.0,
                //     > MaxImbalance when elementImbalance2 < MaxImbalance => 1.0 / elementImbalance1,
                //     _ => (elementImbalance1 + elementImbalance2) / (2 * elementImbalance1 * elementImbalance2)
                // };
                _alpha[globalEdge] = (elementImbalance1 + elementImbalance2) /
                                     (2 * elementImbalance1 * elementImbalance2);
            }
        }
    }

    private int FlowDirection(double flow, int ielem, int iedge)
    {
        flow = Math.Abs(flow) < 1E-14 ? 0.0 : flow;

        return Math.Sign(flow) switch
        {
            0 => 0,
            > 0 => _edgesDirect[ielem, iedge],
            _ => -_edgesDirect[ielem, iedge]
        };
    }

    private bool IsWellElement(int ielem)
        => _mesh.NeumannConditions.Any(condition => condition.Element == ielem);

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
        Array.Fill(_globalVector, 0.0);

        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var edges = _mesh.Elements[ielem].EdgesIndices;
            double imbalance = 0.0;

            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                var globalEdge = edges[localEdge];
                var flowDirection = FlowDirection(flows[globalEdge], ielem, localEdge);

                imbalance += flowDirection * Math.Abs(flows[edges[localEdge]]);
            }

            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                _globalVector[edges[localEdge]] -= _beta[ielem] * _edgesDirect[ielem, localEdge] * imbalance;
            }
        }
    }

    private void FixKnownFlows()
    {
        var bc2 = new int[_mesh.EdgesCount].Select(_ => -1).ToArray();
        var neumann = _mesh.NeumannConditions;

        for (int i = 0; i < neumann.Length; i++)
        {
            int globalEdge = _mesh.Elements[neumann[i].Element].EdgesIndices[neumann[i].Edge];
            bc2[globalEdge] = i;
        }

        for (int i = 0; i < _mesh.EdgesCount; i++)
        {
            int k;
            if (bc2[i] != -1)
            {
                _globalMatrix.Di[i] = 1.0;
                _globalVector[i] = 0.0;

                for (int j = _globalMatrix.Ig[i]; j < _globalMatrix.Ig[i + 1]; j++)
                {
                    k = _globalMatrix.Jg[j];
                    if (bc2[k] == -1)
                    {
                        _globalVector[k] -= _globalMatrix.GGl[j] * _globalVector[i];
                    }
                    _globalMatrix.GGl[j] = 0.0;
                    _globalMatrix.GGu[j] = 0.0;
                }
            }
            else
            {
                for (int j = _globalMatrix.Ig[i]; j < _globalMatrix.Ig[i + 1]; j++)
                {
                    k = _globalMatrix.Jg[j];
                    if (bc2[k] != -1)
                    {
                        _globalVector[i] -= _globalMatrix.GGl[j] * _globalVector[k];
                        _globalMatrix.GGl[j] = 0.0;
                        _globalMatrix.GGu[j] = 0.0;
                    }
                }
            }
        }

        bc2 = bc2.Select(_ => -1).ToArray();
        for (int i = 0; i < _flowsNulls.Length; i++)
        {
            if (_flowsNulls[i])
                bc2[i] = i;
        }

        for (int i = 0; i < _mesh.EdgesCount; i++)
        {
            int k;
            if (bc2[i] != -1)
            {
                _globalMatrix.Di[i] = 1.0;
                _globalVector[i] = 0.0;

                for (int j = _globalMatrix.Ig[i]; j < _globalMatrix.Ig[i + 1]; j++)
                {
                    k = _globalMatrix.Jg[j];
                    if (bc2[k] == -1)
                    {
                        _globalVector[k] -= _globalMatrix.GGl[j] * _globalVector[i];
                    }
                    _globalMatrix.GGl[j] = 0.0;
                    _globalMatrix.GGu[j] = 0.0;
                }
            }
            else
            {
                for (int j = _globalMatrix.Ig[i]; j < _globalMatrix.Ig[i + 1]; j++)
                {
                    k = _globalMatrix.Jg[j];
                    if (bc2[k] != -1)
                    {
                        _globalVector[i] -= _globalMatrix.GGl[j] * _globalVector[k];
                        _globalMatrix.GGl[j] = 0.0;
                        _globalMatrix.GGu[j] = 0.0;
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

            for (int k = 0; k < 4; k++)
            {
                if (FlowDirection(flows[edges[k]], i, k) != FlowDirection(tmpFlows[edges[k]], i, k))
                {
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

                    break;
                }
            }
        }
    }

    public void BalanceFlows(Vector flows)
    {
        FixNullsFlows(flows);

        Array.Fill(_alpha, 0.0, 0, _alpha.Length);

        bool isBalanced = true;
        int iteration = 0;
        double maxFlow = flows.Select(Math.Abs).Prepend(0.0).Max();

        // It's possible the predetermined level of imbalance has already been reached
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            if (ElementImbalance(ielem, flows) / maxFlow > MaxImbalance)
            {
                isBalanced = false;
            }
        }

        #region Print imbalances

#if DEBUGGING
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
#endif

        #endregion

        CalculateAlpha(flows);

        while (!isBalanced && iteration < MaxBalanceIters)
        {
            AssemblyGlobalMatrix();
            AssemblyGlobalVector(flows);
            FixKnownFlows();

            PardisoSolver.Solve(
                _globalMatrix.Size, _globalMatrix.Ig, _globalMatrix.Jg, _globalMatrix.Di, _globalMatrix.GGl,
                _globalVector, _deltaQ, 1);

            bool isNullImbalance = true;

            for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
            {
                double imbalance = ElementImbalance(ielem, flows);

                if (imbalance / maxFlow > MaxImbalance)
                {
                    _beta[ielem] *= 10.0;
                    isNullImbalance = false;
                }
            }

            #region Print imbalances

#if DEBUGGING
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
#endif

            #endregion

            if (isNullImbalance) isBalanced = true;
            iteration++;
        }

        if (!isBalanced) return;

#if DEBUGGING
        CheckFlowsDirection(flows);  
#endif

        for (int i = 0; i < _deltaQ.Length; i++)
        {
            flows[i] += _deltaQ[i];
        }
    }
}