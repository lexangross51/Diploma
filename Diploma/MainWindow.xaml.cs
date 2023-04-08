using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Diploma.Source;
using Ookii.Dialogs.Wpf;

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

    public event PropertyChangedEventHandler? PropertyChanged;
    private Projection _graphArea = new();
    private Mesh? _mesh;
    private readonly int _timeStart = 0, _timeEnd = 100;
    private string _path = string.Empty;
    private Point2D[] _normals;
    
    private int _timeMoment;
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
    
    private int _selectedElement;
    private double _elementSquare;
    
    public int SelectedElement
    {
        get => _selectedElement;
        private set
        {
            _selectedElement = value;
            var p1 = _mesh!.Points[_mesh.Elements[_selectedElement].Nodes[0]].Point;
            var p2 = _mesh!.Points[_mesh.Elements[_selectedElement].Nodes[1]].Point;
            var p3 = _mesh!.Points[_mesh.Elements[_selectedElement].Nodes[2]].Point;
            var p4 = _mesh!.Points[_mesh.Elements[_selectedElement].Nodes[3]].Point;

            //ElementSquare = Quadrilateral.Square(p1, p2, p3, p4);
            OnPropertyChanged();
        }
    }

    public double ElementSquare
    {
        get => _elementSquare;
        private set
        {
            _elementSquare = value;
            OnPropertyChanged();
        } 
    }

    public MainWindow()
    {
        InitializeComponent();

        _graphArea.Left = 0;
        _graphArea.Bottom = 0;
        _graphArea.Right = 1;
        _graphArea.Top = 1;
        
        TimeMoment = 0;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    
    private void CalculateButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_mesh is null) return;
        
        //DataWriter.WriteElements("elements", _mesh);
        PhaseProperty phaseProperty = new(_mesh, "Input/");
        FEMBuilder femBuilder = new();
        
        _pressure = new double[_mesh.Points.Length];
        _saturation = new double[_mesh.Elements.Length];

        double Field(Point2D p) => 10 - p.X;
        double Source(Point2D p) => 0.0;
        
        var fem = femBuilder
            .SetMesh(_mesh)
            .SetPhaseProperties(phaseProperty)
            .SetBasis(new LinearBasis())
            .SetSolver(new CGM(1000, 1E-20))
            .SetTest(Source)
            .Build();

        _colorsPressure = new Color[_mesh.Elements.Length];
        _colorsSaturartion = new Color[_mesh.Elements.Length];

        Filtration filtration = new(_mesh, phaseProperty, fem, new LinearBasis(), _path);
        filtration.ModelFiltration(_timeStart, _timeEnd);

        TimeMoment = 0;
    }
    
    private void LoadButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = "Folder to load calculate",
            UseDescriptionForTitle = true,
            Multiselect = false
        };

        if (!(bool)dialog.ShowDialog(this)!) return;
        
        _path = dialog.SelectedPath;
        var meshParameters = MeshParameters.ReadJson("Input/");
        _mesh = DataWriter.ReadMesh(_path);

        _pressure = new double[_mesh.Points.Length];
        _saturation = new double[_mesh.Elements.Length];

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

        TimeMoment = 0;
    }

    private void BuildMeshButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = "Folder to save calculate",
            UseDescriptionForTitle = true,
            Multiselect = false
        };

        if (!(bool)dialog.ShowDialog(this)!) return;
        
        _path = dialog.SelectedPath;
            
        var meshParameters = MeshParameters.ReadJson("Input/");
        MeshBuilder meshBuilder = new(meshParameters);
        _mesh = meshBuilder.Build();
        
        DataWriter.WriteMesh(_path, _mesh);
        
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
        
        _normals = new Point2D[_mesh.EdgesCount];
        bool[] isUsed = new bool[_mesh.EdgesCount];

        foreach (var element in _mesh.Elements)
        {
            for (int iedge = 0; iedge < 4; iedge++)
            {
                int globalIdx = element.EdgesIndices[iedge];
                var edge = element.Edges[iedge];
                var p1 = _mesh.Points[edge.Node1].Point;
                var p2 = _mesh.Points[edge.Node2].Point;
                double n1 = -(p2.Y - p1.Y);
                double n2 = p2.X - p1.X;
                double norm = Math.Sqrt(n1 * n1 + n2 * n2);

                if (isUsed[globalIdx])
                {
                    continue;
                }

                isUsed[globalIdx] = true;
                _normals[globalIdx] = new Point2D(n1 / norm, n2 / norm);
            }
        }
    }
}