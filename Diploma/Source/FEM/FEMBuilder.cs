using System.Diagnostics.CodeAnalysis;
using System.Transactions;
using System.Windows.Markup;

namespace Diploma.Source.FEM;

public class FEMBuilder
{
    #region Класс МКЭ

    public class FEM
    {
        private readonly Mesh.Mesh _mesh = default!;
        private readonly IBasis _basis = default!;
        private readonly IterativeSolver _solver = default!;
        private readonly Func<double, double, double> _source = default!;
        private readonly Func<double, double, double>? _field;
        private readonly Integration _gauss;
        private readonly SquareMatrix _stiffnessMatrix;
        private readonly SquareMatrix _massMatrix;
        private readonly Vector _localB;
        private readonly SparseMatrix _globalMatrix;
        private readonly Vector _globalVector;
        private readonly SquareMatrix _jacobian;
        private readonly SquareMatrix _jacobianInverse;
        private bool _isPhysical;

        public double Residual { get; private set; }
        public ImmutableArray<double>? Solution => _solver.Solution;


        public FEM(
            Mesh.Mesh mesh, 
            IBasis basis, 
            IterativeSolver solver,
            Func<double, double, double> source,
            Func<double, double, double>? field
        )
        {
            _mesh = mesh;
            _basis = basis;
            _solver = solver;
            _source = source;
            _field = field;

            _gauss = new Integration(Quadratures.GaussOrder3());

            _jacobian = new SquareMatrix(2);
            _jacobianInverse = new SquareMatrix(2);
            _stiffnessMatrix = new SquareMatrix(_basis.Size);
            _massMatrix = new SquareMatrix(_basis.Size);
            _localB = new Vector(_basis.Size);

            PortraitBuilder.PortraitByNodes(_mesh, out int[] ig, out int[] jg);
            _globalMatrix = new SparseMatrix(ig.Length - 1, jg.Length)
            {
                Ig = ig,
                Jg = jg
            };

            _globalVector = new(ig.Length - 1);

            _isPhysical = false;
        }

        private double CalculateCoefficient(int ielem)
        {
            return 1.0;
        }

        private void CalculateJacobian(int ielem)
        {

        }

        private void BuildLocalMatrices(int ielem)
        {
            var nodes = _mesh.Elements[ielem].Nodes;
            var leftBottom = _mesh.Points[nodes[0]];
            var rightTop = _mesh.Points[nodes[_basis.Size]];

            double hx = rightTop.X - leftBottom.X;
            double hy = rightTop.Y - leftBottom.Y;

            _jacobian[0, 0] = hx;
            _jacobian[1, 1] = hy;

            var jacobianDet = _jacobian[0, 0] * _jacobian[1, 1] - _jacobian[0, 1] * _jacobian[1, 0];

            _jacobianInverse[0, 0] = _jacobian[1, 1] / jacobianDet;
            _jacobianInverse[1, 1] = _jacobian[0, 0] / jacobianDet;

            
        }

        // Только если задана нефизическая задача (для тестирования)
        private void BuildLocalVector(int ielem)
        {
            _localB.Fill(0.0);



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
                _globalVector[node] = value;

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
                var coef = CalculateCoefficient(ielem);

                BuildLocalMatrices(ielem);

                if (!_isPhysical)
                {
                    BuildLocalVector(ielem);
                }

                for (int inode = 0; inode < _basis.Size; inode++)
                {
                    _globalVector[nodes[inode]] += _localB[inode];

                    for (int jnode = 0; jnode < _basis.Size; jnode++)
                    { 
                        AddToGlobal(nodes[inode], nodes[jnode], coef * _stiffnessMatrix[inode, jnode]);
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
            if (_field is not null)
            {
                Vector exact = new(_solver.Solution!.Value.Length);

                for (int i = 0; i < _mesh.Points.Length; i++)
                {
                    var point = _mesh.Points[i];

                    exact[i] = _field(point.X, point.Y);
                }

                double exactNorm = exact.Norm();

                for (int i = 0; i < _mesh.Points.Length; i++)
                {
                    exact[i] -= _solver.Solution!.Value[i];
                }

                return exact.Norm() / exactNorm;
            }

            return 0.0;
        }

    }

    #endregion


    #region Класс FEMBuilder

    private Mesh.Mesh _mesh = default!;
    private IBasis _basis = default!;
    private IterativeSolver _solver = default!;
    private Func<double, double, double>? _field;
    private Func<double, double, double> _source = default!;

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

    public FEMBuilder SetTest(Func<double, double, double> source, Func<double, double, double>? field = null)
    {
        _source = source;
        _field = field;
        return this;
    }

    public FEM Build() => new FEM(_mesh, _basis, _solver, _source, _field);

    #endregion
}
