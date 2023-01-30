using Diploma.Source;

namespace Diploma;

public partial class MainWindow
{
    public readonly struct Color
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public Color(byte r, byte g, byte b)
            => (R, G, B) = (r, g, b);
    }

    private Projection _viewport = new();
    private Projection _graphArea = new();
    private readonly Mesh _mesh;
    private int _timeStart = 0, _timeEnd = 1, _timeMoment;

    public MainWindow()
    {
        var meshParameters = MeshParameters.ReadJson("Input/");
        MeshBuilder meshBuilder = new MeshBuilder(meshParameters);
        _mesh = meshBuilder.Build();
        PhaseProperty phaseProperty = new(_mesh, "Input/");
        FEMBuilder femBuilder = new();
        DataWriter.WriteElements("Elements.txt", _mesh);

        _pressure = new double[_mesh.Points.Length];
        _saturation = new double[_mesh.Elements.Length];

        double Field(Point2D p) => p.X - p.Y;
        double Source(Point2D p) => 0.0;

        var fem = femBuilder
            .SetMesh(_mesh)
            .SetPhaseProperties(phaseProperty)
            .SetBasis(new LinearBasis())
            .SetSolver(new CGM(1000, 1E-20))
            .SetTest(Source, Field)
            .Build();
        
        fem.Solve();
        
        DataWriter.WritePressure($"Pressure{_timeMoment}.txt", fem.Solution!);
        DataWriter.WriteSaturation($"Saturation{_timeMoment}.txt", _mesh, phaseProperty.Saturation!);
        
        // Filtration filtration = new(_mesh, phaseProperty, fem, new LinearBasis());
        // filtration.ModelFiltration(_timeStart, _timeEnd);

        _colorsPressure = new Color[_mesh.Elements.Length];
        _colorsSaturartion = new Color[_mesh.Elements.Length];

        _graphArea.Left = meshParameters.Area[0].LeftBottom.X;
        _graphArea.Bottom = meshParameters.Area[0].LeftBottom.Y;
        _graphArea.Right = meshParameters.Area[0].RightTop.X;
        _graphArea.Top = meshParameters.Area[0].RightTop.Y;

        _viewport.Left = _graphArea.Left - 0.07 * _graphArea.Width;
        _viewport.Right = _graphArea.Right + 0.05 * _graphArea.Width;
        _viewport.Bottom = _graphArea.Bottom - 0.05 * _graphArea.Height;
        _viewport.Top = _graphArea.Top + 0.05 * _graphArea.Height;
        
        InitializeComponent();

        TimeMoment.Text = "0";
    }
}