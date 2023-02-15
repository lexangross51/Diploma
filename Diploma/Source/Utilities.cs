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
            sw.WriteLine(value.ToString(CultureInfo.InvariantCulture));
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
            sw.WriteLine(saturation[ielem][0].ToString(CultureInfo.InvariantCulture));
        }
    }

    public static void WriteMesh(Mesh.Mesh mesh, string directory = "Mesh")
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory("Mesh");
        }

        using var swElements = new StreamWriter($"{directory}/elements");
        
        foreach (var element in mesh.Elements)
        {
            swElements.WriteLine($"{element}");
        }
        
        using var swPoints = new StreamWriter($"{directory}/points");
        
        foreach (var (point, isFictitious) in mesh.Points)
        {
            byte flag = (byte)(isFictitious ? 1 : 0);
            swPoints.WriteLine($"{point} {flag}");
        }
    }
}

public static class DataConverter
{
    //public static double PressureToPascal(double atmPressure) => atmPressure * 101325;
    public static double PressureToPascal(double atmPressure) => atmPressure;
    public static double PressureToAtm(double pascalPressure) => pascalPressure / 101325;
    //public static double FlowToCubicMetersPerSecond(double flow) => flow / (24 * 60 * 60);
    public static double FlowToCubicMetersPerSecond(double flow) => flow;
    //public static double PermeabilityToSquareMeter(double permeability) => permeability * 1.01325E-15;
    public static double PermeabilityToSquareMeter(double permeability) => permeability;
}