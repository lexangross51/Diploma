namespace Diploma.Source.FEM;

public class FEMBuilder
{
    #region Класс МКЭ

    public class FEM
    {
        private readonly Mesh.Mesh _mesh;
        private readonly PhaseProperty _phaseProperty;
        private readonly IBasis _basis;
        private readonly IterativeSolver _solver;
        private readonly Func<Point2D, double> _source;
        private readonly Func<Point2D, double>? _field;
        private readonly Integration _gauss;
        private readonly SquareMatrix _stiffnessMatrix;
        private readonly SquareMatrix _massMatrix;
        private readonly SquareMatrix _jacobiMatrix;
        private readonly Vector _localB;
        private readonly SparseMatrix _globalMatrix;
        private readonly Vector _globalVector;
        public Vector? Solution { get; private set; }
        
        public FEM(
            Mesh.Mesh mesh,
            PhaseProperty phaseProperty,
            IBasis basis, 
            IterativeSolver solver,
            Func<Point2D, double> source,
            Func<Point2D, double>? field
        )
        {
            _mesh = mesh;
            _phaseProperty = phaseProperty;
            _basis = basis;
            _solver = solver;
            _source = source;
            _field = field;
            _gauss = new Integration(Quadratures.GaussOrder3());

            _stiffnessMatrix = new SquareMatrix(_basis.Size);
            _massMatrix = new SquareMatrix(_basis.Size);
            _localB = new Vector(_basis.Size);
            _jacobiMatrix = new SquareMatrix(2);

            PortraitBuilder.PortraitByNodes(_mesh, out int[] ig, out int[] jg);
            _globalMatrix = new SparseMatrix(ig.Length - 1, jg.Length)
            {
                Ig = ig,
                Jg = jg
            };

            _globalVector = new Vector(ig.Length - 1);
        }

        private double CalculateCoefficient(int ielem)
        {
            if (_field is not null) return 1.0;

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

        private void BuildLocalMatrixVector(int ielem)
        {
            var leftBottom = _mesh.Points[_mesh.Elements[ielem].Nodes[0]];
            var rightBottom = _mesh.Points[_mesh.Elements[ielem].Nodes[1]];
            var leftTop = _mesh.Points[_mesh.Elements[ielem].Nodes[2]];
            var rightTop = _mesh.Points[_mesh.Elements[ielem].Nodes[3]];

            Rectangle rect = new Rectangle(new Point2D(0, 0), new Point2D(1, 1));
            Vector gradPhiI = new Vector(2);
            Vector gradPhiJ = new Vector(2);
            Vector matrixGradI = new Vector(2);
            Vector matrixGradJ = new Vector(2);
            
            for (int i = 0; i < _basis.Size; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double ScalarFunction(double ksi, double eta)
                    {
                        var point = new Point2D(ksi, eta);
                        MathAddition.JacobiMatrix2D(leftBottom, rightBottom, leftTop, rightTop, point, _jacobiMatrix);
                        double jacobian = MathAddition.Jacobian2D(_jacobiMatrix);
                        MathAddition.InvertJacobiMatrix2D(_jacobiMatrix);

                        gradPhiI[0] = _basis.DPhi(i, 0, point);
                        gradPhiI[1] = _basis.DPhi(i, 1, point);
                        gradPhiJ[0] = _basis.DPhi(i, 0, point);
                        gradPhiJ[1] = _basis.DPhi(i, 1, point);
                        
                        SquareMatrix.Dot(_jacobiMatrix, gradPhiI, matrixGradI);
                        SquareMatrix.Dot(_jacobiMatrix, gradPhiJ, matrixGradJ);

                        return (matrixGradI[0] * matrixGradJ[0] + matrixGradI[1] * matrixGradJ[1]) * Math.Abs(jacobian);
                    }

                    _stiffnessMatrix[i, j] = _stiffnessMatrix[j, i] = _gauss.Integrate2D(ScalarFunction, rect);
                }
            }

            if (_field is null) return;
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
        
        private void ApplyDirichlet()
        {
            var bc1 = new int[_mesh.Points.Length].Select(_ => -1).ToArray();
            var dirichlet = _mesh.DirichletConditions;

            for (int i = 0; i < dirichlet.Length; i++)
            {
                bc1[dirichlet[i].Node] = i;   
            }

            for (int i = 0; i < _mesh.Points.Length; i++)
            {
                int k;
                
                if (bc1[i] != -1)
                {
                    var (node, value) = dirichlet[bc1[i]];
                    
                    _globalMatrix.Di[i] = 1.0;
                    _globalVector[i] = _field?.Invoke(_mesh.Points[node]) ?? value;

                    for (int j = _globalMatrix.Ig[i]; j < _globalMatrix.Ig[i + 1]; j++)
                    {
                        k = _globalMatrix.Jg[j];
                        if (bc1[k] == -1)
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
                        if (bc1[k] != -1)
                        {
                            _globalVector[i] -= _globalMatrix.GGl[j] * _globalVector[k];
                            _globalMatrix.GGl[j] = 0.0;
                            _globalMatrix.GGu[j] = 0.0;
                        }
                    }
                }
            }
        }

        private void ApplyNeumann()
        {
            foreach (var (ielem, iedge, theta) in _mesh.NeumannConditions)
            {
                var nodes = _mesh.Elements[ielem].Nodes;
                double hx = _mesh.Points[nodes[^1]].X - _mesh.Points[nodes[0]].X;
                double hy = _mesh.Points[nodes[^1]].Y - _mesh.Points[nodes[0]].Y;
                double length = hx + hy;
            
                // Left border
                // _globalVector[nodes[0]] += theta / 2.0;
                // _globalVector[nodes[2]] += theta / 2.0;
                
                // for (int i = 0; i < _basis.Size; i++)
                // {
                //     _globalVector[nodes[i]] += length * theta / 2.0;
                // }
                
                for (int i = 0; i < _basis.Size; i++)
                {
                    _globalVector[nodes[i]] += theta;
                }
            }
            
            // Rectangle omega = new(new Point2D(0, 0), new Point2D(1, 1));
            //
            // foreach (var (ielem, theta) in _mesh.NeumannConditions)
            // {
            //     var nodes = _mesh.Elements[ielem].Nodes;
            //     var leftBottom = _mesh.Points[nodes[0]];
            //     var rightTop = _mesh.Points[nodes[^1]];
            //     var hx = rightTop.X - leftBottom.X;
            //     var hy = rightTop.Y - leftBottom.Y;
            //     
            //     for (int i = 0; i < _basis.Size; i++)
            //     {
            //         _localB[i] = 0.0;
            //         
            //         double Function(double ksi, double etta)
            //             => _basis.Phi(i, new Point2D(ksi, etta));
            //         
            //         _localB[i] = _gauss.Integrate2D(Function, omega);
            //     }
            //     
            //     for (int inode = 0; inode < _basis.Size; inode++)
            //     {
            //         _globalVector[nodes[inode]] += _localB[inode] * hx * hy * theta;
            //     }
            // }
        }

        private void AssemblySLAE()
        {
            _globalMatrix.Clear();
            _globalVector.Fill(0.0);

            for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
            {
                var nodes = _mesh.Elements[ielem].Nodes;
                var coefficient = CalculateCoefficient(ielem);

                BuildLocalMatrixVector(ielem);

                for (int inode = 0; inode < _basis.Size; inode++)
                {
                    _globalVector[nodes[inode]] += _localB[inode];

                    for (int jnode = 0; jnode < _basis.Size; jnode++)
                    { 
                        AddToGlobal(nodes[inode], nodes[jnode], coefficient * _stiffnessMatrix[inode, jnode]);
                    }
                }
            }
        }

        public double Solve()
        {
            AssemblySLAE();
            ApplyNeumann();
            ApplyDirichlet();

            _solver.SetSystem(_globalMatrix, _globalVector);
            _solver.Compute();
            Solution = _solver.Solution;

            return Error();
        }

        private double Error()
        {
            if (_field is null) return 0.0;
            
            Vector exact = new(_solver.Solution!.Length);

            for (int i = 0; i < _mesh.Points.Length; i++)
            {
                exact[i] = _field(_mesh.Points[i]);
            }

            double exactNorm = exact.Norm();

            for (int i = 0; i < _mesh.Points.Length; i++)
            {
                exact[i] -= _solver.Solution![i];
            }

            return exact.Norm() / exactNorm;
        }
    }

    #endregion

    #region Класс FEMBuilder

    private Mesh.Mesh _mesh = default!;
    private PhaseProperty _phaseProperty = default!;
    private IBasis _basis = default!;
    private IterativeSolver _solver = default!;
    private Func<Point2D, double>? _field;
    private Func<Point2D, double> _source = default!;

    public FEMBuilder SetMesh(Mesh.Mesh mesh)
    {
        _mesh = mesh;
        return this;
    }

    public FEMBuilder SetPhaseProperties(PhaseProperty phaseProperty)
    {
        _phaseProperty = phaseProperty;
        return this;
    }

    public FEMBuilder SetBasis(IBasis basis)
    {
        _basis = basis;
        return this;
    }

    public FEMBuilder SetSolver(IterativeSolver solver)
    {
        _solver = solver;
        return this;
    }

    public FEMBuilder SetTest(Func<Point2D, double> source, Func<Point2D, double>? field = null)
    {
        _source = source;
        _field = field;
        return this;
    }

    public FEM Build() => new(_mesh, _phaseProperty, _basis, _solver, _source, _field);

    #endregion
}