namespace Diploma.Source.MathClasses;

public abstract class IterativeSolver
{
    public TimeSpan RunningTime;
    protected SparseMatrix Matrix = default!;
    protected Vector RightPart = default!;
    protected readonly int MaxIters;
    protected readonly double Eps;
    public Vector? Solution { get; protected set; }

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
                product.Fill();
                SparseMatrix.Dot(Matrix, z, product);
                var alpha = Vector.Dot(r, r) / Vector.Dot(product,z);

                for (int i = 0; i < RightPart.Length; i++)
                {
                    Solution[i] += alpha * z[i];
                }
                
                for (int i = 0; i < RightPart.Length; i++)
                {
                    r[i] -= alpha * product[i];
                }
                
                var beta = Vector.Dot(r, r) / (residualNorm * residualNorm);
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

            Solution = new(RightPart.Length);

            Vector z = new(RightPart.Length);
            Vector r = new(RightPart.Length);
            Vector p = new(RightPart.Length);
            Vector product = new(RightPart.Length);
            SparseMatrix.Dot(Matrix, Solution, product);

            for (int i = 0; i < product.Length; i++)
            {
                r[i] = RightPart[i] - product[i];
            }
            
            Vector.Copy(r, z);

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
                
                squareNorm = Vector.Dot(r, r) - (alpha * alpha * Vector.Dot(p,p));
                
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