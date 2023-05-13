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

    public static double Square(Point2D leftBottom, Point2D rightBottom, Point2D leftTop, Point2D rightTop)
    {
        double det1 = leftBottom.X * rightBottom.Y - leftBottom.Y * rightBottom.X;
        double det2 = rightBottom.X * rightTop.Y - rightBottom.Y * rightTop.X;
        double det3 = rightTop.X * leftTop.Y - rightTop.Y * leftTop.X;
        double det4 = leftTop.X * leftBottom.Y - leftTop.Y * leftBottom.X;

        return 1.0 / 2.0 * (det1 + det2 + det3 + det4);
    }
}