namespace Diploma.Source.Geometry;

public struct Interval
{
    public double LeftBorder { get; set; }
    public double RightBorder { get; set; }

    public Interval(double left, double right)
        => (LeftBorder, RightBorder) = (left, right);
    
    public double Length => RightBorder - LeftBorder;
}

public struct Rectangle
{
    public Point2D LeftBottom { get; set; }
    public Point2D RightTop { get; set; }

    public Rectangle(Point2D leftBottom, Point2D rightTop)
        => (LeftBottom, RightTop) = (leftBottom, rightTop);
    public static double Square(Point2D leftBottom, Point2D rightTop) 
        => (rightTop.X - leftBottom.X) * (rightTop.Y - leftBottom.Y);
}

public struct Quadrilateral
{
    public Point2D LeftBottom { get; set; }
    public Point2D RightBottom { get; set; }
    public Point2D LeftTop { get; set; }
    public Point2D RightTop { get; set; }

    public Quadrilateral(Point2D leftBottom, Point2D rightBottom, Point2D leftTop, Point2D rightTop)
        => (LeftBottom, RightBottom, LeftTop, RightTop) = (leftBottom, rightBottom, leftTop, rightTop);
    
    private static double TriangleSquare(double a, double b, double c)
    {
        double p = (a + b + c) / 2.0;

        return Math.Sqrt(p * (p - a) * (p - b) * (p - c));
    }
    
    public static double Square(Point2D leftBottom, Point2D rightBottom, Point2D leftTop, Point2D rightTop)
    {
        double a = Math.Sqrt((rightBottom.X - leftBottom.X) * (rightBottom.X - leftBottom.X) +
                             (rightBottom.Y - leftBottom.Y) * (rightBottom.Y - leftBottom.Y));
        double b = Math.Sqrt((leftTop.X - leftBottom.X) * (leftTop.X - leftBottom.X) +
                             (leftTop.Y - leftBottom.Y) * (leftTop.Y - leftBottom.Y));
        double c = Math.Sqrt((rightTop.X - rightBottom.X) * (rightTop.X - rightBottom.X) +
                             (rightTop.Y - rightBottom.Y) * (rightTop.Y - rightBottom.Y));
        double d = Math.Sqrt((rightTop.X - leftTop.X) * (rightTop.X - leftTop.X) +
                             (rightTop.Y - leftTop.Y) * (rightTop.Y - leftTop.Y));
        double diag = Math.Sqrt((rightTop.X - leftBottom.X) * (rightTop.X - leftBottom.X) +
                                (rightTop.Y - leftBottom.Y) * (rightTop.Y - leftBottom.Y));

        return TriangleSquare(a, c, diag) + TriangleSquare(b, d, diag);
    }
}