using System.Windows.Shell;

namespace Diploma.Source.Filtration;

public class FlowsBalancer
{
    private readonly Mesh.Mesh _mesh;
    private readonly SparseMatrix _globalMatrix;
    private readonly Vector _globalVector;
    private readonly double[] _beta;
    private readonly double[] _alpha;
    private readonly double[] _alphaAbs;
    private readonly DirectSolver _solver;
    
    public double MaxNonBalance { get; set; }
    public int MaxBalanceIters { get; set; }
    
    public FlowsBalancer(Mesh.Mesh mesh)
    {
        _mesh = mesh;
        
        MaxBalanceIters = 100;
        MaxNonBalance = 1E-09;
        
        PortraitBuilder.PortraitByEdges(_mesh, out int[] ig, out int[] jg);
        _globalMatrix = new SparseMatrix(ig.Length - 1, jg.Length)
        {
            Ig = ig,
            Jg = jg
        };

        _globalVector = new Vector(ig.Length - 1);

        _beta = new double[_mesh.Elements.Length].Select(beta => 1E+10).ToArray();
        _alpha = new double[_mesh.Elements[^1].Edges[^1] + 1];
        _alphaAbs = new double[_mesh.Elements[^1].Edges[^1] + 1].Select(alpha => 1.0).ToArray();

        _solver = new LUSolver();
    }

    public void BalanceFlows(Vector averageFlows)
    {
        int edgesCount = _alpha.Length;

        for (int i = 0; i < edgesCount; i++)
        {
            _alpha[i] = _alphaAbs[i] / Math.Abs(averageFlows[i]);
        }
        
        AssemblyGlobalMatrix();
        AssemblyGlobalVector(averageFlows);
        _solver.SetSystem(_globalMatrix.ToProfileMatrix(), _globalVector);
        _solver.Compute();
        
    }

    private void AddToGlobal(int i, int j, double value)
    {
        if (i == j)
        {
            _globalMatrix.Di[i] += value;
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

        for (int i = 0; i < _globalMatrix.Size; i++)
        {
            _globalMatrix.Di[i] += _alpha[i];
        }
    }

    private void AssemblyGlobalVector(Vector flows)
    {
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var edges = _mesh.Elements[ielem].Edges;
            var edgesDirect = _mesh.Elements[ielem].EdgesDirect;
            double imbalance = 0.0;
            
            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                imbalance += edgesDirect[localEdge] * flows[edges[localEdge]];
            }
            
            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                _globalVector[edges[localEdge]] -= _beta[ielem] * edgesDirect[localEdge] * imbalance;
            }
        }
    }

    private double CalculateElementImbalance(Vector flows, int element)
    {
        double imbalance = 0.0;
        var edges = _mesh.Elements[element].Edges;

        for (int localEdge = 0; localEdge < edges.Count; localEdge++)
        {
            imbalance += flows[edges[localEdge]];
        }

        return imbalance;
    }
}