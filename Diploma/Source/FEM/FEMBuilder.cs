﻿namespace Diploma.Source.FEM;

public class FEMBuilder
{
    #region Класс МКЭ

    public class Fem
    {
        private readonly Mesh.Mesh _mesh;
        private readonly IBasis _basis;
        private readonly IterativeSolver _solver;
        private readonly Func<Point2D, double> _source;
        private readonly Func<Point2D, double>? _field;
        private readonly Integration _gauss;
        private readonly SquareMatrix _stiffnessMatrix;
        private readonly SquareMatrix _precalcStiffnessX;
        private readonly SquareMatrix _precalcStiffnessY;
        private readonly SquareMatrix? _massMatrix;
        private readonly Vector _localB;
        private readonly SparseMatrix _globalMatrix;
        private readonly Vector _globalVector;

        public double Residual { get; private set; }
        public ImmutableArray<double>? Solution => _solver.Solution;
        
        public Fem(
            Mesh.Mesh mesh, 
            IBasis basis, 
            IterativeSolver solver,
            Func<Point2D, double> source,
            Func<Point2D, double>? field
        )
        {
            _mesh = mesh;
            _basis = basis;
            _solver = solver;
            _source = source;
            _field = field;

            _gauss = new Integration(Quadratures.GaussOrder3());
            
            _stiffnessMatrix = new SquareMatrix(_basis.Size);
            _precalcStiffnessX = new SquareMatrix(_basis.Size);
            _precalcStiffnessY = new SquareMatrix(_basis.Size);
            _massMatrix = _field is null ? null : new SquareMatrix(_basis.Size);
            _localB = new Vector(_basis.Size);

            PortraitBuilder.PortraitByNodes(_mesh, out int[] ig, out int[] jg);
            _globalMatrix = new SparseMatrix(ig.Length - 1, jg.Length)
            {
                Ig = ig,
                Jg = jg
            };

            _globalVector = new Vector(ig.Length - 1);
            
            // Считаем интегралы для матрицы жесткости и массы один раз
            // Интегрируем на шаблоне
            Rectangle omega = new(new Point2D(0, 0), new Point2D(1, 1));

            for (int i = 0; i < _basis.Size; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    var i1 = i;
                    var j1 = j;

                    Func<double, double, double> function = (ksi, etta) =>
                    {
                        Point2D point = new(ksi, etta);

                        double dphiiX = _basis.DPhi(i1, 0, point);
                        double dphijX = _basis.DPhi(j1, 0, point);

                        return dphiiX * dphijX;
                    };

                    _precalcStiffnessX[i, j] = _precalcStiffnessX[j, i] = _gauss.Integrate2D(function, omega);
                    
                    function = (ksi, etta) =>
                    {
                        Point2D point = new(ksi, etta);

                        double dphiiY = _basis.DPhi(i1, 1, point);
                        double dphijY = _basis.DPhi(j1, 1, point);

                        return dphiiY * dphijY;
                    };
                    
                    _precalcStiffnessY[i, j] = _precalcStiffnessY[j, i] = _gauss.Integrate2D(function, omega);

                    if (_massMatrix is not null)
                    {
                        function = (ksi, etta) =>
                        {
                            Point2D point = new(ksi, etta);

                            double phii = _basis.Phi(i1, point);
                            double phij = _basis.Phi(j1, point);

                            return phii * phij;
                        };

                        _massMatrix[i, j] = _massMatrix[j, i] = _gauss.Integrate2D(function, omega);
                    }
                }
            }
        }

        private double CalculateCoefficient(int ielem)
        {
            return 1.0;
        }

        private void BuildLocalMatrixVector(int ielem)
        {
            var nodes = _mesh.Elements[ielem].Nodes;
            var leftBottom = _mesh.Points[nodes[0]];
            var rightTop = _mesh.Points[nodes[_basis.Size - 1]];
            var hx = rightTop.X - leftBottom.X;
            var hy = rightTop.Y - leftBottom.Y;

            for (int i = 0; i < _basis.Size; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    _stiffnessMatrix[i, j] = _stiffnessMatrix[j, i] =
                        hy / hx * _precalcStiffnessX[i, j] +
                        hx / hy * _precalcStiffnessY[i, j];
                }
            }

            if (_field is null) return;
            
            for (int i = 0; i < _basis.Size; i++)
            {
                _localB[i] = 0.0;
                
                for (int j = 0; j < _basis.Size; j++)
                {
                    _localB[i] += _massMatrix![i, j] * _source(_mesh.Points[nodes[j]]);
                }
            }
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

        private void ApplyDirichlet()
        {
            foreach (var (node, value) in _mesh.DirichletConditions)
            {
                _globalMatrix.Di[node] = 1.0;
                _globalVector[node] = _field?.Invoke(_mesh.Points[node]) ?? value;

                for (int i = _globalMatrix.Ig[node]; i < _globalMatrix.Ig[node + 1]; i++)
                {
                    _globalMatrix.GGl[i] = 0.0;
                }

                for (int i = node + 1; i < _mesh.Points.Length; i++)
                {
                    for (int j = _globalMatrix.Ig[i]; j < _globalMatrix.Ig[i + 1]; j++)
                    {
                        if (_globalMatrix.Jg[j] == node)
                        {
                            _globalMatrix.GGu[j] = 0.0;
                        }
                    }
                }
            }
        }

        private void ApplyNeumann()
        {

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

        public void Solve()
        {
            AssemblySLAE();
            ApplyNeumann();
            ApplyDirichlet();

            _solver.SetSystem(_globalMatrix, _globalVector);
            _solver.Compute();

            Residual = Error();
        }

        private double Error()
        {
            if (_field is null) return 0.0;
            
            Vector exact = new(_solver.Solution!.Value.Length);

            for (int i = 0; i < _mesh.Points.Length; i++)
            {
                exact[i] = _field(_mesh.Points[i]);
            }

            double exactNorm = exact.Norm();

            for (int i = 0; i < _mesh.Points.Length; i++)
            {
                exact[i] -= _solver.Solution!.Value[i];
            }

            return exact.Norm() / exactNorm;
        }
    }

    #endregion

    #region Класс FEMBuilder

    private Mesh.Mesh _mesh = default!;
    private IBasis _basis = default!;
    private IterativeSolver _solver = default!;
    private Func<Point2D, double>? _field;
    private Func<Point2D, double> _source = default!;

    public FEMBuilder SetMesh(Mesh.Mesh mesh)
    {
        _mesh = mesh;
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

    public Fem Build() => new Fem(_mesh, _basis, _solver, _source, _field);

    #endregion
}
