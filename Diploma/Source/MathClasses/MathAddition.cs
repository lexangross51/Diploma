namespace Diploma.Source.MathClasses;

public static class MathAddition
{
    public static void JacobiMatrix2D(Point2D[] points, Point2D point, Matrix jacobiMatrix)
    {
        jacobiMatrix[0, 0] = point.Y * (points[0].X - points[1].X - points[2].X + points[3].X) + points[1].X - points[0].X;
        jacobiMatrix[0, 1] = point.Y * (points[0].Y - points[1].Y - points[2].Y + points[3].Y) + points[1].Y - points[0].Y;
        jacobiMatrix[1, 0] = point.X * (points[0].X - points[1].X - points[2].X + points[3].X) + points[2].X - points[0].X;
        jacobiMatrix[1, 1] = point.X * (points[0].Y - points[1].Y - points[2].Y + points[3].Y) + points[2].Y - points[0].Y;
    }

    public static double Jacobian2D(Matrix jacobiMatrix)
        => jacobiMatrix[0, 0] * jacobiMatrix[1, 1] - jacobiMatrix[0, 1] * jacobiMatrix[1, 0];
    
    public static void InvertJacobiMatrix2D(Matrix jacobiMatrix)
    {
        double jacobian = Jacobian2D(jacobiMatrix);
        
        (jacobiMatrix[0, 0], jacobiMatrix[1, 1]) = (jacobiMatrix[1, 1], jacobiMatrix[0, 0]);
        jacobiMatrix[0, 0] /= jacobian;
        jacobiMatrix[1, 1] /= jacobian;
        jacobiMatrix[0, 1] = -jacobiMatrix[0, 1] / jacobian;
        jacobiMatrix[1, 0] = -jacobiMatrix[1, 0] / jacobian;
    }
}