namespace Diploma.Source.MathClasses;

public abstract class IterativeSolver
{
    protected TimeSpan RunningTime;
    protected SparseMatrix Matrix = default!;
    protected Vector RightPart = default!;
    public Vector? Solution { get; protected set; }
    public int MaxIters { get; }
    public double Eps { get; }

    protected IterativeSolver(int maxIters, double eps)
        => (MaxIters, Eps) = (maxIters, eps);

    public void SetSystem(SparseMatrix matrix, Vector rightPart)
        => (Matrix, RightPart) = (matrix, rightPart);

    public abstract void Compute();
}

public class CGM : IterativeSolver
{
    public CGM(int maxIters, double eps) : base(maxIters, eps)
    {
    }

    public override void Compute()
    {
        try
        {
            ArgumentNullException.ThrowIfNull(Matrix, $"{nameof(Matrix)} cannot be null, set the matrix");
            ArgumentNullException.ThrowIfNull(RightPart, $"{nameof(Vector)} cannot be null, set the vector");

            var rightPartNorm = RightPart.Norm();

            Solution = new Vector(RightPart.Length);
            Vector r = new(RightPart.Length);
            Vector z = new(RightPart.Length);
            Vector product = new(RightPart.Length);
            
            SparseMatrix.Dot(Matrix, Solution, product);

            for (int i = 0; i < RightPart.Length; i++)
            {
                r[i] = RightPart[i] - product[i];
            }
            
            Vector.Copy(r, z);
            
            var sw = Stopwatch.StartNew();
            var residualNorm = r.Norm();
            
            for (int iter = 0; iter < MaxIters && residualNorm / rightPartNorm >= Eps; iter++)
            {
                product.Clear();
                SparseMatrix.Dot(Matrix, z, product);
                var alpha = r * r / (product * z);

                for (int i = 0; i < RightPart.Length; i++)
                {
                    Solution[i] += alpha * z[i];
                }
                
                for (int i = 0; i < RightPart.Length; i++)
                {
                    r[i] -= alpha * product[i];
                }
                
                var beta = r * r / (residualNorm * residualNorm);
                residualNorm = r.Norm();
                
                for (int i = 0; i < RightPart.Length; i++)
                {
                    z[i] = r[i] + beta * z[i];
                }
            }

            sw.Stop();

            RunningTime = sw.Elapsed;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"We had problem: {ex.Message}");
        }
    }
}

public abstract class DirectSolver
{
    protected TimeSpan RunningTime;
    protected ProfileMatrix Matrix = default!;
    protected Vector RightPart = default!;
    public Vector? Solution { get; protected set; }

    public void SetSystem(SparseMatrix matrix, Vector rightPart)
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

public class LUSolver : DirectSolver
{
    public override void Compute()
    {
        Solution = new Vector(Matrix.Size);

        Stopwatch sw = Stopwatch.StartNew();
        
        LU(Matrix.Di, Matrix.GGl, Matrix.GGu);

        try {
            for (int i = 0; i < RightPart.Length; i++) {
                int i0 = Matrix.Ig[i];
                int i1 = Matrix.Ig[i + 1];

                int j = i - (i1 - i0);

                var sum = 0.0;

                for (int k = i0; k < i1; k++)
                    sum += Matrix.GGl[k] * Solution[j++];

                if (Math.Abs(Matrix.Di[i]) < 1E-16) 
                {
                    throw new Exception("Division by zero in LUSolver.Compute()");
                }

                Solution[i] = (RightPart[i] - sum) / Matrix.Di[i];
            }

            for (int i = RightPart.Length - 1; i >= 0; i--)
            {
                int i0 = Matrix.Ig[i];
                int i1 = Matrix.Ig[i + 1];

                int j = i - (i1 - i0);

                for (int k = i0; k < i1; k++)
                    Solution[j++] -= Matrix.GGu[k] * Solution[i];
            }
        }
        catch (Exception e) 
        {
            Console.WriteLine($"Exception: {e.Message}");
        }

        sw.Stop();

        RunningTime = sw.Elapsed;
    }
}