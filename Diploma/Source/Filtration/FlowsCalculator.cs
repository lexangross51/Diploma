namespace Diploma.Source.Filtration;

public class FlowsCalculator
{
    private readonly Mesh.Mesh _mesh;
    private readonly IBasis _basis;
    private readonly PhaseProperty _phaseProperty;
    private readonly Vector _averageFlows;
    private readonly Vector _precalcIntegralGradX;
    private readonly Vector _precalcIntegralGradY;
    private readonly int[] _normals = { 1, 1, 1, 1 };

    public FlowsCalculator(Mesh.Mesh mesh, IBasis basis, PhaseProperty phaseProperty)
    {
        _mesh = mesh;
        _basis = basis;
        _phaseProperty = phaseProperty;
        
        var edgesCount = mesh.Elements[^1].Edges[^1] + 1;

        _averageFlows = new Vector(edgesCount);
        _precalcIntegralGradX = new Vector(_basis.Size);
        _precalcIntegralGradY = new Vector(_basis.Size);
        
        Interval omega = new(0, 1);
        Integration gauss = new(Quadratures.GaussOrder5());

        for (int i = 0; i < _basis.Size; i++)
        {
            double DPhi(double etta) => _basis.DPhi(i, 0, new Point2D(0, etta));

            _precalcIntegralGradX[i] = gauss.Integrate1D(DPhi, omega);
        }
        
        for (int i = 0; i < _basis.Size; i++)
        {
            double DPhi(double ksi) => _basis.DPhi(i, 1, new Point2D(ksi, 0));

            _precalcIntegralGradY[i] = gauss.Integrate1D(DPhi, omega);
        }
    }

    private int FlowDirection(double flow, int ielem, int iedge)
        => Math.Sign(flow) switch
        {
            0 => 0,
            > 0 => _mesh.Elements[ielem].EdgesDirect[iedge],
            _ => -_mesh.Elements[ielem].EdgesDirect[iedge]
        };

    private double CalculateGradient(Vector pressure, int element, int localEdge)
    {
        var nodes = _mesh.Elements[element].Nodes;

        double hx = _mesh.Points[nodes[^1]].X - _mesh.Points[nodes[0]].X;
        double hy = _mesh.Points[nodes[^1]].Y - _mesh.Points[nodes[0]].Y;
        double gradient = 0.0;
        
        // Left or right border
        if (localEdge is 1 or 2)
        {
            for (int i = 0; i < _basis.Size; i++)
            {
                gradient += pressure[nodes[i]] * _precalcIntegralGradX[i];
            }

            return gradient * hy / hx;
        }

        // Lower or upper border
        for (int i = 0; i < _basis.Size; i++)
        {
            gradient += pressure[nodes[i]] * _precalcIntegralGradY[i];
        }

        return gradient * hx / hy;
    }
    
    private double CalculateCoefficient(int ielem)
    {
        int area = _mesh.Elements[ielem].Area;
        double coefficient = 0.0;

        int phaseCount = _phaseProperty.Phases![ielem].Count;

        for (int i = 0; i < phaseCount; i++)
        {
            coefficient += _phaseProperty.Phases[ielem][i].Kappa;
            coefficient /= _phaseProperty.Phases[ielem][i].Viscosity;
        }
            
        coefficient *= _mesh.Materials[area].Permeability * 9.86923E-04;

        return coefficient;
    }
    
    private bool IsWellElement(int ielem)
        => Enumerable.Any(_mesh.NeumannConditions, condition => condition.Element == ielem);

    private void FixWellsFlows()
    {
        foreach (var (ielem, flow) in _mesh.NeumannConditions)
        {
            var edges = _mesh.Elements[ielem].Edges;
            var edgesDirect = _mesh.Elements[ielem].EdgesDirect;

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                _averageFlows[edges[localEdge]] = edgesDirect[localEdge] * flow;
            }
        }
    }
    
    public Vector CalculateAverageFlows(Vector pressure)
    {
        List<bool> isUsedEdge = new(_averageFlows.Length);

        for (int i = 0; i < _averageFlows.Length; i++)
        {
            isUsedEdge.Add(false);
        }

        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            if (IsWellElement(ielem)) continue;

            double coefficient = CalculateCoefficient(ielem);
            var edges = _mesh.Elements[ielem].Edges;

            for (int localEdge = 0; localEdge < edges.Count; localEdge++)
            {
                var globalEdge = edges[localEdge];

                double flow = -CalculateGradient(pressure, ielem, localEdge) * _normals[localEdge];

                if (FlowDirection(flow, ielem, localEdge) == 1)
                {
                    flow *= coefficient;
                }

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
        
        FixWellsFlows();
        
        return _averageFlows;
    }
}