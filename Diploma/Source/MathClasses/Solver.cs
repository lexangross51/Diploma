namespace Diploma.Source.MathClasses;

public abstract class IterativeSolver
{
    public TimeSpan RunningTime;
    public int IterationsCount;
    protected SparseMatrix Matrix = default!;
    protected Vector RightPart = default!;
    protected readonly int MaxIters;
    protected readonly double Eps;
    protected Vector? YVector;
    public Vector? Solution { get; protected set; }

    protected IterativeSolver(int maxIters, double eps)
        => (MaxIters, Eps) = (maxIters, eps);

    public void SetSystem(SparseMatrix matrix, Vector rightPart)
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
    
    protected void MoveForCholesky(double[] diNew, double[] ggNew, Vector vector, Vector result)
    {
        YVector ??= new Vector(RightPart.Length);
        Vector.Copy(vector, YVector);
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

        Vector.Copy(YVector, result);

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
            
            for (IterationsCount = 0; IterationsCount < MaxIters && residualNorm / rightPartNorm >= Eps; IterationsCount++)
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

public class CGMCholesky : IterativeSolver
{
    public CGMCholesky(int maxIters, double eps) : base(maxIters, eps)
    {
    }

    public override void Compute()
    {
        ArgumentNullException.ThrowIfNull(Matrix, $"{nameof(Matrix)} cannot be null, set the matrix");
        ArgumentNullException.ThrowIfNull(RightPart, $"{nameof(Vector)} cannot be null, set the vector");

        double rightPartNorm = RightPart.Norm();
        Solution = new Vector(RightPart.Length);
        Vector r = new(RightPart.Length);
        Vector z = new(RightPart.Length);
        Vector result = new(RightPart.Length);
        Vector product = new(RightPart.Length);
        double[] diNew = new double[Matrix.Size];
        double[] ggNew = new double[Matrix.GGl.Length];
        Array.Copy(Matrix.Di, diNew, Matrix.Size);
        Array.Copy(Matrix.GGl, ggNew, Matrix.GGl.Length);
        
        CholeskyDecomposition(diNew, ggNew);
        
        SparseMatrix.Dot(Matrix, Solution, product);

        for (int i = 0; i < r.Length; i++)
        {
            r[i] = RightPart[i] - product[i];
        }
        
        MoveForCholesky(diNew, ggNew, r, z);

        var sw = Stopwatch.StartNew();
        
        for (IterationsCount = 0; IterationsCount < MaxIters && r.Norm() / rightPartNorm >= Eps; IterationsCount++)
        {
            MoveForCholesky(diNew, ggNew, r, result);
            
            SparseMatrix.Dot(Matrix, z, product);
            double mrDotR = Vector.Dot(result, r);
            double alpha = mrDotR / Vector.Dot(product, z);

            for (int i = 0; i < Solution.Length; i++)
            {
                Solution[i] += alpha * z[i];
                r[i] -= alpha * product[i];
            }
            
            MoveForCholesky(diNew, ggNew, r, result);

            double beta = Vector.Dot(result, r) / mrDotR;

            for (int i = 0; i < z.Length; i++)
            {
                z[i] = result[i] + beta * z[i];
            }
        }
        
        sw.Stop();
        RunningTime = sw.Elapsed;
    }
}

public class BiCGSTAB : IterativeSolver
{
    public BiCGSTAB(int maxIters, double eps) : base(maxIters, eps)
    {
    }

    public override void Compute()
    {
        try
        {
            ArgumentNullException.ThrowIfNull(Matrix, $"{nameof(Matrix)} cannot be null, set the matrix");
            ArgumentNullException.ThrowIfNull(RightPart, $"{nameof(Vector)} cannot be null, set the vector");

            double rightPartNorm = RightPart.Norm();
            Solution = new Vector(RightPart.Length);
            Vector product = new(RightPart.Length);
            Vector rk = new(RightPart.Length);
            Vector r = new(RightPart.Length);
            double rhok = 1.0, alphak = 1.0, omegak = 1.0;
            Vector vk = new(RightPart.Length);
            Vector pk = new(RightPart.Length);
            Vector sk = new(RightPart.Length);
            Vector tk = new(RightPart.Length);
            double betaK;
            
            SparseMatrix.Dot(Matrix, Solution, product);

            for (int i = 0; i < rk.Length; i++)
            {
                rk[i] = RightPart[i] - product[i];
                r[i] = rk[i];
            }

            var sw = Stopwatch.StartNew(); 
            
            for (IterationsCount = 0; IterationsCount < MaxIters && rk.Norm() / rightPartNorm >= Eps; IterationsCount++)
            {
                betaK = alphak / (rhok * omegak);
                rhok = Vector.Dot(r, rk);
                betaK *= rhok;

                for (int i = 0; i < pk.Length; i++)
                {
                    pk[i] = rk[i] + betaK * (pk[i] - omegak * vk[i]);
                }
                
                SparseMatrix.Dot(Matrix, pk, vk);
                alphak = rhok / Vector.Dot(r, vk);

                for (int i = 0; i < sk.Length; i++)
                {
                    sk[i] = rk[i] - alphak * vk[i];
                }
                
                SparseMatrix.Dot(Matrix, sk, tk);
                omegak = Vector.Dot(tk, sk) / Vector.Dot(tk, tk);

                for (int i = 0; i < Solution.Length; i++)
                {
                    Solution[i] = Solution[i] + omegak * sk[i] + alphak * pk[i];
                }

                for (int i = 0; i < rk.Length; i++)
                {
                    rk[i] = sk[i] - omegak * tk[i];
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