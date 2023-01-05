using SharpGL.RenderContextProviders;

namespace Diploma.Source;

public struct Projection
{
    public float Left { get; set; }
    public float Right { get; set; }
    public float Bottom { get; set; }
    public float Top { get; set; }

    public Projection()
        => (Left, Right, Bottom, Top) = (-1, 1, -1, 1);

    public Projection(float left, float right, float bottom, float top)
        => (Left, Right, Bottom, Top) = (left, right, bottom, top);
    
    public float Width => Right - Left;
    public float Height => Top - Bottom;

    public Point2D ToProjectionCoordinate(float x, float y, IRenderContextProvider contextProvider)
        => new(Left + Width * x / contextProvider.Width, Top - Height * y / contextProvider.Height);

    public Point2D ToScreenCoordinates(float x, float y, IRenderContextProvider contextProvider)
        => new((x - Left) * contextProvider.Width / Width, (y - Bottom) * contextProvider.Height / Height);
}