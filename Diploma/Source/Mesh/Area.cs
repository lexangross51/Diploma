namespace Diploma.Source.Mesh;

public readonly struct Material
{
    public double Permeability { get; }
    public double Porosity { get; }

    public Material(double permeability, double porosity)
        => (Permeability, Porosity) = (permeability, porosity);
}

public readonly struct Area
{
    public Rectangle Borders { get; init; }
    public Material Material { get; init; }
}