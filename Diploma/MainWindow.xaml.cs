using Diploma.Source;
using Ookii.Dialogs.Wpf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

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
    private int _timeStart, _timeEnd = 30;
    private string _path = string.Empty;

    private int _selectedPhase;
    private int _timeMoment;
    private int _deltaTime = 1;
    private Filtration _filtration;
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

    public ObservableCollection<string> Times { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _graphArea.Left = 0;
        _graphArea.Bottom = 0;
        _graphArea.Right = 1;
        _graphArea.Top = 1;

        TimeMoment = 0;
        TimeWritingText.Text = "1";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void CalculateButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_mesh is null) return;

        PhaseProperty phaseProperty = new(_mesh, "Input/");
        FEMBuilder femBuilder = new();
        _pressure = new double[_mesh.Points.Length];
        _saturation = new double[_mesh.Elements.Length].Select(_ => new double[phaseProperty.Phases!.Count]).ToArray();

        double Field(Point2D p) => p.X;
        double Source(Point2D p) => 0.0;

        var fem = femBuilder
            .SetMesh(_mesh)
            .SetPhaseProperties(phaseProperty)
            .SetBasis(new LinearBasis())
            .SetSolver(new LOS(1000, 1E-15))
            .SetTest(Source)
            .Build();

        _colorsPressure = new Color[_mesh.Elements.Length];
        _colorsSaturartion = new Color[_mesh.Elements.Length];

        _filtration = new(_mesh, phaseProperty, fem, new LinearBasis(), _path);
        _filtration.ModelFiltration(_timeStart, _timeEnd, _deltaTime);
        GenerateAndWriteTimes(_path);

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
        PhaseProperty phaseProperty = new(_mesh, "Input/");

        _pressure = new double[_mesh.Points.Length];
        _saturation = new double[_mesh.Elements.Length].Select(_ => new double[phaseProperty.Phases!.Count]).ToArray();

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

        ReadTimes(_path);

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
    }

    private void ReadTimes(string path)
    {
        using var sr = new StreamReader($"{path}/Times.txt");

        _timeStart = Convert.ToInt32(0);
        _timeEnd = int.Parse(sr.ReadLine() ?? "0");
        while (sr.ReadLine() is { } line)
        {
            Times.Add(line);
        }
    }

    private void GenerateAndWriteTimes(string path)
    {
        foreach (var time in _filtration.Times)
        {
            Times.Add(time.ToString());
        }

        using var sw = new StreamWriter($"{path}/Times.txt");

        sw.WriteLine(_timeEnd);
        foreach (var time in Times)
        {
            sw.WriteLine(time);
        }
    }
}