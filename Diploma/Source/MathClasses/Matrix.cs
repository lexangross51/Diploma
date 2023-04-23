namespace Diploma.Source.MathClasses;

public class Matrix
{
    private readonly double[,] _storage;
    public int RowsCount { get; }
    public int ColumnsCount { get; }

    public double this[int i, int j]
    {
        get => _storage[i, j];
        set => _storage[i, j] = value;
    }

    public Matrix(int nRows, int nColumns)
    {
        RowsCount = nRows;
        ColumnsCount = nColumns;
        _storage = new double[nRows, nColumns];
    }

    public static Matrix Copy(Matrix matrixToCopy)
    {
        Matrix newMatrix = new(matrixToCopy.RowsCount, matrixToCopy.ColumnsCount);

        for (int i = 0; i < matrixToCopy.RowsCount; i++)
        {
            for (int j = 0; j < matrixToCopy.ColumnsCount; j++)
            {
                newMatrix[i, j] = matrixToCopy[i, j];
            }
        }

        return newMatrix;
    }

    public static void Dot(Matrix matrix, Vector vector, Vector? product)
    {
        if (matrix.ColumnsCount != vector.Length)
        {
            throw new Exception("Numbers of columns not equal to size of vector");
        }

        product ??= new Vector(vector.Length);

        for (int i = 0; i < matrix.RowsCount; i++)
        {
            for (int j = 0; j < matrix.ColumnsCount; j++)
            {
                product[i] += matrix[i, j] * vector[j];
            }
        }
    }

    public void Clear()
    {
        for (int i = 0; i < RowsCount; i++)
        {
            for (int j = 0; j < ColumnsCount; j++)
            {
                _storage[i, j] = 0.0;
            }
        }
    }
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

    public static void Dot(SparseMatrix matrix, Vector vector, Vector? product)
    {
        if (matrix.Size != vector.Length)
        {
            throw new Exception("Size of matrix not equal to size of vector");
        }

        product ??= new Vector(vector.Length);
        product.Fill();
        int[] ig = matrix.Ig;
        int[] jg = matrix.Jg;
        double[] di = matrix.Di;
        double[] ggl = matrix.GGl;
        double[] ggu = matrix.GGu;
        
        for (int i = 0; i < vector.Length; i++)
        {
            product[i] = di[i] * vector[i];

            for (int j = ig[i]; j < ig[i + 1]; j++)
            {
                product[i] += ggl[j] * vector[jg[j]];
                product[jg[j]] += ggu[j] * vector[i];
            }
        }
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
            for (int j = 0; j <= i; j++)
            {
                sw.Write($"{a[i, j]:G}\t\t");
            }

            sw.WriteLine();
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
        double[] newDi = new double[Di.Length];
        Array.Copy(Di, newDi, Di.Length);

        int[] newIg = new int[Size + 1];
        newIg[0] = 0;

        for (int i = 0; i < Size; i++)
        {
            int i0 = Ig[i];
            int i1 = Ig[i + 1];

            int prof = i1 - i0;

            if (prof > 0)
            {
                var count = i - Jg[i0];

                newIg[i + 1] = newIg[i] + count;
            }
            else
            {
                newIg[i + 1] = newIg[i];
            }
        }

        double[] newGgl = new double[newIg[^1]];
        double[] newGgu = new double[newIg[^1]];

        for (int i = 0; i < Size; i++)
        {
            int i0P = newIg[i];
            int i1P = newIg[i + 1];

            int j0P = i - (i1P - i0P);
            int i0S = Ig[i];

            for (int rowInd = i0P; rowInd < i1P; rowInd++, j0P++)
            {
                if (i0S < Jg.Length && j0P == Jg[i0S])
                {
                    newGgl[rowInd] = GGl[i0S];
                    newGgu[rowInd] = GGu[i0S];
                    i0S++;
                }
                // else
                // {
                //     newGgl[rowInd] = 0.0;
                //     newGgu[rowInd] = 0.0;
                // }
            }
        }

        return new ProfileMatrix(Di.Length, newGgl.Length)
        {
            Di = newDi,
            Ig = newIg,
            GGl = newGgl,
            GGu = newGgu,
        };
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

    public static void Dot(ProfileMatrix matrix, Vector vector, Vector? product)
    {
        if (matrix.Size != vector.Length)
        {
            throw new Exception("Size of matrix not equal to size of vector");
        }

        product ??= new Vector(vector.Length);

        for (int i = 0; i < product.Length; i++)
        {
            product[i] = matrix.Di[i] * vector[i];

            int l = matrix.Ig[i + 1] - matrix.Ig[i];
            int k = i - 1;

            for (int j = 0; j < l; j++)
            {
                int index = matrix.Ig[i] + j - 1;

                product[i] += matrix.GGl[index] * vector[k];
                product[k] += matrix.GGu[index] * vector[i];
            }
        }
    }

    public void Clear()
    {
        Array.Fill(Di, 0.0);
        Array.Fill(GGl, 0.0);
        Array.Fill(GGu, 0.0);
    }
};