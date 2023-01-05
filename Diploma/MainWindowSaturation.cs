namespace Diploma;

public partial class MainWindow
{
    private readonly Color[] _colorsSaturartion;
    private readonly double[] _saturation;
    private readonly double[] _saturationLegendValues = new double[8];
    private readonly byte[,] _saturationLegendColors =
    {
        { 255, 0, 0 },
        { 255, 127, 0 },
        { 255, 255, 0 },
        { 255, 255, 255 }
    };
    
    private void MakeSaturationsColors(int timeMoment)
    {
        using var sr = new StreamReader($"Saturation/Saturation{timeMoment}.txt");

        for (int i = 0; i < _mesh.Elements.Length; i++)
        {
            _saturation[i] = Convert.ToDouble(sr.ReadLine());
        }

        double maxS = _saturation.Max(), minS = _saturation.Min();
        double stepSBig = (maxS - minS) / 3.0;
        
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            double centerP = _saturation[ielem];
        
            byte rColor, gColor, bColor;
        
            if (centerP >= minS + stepSBig * 2.0)
            {
                rColor = 255;
                gColor = (byte)(127 - (centerP - (minS + stepSBig * 2.0)) / (stepSBig / 127.0));
                bColor = 0;
            }
            else if (centerP >= minS + stepSBig)
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

            _colorsSaturartion[ielem] = new Color(rColor, gColor, bColor);
        }

        foreach (var ielem in _mesh.NeumannConditions.Select(condition => condition.Element))
        {
            _colorsSaturartion[ielem] = new Color(255, 255, 255);
        }
        
        _saturationLegendValues[0] = _saturation.Max();
        _saturationLegendValues[7] = _saturation.Min();
        double step = (_saturationLegendValues[0] - _saturationLegendValues[7]) / 7;

        for (int i = 1; i < 7; i++)
        {
            _saturationLegendValues[i] = _saturationLegendValues[0] - i * step;
        }
    }

    private void OpenGLControlSaturation_OnOpenGLDraw(object sender, OpenGLRoutedEventArgs args)
    {
        OpenGL gl = SaturationControl.OpenGL;
        
        gl.ClearColor(1, 1, 1, 1.0f);
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.LoadIdentity();
        gl.Ortho2D(_projection.Left - 1, _projection.Right + 1, _projection.Bottom - 1, _projection.Top + 1);
        gl.Viewport(0, 0, gl.RenderContextProvider.Width, gl.RenderContextProvider.Height);
        
        MakeSaturationsColors(_timeMoment);

        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT);
        gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
        gl.ShadeModel(OpenGL.GL_SMOOTH);
        gl.Begin(OpenGL.GL_QUADS);
        
        for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var nodes = _mesh.Elements[ielem].Nodes;
            
            var c = _colorsSaturartion[ielem];
            var p1 = _mesh.Points[nodes[0]];
            var p2 = _mesh.Points[nodes[1]];
            var p3 = _mesh.Points[nodes[2]];
            var p4 = _mesh.Points[nodes[3]];

            gl.Color(c.R, c.G, c.B);
            gl.Vertex(p1.X, p1.Y);
            gl.Vertex(p2.X, p2.Y);
            gl.Vertex(p4.X, p4.Y);
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
    
    private void SaturationLegend_OnOpenGLDraw(object sender, OpenGLRoutedEventArgs args)
    {
        OpenGL gl = SaturationLegend.OpenGL;
        
        gl.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.LoadIdentity();
        gl.Ortho2D(0, gl.RenderContextProvider.Width, 0, gl.RenderContextProvider.Height);
    
        double xMin = gl.RenderContextProvider.Width * 0.05;
        double xMax = gl.RenderContextProvider.Width * 0.95;
        double yMin = gl.RenderContextProvider.Height * 0.05;
        double yMax = gl.RenderContextProvider.Height * 0.5;
        double hx = (xMax - xMin) / 3;
        double x = xMin;
    
        gl.ShadeModel(OpenGL.GL_SMOOTH);
        gl.Begin(OpenGL.GL_QUADS);
        
        for (int i = 0; i < 3; x += hx, i++)
        {
            gl.Color(_saturationLegendColors[i, 0], _saturationLegendColors[i, 1], _saturationLegendColors[i, 2]);
            gl.Vertex(x, yMin);
            gl.Vertex(x, yMax);
            gl.Color(_saturationLegendColors[i + 1, 0], _saturationLegendColors[i + 1, 1], _saturationLegendColors[i + 1, 2]);
            gl.Vertex(x + hx, yMax);
            gl.Vertex(x + hx, yMin);
        }
        
        gl.End();
    
        x = xMin;
        hx = (xMax - xMin) / 7;
        
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
            var axisText = $"{_saturationLegendValues[i]:E4}";
            gl.DrawText((int)x, 30, 0f, 0f, 0f, "Arial", 10, axisText);
        }
        
        var axisTex = $"{_saturationLegendValues[7]:E4}";
        gl.DrawText((int)(x - gl.RenderContextProvider.Width * 0.01), 30, 0f, 0f, 0f, "Arial", 10, axisTex);
    }
}