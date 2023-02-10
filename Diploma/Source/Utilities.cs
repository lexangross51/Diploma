namespace Diploma.Source;

public static class DataWriter
{
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

    public static void WriteSaturation(string filename, Mesh.Mesh mesh, List<List<double>>? saturation)
    {
        if (!Directory.Exists("Saturation"))
        {
            Directory.CreateDirectory("Saturation");
        }
        
        using var sw = new StreamWriter("Saturation/" + filename);

        for (var ielem = 0; ielem < saturation!.Count; ielem++)
        {
            sw.WriteLine(saturation[ielem][0]);
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