namespace Diploma.Source.Mesh;

#region  Domain XY

public class DomainXYJsonConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType is JsonToken.Null) return null;

        var token = JObject.Load(reader);

        var area = token["Area borders"];

        if (area is null) return null;
        
        var leftBottom = new Point2D(Convert.ToDouble(area["Left border"]), Convert.ToDouble(area["Bottom border"]));
        var rightTop = new Point2D(Convert.ToDouble(area["Right border"]), Convert.ToDouble(area["Top border"]));
            
        var material = token["Material"];

        double permeability = 0, porosity = 0;

        if (material is not null)
        {
            permeability = Convert.ToDouble(material["Permeability"]);
            porosity = Convert.ToDouble(material["Porosity"]);
        }
        
        var saturation = JsonConvert.DeserializeObject<List<double>>(token["Property"]!["Saturations"]!.ToString())!;
        var plastPressure = Convert.ToDouble(token["Plast pressure"]);

        return new DomainXY(
            leftBottom, 
            rightTop, 
            new Material(permeability, porosity), 
            saturation, 
            plastPressure);

    }

    public override bool CanConvert(Type objectType)
        => objectType == typeof(DomainXY);
}

[JsonConverter(typeof(DomainXYJsonConverter))]
public class DomainXY
{
    public Point2D LeftBottom { get; }
    public Point2D RightTop { get; }
    public Material Material { get; }
    public List<double> Saturation { get; }
    public double PlastPressure { get; }

    public DomainXY(Point2D leftBottom, Point2D rightTop, Material material, List<double> saturation, double plastPressure)
        => (LeftBottom, RightTop, Material, Saturation, PlastPressure) = (leftBottom, rightTop, material, saturation, plastPressure);

    public static DomainXY ReadJson(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath))
            {
                throw new Exception("File doesn't exist\n");
            }

            using var sr = new StreamReader(jsonPath);
            return JsonConvert.DeserializeObject<DomainXY>(sr.ReadToEnd()) ??
                   throw new NullReferenceException("Fill the file correctly\n");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception: " + e.Message);
            throw;
        }
    }
}

#endregion

#region Wells
public class Well
{
    public Point2D Center { get; }
    public double Radius { get; }
    public double Power { get; }

    public Well(Point2D center, double radius, double power)
        => (Center, Radius, Power) = (center, radius, power);

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

#endregion

#region Mesh parameters

public readonly record struct SplitParameters
{
    public int MeshNx { get; init; }
    public int MeshNy { get; init; }
    public int WellNx { get; init; }
    public int WellNy { get; init; }
    public double WellKx { get; init; }
    public double WellKy { get; init; }
    public int Nesting { get; init; }
    
    public static SplitParameters ReadJson(string path)
    {
        using var sr = new StreamReader(path);
        return JsonConvert.DeserializeObject<SplitParameters>(sr.ReadToEnd());
    }

    public static void WriteJson(SplitParameters parameters, string path)
    {
        using var sw = new StreamWriter(path);
        sw.Write(JsonConvert.SerializeObject(parameters));
    }
}

public class MeshParameters
{
    public DomainXY Area { get; }
    public Well[] Wells { get; }
    public List<double> Viscosities { get; }
    public SplitParameters SplitParameters { get; }

    public MeshParameters(DomainXY area, Well[]? wells, double[] viscosities, SplitParameters parameters)
        => (Area, Wells, Viscosities, SplitParameters) = (area, wells ?? Array.Empty<Well>(), viscosities.ToList(), parameters);

    public static MeshParameters ReadJson(string folderName)
    {
        var area = DomainXY.ReadJson(folderName + "DomainXY.json");
        var wells = Well.ReadJson(folderName + "Wells.json");

        using var sr = new StreamReader(folderName + "Phases.json");
        var viscosities = JsonConvert.DeserializeObject<double[]>(sr.ReadToEnd())!;

        var divParameters = SplitParameters.ReadJson(folderName + "MeshXY.json");

        return new MeshParameters(area, wells, viscosities, divParameters);
    }
}

#endregion 