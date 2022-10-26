namespace Diploma.Source.Interfaces;

public interface IBasis
{
    int Size { get; }

    double Phi(int ifunc, Point2D point);

    double DPhi(int ifunc, int ivar, Point2D point);
}