﻿using System.ComponentModel;
using Diploma.Source;
using Microsoft.Win32;

namespace Diploma;

public partial class MainWindow : INotifyPropertyChanged
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

    private Projection _viewport = new();
    private Projection _graphArea = new();
    private readonly Mesh _mesh;
    private int _timeStart = 0, _timeEnd = 1, _timeMoment;

    public MainWindow()
    {
        var pars = MeshParameters.ReadJson("Input/");
        MeshGenerator meshGenerator = new(new MeshBuilder(pars));
        _mesh = meshGenerator.CreateMesh();
        PhaseProperty phaseProperty = new(_mesh, "Input/");
        FEMBuilder femBuilder = new();

        _pressure = new double[_mesh.Points.Length];
        _saturation = new double[_mesh.Elements.Length];

        //double Field(Point2D p) => 10 - p.X;
        double Source(Point2D p) => 0;

        var fem = femBuilder
            .SetMesh(_mesh)
            .SetPhaseProperties(phaseProperty)
            .SetBasis(new LinearBasis())
            .SetSolver(new CGM(1000, 1E-20))
            .SetTest(Source)
            .Build();
        
        fem.Solve();

        var points = DataWriter.GeneratePoints(
            pars.Area[0].LeftBottom.X,
            pars.Area[0].RightTop.X,
            pars.Area[0].LeftBottom.Y,
            pars.Area[0].RightTop.Y,
            pars.SplitParameters.MeshNx / 2,
            pars.SplitParameters.MeshNy / 2);
        DataWriter.WriteData(
            @"C:\Users\lexan\OneDrive\Рабочий стол\НГТУ\Магистратура\1 семестр\Непрерывные математические модели\Data\Numeric\data.txt",
            points, points.Select(p => fem.ValueAtPoint(p.X, p.Y)).ToList());
        
        // Filtration filtration = new(_mesh, phaseProperty, fem, new LinearBasis());
        // filtration.ModelFiltration(_timeStart, _timeEnd);

        _colorsPressure = new Color[_mesh.Elements.Length];
        _colorsSaturartion = new Color[_mesh.Elements.Length];

        _graphArea.Left = _mesh.Points[0].X;
        _graphArea.Bottom = _mesh.Points[0].Y;
        _graphArea.Right = _mesh.Points[_mesh.Elements[_mesh.Elements[0].Nodes[2] - 2].Nodes[1]].X; 
        _graphArea.Top = _mesh.Points[^1].Y;

        _viewport.Left = _graphArea.Left - 0.07 * _graphArea.Width;
        _viewport.Right = _graphArea.Right + 0.05 * _graphArea.Width;
        _viewport.Bottom = _graphArea.Bottom - 0.05 * _graphArea.Height;
        _viewport.Top = _graphArea.Top + 0.05 * _graphArea.Height;
        
        InitializeComponent();
    }
    
    protected virtual void OnPropertyChanged(string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public int TimeMoment
    {
        get => _timeMoment;
        set
        {
            _timeMoment = value;
            OnPropertyChanged(nameof(TimeMoment));
        }
    }
}