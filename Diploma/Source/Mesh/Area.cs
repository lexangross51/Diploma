namespace Diploma.Source.Mesh;

public struct Material
{
    public double Permeability { get; set; }
    public double Porosity { get; set; }

    public Material(double permeability, double porosity)
        => (Permeability, Porosity) = (permeability, porosity);
}

public readonly struct Area
{
    public Rectangle Borders { get; init; }
    public Material Material { get; init; }
}