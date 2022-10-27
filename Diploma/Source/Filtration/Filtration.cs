namespace Diploma.Source.Filtration;

public class Filtration
{
    private readonly Mesh.Mesh _mesh;
    private readonly IBasis _basis;
    private readonly FEMBuilder.Fem _fem;
    private readonly Vector _averageFlows;
    private readonly Vector _precalcIntegralGradX;
    private readonly Vector _precalcIntegralGradY;
    private readonly int[] _normals = new[] { 1, 1, 1, 1 };
    
    public double MaxNonBalance { get; set; }
    public int MaxBalanceIters { get; set; }

    public Filtration(Mesh.Mesh mesh, FEMBuilder.Fem fem, IBasis basis)
    {
        _mesh = mesh;
        _basis = basis;
        _fem = fem;

        var edgesCount = _mesh.Elements[^1].Edges[^1] + 1;
        _averageFlows = new Vector(edgesCount);
        _precalcIntegralGradX = new Vector(_basis.Size);
        _precalcIntegralGradY = new Vector(_basis.Size);
        
        Interval omega = new(0, 1);
        Integration gauss = new(Quadratures.GaussOrder3());

        for (int i = 0; i < _basis.Size; i++)
        {
            double DPhi(double etta) => _basis!.DPhi(i, 0, new Point2D(0, etta));

            _precalcIntegralGradX[i] = gauss.Integrate1D(DPhi, omega);
        }
        
        for (int i = 0; i < _basis.Size; i++)
        {
            double DPhi(double ksi) => _basis!.DPhi(i, 1, new Point2D(ksi, 0));

            _precalcIntegralGradY[i] = gauss.Integrate1D(DPhi, omega);
        }
    }

    public void ModelFlows()
    {
        _fem.Solve();
        
        CalculateAverageFlows();
    }

    private double CalculateGradient(int element, int localEdge)
    {
        var nodes = _mesh.Elements[element].Nodes;

        double hx = _mesh.Points[nodes[^1]].X - _mesh.Points[nodes[0]].X;
        double hy = _mesh.Points[nodes[^1]].Y - _mesh.Points[nodes[0]].Y;
        double gradient = 0.0;
        
        // Левая или правая граница
        if (localEdge is 1 or 2)
        {
            for (int i = 0; i < _basis.Size; i++)
            {
                gradient += _fem.Solution!.Value[nodes[i]] * _precalcIntegralGradX[i];
            }

            return gradient * hy / hx;
        }

        // Нижняя или верхняя граница
        for (int i = 0; i < _basis.Size; i++)
        {
            gradient += _fem.Solution!.Value[nodes[i]] * _precalcIntegralGradY[i];
        }

        return gradient * hx / hy;
    }
    
    private void CalculateAverageFlows()
    {
        List<bool> isUsedEdge = new(_averageFlows.Length);

        for (int i = 0; i < _averageFlows.Length; i++)
        {
            isUsedEdge.Add(false);
        }

        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var area = _mesh.Elements[ielem].Area;
            var permeability = _mesh.Materials[area].Permeability;
            var nodes = _mesh.Elements[ielem].Nodes;
            var edges = _mesh.Elements[ielem].Edges;

            double hx = _mesh.Points[nodes[^1]].X - _mesh.Points[nodes[0]].X;
            double hy = _mesh.Points[nodes[^1]].Y - _mesh.Points[nodes[0]].Y;

            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                var globalEdge = edges[localEdge];

                double flow = - permeability * CalculateGradient(ielem, localEdge) * _normals[localEdge];

                if (isUsedEdge[globalEdge])
                {
                    _averageFlows[globalEdge] = (_averageFlows[globalEdge] + flow) / 2.0;
                }
                else
                {
                    _averageFlows[globalEdge] = flow;
                    isUsedEdge[globalEdge] = true;
                }
            }
        }
    }

    private void BalanceFlows()
    {
        
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