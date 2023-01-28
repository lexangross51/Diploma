namespace Diploma.Source;

public static class DataWriter
{
    private static bool IsWellElement(Mesh.Mesh mesh, int ielem)
        => Enumerable.Any(mesh.NeumannConditions, condition => condition.Element == ielem);
    
    public static void WritePressure(string filename, Vector pressure)
    {
        if (!Directory.Exists("Pressure"))
        {
            Directory.CreateDirectory("Pressure");
        }
        
        using var sw = new StreamWriter("Pressure/" + filename);

        foreach (var value in pressure)
        {
            sw.WriteLine(value);
        }
    }

    public static void WriteSaturation(string filename, Mesh.Mesh mesh, List<List<double>> saturation)
    {
        if (!Directory.Exists("Saturation"))
        {
            Directory.CreateDirectory("Saturation");
        }
        
        using var sw = new StreamWriter("Saturation/" + filename);

        for (var ielem = 0; ielem < saturation.Count; ielem++)
        {
            sw.WriteLine(IsWellElement(mesh, ielem) ? 0.0 : saturation[ielem][0]);
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