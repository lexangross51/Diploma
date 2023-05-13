namespace Diploma.Source.Filtration;

public class FlowsCalculator
{
    private readonly Mesh.Mesh _mesh;
    private readonly IBasis _basis;
    private readonly PhaseProperty _phaseProperty;
    private readonly Matrix _jacobiMatrix;
    private readonly FlowsBalancer _flowsBalancer;
    private readonly double[,,] _templateFlows;    // for each edge of each element

    public FlowsCalculator(Mesh.Mesh mesh, IBasis basis, PhaseProperty phaseProperty, Point2D[] normals, int[,] edgesDirect)
    {
        _mesh = mesh;
        _basis = basis;
        _phaseProperty = phaseProperty;
        _jacobiMatrix = new Matrix(2, 2);
        _flowsBalancer = new FlowsBalancer(mesh, edgesDirect);
        _templateFlows = new double[_mesh.ElementsCount, 4, 4];

        var gauss = new Integration(Quadratures.GaussOrder3());
        var masterInterval = new Interval(0, 1);
        var elementPoints = new Point2D[4];
        for (var ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var element = _mesh.Elements[ielem];
            var nodes = element.Nodes;
            var edgesIndices = element.EdgesIndices;

            elementPoints[0] = _mesh.Points[nodes[0]].Point;
            elementPoints[1] = _mesh.Points[nodes[1]].Point;
            elementPoints[2] = _mesh.Points[nodes[2]].Point;
            elementPoints[3] = _mesh.Points[nodes[3]].Point;

            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                int fixedVar = localEdge is 0 or 3 ? 1 : 0;
                int opposite = localEdge is 0 or 1 ? 0 : 1;

                var globalEdge = edgesIndices[localEdge];
                var normal = normals[globalEdge];

                for (int inode = 0; inode < 4; inode++)
                {
                    double ScalarFunction(double ksi)
                    {
                        var point = fixedVar == 0 ? new Point2D(opposite, ksi) : new Point2D(ksi, opposite);

                        MathAddition.JacobiMatrix2D(elementPoints, point, _jacobiMatrix);
                        MathAddition.InvertJacobiMatrix2D(_jacobiMatrix);

                        double gradX = _basis.DPhi(inode, 0, point);
                        double gradY = _basis.DPhi(inode, 1, point);

                        return gradX * (_jacobiMatrix[0, 0] * normal.X + _jacobiMatrix[1, 0] * normal.Y) +
                               gradY * (_jacobiMatrix[0, 1] * normal.X + _jacobiMatrix[1, 1] * normal.Y);
                    }

                    _templateFlows[ielem, localEdge, inode] = gauss.Integrate1D(ScalarFunction, masterInterval);
                }
            }
        }
    }

    private void FixKnownFlows(Vector averageFlows)
    {
        // Flows from wells
        foreach (var (ielem, iedge, flow) in _mesh.NeumannConditions)
        {
            int globalEdge = _mesh.Elements[ielem].EdgesIndices[iedge];
            var edge = _mesh.Elements[ielem].Edges[iedge];
            var p1 = _mesh.Points[edge.Node1].Point;
            var p2 = _mesh.Points[edge.Node2].Point;
            double length = Point2D.Distance(p1, p2);

            averageFlows[globalEdge] = -flow * length;
        }
    }

    private double CalculateCoefficient(int ielem)
    {
        double coefficient = _phaseProperty.Phases![ielem].Sum(phase => phase.Kappa / phase.Viscosity);
        coefficient *= _mesh.Materials[_mesh.Elements[ielem].Area].Permeability;
        return coefficient;
    }

    public void CalculateAverageFlows(double[] pressure, Vector averageFlows)
    {
        bool[] isUsed = new bool[averageFlows.Length];
        double lambdaE = 0.0, lambdaK;

        for (var ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var element = _mesh.Elements[ielem];
            var nodes = element.Nodes;
            var edges = element.Edges;
            var edgesIndices = element.EdgesIndices;

            for (int localEdge = 0; localEdge < 4; localEdge++)
            {
                var globalEdge = edgesIndices[localEdge];
                var edge = edges[localEdge];
                var p1 = _mesh.Points[edge.Node1].Point;
                var p2 = _mesh.Points[edge.Node2].Point;
                double lenght = Point2D.Distance(p1, p2);
                double gradient = 0.0;

                for (int inode = 0; inode < 4; inode++)
                {
                    gradient += pressure[nodes[inode]] * _templateFlows[ielem, localEdge, inode];
                }

                double flow = -gradient * lenght;

                if (isUsed[globalEdge])
                {
                    lambdaK = CalculateCoefficient(ielem);
                    flow *= lambdaK;
                    //averageFlows[globalEdge] = (flow + averageFlows[globalEdge]) / 2.0;
                    averageFlows[globalEdge] = lambdaK / (lambdaK + lambdaE) * averageFlows[globalEdge] +
                                                lambdaE / (lambdaE + lambdaK) * flow;
                }
                else
                {
                    lambdaE = CalculateCoefficient(ielem);
                    flow *= lambdaE;
                    averageFlows[globalEdge] = flow;
                    isUsed[globalEdge] = true;
                }
            }
        }

        FixKnownFlows(averageFlows);
        _flowsBalancer.BalanceFlows(averageFlows);
    }
}