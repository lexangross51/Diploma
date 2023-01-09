namespace Diploma.Source.PhaseComponents;

public struct Phase
{
    [JsonProperty("Name")] public string Name;
    [JsonProperty("Density")] public double Density;
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

        int phasesCount = phaseParameters.Length;

        Saturation = new List<List<double>>();
        Phases = new List<List<Phase>>();
        InjectedPhases = new List<Phase>();
        RemoteBordersPhases = new List<Phase>();
        
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

        for (var ielem = 0; ielem < mesh.Elements.Length; ielem++)
        {
            if (mesh.Elements[ielem].IsFictitious)
            {
                Saturation?[ielem].Clear();
                Phases?[ielem].Clear();
            }
        }

        if (wellPhaseParameters != null)
        {
            foreach (var phase in wellPhaseParameters)
            {
                InjectedPhases.Add(phase);
            }
        }

        foreach (var phase in remotePhasesParameters)
        {
            RemoteBordersPhases?.Add(phase);
        }
    }
}

public class PhaseComponentsConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        => throw new Exception("Can't write data to file");

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;

        var table = JObject.Load(reader);

        var phasesCount = table.Count;
        var componentsCount = table[table.First?.Path ?? string.Empty]!.Count();

        List<string> phasesNames = new(phasesCount);
        List<string> componentsNames = new(componentsCount);
        double[,] components = new double[phasesCount, componentsCount];
        int i = 0, j;
        
        foreach (var row in table)
        {
            phasesNames.Add(row.Key);
            j = 0;
            
            foreach (var column in row.Value!)
            {
                var name = column.First?.Path;
                name = name!.Remove(0, name.IndexOf('.') + 1);
                var value = Convert.ToDouble(column.First?.First!);

                if (!componentsNames.Contains(name))
                {
                    componentsNames.Add(name);
                }
                
                components[i, j++] = value;
            }

            i++;
        }

        return new PhaseComponentsTable(phasesNames.ToArray(), componentsNames.ToArray(), components);
    }

    public override bool CanConvert(Type objectType)
        => objectType == typeof(PhaseComponentsTable);
}

[JsonConverter(typeof(PhaseComponentsConverter))]
public class PhaseComponentsTable : ICloneable
{
    private readonly string[] _phasesNames;
    private readonly string[] _componentsNames;
    private readonly double[,] _table; 

    public double this[int iphase, int icomponent]
    {
        get => _table[iphase, icomponent];
        set => _table[iphase, icomponent] = value;
    }

    public int Rows => _table.GetLength(0);
    public int Columns => _table.GetLength(1);

    public PhaseComponentsTable(string[] phasesNames, string[] componentsNames, double[,] data)
    {
        _phasesNames = phasesNames;
        _componentsNames = componentsNames;
        _table = data;
    }

    public static PhaseComponentsTable ReadJson(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath))
            {
                throw new Exception("File doesn't exist\n");
            }

            using var sr = new StreamReader(jsonPath);
            return JsonConvert.DeserializeObject<PhaseComponentsTable>(sr.ReadToEnd()) ??
                   throw new NullReferenceException("Fill the file correctly\n");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception: " + e.Message);
            throw;
        }
    }

    public void Print(string filename)
    {
        int totalSymbolsCount = 18;
        
        using StreamWriter sw = File.AppendText(filename);
        sw.Write($"{"Phase\\Components", -17}|");
        
        foreach (var name in _componentsNames)
        {
            sw.Write($"{name, -10}|");
            totalSymbolsCount += 11;
        }
        sw.WriteLine();
        
        for (int i = 0; i < totalSymbolsCount; i++)
        {
            sw.Write("-");            
        }
        sw.WriteLine();

        for (int i = 0; i < _phasesNames.Length; i++)
        {
            sw.Write($"{_phasesNames[i], -17}|");

            for (int j = 0; j < _componentsNames.Length; j++) sw.Write($"{_table[i, j].ToString("0.000000"),-10}|");
            
            sw.WriteLine();
            
            for (int j = 0; j < totalSymbolsCount; j++) sw.Write("-");
            
            sw.WriteLine();
        }
        sw.WriteLine();
    }

    public object Clone() => new PhaseComponentsTable(_phasesNames, _componentsNames, _table);
}