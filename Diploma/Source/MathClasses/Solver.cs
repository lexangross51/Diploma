using System.Runtime.InteropServices;

namespace Diploma.Source.MathClasses;

public abstract class IterativeSolver
{
    public TimeSpan RunningTime;
    public int IterationsCount;
    protected SparseMatrix Matrix = default!;
    protected double[] RightPart = default!;
    protected readonly int MaxIters;
    protected readonly double Eps;
    protected double[]? YVector;
    public double[]? Solution { get; protected set; }

    protected IterativeSolver(int maxIters, double eps)
        => (MaxIters, Eps) = (maxIters, eps);

    public void SetSystem(SparseMatrix matrix, double[] rightPart)
        => (Matrix, RightPart) = (matrix, rightPart);

    public abstract void Compute();

    protected void CholeskyDecomposition(double[] diNew, double[] ggNew)
    {
        double sumLower = 0.0;
        double sumDi = 0.0;

        for (int i = 0; i < Matrix.Size; i++)
        {
            int i0 = Matrix.Ig[i];
            int i1 = Matrix.Ig[i + 1];

            for (int k = i0; k < i1; k++)
            {
                int j = Matrix.Jg[k];
                int j0 = Matrix.Ig[j];
                int j1 = Matrix.Ig[j + 1];
                int ik = i0;
                int kj = j0;

                while (ik < k && kj < j1)
                {
                    if (Matrix.Jg[ik] == Matrix.Jg[kj])
                    {
                        sumLower += ggNew[ik] * ggNew[kj];
                        ik++;
                        kj++;
                    }
                    else
                    {
                        if (Matrix.Jg[ik] > Matrix.Jg[kj])
                            kj++;
                        else
                            ik++;
                    }
                }

                ggNew[k] = (ggNew[k] - sumLower) / diNew[j];
                sumDi += ggNew[k] * ggNew[k];
                sumLower = 0.0;
            }

            diNew[i] = Math.Sqrt(diNew[i] - sumDi);
            sumDi = 0.0;
        }
    }

    protected void MoveForCholesky(double[] diNew, double[] ggNew, double[] vector, double[] result)
    {
        YVector ??= new double[RightPart.Length];
        Array.Copy(vector, YVector, vector.Length);
        double sum = 0.0;

        for (int i = 0; i < Matrix.Size; i++)
        {
            int i0 = Matrix.Ig[i];
            int i1 = Matrix.Ig[i + 1];

            for (int k = i0; k < i1; k++)
                sum += ggNew[k] * YVector[Matrix.Jg[k]];

            YVector[i] = (YVector[i] - sum) / diNew[i];
            sum = 0.0;
        }

        Array.Copy(YVector, result, YVector.Length);

        for (int i = Matrix.Size - 1; i >= 0; i--)
        {
            int i0 = Matrix.Ig[i];
            int i1 = Matrix.Ig[i + 1];
            result[i] = YVector[i] / diNew[i];

            for (int k = i0; k < i1; k++)
                YVector[Matrix.Jg[k]] -= ggNew[k] * result[i];
        }
    }
}

// public class CGM : IterativeSolver
// {
//     public CGM(int maxIters, double eps) : base(maxIters, eps)
//     {
//     }
//
//     public override void Compute()
//     {
//         try
//         {
//             ArgumentNullException.ThrowIfNull(Matrix, $"{nameof(Matrix)} cannot be null, set the matrix");
//             ArgumentNullException.ThrowIfNull(RightPart, $"{nameof(Vector)} cannot be null, set the vector");
//
//             var rightPartNorm = RightPart.Norm();
//
//             Solution = new double[RightPart.Length];
//             double[] r = new double[RightPart.Length];
//             double[] z = new double[RightPart.Length];
//             double[] product = new double[RightPart.Length];
//             
//             SparseMatrix.Dot(Matrix, Solution, product);
//
//             for (int i = 0; i < RightPart.Length; i++)
//             {
//                 r[i] = RightPart[i] - product[i];
//             }
//             
//             Array.Copy(r, z, r.Length);
//             
//             var sw = Stopwatch.StartNew();
//             var residualNorm = r.Norm();
//             
//             for (IterationsCount = 0; IterationsCount < MaxIters && residualNorm / rightPartNorm >= Eps; IterationsCount++)
//             {
//                 Array.Fill(product, 0.0);
//                 SparseMatrix.Dot(Matrix, z, product);
//                 var alpha = Vector.Dot(r, r) / Vector.Dot(product,z);
//
//                 for (int i = 0; i < RightPart.Length; i++)
//                 {
//                     Solution[i] += alpha * z[i];
//                 }
//                 
//                 for (int i = 0; i < RightPart.Length; i++)
//                 {
//                     r[i] -= alpha * product[i];
//                 }
//                 
//                 var beta = Vector.Dot(r, r) / (residualNorm * residualNorm);
//                 residualNorm = r.Norm();
//                 
//                 for (int i = 0; i < RightPart.Length; i++)
//                 {
//                     z[i] = r[i] + beta * z[i];
//                 }
//             }
//
//             sw.Stop();
//
//             RunningTime = sw.Elapsed;
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"We had problem: {ex.Message}");
//         }
//     }
// }
//
// public class CGMCholesky : IterativeSolver
// {
//     public CGMCholesky(int maxIters, double eps) : base(maxIters, eps)
//     {
//     }
//
//     public override void Compute()
//     {
//         ArgumentNullException.ThrowIfNull(Matrix, $"{nameof(Matrix)} cannot be null, set the matrix");
//         ArgumentNullException.ThrowIfNull(RightPart, $"{nameof(Vector)} cannot be null, set the vector");
//
//         double rightPartNorm = RightPart.Norm();
//         Solution = new Vector(RightPart.Length);
//         Vector r = new(RightPart.Length);
//         Vector z = new(RightPart.Length);
//         Vector result = new(RightPart.Length);
//         Vector product = new(RightPart.Length);
//         double[] diNew = new double[Matrix.Size];
//         double[] ggNew = new double[Matrix.GGl.Length];
//         Array.Copy(Matrix.Di, diNew, Matrix.Size);
//         Array.Copy(Matrix.GGl, ggNew, Matrix.GGl.Length);
//         
//         CholeskyDecomposition(diNew, ggNew);
//         
//         SparseMatrix.Dot(Matrix, Solution, product);
//
//         for (int i = 0; i < r.Length; i++)
//         {
//             r[i] = RightPart[i] - product[i];
//         }
//         
//         MoveForCholesky(diNew, ggNew, r, z);
//
//         var sw = Stopwatch.StartNew();
//         
//         for (IterationsCount = 0; IterationsCount < MaxIters && r.Norm() / rightPartNorm >= Eps; IterationsCount++)
//         {
//             MoveForCholesky(diNew, ggNew, r, result);
//             
//             SparseMatrix.Dot(Matrix, z, product);
//             double mrDotR = Vector.Dot(result, r);
//             double alpha = mrDotR / Vector.Dot(product, z);
//
//             for (int i = 0; i < Solution.Length; i++)
//             {
//                 Solution[i] += alpha * z[i];
//                 r[i] -= alpha * product[i];
//             }
//             
//             MoveForCholesky(diNew, ggNew, r, result);
//
//             double beta = Vector.Dot(result, r) / mrDotR;
//
//             for (int i = 0; i < z.Length; i++)
//             {
//                 z[i] = result[i] + beta * z[i];
//             }
//         }
//         
//         sw.Stop();
//         RunningTime = sw.Elapsed;
//     }
// }

public class LOS : IterativeSolver
{
    public LOS(int maxIters, double eps) : base(maxIters, eps)
    {
    }

    public override void Compute()
    {
        try
        {
            ArgumentNullException.ThrowIfNull(Matrix, $"{nameof(Matrix)} cannot be null, set the matrix");
            ArgumentNullException.ThrowIfNull(RightPart, $"{nameof(RightPart)} cannot be null, set the vector");

            Solution = new double[RightPart.Length];

            double[] z = new double[RightPart.Length];
            double[] r = new double[RightPart.Length];
            double[] p = new double[RightPart.Length];
            double[] product = new double[RightPart.Length];
            SparseMatrix.Dot(Matrix, Solution, product);

            for (int i = 0; i < product.Length; i++)
            {
                r[i] = RightPart[i] - product[i];
            }

            Array.Copy(r, z, r.Length);
            SparseMatrix.Dot(Matrix, z, p);

            var squareNorm = Vector.Dot(r, r);

            Stopwatch sw = Stopwatch.StartNew();
            for (int index = 0; index < MaxIters && squareNorm > Eps; index++)
            {
                var alpha = Vector.Dot(p, r) / Vector.Dot(p, p);

                for (int i = 0; i < Solution.Length; i++)
                {
                    Solution[i] += alpha * z[i];
                }

                squareNorm = Vector.Dot(r, r) - (alpha * alpha * Vector.Dot(p, p));

                for (int i = 0; i < Solution.Length; i++)
                {
                    r[i] -= alpha * p[i];
                }

                SparseMatrix.Dot(Matrix, r, product);

                var beta = -Vector.Dot(p, product) / Vector.Dot(p, p);

                for (int i = 0; i < Solution.Length; i++)
                {
                    z[i] = r[i] + beta * z[i];
                    p[i] = product[i] + beta * p[i];
                }
            }

            sw.Stop();

            RunningTime = sw.Elapsed;
        }
        catch (ArgumentNullException ex)
        {
            Console.WriteLine($"We had problem: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"We had problem: {ex.Message}");
        }
    }
}


public abstract class DirectSolver
{
    public TimeSpan RunningTime;
    protected ProfileMatrix Matrix = default!;
    protected double[] RightPart = default!;
    public double[]? Solution { get; protected set; }

    public void SetSystem(SparseMatrix matrix, double[] rightPart)
        => (Matrix, RightPart) = (matrix.ToProfileMatrix(), rightPart);

    public abstract void Compute();

    protected void LU(double[] dinew, double[] gglnew, double[] ggunew)
    {
        for (int i = 1; i < Matrix.Size; i++)
        {
            double sumDi = 0.0;

            int j0 = i - (Matrix.Ig[i + 1] - Matrix.Ig[i]);

            for (int ii = Matrix.Ig[i]; ii < Matrix.Ig[i + 1]; ii++)
            {
                int j = ii - Matrix.Ig[i] + j0;
                int jbeg = Matrix.Ig[j];
                int jend = Matrix.Ig[j + 1];

                if (jbeg < jend)
                {
                    int i0 = j - (jend - jbeg);
                    int jjbeg = j0 > i0 ? j0 : i0;
                    int jjend = j < i - 1 ? j : i - 1;

                    double sumAl = 0.0;
                    double sumAu = 0.0;

                    for (int k = 0; k < jjend - jjbeg; k++)
                    {
                        int indAu = Matrix.Ig[j] + jjbeg - i0 + k;
                        int indAl = Matrix.Ig[i] + jjbeg - j0 + k;
                        sumAl += ggunew[indAu] * gglnew[indAl];
                    }

                    gglnew[ii] -= sumAl;

                    for (int k = 0; k < jjend - jjbeg; k++)
                    {
                        int indAl = Matrix.Ig[j] + jjbeg - i0 + k;
                        int indAu = Matrix.Ig[i] + jjbeg - j0 + k;
                        sumAu += ggunew[indAu] * gglnew[indAl];
                    }

                    ggunew[ii] -= sumAu;
                }

                if (Math.Abs(dinew[j]) < 1E-16)
                {
                    throw new Exception("Division by zero in LU decomposer for profile matrix");
                }

                ggunew[ii] /= dinew[j];

                sumDi += gglnew[ii] * ggunew[ii];
            }

            dinew[i] -= sumDi;
        }
    }
}

public static class PardisoSolver
{
    private const string PathToDll = @"C:\\Filtration1\\PardisoInterface.dll";

    [DllImport(PathToDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "solvePardiso")]
    public static extern void Solve(int n, int[] ig, int[] jg, double[] di, double[] gg, double[] b, double[] solution,
        int numThreads);
}