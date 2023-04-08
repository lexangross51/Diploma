namespace Diploma.Source;

public static class DataWriter
{
    public static void WriteMesh(string path, Mesh.Mesh mesh)
    {
        using var sw = new StreamWriter($"{path}/mesh.json");
        
        sw.Write(JsonConvert.SerializeObject(mesh));
    }
    
    public static Mesh.Mesh ReadMesh(string path)
    {
        using var sr = new StreamReader($"{path}/mesh.json");
        return JsonConvert.DeserializeObject<Mesh.Mesh>(sr.ReadToEnd()) ??
                          throw new NullReferenceException("Fill the file correctly");
    }
    
    public static void WritePressure(string path, string filename, Vector pressure)
    {
        if (!Directory.Exists($"{path}/Output2D"))
        {
            Directory.CreateDirectory($"{path}/Output2D");
        }
        
        using var sw = new StreamWriter($"{path}/Output2D/" + filename);

        foreach (var value in pressure)
        {
            sw.WriteLine(value);
        }
    }

    public static void WriteSaturation(string path, string filename, Mesh.Mesh mesh, List<List<double>>? saturation)
    {
        if (!Directory.Exists($"{path}/Output2D"))
        {
            Directory.CreateDirectory($"{path}/Output2D");
        }
        
        using var sw = new StreamWriter($"{path}/Output2D/" + filename);

        for (var ielem = 0; ielem < saturation!.Count; ielem++)
        {
            sw.WriteLine(saturation[ielem][1]);
        }
    }

    public static void WriteElements(string filename, Mesh.Mesh mesh)
    {
        using var sw = new StreamWriter(filename);
        
        for (int ielem = 0; ielem < mesh.Elements.Length; ielem++)
        {
            sw.WriteLine($"Element № {ielem} ----------------------------------------");
            sw.WriteLine(mesh.Elements[ielem]);
            sw.WriteLine();
        }
    }
}

public static class DataConverter
{
    public static double PressureToPascal(double atmPressure) => atmPressure * 101325;
    public static double PressureToAtm(double pascalPressure) => pascalPressure / 101325;
    public static double FlowToCubicMetersPerSecond(double flow) => flow / (24 * 60 * 60);
    public static double PermeabilityToSquareMeter(double permeability) => permeability * 1.01325E-15;
}

// public static class DataConverter
// {
//     public static double PressureToPascal(double atmPressure) => atmPressure;
//     public static double PressureToAtm(double pascalPressure) => pascalPressure;
//     public static double FlowToCubicMetersPerSecond(double flow) => flow;
//     public static double PermeabilityToSquareMeter(double permeability) => permeability;
// }

public static class EnumerableExtensions
{
    public static int IndexOf<T>(this IEnumerable<T> collection, T element)
        => collection.TakeWhile(elem => elem == null || !elem.Equals(element)).Count();
}