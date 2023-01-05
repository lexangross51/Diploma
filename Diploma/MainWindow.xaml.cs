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

    private readonly Projection _projection = new();
    private readonly Mesh _mesh;
    private int _timeStart = 0, _timeEnd = 1, _timeMoment;
    
    public MainWindow()
    {
        MeshGenerator meshGenerator = new(new MeshBuilder(MeshParameters.ReadJson("Input/")));
        _mesh = meshGenerator.CreateMesh();
        PhaseProperty phaseProperty = new(_mesh, "Input/");
        FEMBuilder femBuilder = new();

        _pressure = new double[_mesh.Points.Length];
        _saturation = new double[_mesh.Elements.Length];

        //double Field(Point2D p) => p.X*p.X - p.Y*p.Y;
        double Source(Point2D p) => 0;

        var fem = femBuilder
            .SetMesh(_mesh)
            .SetPhaseProperties(phaseProperty)
            .SetBasis(new LinearBasis())
            .SetSolver(new CGM(1000, 1E-20))
            .SetTest(Source)
            .Build();

        Filtration filtration = new(_mesh, phaseProperty, fem, new LinearBasis());
        filtration.ModelFiltration(_timeStart, _timeEnd);

        _colorsPressure = new Color[_mesh.Elements.Length];
        _colorsSaturartion = new Color[_mesh.Elements.Length];

        _projection.Left = (float)_mesh.Points[0].X;
        _projection.Bottom = (float)_mesh.Points[0].Y;
        _projection.Right = (float)_mesh.Points[_mesh.Elements[_mesh.Elements[0].Nodes[2] - 2].Nodes[1]].X; 
        _projection.Top = (float)_mesh.Points[^1].Y;

        InitializeComponent();

        TimeMoment.Text = "0";
    }
}