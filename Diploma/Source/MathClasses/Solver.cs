namespace Diploma.Source.MathClasses;

public abstract class IterativeSolver
{
    protected TimeSpan? _runningTime;
    protected SparseMatrix _matrix = default!;
    protected double[] _vector = default!;
    protected double[]? _solution;

    public int MaxIters { get; }
    public double Eps { get; }
    public TimeSpan? RunningTime => _runningTime;
    public ImmutableArray<double>? Solution => _solution?.ToImmutableArray();
    
    protected IterativeSolver(int maxIters, double eps)
        => (MaxIters, Eps) = (maxIters, eps);

    public void SetSystem(SparseMatrix matrix, double[] vector)
        => (_matrix, _vector) = (matrix, vector);

    public abstract void Compute();

    protected double[] Direct(double[] vector, double[] gglnew, double[] dinew)
    {
        var y = new double[vector.Length];
        Array.Copy(vector, y, vector.Length);

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

    protected double[] Reverse(double[] vector, double[] ggunew)
    {
        var result = new double[vector.Length];
        Array.Copy(vector, result, vector.Length);

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

            _solution = new double[_vector.Length];

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