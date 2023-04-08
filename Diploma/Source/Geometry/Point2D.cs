namespace Diploma.Source.Geometry;

public struct Point2D
{
    public double X { get; set; }
    public double Y { get; set; }

    public Point2D(double x = 0.0, double y = 0.0) => (X, Y) = (x, y);

    public static double Distance(Point2D a, Point2D b)
        => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}