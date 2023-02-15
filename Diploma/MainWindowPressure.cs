using Diploma.Source;

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

        // Legend
        _pressureLegendValues[0] = _pressure.Max();
        _pressureLegendValues[7] = _pressureLegendValues[0];

        for (int i = 0; i < _mesh!.Points.Length; i++)
        {
            if (_pressure[i] < _pressureLegendValues[7] && !_mesh.Points[i].IsFictitious)
            {
                _pressureLegendValues[7] = _pressure[i];
            }
        }
        
        double step = (_pressureLegendValues[0] - _pressureLegendValues[7]) / 7;

        for (int i = 1; i < 7; i++)
        {
            _pressureLegendValues[i] = _pressureLegendValues[0] - i * step;
        }

        // Field
        double pressMax = _pressureLegendValues[0];
        double pressMin = _pressureLegendValues[7];
        
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
                gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
                gl.ShadeModel(OpenGL.GL_SMOOTH);
                gl.Begin(OpenGL.GL_QUADS);
                
                for (var ielem = 0; ielem < _mesh.Elements.Length; ielem++)
                {
                    var nodes = _mesh.Elements[ielem].Nodes;
                
                    var c = _colorsPressure[ielem];
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

                foreach (var element in _mesh.Elements)
                {
                    var p1 = _mesh.Points[element.Nodes[0]].Point;
                    var p2 = _mesh.Points[element.Nodes[1]].Point;
                    var p3 = _mesh.Points[element.Nodes[2]].Point;
                    var p4 = _mesh.Points[element.Nodes[3]].Point;

                    gl.Vertex(p1.X, p1.Y);
                    gl.Vertex(p2.X, p2.Y);
                    gl.Vertex(p4.X, p4.Y);
                    gl.Vertex(p3.X, p3.Y);
                }

                gl.End();

                //ShowDirichletConditions(gl);
                ShowNeumannConditions(gl);
            }
            gl.PopMatrix();

            // DrawLegend(PressureControl.OpenGL, _pressureLegendValues.Select(DataConverter.PressureToAtm).ToArray(),
            //     _pressureLegendColors);
            DrawLegend(PressureControl.OpenGL, _pressureLegendValues, _pressureLegendColors);
        }

        gl.Finish();
    }

    private void ShowDirichletConditions(OpenGL gl)
    {
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

    private void ShowNeumannConditions(OpenGL gl)
    {
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
}