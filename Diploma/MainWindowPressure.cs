using System.Windows;
using System.Windows.Controls;

namespace Diploma;

public sealed partial class MainWindow
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
        if (_mesh is null) return;
        
        using var sr = new StreamReader($"Pressure/Pressure{timeMoment}.txt");

        for (int i = 0; i < _mesh?.Points.Length; i++)
        {
            _pressure[i] = Convert.ToDouble(sr.ReadLine());
        }

        double pressMin = _pressure.Min();
        double pressMax = _pressure.Max();
        double stepPBig = (pressMax - pressMin) / 4.0;

        for (var ielem = 0; ielem < _mesh?.Elements.Length; ielem++)
        {
            var nodes = _mesh.Elements[ielem].Nodes;

            double centerP = (_pressure[nodes[0]] + _pressure[nodes[1]] + _pressure[nodes[2]] + _pressure[nodes[3]]) /
                             4.0;

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
       
        gl.ClearColor(1f, 1f, 1f, 1.0f);
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.LoadIdentity();
        gl.MatrixMode(OpenGL.GL_MODELVIEW);
        gl.LoadIdentity();
        gl.Ortho2D(_graphArea.Left, _graphArea.Right, _graphArea.Bottom, _graphArea.Top);
        gl.Viewport(0, 0, gl.RenderContextProvider.Width, gl.RenderContextProvider.Height);
        
        DrawAxes(PressureControl.OpenGL);

        if (_mesh is not null)
        {
            gl.PushMatrix();
            {
                gl.MatrixMode(OpenGL.GL_PROJECTION);
                gl.LoadIdentity();
                gl.MatrixMode(OpenGL.GL_MODELVIEW);
                gl.LoadIdentity();
                gl.Ortho2D(_graphArea.Left, _graphArea.Right, _graphArea.Bottom, _graphArea.Top);
                gl.Viewport(20, 20, gl.RenderContextProvider.Width - 30, gl.RenderContextProvider.Height - 30);
                // gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
                // gl.ShadeModel(OpenGL.GL_SMOOTH);
                // gl.Begin(OpenGL.GL_QUADS);
                //
                // for (var ielem = 0; ielem < _mesh.Elements.Length; ielem++)
                // {
                //     var nodes = _mesh.Elements[ielem].Nodes;
                //
                //     var c = _colorsPressure[ielem];
                //     var p1 = _mesh.Points[nodes[0]].Point;
                //     var p2 = _mesh.Points[nodes[1]].Point;
                //     var p3 = _mesh.Points[nodes[2]].Point;
                //     var p4 = _mesh.Points[nodes[3]].Point;
                //
                //     gl.Color(c.R, c.G, c.B);
                //     gl.Vertex(p1.X, p1.Y);
                //     gl.Vertex(p2.X, p2.Y);
                //     gl.Vertex(p4.X, p4.Y);
                //     gl.Vertex(p3.X, p3.Y);
                // }
                //
                // gl.End();
                
                // Show normals
                
                for (int ielem = 0; ielem < _mesh.Elements.Length; ielem++)
                {
                    if (ielem == _currentElem)
                    {
                        gl.Color(0f, 0f, 0f);
                        gl.Begin(OpenGL.GL_LINES);

                        for (int iedge = 0; iedge < 4; iedge++)
                        {
                            var edge = _mesh.Elements[ielem].Edges[iedge];
                            var p1 = _mesh.Points[edge.Node1].Point;
                            var p2 = _mesh.Points[edge.Node2].Point;

                            double midX = (p2.X + p1.X) / 2.0;
                            double midY = (p2.Y + p1.Y) / 2.0;

                            var normal = _normals[ielem, iedge];

                            gl.Vertex(midX, midY);
                            gl.Vertex(midX + normal.X, midY + normal.Y);
                        }

                        gl.End();
                        break;
                    }
                }
                

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

                //ShowDirichletConditions(args);
                ShowNeumannConditions(args);
            }
            gl.PopMatrix();

            #region Draw legend

            gl.PushMatrix();
            {
                gl.MatrixMode(OpenGL.GL_PROJECTION);
                gl.LoadIdentity();
                gl.MatrixMode(OpenGL.GL_MODELVIEW);
                gl.LoadIdentity();
                gl.Ortho2D(0, 1920, 0, 1080);
                gl.Viewport(gl.RenderContextProvider.Width - 90, 0, 90, 150);

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

                gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
                gl.ShadeModel(OpenGL.GL_SMOOTH);
                gl.Begin(OpenGL.GL_QUADS);

                double y = 1020;
                double hy = 240;
                for (int i = 0; i < 4; y -= hy, i++)
                {
                    gl.Color(_pressureLegendColors[i, 0], _pressureLegendColors[i, 1], _pressureLegendColors[i, 2]);
                    gl.Vertex(100, y);
                    gl.Vertex(800, y);
                    gl.Color(_pressureLegendColors[i + 1, 0], _pressureLegendColors[i + 1, 1], _pressureLegendColors[i + 1, 2]);
                    gl.Vertex(800, y - hy);
                    gl.Vertex(100, y - hy);
                }

                gl.End();
                
                gl.Color(0f, 0f, 0f);
                gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_LINE);
                gl.Begin(OpenGL.GL_QUADS);
                gl.Vertex(100, 60);
                gl.Vertex(800, 60);
                gl.Vertex(800, 1020);
                gl.Vertex(100, 1020);
                gl.End();

                gl.Begin(OpenGL.GL_LINES);

                y = 1020;
                hy = 960.0 / 7.0;
                for (int i = 0; i < 8; y -= hy, i++)
                {
                    gl.Vertex(800, y);
                    gl.Vertex(900, y);
                }

                gl.End();

                y = 1020;
                 for (int i = 0; i < 8; y -= hy, i++)
                 {
                     var axisText = $"{_pressureLegendValues[i] / 10000:f3}";
                     int height = (int)y;
                     
                     gl.DrawText(1000, height - 20, 0f, 0f, 0f, "Arial", 10, axisText, 1920, 1080);
                 }
            }
            gl.PopMatrix();

            #endregion
        }

        gl.Finish();
    }

    private void ShowDirichletConditions(OpenGLRoutedEventArgs args)
    {
        OpenGL gl = args.OpenGL;

        gl.Color(0f, 0f, 0f);
        gl.PointSize(5);
        gl.Begin(OpenGL.GL_POINTS);

        foreach (var (inode, _) in _mesh!.DirichletConditions)
        {
            var point = _mesh.Points[inode].Point;

            gl.Vertex(point.X, point.Y);
        }

        gl.End();
    }

    private void ShowNeumannConditions(OpenGLRoutedEventArgs args)
    {
        OpenGL gl = args.OpenGL;

        gl.Color(1f, 0f, 0f);
        gl.Begin(OpenGL.GL_LINES);

        foreach (var (ielem, iedge, _) in _mesh!.NeumannConditions)
        {
            var edge = _mesh.Elements[ielem].Edges[iedge];
            var p1 = _mesh.Points[edge.Node1].Point;
            var p2 = _mesh.Points[edge.Node2].Point;

            gl.Vertex(p1.X, p1.Y);
            gl.Vertex(p2.X, p2.Y);
        }

        gl.End();
    }

    private void Button_OnClick(object sender, RoutedEventArgs e)
    {
        var button = (sender as Button)!;

        switch (button.Name)
        {
            case "NextButton":
            {
                if (_currentElem < _mesh!.Elements.Length)
                    _currentElem++;
                break;
            }
            case "PrevButton":
            {
                if (_currentElem > 0)
                    _currentElem--;
                break;
            }
        }
    }
}