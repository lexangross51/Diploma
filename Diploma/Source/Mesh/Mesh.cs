using System.Collections.Immutable;
using System.IO;

namespace Diploma.Source.Mesh;

public class Mesh
{
    public ImmutableArray<Point2D> Points { get; }
    public ImmutableArray<FiniteElement> Elements { get; }

    public Mesh(
        IEnumerable<Point2D> points,
        IEnumerable<FiniteElement> elements
    )
    {
        Points = points.ToImmutableArray();
        Elements = elements.ToImmutableArray();
    }
    
    public void Save(string path)
    {
        using var sw = new StreamWriter(path);
        sw.Write(JsonConvert.SerializeObject(this));
    }
}