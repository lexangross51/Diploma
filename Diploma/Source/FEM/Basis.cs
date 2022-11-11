namespace Diploma.Source.FEM;

public struct LinearBasis : IBasis
{
    public int Size => 4;

    public double Phi(int ifunc, Point2D point)
        => ifunc switch
        {
            0 => (1 - point.X) * (1 - point.Y),
            1 => point.X * (1 - point.Y),
            2 => (1 - point.X) * point.Y,
            3 => point.X * point.Y,
            _ => throw new ArgumentOutOfRangeException(nameof(ifunc), $"Not expected ifunc value: {ifunc}")
        };

    public double DPhi(int ifunc, int ivar, Point2D point)
        => ivar switch
        {
            0 => ifunc switch
            {
                0 => point.Y - 1,
                1 => 1 - point.Y,
                2 => -point.Y,
                3 => point.Y,
                _ => throw new ArgumentOutOfRangeException(nameof(ifunc), $"Not expected ifunc value: {ifunc}")
            },
            1 => ifunc switch
            {
                0 => point.X - 1,
                1 => -point.X,
                2 => 1 - point.X,
                3 => point.X,
                _ => throw new ArgumentOutOfRangeException(nameof(ifunc), $"Not expected ifunc value: {ifunc}")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(ivar), $"Not expected ivar value: {ivar}")
        };
}