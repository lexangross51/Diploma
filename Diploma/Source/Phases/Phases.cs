namespace Diploma.Source.Phases;

public struct Phase
{
    [JsonProperty("Name")] public string Name;
    [JsonProperty("Viscosity")] public double Viscosity;
    [JsonProperty("Saturation")] public double Kappa;

    public static double KappaDependence(double saturation) => saturation;
}

public class PhaseProperty
{
    public List<List<Phase>>? Phases { get; }
    public List<List<double>>? Saturation { get; }
    public List<Phase>? InjectedPhases { get; }
    public List<Phase>? RemoteBordersPhases { get; }

    public PhaseProperty(Mesh.Mesh mesh, string folderName)
    {
        Phase[]? phaseParameters;
        Phase[]? wellPhaseParameters;
        Phase[]? remotePhasesParameters;

        using (var sr = new StreamReader(folderName + "AreaPhases.json"))
        {
            phaseParameters = JsonConvert.DeserializeObject<Phase[]>(sr.ReadToEnd()) ??
                              throw new NullReferenceException("Fill the file correctly");
        }

        using (var sr = new StreamReader(folderName + "InjectedPhase.json"))
        {
            wellPhaseParameters = JsonConvert.DeserializeObject<Phase[]>(sr.ReadToEnd()) ?? null;
        }

        using (var sr = new StreamReader(folderName + "RemotePhases.json"))
        {
            remotePhasesParameters = JsonConvert.DeserializeObject<Phase[]>(sr.ReadToEnd()) ??
                                     throw new NullReferenceException("Fill the file correctly");
        }

        int elementsCount = mesh.Elements.Length;
        int phasesCount = phaseParameters.Length;

        Phases = new List<List<Phase>>(elementsCount * phasesCount);
        Saturation = new List<List<double>>(elementsCount * phasesCount);
        RemoteBordersPhases = new List<Phase>(remotePhasesParameters.Length);

        for (int ielem = 0; ielem < mesh.Elements.Length; ielem++)
        {
            Saturation?.Add(new List<double>());
            Phases?.Add(new List<Phase>());

            for (int iphase = 0; iphase < phasesCount; iphase++)
            {
                Saturation?[ielem].Add(phaseParameters[iphase].Kappa);
                phaseParameters[iphase].Kappa = Phase.KappaDependence(phaseParameters[iphase].Kappa);
                Phases?[ielem].Add(phaseParameters[iphase]);
            }
        }

        if (wellPhaseParameters is not null)
        {
            InjectedPhases = new List<Phase>(wellPhaseParameters.Length);

            foreach (var phase in wellPhaseParameters)
            {
                InjectedPhases!.Add(phase);
            }
        }

        foreach (var phase in remotePhasesParameters)
        {
            RemoteBordersPhases?.Add(phase);
        }
    }
}