using System.ComponentModel;
using System.Runtime.CompilerServices;
using Diploma.Source;

namespace Diploma;

public sealed partial class MainWindow : INotifyPropertyChanged
{
    public readonly struct Color
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public Color(byte r, byte g, byte b)
            => (R, G, B) = (r, g, b);
    }

    private Projection _graphArea = new();
    private readonly Mesh? _mesh;
    private readonly int _timeStart = 0, _timeEnd = 1;
    private int _timeMoment;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int TimeMoment
    {
        get => _timeMoment;
        private set
        {
            _timeMoment = value;
            OnPropertyChanged();
            MakePressureColors(_timeMoment);
            MakeSaturationsColors(_timeMoment);
        }
    }

    public MainWindow()
    {
        var meshParameters = MeshParameters.ReadJson("Input/");
        MeshBuilder meshBuilder = new(meshParameters);
        _mesh = meshBuilder.Build();
        DataWriter.WriteElements("elements", _mesh);
        PhaseProperty phaseProperty = new(_mesh, "Input/");
        FEMBuilder femBuilder = new();
        
        _pressure = new double[_mesh.Points.Length];
        _saturation = new double[_mesh.Elements.Length];

        double Field(Point2D p) => p.X + p.Y;
        double Source(Point2D p) => 0.0;
        
        var fem = femBuilder
            .SetMesh(_mesh)
            .SetPhaseProperties(phaseProperty)
            .SetBasis(new LinearBasis())
            .SetSolver(new CGM(1000, 1E-20))
            .SetTest(Source)
            .Build();
        
        fem.Solve();
            
        DataWriter.WritePressure($"Pressure{0}.txt", fem.Solution!);
        DataWriter.WriteSaturation($"Saturation{0}.txt", _mesh, phaseProperty.Saturation!);
        
        Filtration filtration = new(_mesh, phaseProperty, fem, new LinearBasis());
        filtration.ModelFiltration(_timeStart, _timeEnd);
        
        _colorsPressure = new Color[_mesh.Elements.Length];
        _colorsSaturartion = new Color[_mesh.Elements.Length];
        
        double leftBottom =
            Math.Abs(meshParameters.Area[0].LeftBottom.X) > Math.Abs(meshParameters.Area[0].LeftBottom.Y)
                ? meshParameters.Area[0].LeftBottom.X
                : meshParameters.Area[0].LeftBottom.Y;
        double rightTop =
            Math.Abs(meshParameters.Area[0].RightTop.X) > Math.Abs(meshParameters.Area[0].RightTop.Y)
                ? meshParameters.Area[0].RightTop.X
                : meshParameters.Area[0].RightTop.Y;

        _graphArea.Left = leftBottom;
        _graphArea.Bottom = leftBottom;
        _graphArea.Right = rightTop;
        _graphArea.Top = rightTop;

        InitializeComponent();

        TimeMoment = 0;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}