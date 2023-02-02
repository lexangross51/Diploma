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

        gl.ClearColor(1f, 1f, 1.0f, 1.0f);
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.LoadIdentity();
        
        gl.Viewport(0, 0, gl.RenderContextProvider.Width, gl.RenderContextProvider.Height);
        gl.Ortho2D(_graphArea.Left, _graphArea.Right, _graphArea.Bottom, _graphArea.Top);

        gl.PushMatrix();
        {
            MakeSaturationsColors(_timeMoment);
            
            gl.Viewport(20, 20, gl.RenderContextProvider.Width - 30, gl.RenderContextProvider.Height - 30);
            gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
            gl.ShadeModel(OpenGL.GL_SMOOTH);
            gl.Begin(OpenGL.GL_QUADS);
        
            for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
            {
                var nodes = _mesh.Elements[ielem].Nodes;
            
                var c = _colorsSaturartion[ielem];
                var p1 = _mesh.Points[nodes[0]].Point;
                var p2 = _mesh.Points[nodes[1]].Point;
                var p3 = _mesh.Points[nodes[2]].Point;
                var p4 = _mesh.Points[nodes[3]].Point;
        
                gl.Color(c.R, c.G, c.B);
                gl.Vertex(p1.X, p1.Y);
                gl.Vertex(p2.X, p2.Y);
                gl.Vertex(p4.X, p4.Y);
                gl.Vertex(p3.X, p3.Y);
            }
            
            gl.End();
            
            gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_LINE);
            gl.Color(0, 0, 0, 1);
            gl.Begin(OpenGL.GL_QUADS);

            foreach (var t in _mesh.Elements)
            {
                var nodes = t.Nodes;
                var p1 = _mesh.Points[nodes[0]].Point;
                var p2 = _mesh.Points[nodes[1]].Point;
                var p3 = _mesh.Points[nodes[2]].Point;
                var p4 = _mesh.Points[nodes[3]].Point;

                gl.Vertex(p1.X, p1.Y);
                gl.Vertex(p2.X, p2.Y);
                gl.Vertex(p4.X, p4.Y);
                gl.Vertex(p3.X, p3.Y);
            }

            gl.End();
        }
        gl.PopMatrix();
        
        DrawAxes(SaturationControl.OpenGL);

        #region Draw legend

        gl.PushMatrix();
        {
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            gl.Ortho2D(0, 1920, 0, 1080);
            gl.Viewport(gl.RenderContextProvider.Width - 90, 0, 90, 150);
            
             //gl.Color(0.937f, 0.73f, 0.7f);
             gl.Color(1f, 1f, 1f);
             gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
             gl.Begin(OpenGL.GL_QUADS);
             gl.Vertex(0, 0);
             gl.Vertex(1920, 0);
             gl.Vertex(1920, 1080);
             gl.Vertex(0, 1080);
             gl.End();
            
             gl.Color(0f, 0f, 0f);
             gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_LINE);
             gl.Begin(OpenGL.GL_QUADS);
             gl.Vertex(0, 0);
             gl.Vertex(1920, 0);
             gl.Vertex(1920, 1080);
             gl.Vertex(0, 1080);
             gl.End();
            
             double y = 1040;
             double hy = 1000.0 / 3.0;
             
             gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
             gl.ShadeModel(OpenGL.GL_SMOOTH);
             gl.Begin(OpenGL.GL_QUADS);
            
             for (int i = 0; i < 3; y -= hy, i++)
             {
                 gl.Color(_saturationLegendColors[i, 0], _saturationLegendColors[i, 1], _saturationLegendColors[i, 2]);
                 gl.Vertex(100, y);
                 gl.Vertex(900, y);
                 gl.Color(_saturationLegendColors[i + 1, 0], _saturationLegendColors[i + 1, 1], _saturationLegendColors[i + 1, 2]);
                 gl.Vertex(900, y - hy);
                 gl.Vertex(100, y - hy);
             }
            
             gl.End();
            
             gl.Color(0f, 0f, 0f);
             gl.Begin(OpenGL.GL_LINES);
            
             y = 1040;
             hy = 1000.0 / 7.0;
             for (int i = 0; i < 8; y -= hy, i++)
             {
                 gl.Vertex(900, y);
                 gl.Vertex(1100, y);
             }
            
             gl.End();

             //y = 40;
             // for (int i = 0; i < 7; y += hy, i++)
             // {
             //     var axisText = $"{_pressureLegendValues[i] / 10000:f3}";
             //     int height = (int)y;
             //     
             //     gl.DrawText(1400, height, 0f, 0f, 0f, "Arial", 10, axisText, 1920, 1080);
             // }
             //var axisTex = $"{_pressureLegendValues[7]:f3}";
             var axisTex = "X";
             gl.DrawText(1200, 40, 0f, 0f, 0f, "Arial", 12, axisTex, 1920, 1080);
             gl.DrawText(500, 200, 0f, 0f, 0f, "Arial", 12, axisTex, 1920, 1080);
             gl.DrawText(500, 400, 0f, 0f, 0f, "Arial", 12, axisTex, 1920, 1080);
             gl.DrawText(1200, 1040, 0f, 0f, 0f, "Arial", 12, axisTex, 1920, 1080);
        }
        gl.PopMatrix();

        #endregion

        gl.Finish();
    }
}