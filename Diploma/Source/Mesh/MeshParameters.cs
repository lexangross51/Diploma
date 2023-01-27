using System.DirectoryServices.ActiveDirectory;

namespace Diploma.Source.Mesh;

// public class DomainXYJsonConverter : JsonConverter
// {
//     public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
//         => throw new Exception("Can't write data to json file");
//
//     public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
//     {
//         if (reader.TokenType is JsonToken.Null) return null;
//
//         var token = JObject.Load(reader);
//
//         List<DomainXY> domains = new();
//
//         // Main area
//         var area = token["Main area"]?["Area borders"];
//
//         if (area is null) throw new Exception("Main area can't be empty");
//         
//         var leftBottom = new Point2D(Convert.ToDouble(area["Left border"]), Convert.ToDouble(area["Bottom border"]));
//         var rightTop = new Point2D(Convert.ToDouble(area["Right border"]), Convert.ToDouble(area["Top border"]));
//
//         var material = JsonConvert.DeserializeObject<Material?>(token["Main area"]?["Material"]?.ToString() ?? string.Empty);
//
//         if (material is null) throw new Exception("Main area must have a set material");
//
//         double.TryParse(token["Main area"]?["Plast pressure"]?.ToString(), out double plastPressure);
//
//         domains.Add(new (leftBottom, rightTop, material.Value, plastPressure)); 
//         
//         // Subarea with another permeability 
//         area = token["Subarea"]?["Area borders"];
//
//         if (area is null) return domains.ToArray();
//         
//         leftBottom = new Point2D(Convert.ToDouble(area["Left border"]), Convert.ToDouble(area["Bottom border"]));
//         rightTop = new Point2D(Convert.ToDouble(area["Right border"]), Convert.ToDouble(area["Top border"]));
//
//         material = JsonConvert.DeserializeObject<Material?>(token["Subarea"]?["Material"]?.ToString() ?? string.Empty);
//
//         if (material is null) throw new Exception("Subarea must have a set material");
//         
//         domains.Add(new (leftBottom, rightTop, material.Value, plastPressure));
//
//         return domains.ToArray();
//     }
//
//     public override bool CanConvert(Type objectType)
//         => objectType == typeof(DomainXY);
// }

//[JsonConverter(typeof(DomainXYJsonConverter))]
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

public readonly record struct SplitParameters(int MeshNx, int MeshNy, int Nesting)
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