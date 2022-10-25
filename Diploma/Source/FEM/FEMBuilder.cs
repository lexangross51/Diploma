using System.Diagnostics.CodeAnalysis;

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

        public ImmutableArray<double>? Solution => _solver.Solution;

        public double Residual { get; private set; }

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
        }

        private void BuildLocalMatrices(int ielem)
        {

        }

        // Только если задана нефизическая задача (для тестирования)
        private void BuildLocalVector(int ielem)
        {

        }

        private void AddToGlobal(int i, int j, double value)
        {

        }

        private void ApplyDirichlet()
        {

        }

        private void ApplyNeumann()
        {

        }

        public void Solve()
        {

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
