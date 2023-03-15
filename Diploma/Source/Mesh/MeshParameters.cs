namespace Diploma.Source.Mesh;

public class DomainXY
{
    public Point2D LeftBottom { get; }
    public Point2D RightTop { get; }
    public Material Material { get; }
    public double PlastPressure { get; }

    public DomainXY(Point2D leftBottom, Point2D rightTop, Material material, double plastPressure)
        => (LeftBottom, RightTop, Material, PlastPressure) = (leftBottom, rightTop, material, plastPressure);

    public static DomainXY[] ReadJson(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath))
            {
                throw new Exception("File doesn't exist\n");
            }

            using var sr = new StreamReader(jsonPath);
            return JsonConvert.DeserializeObject<DomainXY[]>(sr.ReadToEnd()) ??
                   throw new NullReferenceException("Fill the file correctly\n");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception: " + e.Message);
            throw;
        }
    }
}

public class Well
{
    public Point2D Center { get; }
    public double Radius { get; }
    public double OuterRadius { get; }  // is used to build mesh
    public double Power { get; }

    public Well(Point2D center, double radius, double outerRadius, double power)
        => (Center, Radius, OuterRadius, Power) = (center, radius, outerRadius, power);

    public static Well[]? ReadJson(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath))
            {
                throw new Exception("File doesn't exist");
            }

            using var sr = new StreamReader(jsonPath);
            return JsonConvert.DeserializeObject<Well[]>(sr.ReadToEnd()) ?? null;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception: " + e.Message);
            throw;
        }
    }
}

public readonly record struct SplitParameters(int MeshNx, int MeshNy, double WellsCoefficient, int Nesting)
{
    public static SplitParameters ReadJson(string path)
    {
        using var sr = new StreamReader(path);
        return JsonConvert.DeserializeObject<SplitParameters>(sr.ReadToEnd());
    }
}

public class MeshParameters 
{
    public DomainXY[] Area { get; }
    public Well[] Wells { get; }
    public SplitParameters SplitParameters { get; }

    private MeshParameters(DomainXY[] areas, Well[]? wells, SplitParameters parameters)
        => (Area, Wells, SplitParameters) = (areas, wells ?? Array.Empty<Well>(), parameters);

    public static MeshParameters ReadJson(string folderName)
    {
        var area = DomainXY.ReadJson(folderName + "DomainXY.json");
        var wells = Well.ReadJson(folderName + "Wells.json");
        var splitParameters = SplitParameters.ReadJson(folderName + "MeshXY.json");

        return new MeshParameters(area, wells, splitParameters);
    }
}