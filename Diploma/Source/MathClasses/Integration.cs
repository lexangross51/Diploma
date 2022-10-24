namespace Diploma.Source.MathClasses;

public class Integration
{
    private readonly IEnumerable<QuadratureNode<double>> _quadratures;

    public Integration(IEnumerable<QuadratureNode<double>> quadratures) =>
        _quadratures = quadratures;

    public double Integrate1D(Func<double, double> f, Interval interval)
    {
        double a = interval.LeftBorder;
        double b = interval.RightBorder;
        double h = interval.Length;

        double sum = 0.0;

        foreach (var quad in _quadratures)
        {
            double qi = quad.Weight;
            double pi = (a + b + quad.Node * h) / 2.0;

            sum += qi * f(pi);
        }

        return sum * h / 2.0;
    }

    public double Integrate2D(Func<double, double, double> f, Rectangle rectangle)
    {
        var leftBottom = rectangle.LeftBottom;
        var rightTop = rectangle.RightTop;

        double hx = rightTop.X - leftBottom.X;
        double hy = rightTop.Y - leftBottom.Y;

        double sum = 0.0;

        foreach (var iquad in _quadratures)
        {
            double qi = iquad.Weight;
            double pi = (leftBottom.X + rightTop.X + iquad.Node * hx) / 2.0;

            foreach (var jquad in _quadratures)
            {
                double qj = jquad.Weight;
                double pj = (leftBottom.Y + rightTop.X + jquad.Node * hy) / 2.0;

                sum += qi * qj * f(pi, pj);
            }
        }

        return sum * hx * hy / 4.0;
    }
}