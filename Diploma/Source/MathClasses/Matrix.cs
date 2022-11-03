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

    public static IEnumerable<double> operator *(SquareMatrix matrix, Vector vector)
    {
        if (matrix.Size != vector.Length)
        {
            throw new Exception("Numbers of columns not equal to size of vector");
        }

        Vector product = new(vector.Length);

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
    public int[] Ig { get; init; }
    public int[] Jg { get; init; }
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

    public static Vector operator *(SparseMatrix matrix, Vector vector)
    {
        Vector product = new (vector.Length);

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
    
    public ProfileMatrix ToProfileMatrix()
    {
        int[] ignew = Ig.ToArray();

        for (int i = 0; i < Size; i++)
        {
            int i0 = Ig[i];
            int i1 = Ig[i + 1];

            int profile = i1 - i0;

            if (profile > 0)
            {
                int count = i - Jg[i0];
                ignew[i + 1] = ignew[i] + count;
            }
            else
            {
                ignew[i + 1] = ignew[i];
            }
        }

        double[] gglnew = new double[ignew[^1]];
        double[] ggunew = new double[ignew[^1]];

        for (int i = 0; i < Size; i++)
        {
            int i0 = ignew[i];
            int i1 = ignew[i + 1];

            int j = i - (i1 - i0);

            int i0Old = Ig[i];

            for (int ik = i0; ik < i1; ik++, j++)
            {
                if (j == Jg[i0Old])
                {
                    gglnew[ik] = GGl[i0Old];
                    ggunew[ik] = GGu[i0Old];
                    i0Old++;
                }
                else
                {
                    gglnew[ik] = 0.0;
                    ggunew[ik] = 0.0;
                }
            }
        }

        ProfileMatrix profileMatrix = new(Di.Length, gglnew.Length)
        {
            Ig = ignew,
            Di = Di,
            GGl = gglnew,
            GGu = ggunew
        };

        return profileMatrix;
    }
}

public class ProfileMatrix
{
    public int[] Ig { get; init; }
    public double[] Di { get; init; }
    public double[] GGl { get; init; }
    public double[] GGu { get; init; }
    public int Size { get; }
    
    public ProfileMatrix(int size, int sizeOffDiag)
    {
        Size = size;
        Ig = new int[size + 1];
        GGl = new double[sizeOffDiag];
        GGu = new double[sizeOffDiag];
        Di = new double[size];
    }
    
    public static Vector operator *(ProfileMatrix matrix, Vector vector)
    {
        Vector product = new (vector.Length);

        for (int i = 0; i < product.Length; i++) {
            product[i] = matrix.Di[i] * vector[i];

            int l = matrix.Ig[i + 1] - matrix.Ig[i];
            int k = i - 1;

            for (int j = 0; j < l; j++) {
                int index = matrix.Ig[i] + j - 1;

                product[i] += matrix.GGl[index] * vector[k];
                product[k] += matrix.GGu[index] * vector[i];
            }
        }

        return product;
    }
    
    public void Clear()
    {
        Array.Fill(Di, 0.0);
        Array.Fill(GGl, 0.0);
        Array.Fill(GGu, 0.0);
    }
    
};