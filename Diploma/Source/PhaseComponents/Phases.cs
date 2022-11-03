namespace Diploma.Source.PhaseComponents;

public struct Phase
{
    public double Density;
    public double Viscosity;
    public double Kappa;
}

public class PhaseProperty
{
    private readonly record struct PhasePropertyParameters(double Density, double Viscosity, double Saturation);

    public List<List<Phase>> Phases { get; }
    public List<List<double>>? Saturation { get; }

    public PhaseProperty(Mesh.Mesh mesh, string folderName)
    {
        using var sr = new StreamReader(folderName + "Phases.json");
        var phaseParameters = JsonConvert.DeserializeObject<PhasePropertyParameters[]>(sr.ReadToEnd()) ??
                              throw new NullReferenceException("Fill in the file correctly");
        int phasesCount = phaseParameters.Length;

        Saturation = new List<List<double>>();
        Phases = new List<List<Phase>>();
        
        for (int ielem = 0; ielem < mesh.Elements.Length; ielem++)
        {
            Saturation?.Add(new List<double>());
            Phases.Add(new List<Phase>());

            for (int iphase = 0; iphase < phasesCount; iphase++)
            {
                Saturation?[ielem].Add(phaseParameters[iphase].Saturation);

                Phases[ielem].Add(new() 
                {
                    Density = phaseParameters[iphase].Density,
                    Viscosity = phaseParameters[iphase].Viscosity,
                    Kappa = phaseParameters[iphase].Saturation
                });
            }
        }
    }
}