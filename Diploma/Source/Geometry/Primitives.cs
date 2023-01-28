namespace Diploma.Source.Geometry;

public readonly record struct Interval(double LeftBorder, double RightBorder)
{
    public double Length { get; } = RightBorder - LeftBorder;
}

public readonly record struct Rectangle(Point2D LeftBottom, Point2D RightTop)
{
    public double Square { get; } = (RightTop.X - LeftBottom.X) * (RightTop.Y - LeftBottom.Y);
}