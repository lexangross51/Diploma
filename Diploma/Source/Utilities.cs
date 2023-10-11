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

    public static void WriteSaturation(string filename, Mesh.Mesh mesh, List<List<double>> saturation)
    {
        if (!Directory.Exists("Saturation"))
        {
            Directory.CreateDirectory("Saturation");
        }
        
        using var sw = new StreamWriter("Saturation/" + filename);

        for (var ielem = 0; ielem < saturation.Count; ielem++)
        {
            sw.WriteLine(mesh.Elements[ielem].IsFictitious ? 0.0 : saturation[ielem][0]);
        }
    }
    
    public static List<Point2D> GeneratePoints(double x0, double x1, double y0, double y1, int nx, int ny)
    {
        var points = new List<Point2D>();

        var hx = (x1 - x0) / (nx - 1);
        var hy = (y1 - y0) / (ny - 1);

        for (int i = 0; i < ny; i++)
        {
            for (int j = 0; j < nx; j++)
            {
                points.Add(new Point2D(x0 + j * hx, y0 + i * hy));
            }
        }
        
        return points;
    }

    public static void WriteData(string filename, List<Point2D> points, List<double> values)
    {
        var sw = new StreamWriter(filename);

        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            sw.WriteLine($"{p.X} {p.Y} {values[i]}");
        }

        sw.Close();
    }
}