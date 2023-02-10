namespace Diploma.Source.Filtration;

public class FlowsCalculator
{
    private readonly Mesh.Mesh _mesh;
    private readonly IBasis _basis;
    private readonly PhaseProperty _phaseProperty;
    private readonly Vector _averageFlows;
    private readonly Matrix _jacobiMatrix;
    private readonly Integration _gauss;
    private readonly Interval _masterInterval;
    private readonly Point2D[] _normals;
    private readonly int[,] _edgesDirect;
    private readonly FlowsBalancer _flowsBalancer;

    public FlowsCalculator(Mesh.Mesh mesh, IBasis basis, PhaseProperty phaseProperty, Point2D[] normals,
        int[,] edgesDirect)
    {
        _mesh = mesh;
        _basis = basis;
        _phaseProperty = phaseProperty;
        _normals = normals;
        _edgesDirect = edgesDirect;
        _jacobiMatrix = new Matrix(2, 2);
        _gauss = new Integration(Quadratures.GaussOrder3());
        _masterInterval = new Interval(0, 1);
        
        var edgesCount = mesh.Elements[^1].EdgesIndices[^1] + 1;

        _averageFlows = new Vector(edgesCount);
        _flowsBalancer = new FlowsBalancer(mesh, edgesDirect);
    }

    private int FlowDirection(double flow, int ielem, int iedge)
        => Math.Sign(flow) switch
        {
            0 => 0,
            > 0 => _edgesDirect[ielem, iedge],
            _ => -_edgesDirect[ielem, iedge]
        };

    private double CalculateCoefficient(int ielem)
    {
        int area = _mesh.Elements[ielem].Area;
        double coefficient = 0.0;

        int phaseCount = _phaseProperty.Phases![ielem].Count;

        for (int i = 0; i < phaseCount; i++)
        {
            coefficient += _phaseProperty.Phases[ielem][i].Kappa / _phaseProperty.Phases[ielem][i].Viscosity;
        }
            
        coefficient *= _mesh.Materials[area].Permeability;

        return coefficient;
    }

    private void FixWellsFlows()
    {
        foreach (var (ielem, iedge, flow) in _mesh.NeumannConditions)
        {
            int globalEdge = _mesh.Elements[ielem].EdgesIndices[iedge];
            var edge = _mesh.Elements[ielem].Edges[iedge];
            var p1 = _mesh.Points[edge.Node1].Point;
            var p2 = _mesh.Points[edge.Node2].Point;

            double edgeLen = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
            
            _averageFlows[globalEdge] = -flow * edgeLen;
        }
    }
    
    public Vector CalculateAverageFlows(Vector pressure)
    {
        bool[] isUsed = new bool[_averageFlows.Length];

        foreach (var element in _mesh.Elements)
        {
            var nodes = element.Nodes;

            Point2D[] elementPoints =
            {
                _mesh.Points[nodes[0]].Point,
                _mesh.Points[nodes[1]].Point,
                _mesh.Points[nodes[2]].Point,
                _mesh.Points[nodes[3]].Point
            };

            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                double fixedVar = localEdge is 0 or 3 ? 0 : 1;
                double opposite = localEdge is 0 or 1 ? 0 : 1;

                var globalEdge = element.EdgesIndices[localEdge];
                var normal = _normals[globalEdge];
                var edge = element.Edges[localEdge];
                var p1 = _mesh.Points[edge.Node1].Point;
                var p2 = _mesh.Points[edge.Node2].Point;
                double lenght = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));

                double ScalarFunc(double ksi)
                {
                    Vector gradP = new(2);
                    Vector matrixGrad = new(2);
                    var point = fixedVar == 0 ? new Point2D(opposite, ksi) : new Point2D(ksi, opposite);

                    MathAddition.JacobiMatrix2D(elementPoints, point, _jacobiMatrix);
                    MathAddition.InvertJacobiMatrix2D(_jacobiMatrix);

                    for (int inode = 0; inode < 4; inode++)
                    {
                        gradP[0] += pressure[nodes[inode]] * _basis.DPhi(inode, 0, point);
                        gradP[1] += pressure[nodes[inode]] * _basis.DPhi(inode, 1, point);
                    }

                    Matrix.Dot(_jacobiMatrix, gradP, matrixGrad);

                    return (matrixGrad[0] * normal.X + matrixGrad[1] * normal.Y) * lenght;
                }

                double flow = -_gauss.Integrate1D(ScalarFunc, _masterInterval);

                if (isUsed[globalEdge])
                {
                    _averageFlows[globalEdge] = (_averageFlows[globalEdge] + flow) / 2.0;
                }
                else
                {
                    _averageFlows[globalEdge] = flow;
                    isUsed[globalEdge] = true;
                }
            }
        }

        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            double coefficient = CalculateCoefficient(ielem);

            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                double flow = _averageFlows[_mesh.Elements[ielem].EdgesIndices[localEdge]];

                if (FlowDirection(flow, ielem, localEdge) == 1)
                {
                    _averageFlows[_mesh.Elements[ielem].EdgesIndices[localEdge]] *= coefficient;
                }
            }
        }
        
        FixWellsFlows();
        
        //_flowsBalancer.BalanceFlows(_averageFlows);
        
        return _averageFlows;
    }
}