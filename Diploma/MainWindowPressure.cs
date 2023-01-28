using System.Windows.Input;

namespace Diploma;

public partial class MainWindow
{
    private readonly Color[] _colorsPressure;
    private readonly double[] _pressure;
    private readonly double[] _pressureLegendValues = new double[8];
    private readonly byte[,] _pressureLegendColors =
    {
        { 255, 0, 0 },
        { 255, 255, 0 },
        { 0, 255, 255 },
        { 0, 0, 255 },
        { 139, 0, 255 }
    };
    
    private void MakePressureColors(int timeMoment)
    {
        using var sr = new StreamReader($"Pressure/Pressure{timeMoment}.txt");

        for (int i = 0; i < _mesh.Points.Length; i++)
        {
            _pressure[i] = Convert.ToDouble(sr.ReadLine());
        }
        
        double pressMin = _pressure.Min();
        double pressMax = _pressure.Max();
        double stepPBig = (pressMax - pressMin) / 4.0;

        for (var ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        {
            var t = _mesh.Elements[ielem];
            var nodes = t.Nodes;

            double centerP = (_pressure[nodes[0]] + _pressure[nodes[1]] + _pressure[nodes[2]] + _pressure[nodes[3]]) / 4.0;
            
            byte rColor, gColor, bColor;

            if (centerP > pressMin + stepPBig * 3.0)
            {
                rColor = 255;
                gColor = (byte)(255 - (centerP - (pressMin + stepPBig * 3.0)) / (stepPBig / 255.0));
                bColor = 0;
            }
            else if (centerP > pressMin + stepPBig * 2.0)
            {
                byte tmp = (byte)((centerP - (pressMin + stepPBig * 2.0)) / (stepPBig / 255.0));
                rColor = tmp;
                gColor = 255;
                bColor = (byte)(255 - tmp);
            }
            else if (centerP > pressMin + stepPBig)
            {
                rColor = 0;
                gColor = (byte)((centerP - (pressMin + stepPBig)) / (stepPBig / 255.0));
                bColor = 255;
            }
            else
            {
                rColor = (byte)(139 - (centerP - pressMin) / (stepPBig / 139.0));
                gColor = 0;
                bColor = 255;
            }
            
            _colorsPressure[ielem] = new Color(rColor, gColor, bColor);
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
        gl.Ortho2D(_viewport.Left, _viewport.Right, _viewport.Bottom, _viewport.Top);

        //MakePressureColors(_timeMoment);

        // gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
        // gl.ShadeModel(OpenGL.GL_SMOOTH);
        // gl.Begin(OpenGL.GL_QUADS);
        //
        // for (var ielem = 0; ielem < _mesh.Elements.Length; ielem++)
        // {
        //     var nodes = _mesh.Elements[ielem].Nodes;
        //
        //     var c = _colorsPressure[ielem];
        //     var p1 = _mesh.Points[nodes[0]];
        //     var p2 = _mesh.Points[nodes[1]];
        //     var p3 = _mesh.Points[nodes[2]];
        //     var p4 = _mesh.Points[nodes[3]];
        //
        //     gl.Color(c.R, c.G, c.B);
        //     gl.Vertex(p1.X, p1.Y);
        //     gl.Vertex(p2.X, p2.Y);
        //     gl.Vertex(p4.X, p4.Y);
        //     gl.Vertex(p3.X, p3.Y);
        // }
        //
        // gl.End();
        
        gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_LINE);
        gl.Color(0, 0, 0, 1);
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

        ShowDirichletConditions(args);
        DrawAxes(PressureControl.OpenGL);
        
        gl.Finish();
    }
    
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
        double hx = (xMax - xMin) / 4;
        double x = xMin;
    
        gl.ShadeModel(OpenGL.GL_SMOOTH);
        gl.Begin(OpenGL.GL_QUADS);
        
        for (int i = 0; i < 4; x += hx, i++)
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
            var axisText = $"{_pressureLegendValues[i]:E7}";
            gl.DrawText((int)x, 30, 0f, 0f, 0f, "Arial", 10, axisText);
        }
        
        var axisTex = $"{_pressureLegendValues[7]:E7}";
        gl.DrawText((int)(x - gl.RenderContextProvider.Width * 0.01), 30, 0f, 0f, 0f, "Arial", 10, axisTex);
    }

    private void ShowDirichletConditions(OpenGLRoutedEventArgs args)
    {
        OpenGL gl = args.OpenGL;
        
        gl.Color(0f, 0f, 0f);
        gl.PointSize(5);
        gl.Begin(OpenGL.GL_POINTS);
        
        foreach (var (inode, pressure) in _mesh.DirichletConditions)
        {
            var point = _mesh.Points[inode];
            
            gl.Vertex(point.X, point.Y);
        }
        
        gl.End();
    }
}