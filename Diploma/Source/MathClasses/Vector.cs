namespace Diploma.Source.MathClasses;

public class Vector : IEnumerable<double>
{
    private readonly double[] _storage;
    public int Length { get; }

    public double this[int idx]
    {
        get => _storage[idx];
        set => _storage[idx] = value;
    }

    public Vector(int length)
        => (Length, _storage) = (length, new double[length]);

    public static double operator *(Vector a, Vector b)
    {
        double result = 0.0;

        for (int i = 0; i < a.Length; i++)
        {
            result += a[i] * b[i];
        }

        return result;
    }

    public static Vector operator *(double constant, Vector vector)
    {
        Vector result = new(vector.Length);

        for (int i = 0; i < vector.Length; i++)
        {
            result[i] = vector[i] * constant;
        }

        return result;
    }

    public static Vector operator +(Vector a, Vector b)
    {
        Vector result = new(a.Length);

        for (int i = 0; i < a.Length; i++)
        {
            result[i] = a[i] + b[i];
        }

        return result;
    }

    public static Vector operator -(Vector a, Vector b)
    {
        Vector result = new(a.Length);

        for (int i = 0; i < a.Length; i++)
        {
            result[i] = a[i] - b[i];
        }

        return result;
    }

    public static void Copy(Vector source, Vector destination)
    {
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    public void Fill(double value)
    {
        for (int i = 0; i < Length; i++)
        {
            _storage[i] = value;
        }
    }

    public double Norm()
    {
        double result = 0.0;

        for (int i = 0; i < Length; i++)
        {
            result += _storage[i] * _storage[i];
        }

        return Math.Sqrt(Convert.ToDouble(result));
    }

    public ImmutableArray<double> ToImmutableArray()
        => ImmutableArray.Create(_storage);

    public IEnumerator<double> GetEnumerator()
        => ((IEnumerable<double>)_storage).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    public void Clear() => Array.Fill(_storage, 0.0);
}