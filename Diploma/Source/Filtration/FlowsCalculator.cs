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
        _averageFlows = new Vector(_mesh.EdgesCount);
        _flowsBalancer = new FlowsBalancer(mesh, edgesDirect);
    }

    private int FlowDirection(double flow, int ielem, int iedge)
    {
        //flow = Math.Abs(flow) < 1E-10 ? 0.0 : flow;
        
        return Math.Sign(flow) switch
        {
            0 => 0,
            > 0 => _edgesDirect[ielem, iedge],
            _ => -_edgesDirect[ielem, iedge]
        };   
    }

    private void FixKnownFlows()
    {
        // // Flow from wells
        // foreach (var (ielem, iedge, flow) in _mesh.NeumannConditions)
        // {
        //     double coefficient = 0.0;
        //
        //     if (flow < 0) coefficient += _phaseProperty.Phases![ielem].Sum(phase => phase.Kappa / phase.Viscosity);
        //     else  coefficient += _phaseProperty.InjectedPhases!.Sum(phase => phase.Kappa / phase.Viscosity);
        //
        //     //coefficient *= _mesh.Materials[_mesh.Elements[ielem].Area].Permeability;
        //     
        //     int globalEdge = _mesh.Elements[ielem].EdgesIndices[iedge];
        //     var edge = _mesh.Elements[ielem].Edges[iedge];
        //     var p1 = _mesh.Points[edge.Node1].Point;
        //     var p2 = _mesh.Points[edge.Node2].Point;
        //     double length = Point2D.Distance(p1, p2);
        //
        //     _averageFlows[globalEdge] = -flow * length * coefficient;
        // }
        
        // Almost zero flows
        for (int i = 0; i < _averageFlows.Length; i++)
        {
            if (Math.Abs(_averageFlows[i]) < 1E-10)
            {
                _averageFlows[i] = 0.0;
            }
        }
    }
    
    public Vector CalculateAverageFlows(Vector pressure)
    {
        bool[] isUsed = new bool[_averageFlows.Length]; 
        double lambdaE = 0.0, lambdaK;

        foreach (var element in _mesh.Elements)
        {
            var nodes = element.Nodes;
            var edges = element.Edges;
            var edgesIndices = element.EdgesIndices;

            Point2D[] elementPoints =
            {
                _mesh.Points[nodes[0]].Point,
                _mesh.Points[nodes[1]].Point,
                _mesh.Points[nodes[2]].Point,
                _mesh.Points[nodes[3]].Point
            };

            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                double fixedVar = localEdge is 0 or 3 ? 1 : 0;
                double opposite = localEdge is 0 or 1 ? 0 : 1;

                var globalEdge = edgesIndices[localEdge];
                var edge = edges[localEdge];
                var normal = _normals[globalEdge];
                var p1 = _mesh.Points[edge.Node1].Point;
                var p2 = _mesh.Points[edge.Node2].Point;
                double lenght = Point2D.Distance(p1, p2);

                double ScalarFunction(double ksi)
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

                double flow = -_gauss.Integrate1D(ScalarFunction, _masterInterval);

                if (isUsed[globalEdge])
                {
                    lambdaK = _mesh.Materials[element.Area].Permeability;
                    _averageFlows[globalEdge] = lambdaK / (lambdaK + lambdaE) * _averageFlows[globalEdge] +
                                                lambdaE / (lambdaE + lambdaK) * flow;
                }
                else
                {
                    lambdaE = _mesh.Materials[element.Area].Permeability;
                    _averageFlows[globalEdge] = flow;
                    isUsed[globalEdge] = true;
                }
            }
        }

        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            double coefficient = _phaseProperty.Phases![ielem].Sum(phase => phase.Kappa / phase.Viscosity);
        
            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                double flow = _averageFlows[_mesh.Elements[ielem].EdgesIndices[localEdge]];
        
                if (FlowDirection(flow, ielem, localEdge) == 1)
                {
                    int globalEdge = _mesh.Elements[ielem].EdgesIndices[localEdge];
                    _averageFlows[globalEdge] *= coefficient;
                }
            }
        }

        foreach (var (ielem, iedge, _) in _mesh.NeumannConditions)
        {
            int globalEdge = _mesh.Elements[ielem].EdgesIndices[iedge];
            //_averageFlows[globalEdge] *= _mesh.Materials[_mesh.Elements[ielem].Area].Permeability;
        
            if (FlowDirection(_averageFlows[globalEdge], ielem, iedge) == -1)
            {
                double coefficient = _phaseProperty.InjectedPhases!.Sum(phase => phase.Kappa / phase.Viscosity);
                _averageFlows[globalEdge] *= coefficient;
            }
        }
        
        // // НАДО ЛИ ЭТО ДЕЛАТЬ?????
        foreach (var (ielem, iedge) in _mesh.RemoteEdges)
        {
            int globalEdge = _mesh.Elements[ielem].EdgesIndices[iedge];
            //_averageFlows[globalEdge] *= _mesh.Materials[_mesh.Elements[ielem].Area].Permeability;
        
            if (FlowDirection(_averageFlows[globalEdge], ielem, iedge) == -1)
            {
                double coefficient = _phaseProperty.RemoteBordersPhases!.Sum(phase => phase.Kappa / phase.Viscosity);
                _averageFlows[globalEdge] *= coefficient;
            }
        }

        FixKnownFlows();
        //_flowsBalancer.BalanceFlows(_averageFlows);
        
        return _averageFlows;
    }
}