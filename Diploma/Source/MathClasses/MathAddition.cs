namespace Diploma.Source.MathClasses;

public static class MathAddition
{
    public static void JacobiMatrix2D(Point2D leftBottom, Point2D rightBottom, Point2D leftTop, Point2D rightTop,
        Point2D point, SquareMatrix jacobiMatrix)
    {
        jacobiMatrix[0, 0] = point.Y * (leftBottom.X - rightBottom.X - leftTop.X + rightTop.X) + rightBottom.X - leftBottom.X;
        jacobiMatrix[0, 1] = point.Y * (leftBottom.Y - rightBottom.Y - leftTop.Y + rightTop.Y) + rightBottom.Y - leftBottom.Y;
        jacobiMatrix[1, 0] = point.X * (leftBottom.X - rightBottom.X - leftTop.X + rightTop.X) + leftTop.X - leftBottom.X;
        jacobiMatrix[1, 1] = point.X * (leftBottom.Y - rightBottom.Y - leftTop.Y + rightTop.Y) + leftTop.Y - leftBottom.Y;
    }

    public static double Jacobian2D(SquareMatrix jacobiMatrix)
        => jacobiMatrix[0, 0] * jacobiMatrix[1, 1] - jacobiMatrix[0, 1] * jacobiMatrix[1, 0];
    
    public static void InvertJacobiMatrix2D(SquareMatrix jacobiMatrix)
    {
        double jacobian = Jacobian2D(jacobiMatrix);
        
        (jacobiMatrix[0, 0], jacobiMatrix[1, 1]) = (jacobiMatrix[1, 1], jacobiMatrix[0, 0]);
        jacobiMatrix[0, 0] /= jacobian;
        jacobiMatrix[1, 1] /= jacobian;
        jacobiMatrix[0, 1] = -jacobiMatrix[0, 1] / jacobian;
        jacobiMatrix[1, 0] = -jacobiMatrix[1, 0] / jacobian;
    }
}