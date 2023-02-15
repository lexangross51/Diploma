namespace Diploma.Source.Geometry;

public struct Point2D
{
    public double X { get; set; }
    public double Y { get; set; }
    public Point2D(double x = 0.0, double y = 0.0) => (X, Y) = (x, y);

    public override string ToString()
        => $"{X.ToString(CultureInfo.InvariantCulture)} {Y.ToString(CultureInfo.InvariantCulture)}";
}