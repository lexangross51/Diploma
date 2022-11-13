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
    private readonly Color[] _colors;
    private readonly double _xMin, _xMax, _yMin, _yMax;

    public MainWindow()
    {
        MeshGenerator meshGenerator = new(new MeshBuilder(MeshParameters.ReadJson("Input/")));
        _mesh = meshGenerator.CreateMesh();
        PhaseProperty phaseProperty = new(_mesh, "Input/");
        FEMBuilder femBuilder = new();

        double Field(Point2D p) => p.X + p.Y;
        double Source(Point2D p) => 0;

        var fem = femBuilder
            .SetMesh(_mesh)
            .SetPhaseProperties(phaseProperty)
            .SetBasis(new LinearBasis())
            .SetSolver(new CGM(1000, 1E-20))
            .SetTest(Source)
            .Build();

        Filtration filtration = new(_mesh, phaseProperty, fem, new LinearBasis());
        filtration.ModelFlows();

        _colors = new Color[_mesh.Points.Length];
        var pressure = fem.Solution!.Value;
        double pressMin = pressure.Min();
        double pressMax = pressure.Max();

        double stepPBig = (pressMax - pressMin) / 4.0;
        double stepPSmall = stepPBig / 256.0;

        #region Rainbow

        foreach (var t in _mesh.Elements)
        {
            var nodes = t.Nodes;
        
            double centerP = (pressure[nodes[0]] + pressure[nodes[1]] + pressure[nodes[2]] + pressure[nodes[3]]) / 4.0;
        
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

            _colors[nodes[0]] = new Color(rColor, gColor, bColor);
            _colors[nodes[1]] = new Color(rColor, gColor, bColor);
            _colors[nodes[2]] = new Color(rColor, gColor, bColor);
            _colors[nodes[3]] = new Color(rColor, gColor, bColor);
        }

        #endregion
        
        #region From red to white

        // foreach (var t in _mesh.Elements)
        // {
        //     var nodes = t.Nodes;
        //
        //     double centerP = (pressure[nodes[0]] + pressure[nodes[1]] + pressure[nodes[2]] + pressure[nodes[3]]) / 4.0;
        //
        //     byte rColor, gColor, bColor;
        //
        //     if (centerP > pressMin + stepPBig * 2.0)
        //     {
        //         rColor = 255;
        //         gColor = (byte)(127 - (centerP - (pressMin + stepPBig * 2.0)) / (stepPBig / 127.0) );
        //         bColor = 0;
        //     }
        //     else if (centerP > pressMin + stepPBig)
        //     {
        //         rColor = 255;
        //         gColor = (byte)(255 - (centerP - (pressMin + stepPBig)) / (stepPBig / 127.0));
        //         bColor = 0;
        //     }
        //     else
        //     {
        //         rColor = 255;
        //         gColor = 255;
        //         bColor = (byte)(255 - (centerP - pressMin) / (stepPBig / 255.0));
        //     }
        //
        //     _colors[nodes[0]] = new Color(rColor, gColor, bColor);
        //     _colors[nodes[1]] = new Color(rColor, gColor, bColor);
        //     _colors[nodes[2]] = new Color(rColor, gColor, bColor);
        //     _colors[nodes[3]] = new Color(rColor, gColor, bColor);
        // }
        
        #endregion

        _xMin = _mesh.Points[0].X;
        _yMin = _mesh.Points[0].Y;
        var nx = _mesh.Elements[0].Nodes[2] - 2;
        var ix = _mesh.Elements[nx].Nodes[1];
        _xMax = _mesh.Points[ix].X;
        _yMax = _mesh.Points[^1].Y;

        InitializeComponent();
    }

    private void OpenGLControl_OnOpenGLInitialized(object sender, OpenGLRoutedEventArgs args)
    {
        OpenGL gl = args.OpenGL;

        gl.ClearColor(1, 1, 1, 1.0f);
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.LoadIdentity();
        gl.Ortho2D(_xMin - 1, _xMax + 1, _yMin - 1, _yMax + 1);
        gl.Viewport(0, 0, gl.RenderContextProvider.Width, gl.RenderContextProvider.Height);
    }

    private void OpenGLControl_OnOpenGLDraw(object sender, OpenGLRoutedEventArgs args)
    {
        OpenGL gl = args.OpenGL;

        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT);
        gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
        gl.Color(1, 0, 0, 1);
        gl.Begin(OpenGL.GL_QUADS);
        
        foreach (var t in _mesh.Elements)
        {
            var nodes = t.Nodes;
            var p1 = _mesh.Points[nodes[0]];
            var c1 = _colors[nodes[0]];
            
            var p2 = _mesh.Points[nodes[1]];
            var c2 = _colors[nodes[1]];
            
            var p3 = _mesh.Points[nodes[2]];
            var c3 = _colors[nodes[2]];
            
            var p4 = _mesh.Points[nodes[3]];
            var c4 = _colors[nodes[3]];
            
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
        gl.Flush();
    }

    private void OpenGLControl_OnResized(object sender, OpenGLRoutedEventArgs args)
        => OpenGLControl_OnOpenGLInitialized(sender, args);
}