namespace Diploma.Source.Filtration;

public class FlowsBalancer
{
    private readonly Mesh.Mesh _mesh;
    private readonly SparseMatrix _globalMatrix;
    private readonly Vector _globalVector;
    private readonly double[] _beta;
    private readonly double[] _alpha;
    
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

        _beta = new double[_mesh.Elements.Length];
        _alpha = new double[_mesh.Elements[^1].Edges[^1] + 1];
    }

    public void BalanceFlows(Vector averageFlows)
    {
        
    }
}