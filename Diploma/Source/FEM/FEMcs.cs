﻿using Matrix = Diploma.Source.MathClasses.Matrix;
using SparseMatrix = Diploma.Source.MathClasses.SparseMatrix;
using Vector = Diploma.Source.MathClasses.Vector;

namespace Diploma.Source.FEM;

public class FEMBuilder
{
    #region Класс МКЭ

    public class FEM
    {
        private readonly Mesh.Mesh _mesh;
        private readonly PhaseProperty _phaseProperty;
        private readonly IBasis _basis;
        private readonly Func<Point2D, double> _source;
        private readonly Func<Point2D, double>? _field;
        private readonly Integration _gauss;
        private Matrix[]? _stiffnessMatrices;
        private Vector[]? _localVectors;
        private readonly Matrix _massMatrix;
        private readonly Matrix _jacobiMatrix;
        private readonly Rectangle _masterElement;
        private readonly SparseMatrix _globalMatrix;
        private readonly double[] _globalVector;
        private readonly double[] _solution;
        public double[]? Solution { get; private set; }
        
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
            _source = source;
            _field = field;
            _gauss = new Integration(Quadratures.GaussOrder3());
            
            _massMatrix = new Matrix(_basis.Size, _basis.Size);
            _jacobiMatrix = new Matrix(2, 2);
            _masterElement = new Rectangle(new Point2D(), new Point2D(1, 1));

            PortraitBuilder.PortraitByNodes(_mesh, out int[] ig, out int[] jg);
            _globalMatrix = new SparseMatrix(ig.Length - 1, jg.Length)
            {
                Ig = ig,
                Jg = jg
            };
            
            _globalVector = new double[ig.Length - 1];
            _solution = new double[ig.Length - 1];
        }
        
        private double CalculateCoefficient(int ielem)
        {
            if (_field is not null) return 1.0;
            
            double coefficient = _phaseProperty.Phases![ielem].Sum(phase => phase.Kappa / phase.Viscosity);
            coefficient *= _mesh.Materials[_mesh.Elements[ielem].Area].Permeability;

            return coefficient;
        }
        
        private void BuildLocalMatrixVector(int ielem)
        {
            _stiffnessMatrices![ielem] = new Matrix(_basis.Size, _basis.Size);
            _localVectors![ielem] = new Vector(_basis.Size);
            var nodes = _mesh.Elements[ielem].Nodes;

            Point2D[] elementPoints =
            {
                _mesh.Points[nodes[0]].Point,
                _mesh.Points[nodes[1]].Point,
                _mesh.Points[nodes[2]].Point,
                _mesh.Points[nodes[3]].Point
            };
            
            Vector gradPhiI = new(2);
            Vector gradPhiJ = new(2);
            Vector matrixGradI = new(2);
            Vector matrixGradJ = new(2);
            
            for (int i = 0; i < _basis.Size; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double ScalarFunc(double ksi, double eta)
                    {
                        var point = new Point2D(ksi, eta);
                        MathAddition.JacobiMatrix2D(elementPoints, point, _jacobiMatrix);
                        double jacobian = MathAddition.Jacobian2D(_jacobiMatrix);
                        MathAddition.InvertJacobiMatrix2D(_jacobiMatrix);

                        gradPhiI[0] = _basis.DPhi(i, 0, point);
                        gradPhiI[1] = _basis.DPhi(i, 1, point);
                        gradPhiJ[0] = _basis.DPhi(j, 0, point);
                        gradPhiJ[1] = _basis.DPhi(j, 1, point);
                        matrixGradI.Fill();
                        matrixGradJ.Fill();

                        Matrix.Dot(_jacobiMatrix, gradPhiI, matrixGradI);
                        Matrix.Dot(_jacobiMatrix, gradPhiJ, matrixGradJ);

                        return (matrixGradI[0] * matrixGradJ[0] + matrixGradI[1] * matrixGradJ[1]) * Math.Abs(jacobian);
                    }


                    _stiffnessMatrices[ielem][i, j] = _stiffnessMatrices[ielem][j, i] = _gauss.Integrate2D(ScalarFunc, _masterElement);
                }
            }

            if (_field is null) return;
            
            for (int i = 0; i < _basis.Size; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double ScalarFunc(double ksi, double eta)
                    {
                        var point = new Point2D(ksi, eta);
                        MathAddition.JacobiMatrix2D(elementPoints, point, _jacobiMatrix);
                        double jacobian = MathAddition.Jacobian2D(_jacobiMatrix);

                        return _basis.Phi(i, point) * _basis.Phi(j, point) * Math.Abs(jacobian);
                    }

                    _massMatrix[i, j] = _massMatrix[j, i] = _gauss.Integrate2D(ScalarFunc, _masterElement);
                }
            }
            
            for (int i = 0; i < _basis.Size; i++)
            {
                _localVectors[ielem][i] = 0.0;
                
                for (int j = 0; j < _basis.Size; j++)
                {
                    _localVectors[ielem][i] += _massMatrix[i, j] * _source(_mesh.Points[nodes[j]].Point);
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
                    _globalVector[i] = _field?.Invoke(_mesh.Points[node].Point) ?? value;

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

            #region For testing in wells borders

            if (_mesh.NeumannConditions.Length != 0 && _field is not null)
            {
                HashSet<DirichletCondition> dirichletNodes = new();

                foreach (var condition in _mesh.NeumannConditions)
                {
                    int ielem = condition.Element;
                    int iedge = condition.Edge;
                    var edge = _mesh.Elements[ielem].Edges[iedge];

                    dirichletNodes.Add(new DirichletCondition(edge.Node1, 0.0));
                    dirichletNodes.Add(new DirichletCondition(edge.Node2, 0.0));
                }

                
                bc1 = bc1.Select(_ => -1).ToArray();

                int l = 0;
                
                foreach (var (node, _) in dirichletNodes)
                {
                    bc1[node] = l++;   
                }

                var dirichlet1 = dirichletNodes.ToImmutableArray();
                
                for (int i = 0; i < _mesh.Points.Length; i++)
                {
                    int k;
                
                    if (bc1[i] != -1)
                    {
                        var (node, value) = dirichlet1[bc1[i]];
                    
                        _globalMatrix.Di[i] = 1.0;
                        _globalVector[i] = _field?.Invoke(_mesh.Points[node].Point) ?? value;

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

            #endregion
        }

        private void ApplyNeumann()
        {
            foreach (var (ielem, iedge, power) in _mesh.NeumannConditions)
            {
                var edge = _mesh.Elements[ielem].Edges[iedge];
                var p1 = _mesh.Points[edge.Node1].Point;
                var p2 = _mesh.Points[edge.Node2].Point;
                double length = Point2D.Distance(p1, p2);
                
                _globalVector[edge.Node1] += length / 2.0 * power;
                _globalVector[edge.Node2] += length / 2.0 * power;
            }
        }

        private void AssemblySLAE()
        {
            _globalMatrix.Clear();
            Array.Fill(_globalVector, 0.0);

            if (_stiffnessMatrices is null)
            {
                _stiffnessMatrices = new Matrix[_mesh.ElementsCount];
                _localVectors = new Vector[_mesh.ElementsCount];
                
                for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
                {
                    BuildLocalMatrixVector(ielem);
                }
            }

            for (int ielem = 0; ielem < _mesh.ElementsCount; ielem++)
            {
                var nodes = _mesh.Elements[ielem].Nodes;
                var stiffnessMatrix = _stiffnessMatrices[ielem];
                double coefficient = CalculateCoefficient(ielem);

                for (int i = 0; i < 4; i++)
                {
                    _globalVector[nodes[i]] += _localVectors![ielem][i];
                    
                    for (int j = 0; j < 4; j++)
                    {
                        AddToGlobal(nodes[i], nodes[j], coefficient * stiffnessMatrix[i, j]);
                    }
                }
            }

            for (int i = 0; i < _mesh.Points.Length; i++)
            {
                if (_mesh.Points[i].IsFictitious)
                {
                    _globalMatrix.Di[i] = 1.0;
                }
            }
        }

        public double Solve()
        {
            AssemblySLAE();
            ApplyNeumann();
            ApplyDirichlet();
            
            PardisoSolver.Solve(
                _globalMatrix.Size, _globalMatrix.Ig, _globalMatrix.Jg, _globalMatrix.Di, _globalMatrix.GGl,
                _globalVector, _solution, 8);
            Solution = _solution;

            return Error();
        }

        private double Error()
        {
            if (_field is null) return 0.0;
            
            Vector exact = new(_solution.Length);

            for (int i = 0; i < _mesh.Points.Length; i++)
            {
                if (!_mesh.Points[i].IsFictitious)
                {
                    exact[i] = _field(_mesh.Points[i].Point);
                }
            }

            double exactNorm = exact.Norm();

            for (int i = 0; i < _mesh.Points.Length; i++)
            {
                exact[i] -= _solution[i];
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