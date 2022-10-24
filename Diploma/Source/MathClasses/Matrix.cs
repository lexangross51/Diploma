namespace Diploma.Source.MathClasses;

public class SquareMatrix
{
    private double[][] _storage;
    public int Size { get; }

    public double this[int i, int j]
    {
        get => _storage[i][j];
        set => _storage[i][j] = value;
    }

    public SquareMatrix(int size)
    {
        Size = size;
        _storage = new double[size].Select(_ => new double[size]).ToArray();
    }

    public static SquareMatrix Copy(SquareMatrix otherMatrix)
    {
        SquareMatrix newMatrix = new(otherMatrix.Size);

        for (int i = 0; i < otherMatrix.Size; i++)
        {
            for (int j = 0; j < otherMatrix.Size; j++)
            {
                newMatrix[i, j] = otherMatrix[i, j];
            }
        }

        return newMatrix;
    }

    public static IEnumerable<double> operator *(SquareMatrix matrix, double[] vector)
    {
        if (matrix.Size != vector.Length)
        {
            throw new Exception("Numbers of columns not equal to size of vector");
        }

        var product = new double[vector.Length];

        for (int i = 0; i < matrix.Size; i++)
        {
            for (int j = 0; j < matrix.Size; j++)
            {
                product[i] += matrix[i, j] * vector[j];
            }
        }

        return product;
    }

    public void Clear()
        => _storage = _storage.Select(row => row.Select(_ => 0.0).ToArray()).ToArray();
}

public class SparseMatrix
{
    public int[] Ig { get; }
    public int[] Jg { get; }
    public double[] Di { get; }
    public double[] GGl { get; }
    public double[] GGu { get; }
    public int Size { get; }

    public SparseMatrix(int size, int sizeOffDiag)
    {
        Size = size;
        Ig = new int[size + 1];
        Jg = new int[sizeOffDiag];
        GGl = new double[sizeOffDiag];
        GGu = new double[sizeOffDiag];
        Di = new double[size];
    }

    public static double[] operator *(SparseMatrix matrix, double[] vector)
    {
        var product = new double[vector.Length];

        for (int i = 0; i < vector.Length; i++)
        {
            product[i] = matrix.Di[i] * vector[i];

            for (int j = matrix.Ig[i]; j < matrix.Ig[i + 1]; j++)
            {
                product[i] += matrix.GGl[j] * vector[matrix.Jg[j]];
                product[matrix.Jg[j]] += matrix.GGu[j] * vector[i];
            }
        }

        return product;
    }

    public void PrintDense(string path)
    {
        double[,] a = new double[Size, Size];

        for (int i = 0; i < Size; i++)
        {
            a[i, i] = Di[i];

            for (int j = Ig[i]; j < Ig[i + 1]; j++)
            {
                a[i, Jg[j]] = GGl[j];
                a[Jg[j], i] = GGu[j];
            }
        }

        using var sw = new StreamWriter(path);
        for (int i = 0; i < Size; i++)
        {
            for (int j = 0; j < Size; j++)
            {
                sw.Write(a[i, j].ToString("0.0000") + "\t\t");
            }

            sw.WriteLine();
        }
    }

    public void Clear()
    {
        Array.Fill(Di, 0.0);
        Array.Fill(GGl, 0.0);
        Array.Fill(GGu, 0.0);
    }
}