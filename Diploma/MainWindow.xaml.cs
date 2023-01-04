using System.Windows.Input;
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

    private readonly Mesh _mesh;
    private readonly Color[] _colorsPressure;
    private readonly Color[] _colorsSaturartion;
    private readonly double _xMin, _xMax, _yMin, _yMax;
    private readonly double[] _pressure;
    private readonly double[] _saturation;
    private int _timeStart = 0, _timeEnd = 1, _timeMoment;

    private readonly byte[,] _pressureLegendColors =
    {
        { 255, 0, 0 },
        { 255, 102, 0 },
        { 255, 255, 0 },
        { 0, 255, 0 },
        { 128, 166, 255 },
        { 0, 0, 255 },
        { 139, 0, 255 },
        { 139, 0, 255 }
    };

    private readonly double[] _pressureLegendValues = new double[8]; 

    public MainWindow()
    {
        MeshGenerator meshGenerator = new(new MeshBuilder(MeshParameters.ReadJson("Input/")));
        _mesh = meshGenerator.CreateMesh();
        PhaseProperty phaseProperty = new(_mesh, "Input/");
        FEMBuilder femBuilder = new();

        _pressure = new double[_mesh.Points.Length];
        _saturation = new double[_mesh.Elements.Length];

        double Field(Point2D p) => p.X*p.X - p.Y*p.Y;
        double Source(Point2D p) => 0;

        var fem = femBuilder
            .SetMesh(_mesh)
            .SetPhaseProperties(phaseProperty)
            .SetBasis(new LinearBasis())
            .SetSolver(new CGM(1000, 1E-20))
            .SetTest(Source, Field)
            .Build();

        Filtration filtration = new(_mesh, phaseProperty, fem, new LinearBasis());
        filtration.ModelFiltration(_timeStart, _timeEnd);

        _colorsPressure = new Color[_mesh.Points.Length];
        _colorsSaturartion = new Color[_mesh.Points.Length];

        _xMin = _mesh.Points[0].X;
        _yMin = _mesh.Points[0].Y;
        var nx = _mesh.Elements[0].Nodes[2] - 2;
        var ix = _mesh.Elements[nx].Nodes[1];
        _xMax = _mesh.Points[ix].X;
        _yMax = _mesh.Points[^1].Y;

        InitializeComponent();

        TimeMoment.Text = "0";
    }

    #region Interface

    private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
            {
                if (_timeMoment < _timeEnd - 1) _timeMoment++;
                break;
            }
            case Key.Down:
            {
                if (_timeMoment > _timeStart) _timeMoment--;
                break;
            }
        }

        TimeMoment.Text = _timeMoment.ToString();
    }

    #endregion
    
    #region Pressure control
    
    private void MakePressureColors(int timeMoment)
    {
        var pressureLines = File.ReadAllLines("Output/Pressure" + timeMoment + ".txt");

        for (int i = 0; i < pressureLines.Length; i++)
        {
            _pressure[i] = Convert.ToDouble(pressureLines[i]);
        }

        double pressMin = _pressure.Min();
        double pressMax = _pressure.Max();

        double stepPBig = (pressMax - pressMin) / 4.0;
        double stepPSmall = stepPBig / 256.0;

        foreach (var t in _mesh.Elements)
        {
            var nodes = t.Nodes;

            double centerP = (_pressure[nodes[0]] + _pressure[nodes[1]] + _pressure[nodes[2]] + _pressure[nodes[3]]) / 4.0;

            byte rColor, gColor, bColor;

            if (centerP > pressMin + stepPBig * 3.0)
            {
                rColor = 255;
                gColor = (byte)(255 - (centerP - (pressMin + stepPBig * 3.0)) / stepPSmall);
                bColor = 0;
            }
            else if (centerP > pressMin + stepPBig * 2.0)
            {
                rColor = (byte)((centerP - (pressMin + stepPBig * 2.0)) / stepPSmall);
                gColor = 255;
                bColor = 0;
            }
            else if (centerP > pressMin + stepPBig)
            {
                byte tmp = (byte)((centerP - (pressMin + stepPBig)) / stepPSmall);
                rColor = 0;
                gColor = tmp;
                bColor = (byte)(255 - tmp);
            }
            else
            {
                byte tmp = (byte)(76 - (centerP - pressMin) / (stepPSmall * (255.0 / 76.0)));
                rColor = tmp;
                gColor = 0;
                bColor = (byte)(255 - tmp);
            }

            _colorsPressure[nodes[0]] = new Color(rColor, gColor, bColor);
            _colorsPressure[nodes[1]] = new Color(rColor, gColor, bColor);
            _colorsPressure[nodes[2]] = new Color(rColor, gColor, bColor);
            _colorsPressure[nodes[3]] = new Color(rColor, gColor, bColor);
        }

        _pressureLegendValues[0] = _pressure.Max();
        _pressureLegendValues[7] = _pressure.Min();
        double step = (_pressureLegendValues[0] - _pressureLegendValues[7]) / 7;

        for (int i = 1; i < 7; i++)
        {
            _pressureLegendValues[i] = _pressureLegendValues[0] - i * step;
        }
    }
    
    private void PressureControl_OnOpenGLDraw(object sender, OpenGLRoutedEventArgs args)
    {
        OpenGL gl = PressureControl.OpenGL;
        
        
        gl.ClearColor(1, 1, 1, 1.0f);
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.LoadIdentity();
        gl.Ortho2D(_xMin - 1, _xMax + 1, _yMin - 1, _yMax + 1);
        gl.Viewport(0, 0, gl.RenderContextProvider.Width, gl.RenderContextProvider.Height);
        
        MakePressureColors(_timeMoment);
        
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT);
        gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
        gl.Color(1, 0, 0, 1);
        gl.Begin(OpenGL.GL_QUADS);
        
        foreach (var t in _mesh.Elements)
        {
            var nodes = t.Nodes;
            var p1 = _mesh.Points[nodes[0]];
            var c1 = _colorsPressure[nodes[0]];
        
            var p2 = _mesh.Points[nodes[1]];
            var c2 = _colorsPressure[nodes[1]];
        
            var p3 = _mesh.Points[nodes[2]];
            var c3 = _colorsPressure[nodes[2]];
        
            var p4 = _mesh.Points[nodes[3]];
            var c4 = _colorsPressure[nodes[3]];
        
            gl.Color(c1.R, c1.G, c1.B);
            gl.Vertex(p1.X, p1.Y);
        
            gl.Color(c2.R, c2.G, c2.B);
            gl.Vertex(p2.X, p2.Y);
        
            gl.Color(c4.R, c4.G, c4.B);
            gl.Vertex(p4.X, p4.Y);
        
            gl.Color(c3.R, c3.G, c3.B);
            gl.Vertex(p3.X, p3.Y);
        }
        
        gl.End();
        
        gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_LINE);
        gl.Color(1, 0, 0, 1);
        gl.Begin(OpenGL.GL_QUADS);
        
        foreach (var t in _mesh.Elements)
        {
            var nodes = t.Nodes;
            var p1 = _mesh.Points[nodes[0]];
            var p2 = _mesh.Points[nodes[1]];
            var p3 = _mesh.Points[nodes[2]];
            var p4 = _mesh.Points[nodes[3]];
        
            gl.Vertex(p1.X, p1.Y);
            gl.Vertex(p2.X, p2.Y);
            gl.Vertex(p4.X, p4.Y);
            gl.Vertex(p3.X, p3.Y);
        }
        
        gl.End();
        gl.Finish();
    }
    
    #endregion

    #region Saturation control

    private void MakeSaturationsColors(int timeMoment)
    {
        var saturationLines = File.ReadAllLines("Output/Saturation" + timeMoment + ".txt");
        var usedPoint = new bool[_mesh.Points.Length].Select(_ => false).ToArray();
        
        for (int i = 0; i < saturationLines.Length; i++)
        {
            _saturation[i] = Convert.ToDouble(saturationLines[i]);
        }

        double maxS = _saturation.Max(), minS = _saturation.Min();

        double stepSBig = (maxS - minS) / 3.0;
        double stepSSmall = stepSBig / 256.0;
        
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var nodes = _mesh.Elements[ielem].Nodes;

            double centerP = _saturation[ielem];
        
            byte rColor, gColor, bColor;
        
            if (centerP > minS + stepSBig * 2.0)
            {
                rColor = 255;
                gColor = (byte)(127 - (centerP - (minS + stepSBig * 2.0)) / (stepSBig / 127.0) );
                bColor = 0;
            }
            else if (centerP > minS + stepSBig)
            {
                rColor = 255;
                gColor = (byte)(255 - (centerP - (minS + stepSBig)) / (stepSBig / 127.0));
                bColor = 0;
            }
            else
            {
                rColor = 255;
                gColor = 255;
                bColor = (byte)(255 - (centerP - minS) / (stepSBig / 255.0));
            }

            _colorsSaturartion[nodes[0]] = new Color(rColor, gColor, bColor);
            _colorsSaturartion[nodes[1]] = new Color(rColor, gColor, bColor);
            _colorsSaturartion[nodes[2]] = new Color(rColor, gColor, bColor);
            _colorsSaturartion[nodes[3]] = new Color(rColor, gColor, bColor);
        }

        foreach (var condition in _mesh.NeumannConditions)
        {
            var ielem = condition.Element;
            var nodes = _mesh.Elements[ielem].Nodes;
            
            _colorsSaturartion[nodes[0]] = new Color(255, 255, 255);
            _colorsSaturartion[nodes[1]] = new Color(255, 255, 255);
            _colorsSaturartion[nodes[2]] = new Color(255, 255, 255);
            _colorsSaturartion[nodes[3]] = new Color(255, 255, 255);
        }
    }

    private void OpenGLControlSaturation_OnOpenGLDraw(object sender, OpenGLRoutedEventArgs args)
    {
        OpenGL gl = SaturationControl.OpenGL;
        
        gl.ClearColor(1, 1, 1, 1.0f);
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.LoadIdentity();
        gl.Ortho2D(_xMin - 1, _xMax + 1, _yMin - 1, _yMax + 1);
        gl.Viewport(0, 0, gl.RenderContextProvider.Width, gl.RenderContextProvider.Height);
        
        MakeSaturationsColors(_timeMoment);

        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT);
        
        gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
        gl.ShadeModel(OpenGL.GL_SMOOTH);
        
        gl.Begin(OpenGL.GL_QUADS);
        foreach (var t in _mesh.Elements)
        {
            var nodes = t.Nodes;
            
            var p1 = _mesh.Points[nodes[0]];
            var p2 = _mesh.Points[nodes[1]];
            var p3 = _mesh.Points[nodes[2]];
            var p4 = _mesh.Points[nodes[3]];
            var c1 = _colorsSaturartion[nodes[0]];
            var c2 = _colorsSaturartion[nodes[1]];
            var c3 = _colorsSaturartion[nodes[2]];
            var c4 = _colorsSaturartion[nodes[3]];
        
            gl.Color(c1.R, c1.G, c1.B);
            gl.Vertex(p1.X, p1.Y);
        
            gl.Color(c2.R, c2.G, c2.B);
            gl.Vertex(p2.X, p2.Y);
        
            gl.Color(c4.R, c4.G, c4.B);
            gl.Vertex(p4.X, p4.Y);
        
            gl.Color(c3.R, c3.G, c3.B);
            gl.Vertex(p3.X, p3.Y);
        }
        
        gl.End();
        
        gl.Color(0, 0, 0);
        gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_LINE);
        
        gl.Begin(OpenGL.GL_QUADS);
        foreach (var t in _mesh.Elements)
        {
            var nodes = t.Nodes;
            
            var p1 = _mesh.Points[nodes[0]];
            var p2 = _mesh.Points[nodes[1]];
            var p3 = _mesh.Points[nodes[2]];
            var p4 = _mesh.Points[nodes[3]];
        
            gl.Vertex(p1.X, p1.Y);
            gl.Vertex(p2.X, p2.Y);
            gl.Vertex(p4.X, p4.Y);
            gl.Vertex(p3.X, p3.Y);
        }
        
        gl.End();
        gl.Finish();
    }

    #endregion

    #region Legends

    // Pressure legend
    private void PressureLegend_OnOpenGLDraw(object sender, OpenGLRoutedEventArgs args)
    {
        OpenGL gl = PressureLegend.OpenGL;
        
        gl.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.LoadIdentity();
        gl.Ortho2D(0, gl.RenderContextProvider.Width, 0, gl.RenderContextProvider.Height);
    
        double xMin = gl.RenderContextProvider.Width * 0.05;
        double xMax = gl.RenderContextProvider.Width * 0.95;
        double yMin = gl.RenderContextProvider.Height * 0.05;
        double yMax = gl.RenderContextProvider.Height * 0.5;
        double hx = (xMax - xMin) / 7;
        double x = xMin;
    
        gl.ShadeModel(OpenGL.GL_SMOOTH);
        gl.Begin(OpenGL.GL_QUADS);
        
        for (int i = 0; i < 7; x += hx, i++)
        {
            gl.Color(_pressureLegendColors[i, 0], _pressureLegendColors[i, 1], _pressureLegendColors[i, 2]);
            gl.Vertex(x, yMin);
            gl.Vertex(x, yMax);
            gl.Color(_pressureLegendColors[i + 1, 0], _pressureLegendColors[i + 1, 1], _pressureLegendColors[i + 1, 2]);
            gl.Vertex(x + hx, yMax);
            gl.Vertex(x + hx, yMin);
        }
        
        gl.End();
    
        x = xMin;
        
        gl.Color(0, 0, 0);
        gl.Begin(OpenGL.GL_LINES);
        
        for (int i = 0; i < 7; x += hx, i++)
        {
            gl.Vertex(x, yMin);
            gl.Vertex(x, gl.RenderContextProvider.Height * 0.6);
        }
        
        gl.Vertex(x, yMin);
        gl.Vertex(x, gl.RenderContextProvider.Height * 0.6);
        gl.End();
        gl.Finish();
    
        x = gl.RenderContextProvider.Width * 0.01;
        
        for (int i = 0; i < 7; x += hx, i++)
        {
            var axisText = $"{_pressureLegendValues[i]:E7}";
            gl.DrawText((int)x, 30, 0f, 0f, 0f, "Arial", 10, axisText);
        }
        
        var axisTex = $"{_pressureLegendValues[7]:E7}";
        gl.DrawText((int)(x - gl.RenderContextProvider.Width * 0.01), 30, 0f, 0f, 0f, "Arial", 10, axisTex);
    }
    //
    // // Saturation legend
    // private void SaturationLegend_OnOpenGLDraw(object sender, OpenGLRoutedEventArgs args)
    // {
    //     OpenGL gl = SaturationLegend.OpenGL;
    //     
    //     gl.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
    //     gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
    //     gl.MatrixMode(OpenGL.GL_PROJECTION);
    //     gl.LoadIdentity();
    //     gl.Ortho2D(0, 1920, 0, 1080);
    // }
    
    #endregion

}