namespace Diploma.Source.Geometry;

public readonly record struct Interval(double LeftBorder, double RightBorder)
{
    public double LeftBorder { get; init; } = LeftBorder;
    public double RightBorder { get; init; } = RightBorder;
    
    public double Length { get; } = RightBorder - LeftBorder;
}

public readonly record struct Rectangle(Point2D LeftBottom, Point2D RightTop)
{
    public Point2D LeftBottom { get; init; } = LeftBottom;
    public Point2D RightTop { get; init; } = RightTop;
    
    public double Square { get; } = (RightTop.X - LeftBottom.X) * (RightTop.Y - LeftBottom.Y);
}