namespace Diploma;

public partial class MainWindow
{
    private readonly Mesh _mesh;
    FEMBuilder.FEM _fem;
    private readonly double _xMin, _xMax, _yMin, _yMax; 
    
    public MainWindow()
    {
        MeshGenerator meshGenerator = new(new MeshBuilder(MeshParameters.ReadJson("Input/")));
        _mesh = meshGenerator.CreateMesh();

        FEMBuilder femBuilder = new();

        double Field(double x, double y) => x*x + y;
        double Source(double x, double y) => -2.0;

        _fem = femBuilder
            .SetMesh(_mesh)
            .SetBasis(new LinearBasis())
            .SetSolver(new LOSLU(1000, 1E-14))
            .SetTest(Source, Field)
            .Build();
        
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