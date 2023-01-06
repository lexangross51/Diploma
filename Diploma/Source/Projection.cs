using SharpGL.RenderContextProviders;

namespace Diploma.Source;

public struct Projection
{
    public double Left { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
    public double Top { get; set; }

    public Projection()
        => (Left, Right, Bottom, Top) = (-1, 1, -1, 1);

    public Projection(float left, float right, float bottom, float top)
        => (Left, Right, Bottom, Top) = (left, right, bottom, top);
    
    public double Width => Right - Left;
    public double Height => Top - Bottom;

    public Point2D ToProjectionCoordinate(double x, double y, IRenderContextProvider contextProvider)
        => new(Left + Width * x / contextProvider.Width, Top - Height * y / contextProvider.Height);

    public Point2D ToScreenCoordinates(double x, double y, IRenderContextProvider contextProvider)
        => new((x - Left) * contextProvider.Width / Width, (Bottom - y) * contextProvider.Height / Height);
}