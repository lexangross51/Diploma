namespace Diploma.Source.MathClasses;

public abstract class IterativeSolver
{
    protected TimeSpan? _runningTime;
    protected SparseMatrix _matrix = default!;
    protected Vector _vector = default!;
    protected Vector? _solution;

    public int MaxIters { get; }
    public double Eps { get; }
    public TimeSpan? RunningTime => _runningTime;
    public ImmutableArray<double>? Solution => _solution?.ToImmutableArray();

    protected IterativeSolver(int maxIters, double eps)
        => (MaxIters, Eps) = (maxIters, eps);

    public void SetSystem(SparseMatrix matrix, Vector vector)
        => (_matrix, _vector) = (matrix, vector);

    public abstract void Compute();

    protected Vector Direct(Vector vector, double[] gglnew, double[] dinew)
    {
        Vector y = new(vector.Length);
        Vector.Copy(vector, y);

        double sum = 0.0;

        for (int i = 0; i < _matrix.Size; i++)
        {
            int i0 = _matrix.Ig[i];
            int i1 = _matrix.Ig[i + 1];

            for (int k = i0; k < i1; k++)
                sum += gglnew[k] * y[_matrix.Jg[k]];

            y[i] = (y[i] - sum) / dinew[i];
            sum = 0.0;
        }

        return y;
    }

    protected Vector Reverse(Vector vector, double[] ggunew)
    {
        Vector result = new(vector.Length);
        Vector.Copy(vector, result);

        for (int i = _matrix.Size - 1; i >= 0; i--)
        {
            int i0 = _matrix.Ig[i];
            int i1 = _matrix.Ig[i + 1];

            for (int k = i0; k < i1; k++)
                result[_matrix.Jg[k]] -= ggunew[k] * result[i];
        }

        return result;
    }

    protected void LU(double[] gglnew, double[] ggunew, double[] dinew)
    {
        double suml = 0.0;
        double sumu = 0.0;
        double sumdi = 0.0;

        for (int i = 0; i < _matrix.Size; i++)
        {
            int i0 = _matrix.Ig[i];
            int i1 = _matrix.Ig[i + 1];

            for (int k = i0; k < i1; k++)
            {
                int j = _matrix.Jg[k];
                int j0 = _matrix.Ig[j];
                int j1 = _matrix.Ig[j + 1];
                int ik = i0;
                int kj = j0;

                while (ik < k && kj < j1)
                {
                    if (_matrix.Jg[ik] == _matrix.Jg[kj])
                    {
                        suml += gglnew[ik] * ggunew[kj];
                        sumu += ggunew[ik] * gglnew[kj];
                        ik++;
                        kj++;
                    }
                    else if (_matrix.Jg[ik] > _matrix.Jg[kj])
                    {
                        kj++;
                    }
                    else
                    {
                        ik++;
                    }
                }

                gglnew[k] -= suml;
                ggunew[k] = (ggunew[k] - sumu) / dinew[j];
                sumdi += gglnew[k] * ggunew[k];
                suml = 0.0;
                sumu = 0.0;
            }

            dinew[i] -= sumdi;
            sumdi = 0.0;
        }
    }
}

public class LOSLU : IterativeSolver
{
    public LOSLU(int maxIters, double eps) : base(maxIters, eps)
    {
    }

    public override void Compute()
    {
        try
        {
            ArgumentNullException.ThrowIfNull(_matrix, $"{nameof(_matrix)} cannot be null, set the matrix");
            ArgumentNullException.ThrowIfNull(_vector, $"{nameof(_vector)} cannot be null, set the vector");

            _solution = new(_vector.Length);

            double[] gglnew = new double[_matrix.GGl.Length];
            double[] ggunew = new double[_matrix.GGu.Length];
            double[] dinew = new double[_matrix.Di.Length];

            Array.Copy(_matrix.GGl, gglnew, gglnew.Length);
            Array.Copy(_matrix.GGu, ggunew, ggunew.Length);
            Array.Copy(_matrix.Di, dinew, dinew.Length);

            Stopwatch sw = Stopwatch.StartNew();

            LU(gglnew, ggunew, dinew);

            var r = Direct(_vector - (_matrix * _solution), gglnew, dinew);
            var z = Reverse(r, ggunew);
            var p = Direct(_matrix * z, gglnew, dinew);

            var squareNorm = r * r;

            for (int iter = 0; iter < MaxIters && squareNorm > Eps; iter++)
            {
                var alpha = p * r / (p * p);
                squareNorm = (r * r) - (alpha * alpha * (p * p));
                _solution += alpha * z;
                r -= alpha * p;

                var tmp = Direct(_matrix * Reverse(r, ggunew), gglnew, dinew);

                var beta = -(p * tmp) / (p * p);
                z = Reverse(r, ggunew) + (beta * z);
                p = tmp + (beta * p);
            }

            sw.Stop();

            _runningTime = sw.Elapsed;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
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
            ArgumentNullException.ThrowIfNull(_matrix, $"{nameof(_matrix)} cannot be null, set the matrix");
            ArgumentNullException.ThrowIfNull(_vector, $"{nameof(_vector)} cannot be null, set the vector");

            double vectorNorm = _vector.Norm();

            _solution = new(_vector.Length);

            Vector z = new(_vector.Length);
            var r = _vector - (_matrix * _solution);
            Vector.Copy(r, z);
            
            Stopwatch sw = Stopwatch.StartNew();

            double squareNorm = r * r;
            
            for (int iter = 0; iter < MaxIters && (Math.Sqrt(squareNorm) / vectorNorm) >= Eps; iter++)
            {
                var tmp = _matrix * z;
                var alpha = r * r / (tmp * z);
                _solution += alpha * z;
                r -= alpha * tmp;
                var beta = r * r / squareNorm;
                squareNorm = r * r;
                z = r + beta * z;
            }

            sw.Stop();

            _runningTime = sw.Elapsed;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"We had problem: {ex.Message}");
        }
    }
}

public abstract class DirectSolver
{
    protected TimeSpan? _runningTime;
    protected ProfileMatrix _matrix = default!;
    protected Vector _vector = default!;
    protected Vector? _solution;

    public TimeSpan? RunningTime => _runningTime;

    public ImmutableArray<double>? Solution => _solution?.ToImmutableArray();

    public void SetSystem(SparseMatrix matrix, Vector vector)
        => (_matrix, _vector) = (matrix.ToProfileMatrix(), vector);

    public abstract void Compute();

    protected void LU(double[] dinew, double[] gglnew, double[] ggunew)
    {
        for (int i = 1; i < _matrix.Size; i++)
        {
            double sumDi = 0.0;

            int j0 = i - (_matrix.Ig[i + 1] - _matrix.Ig[i]);

            for (int ii = _matrix.Ig[i]; ii < _matrix.Ig[i + 1]; ii++)
            {
                int j = ii - _matrix.Ig[i] + j0;
                int jbeg = _matrix.Ig[j];
                int jend = _matrix.Ig[j + 1];

                if (jbeg < jend)
                {
                    int i0 = j - (jend - jbeg);
                    int jjbeg = j0 > i0 ? j0 : i0;
                    int jjend = j < i - 1 ? j : i - 1;

                    double sumAl = 0.0;
                    double sumAu = 0.0;

                    for (int k = 0; k < jjend - jjbeg; k++)
                    {
                        int indAu = _matrix.Ig[j] + jjbeg - i0 + k;
                        int indAl = _matrix.Ig[i] + jjbeg - j0 + k;
                        sumAl += ggunew[indAu] * gglnew[indAl];
                    }

                    gglnew[ii] -= sumAl;

                    for (int k = 0; k < jjend - jjbeg; k++)
                    {
                        int indAl = _matrix.Ig[j] + jjbeg - i0 + k;
                        int indAu = _matrix.Ig[i] + jjbeg - j0 + k;
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
        _solution = new Vector(_matrix.Size);

        Stopwatch sw = Stopwatch.StartNew();
        
        LU(_matrix.Di, _matrix.GGl, _matrix.GGu);

        try {
            for (int i = 0; i < _vector.Length; i++) {
                int i0 = _matrix.Ig[i];
                int i1 = _matrix.Ig[i + 1];

                int j = i - (i1 - i0);

                var sum = 0.0;

                for (int k = i0; k < i1; k++)
                    sum += _matrix.GGl[k] * _solution[j++];

                if (Math.Abs(_matrix.Di[i]) < 1E-14) 
                {
                    throw new Exception("Division by zero in LUSolver.Compute()");
                }

                _solution[i] = (_vector[i] - sum) / _matrix.Di[i];
            }

            for (int i = _vector.Length - 1; i >= 0; i--)
            {
                int i0 = _matrix.Ig[i];
                int i1 = _matrix.Ig[i + 1];

                int j = i - (i1 - i0);

                for (int k = i0; k < i1; k++)
                    _solution[j++] -= _matrix.GGu[k] * _solution[i];
            }
        }
        catch (Exception e) 
        {
            Console.WriteLine($"Exception: {e.Message}");
        }

        sw.Stop();

        _runningTime = sw.Elapsed;
    }
}