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
        private readonly SquareMatrix _stiffnessMatrix;
        private readonly SquareMatrix _precalcStiffnessX;
        private readonly SquareMatrix _precalcStiffnessY;
        private readonly SquareMatrix? _massMatrix;
        private readonly Vector _localB;
        private readonly SparseMatrix _globalMatrix;
        private readonly Vector _globalVector;
        public ImmutableArray<double>? Solution => _solver.Solution;
        
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

            var gauss = new Integration(Quadratures.GaussOrder5());
            
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

            _precalcStiffnessX[0, 0] = 2;
            _precalcStiffnessX[0, 1] = -2;
            _precalcStiffnessX[0, 2] = 1;
            _precalcStiffnessX[0, 3] = -1;
            _precalcStiffnessX[1, 0] = -2;
            _precalcStiffnessX[1, 1] = 2;
            _precalcStiffnessX[1, 2] = -1;
            _precalcStiffnessX[1, 3] = 1;
            _precalcStiffnessX[2, 0] = 1;
            _precalcStiffnessX[2, 1] = -1;
            _precalcStiffnessX[2, 2] = 2;
            _precalcStiffnessX[2, 3] = -2;
            _precalcStiffnessX[3, 0] = -1;
            _precalcStiffnessX[3, 1] = 1;
            _precalcStiffnessX[3, 2] = -2;
            _precalcStiffnessX[3, 3] = 2;
            
            _precalcStiffnessY[0, 0] = 2;
            _precalcStiffnessY[0, 1] = 1;
            _precalcStiffnessY[0, 2] = -2;
            _precalcStiffnessY[0, 3] = -1;
            _precalcStiffnessY[1, 0] = 1;
            _precalcStiffnessY[1, 1] = 2;
            _precalcStiffnessY[1, 2] = -1;
            _precalcStiffnessY[1, 3] = -2;
            _precalcStiffnessY[2, 0] = -2;
            _precalcStiffnessY[2, 1] = -1;
            _precalcStiffnessY[2, 2] = 2;
            _precalcStiffnessY[2, 3] = 1;
            _precalcStiffnessY[3, 0] = -1;
            _precalcStiffnessY[3, 1] = -2;
            _precalcStiffnessY[3, 2] = 1;
            _precalcStiffnessY[3, 3] = 2;
            
            if (_massMatrix is not null)
            {
                _massMatrix[0, 0] = 4;
                _massMatrix[0, 1] = 2;
                _massMatrix[0, 2] = 2;
                _massMatrix[0, 3] = 1;
                _massMatrix[1, 0] = 2;
                _massMatrix[1, 1] = 4;
                _massMatrix[1, 2] = 1;
                _massMatrix[1, 3] = 2;
                _massMatrix[2, 0] = 2;
                _massMatrix[2, 1] = 1;
                _massMatrix[2, 2] = 4;
                _massMatrix[2, 3] = 2;
                _massMatrix[3, 0] = 1;
                _massMatrix[3, 1] = 2;
                _massMatrix[3, 2] = 2;
                _massMatrix[3, 3] = 4;
            }

            #region Численный расчет матриц жесткости и массы

            // Считаем интегралы для матрицы жесткости и массы один раз
            // Интегрируем на шаблоне
            //Rectangle omega = new(new Point2D(0, 0), new Point2D(1, 1));
            //
            // for (int i = 0; i < _basis.Size; i++)
            // {
            //     for (int j = 0; j <= i; j++)
            //     {
            //         var i1 = i;
            //         var j1 = j;
            //
            //         Func<double, double, double> function = (ksi, etta) =>
            //         {
            //             Point2D point = new(ksi, etta);
            //
            //             double dphiiX = _basis.DPhi(i1, 0, point);
            //             double dphijX = _basis.DPhi(j1, 0, point);
            //
            //             return dphiiX * dphijX;
            //         };
            //
            //         _precalcStiffnessX[i, j] = _precalcStiffnessX[j, i] = gauss.Integrate2D(function, omega);
            //         
            //         function = (ksi, etta) =>
            //         {
            //             Point2D point = new(ksi, etta);
            //
            //             double dphiiY = _basis.DPhi(i1, 1, point);
            //             double dphijY = _basis.DPhi(j1, 1, point);
            //
            //             return dphiiY * dphijY;
            //         };
            //         
            //         _precalcStiffnessY[i, j] = _precalcStiffnessY[j, i] = gauss.Integrate2D(function, omega);
            //
            //         if (_massMatrix is not null)
            //         {
            //             function = (ksi, etta) =>
            //             {
            //                 Point2D point = new(ksi, etta);
            //
            //                 double phii = _basis.Phi(i1, point);
            //                 double phij = _basis.Phi(j1, point);
            //
            //                 return phii * phij;
            //             };
            //
            //             _massMatrix[i, j] = _massMatrix[j, i] = gauss.Integrate2D(function, omega);
            //         }
            //     }
            // }

            #endregion
        }

        private double CalculateCoefficient(int ielem)
        {
            if (_field is not null) return 1.0;

            int area = _mesh.Elements[ielem].Area;
            double coefficient = 0.0;

            int phaseCount = _phaseProperty.Phases[ielem].Count;

            for (int i = 0; i < phaseCount; i++)
            {
                coefficient += _phaseProperty.Phases[ielem][i].Kappa;
                coefficient /= _phaseProperty.Phases[ielem][i].Viscosity;
            }
            
            coefficient *= _mesh.Materials[area].Permeability;

            return coefficient;
        }

        private void BuildLocalMatrixVector(int ielem)
        {
            var nodes = _mesh.Elements[ielem].Nodes;
            var leftBottom = _mesh.Points[nodes[0]];
            var rightTop = _mesh.Points[nodes[^1]];
            var hx = rightTop.X - leftBottom.X;
            var hy = rightTop.Y - leftBottom.Y;

            for (int i = 0; i < _basis.Size; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    _stiffnessMatrix[i, j] = _stiffnessMatrix[j, i] =
                        hy / (hx * 6.0) * _precalcStiffnessX[i, j] +
                        hx / (hy * 6.0) * _precalcStiffnessY[i, j];
                }
            }

            if (_field is null) return;
            
            for (int i = 0; i < _basis.Size; i++)
            {
                _localB[i] = 0.0;
                
                for (int j = 0; j < _basis.Size; j++)
                {
                    _localB[i] += hx * hy * _massMatrix![i, j] / 36.0 * _source(_mesh.Points[nodes[j]]);
                }
            }
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
            #region Вариант с симметризацией

            var bc1 = new int[_mesh.Points.Length].Select(_ => -1).ToArray();
            var dirichlet = _mesh.DirichletConditions;

            for (int i = 0; i < dirichlet.Length; i++)
            {
                bc1[dirichlet[i].Node] = i;   
            }

            int k;
            
            for (int i = 0; i < _mesh.Points.Length; i++)
            {
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

            #endregion

            #region Вариант с зануленим строки и нарушением симметричности матрицы

            // foreach (var (node, value) in _mesh.DirichletConditions)
            // {
            //     _globalMatrix.Di[node] = 1.0;
            //     _globalVector[node] = _field?.Invoke(_mesh.Points[node]) ?? value;
            //
            //     for (int i = _globalMatrix.Ig[node]; i < _globalMatrix.Ig[node + 1]; i++)
            //     {
            //         _globalMatrix.GGl[i] = 0.0;
            //     }
            //     
            //     for (int i = node + 1; i < _mesh.Points.Length; i++)
            //     {
            //         for (int j = _globalMatrix.Ig[i]; j < _globalMatrix.Ig[i + 1]; j++)
            //         {
            //             if (_globalMatrix.Jg[j] == node)
            //             {
            //                 _globalMatrix.GGu[j] = 0.0;
            //             }
            //         }
            //     }
            // }

            #endregion
        }

        private void ApplyNeumann()
        {
            foreach (var (ielem, theta) in _mesh.NeumannConditions)
            {
                var coefficient = CalculateCoefficient(ielem);
                var nodes = _mesh.Elements[ielem].Nodes;
                double hx = _mesh.Points[nodes[^1]].X - _mesh.Points[nodes[0]].X;
                double hy = _mesh.Points[nodes[^1]].Y - _mesh.Points[nodes[0]].Y;
                double length = hx + hy;

                for (int i = 0; i < _basis.Size; i++)
                {
                    _globalVector[nodes[i]] += length * theta * coefficient / 2.0;
                }
            }
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

            return Error();
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

    public FEM Build() => new FEM(_mesh, _phaseProperty, _basis, _solver, _source, _field);

    #endregion
}