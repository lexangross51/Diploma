using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Diploma;

public sealed partial class MainWindow
{
    private bool _canNavigate;
    private Point2D _fulcrum;
    private double _xShift, _yShift;
    
    private void DataGridTimes_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataGridTimes.SelectedItem is not null)
        {
            TimeMoment = Convert.ToInt32(DataGridTimes.SelectedItem);
        }
    }

    // Pressure control
    private void PressureControl_OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(PressureControl);
        var projPoint = _graphArea.ToProjectionCoordinate(pos.X, pos.Y, PressureControl.OpenGL.RenderContextProvider);
        
        if (_canNavigate)
        {
            var xShift = projPoint.X - _fulcrum.X;
            var yShift = projPoint.Y - _fulcrum.Y;

            _xShift -= xShift;
            _yShift -= yShift;

            _graphArea.Left -= xShift;
            _graphArea.Right -= xShift;
            _graphArea.Bottom -= yShift;
            _graphArea.Top -= yShift;
        }
    }
    
    private void PressureControl_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        OpenGL gl = PressureControl.OpenGL;
        var cursorPosition = e.GetPosition(PressureControl);
        double xPos = cursorPosition.X;
        double yPos = cursorPosition.Y;
        Point2D screenPoint = _graphArea.ToProjectionCoordinate(xPos, yPos, gl.RenderContextProvider);

        if (e.Delta > 0)
        {
            Scale(screenPoint, 1.05);
        }
        else
        {
            Scale(screenPoint, 1.0 / 1.05);
        }
    }
    
    private void PressureControl_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenGL gl = PressureControl.OpenGL;
        
        _canNavigate = true;
        var cursorPosition = e.GetPosition(PressureControl);
        double xPos = (float)cursorPosition.X;
        double yPos = (float)cursorPosition.Y;
        _fulcrum = _graphArea.ToProjectionCoordinate(xPos, yPos, gl.RenderContextProvider);
    }

    private void PressureControl_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => _canNavigate = false;
    
    // Saturation control
    private void SaturationControl_OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(SaturationControl);
        var projPoint = _graphArea.ToProjectionCoordinate(pos.X, pos.Y, SaturationControl.OpenGL.RenderContextProvider);
        
        if (_canNavigate)
        {
            var xShift = projPoint.X - _fulcrum.X;
            var yShift = projPoint.Y - _fulcrum.Y;
            
            _xShift -= xShift;
            _yShift -= yShift;

            _graphArea.Left -= xShift;
            _graphArea.Right -= xShift;
            _graphArea.Bottom -= yShift;
            _graphArea.Top -= yShift;
        }
    }
    
    private void SaturationControl_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        OpenGL gl = SaturationControl.OpenGL;
        var cursorPosition = e.GetPosition(SaturationControl);
        double xPos = cursorPosition.X;
        double yPos = cursorPosition.Y;
        Point2D screenPoint = _graphArea.ToProjectionCoordinate(xPos, yPos, gl.RenderContextProvider);

        if (e.Delta > 0)
        {
            Scale(screenPoint, 1.1);
        }
        else
        {
            Scale(screenPoint, 1.0 / 1.1);
        }
    }

    private void SaturationControl_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenGL gl = SaturationControl.OpenGL;
        
        _canNavigate = true;
        var cursorPosition = e.GetPosition(SaturationControl);
        double xPos = (float)cursorPosition.X;
        double yPos = (float)cursorPosition.Y;
        _fulcrum = _graphArea.ToProjectionCoordinate(xPos, yPos, gl.RenderContextProvider);
    }

    private void SaturationControl_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => _canNavigate = false;

    // Common
    private void Scale(Point2D pivot, double scale)
    {
        var pL = (pivot.X - _graphArea.Left) / scale;
        var pR = (_graphArea.Right - pivot.X) / scale;
        var pB = (pivot.Y - _graphArea.Bottom) / scale;
        var pT = (_graphArea.Top - pivot.Y) / scale;

        _graphArea.Left = pivot.X - pL;
        _graphArea.Right = pivot.X + pR;
        _graphArea.Bottom = pivot.Y - pB;
        _graphArea.Top = pivot.Y + pT;
    }

    private void DrawAxes(OpenGL gl)
    {
        gl.PushMatrix();
        {
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();
            gl.Viewport(20, 20, gl.RenderContextProvider.Width - 30, gl.RenderContextProvider.Height - 30);
            gl.Color(0f, 0f, 0f);
            gl.Begin(OpenGL.GL_LINES);
            // X & Y axes
            gl.Vertex(-1, -1);
            gl.Vertex(1, -1);
            gl.Vertex(-1, -1);
            gl.Vertex(-1, 1);
            
            // For borders
            gl.Vertex(1, -1);
            gl.Vertex(1, 1);
            gl.Vertex(-1, 1);
            gl.Vertex(1, 1);
            gl.End();
        }
        gl.PopMatrix();
        
        double hx = _graphArea.Width / 10.0;
        double hy = _graphArea.Height / 10.0;
        int xSteps = (int)(Math.Abs(_graphArea.Left - 2 * _xShift) / hx);
        int ySteps = (int)(Math.Abs(_graphArea.Bottom - 2 * _yShift) / hy);

        gl.Color(0, 0, 0);
        gl.PushMatrix();
        {
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();
            gl.Ortho2D(_graphArea.Left, _graphArea.Right, _graphArea.Bottom, _graphArea.Top);
            gl.Viewport(20, 0, gl.RenderContextProvider.Width - 30, 20);
            gl.Begin(OpenGL.GL_LINES);

            for (double x = _graphArea.Left - _xShift - xSteps * hx; x < _graphArea.Right; x += hx)
            {
                gl.Vertex(x, _graphArea.Top);
                gl.Vertex(x, _graphArea.Top - _graphArea.Height * 0.4);
            }

            gl.End();

            for (double x = _graphArea.Left - _xShift - xSteps * hx; x < _graphArea.Right; x += hx)
            {
                var axisX = $"{x:f2}";
                var pos = _graphArea.ToScreenCoordinates(x, _graphArea.Bottom + _graphArea.Height * 0.015,
                    gl.RenderContextProvider);
                var width = gl.RenderContextProvider.Width;
                
                gl.DrawText((int)(pos.X - width * 0.017), (int)-pos.Y, 
                    0f, 0f, 0f, 
                    "Times New Roman", 10, 
                    axisX);
            }
        }
        gl.PopMatrix();
        
        gl.PushMatrix();
        {
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();
            gl.Ortho2D(_graphArea.Left, _graphArea.Right, _graphArea.Bottom, _graphArea.Top);
            gl.Viewport(0, 20, 20, gl.RenderContextProvider.Height - 30);
            gl.Begin(OpenGL.GL_LINES);

            for (double y = _graphArea.Bottom - _yShift - ySteps * hy; y < _graphArea.Top; y += hy)
            {
                gl.Vertex(_graphArea.Right, y);
                gl.Vertex(_graphArea.Right - _graphArea.Width * 0.4, y);
            }

            gl.End();

            for (double y = _graphArea.Bottom - _yShift - ySteps * hy; y < _graphArea.Top; y += hy)
            {
                var axisY = $"{y:f2}";
                var pos = _graphArea.ToScreenCoordinates(_graphArea.Left, y,
                    gl.RenderContextProvider);
                var height = gl.RenderContextProvider.Height;

                gl.DrawText((int)(pos.X), (int)(-pos.Y + height * 0.008), 
                    0f, 0f, 0f, 
                    "Times New Roman", 10, 
                    axisY);
            }
        }
        gl.PopMatrix();
    }

    private void DrawLegend(OpenGL gl, double[] legendValues, byte[,] legendColors)
    {
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
            double hy = 960.0 / (legendColors.GetLength(0) - 1);
            for (int i = 0; i < legendColors.GetLength(0) - 1; y -= hy, i++)
            {
                gl.Color(legendColors[i, 0], legendColors[i, 1], legendColors[i, 2]);
                gl.Vertex(100, y);
                gl.Vertex(800, y);
                gl.Color(legendColors[i + 1, 0], legendColors[i + 1, 1], legendColors[i + 1, 2]);
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
                var axisText = $"{legendValues[i]:f3}";
                int height = (int)y;

                gl.DrawText(1000, height - 20, 0f, 0f, 0f, "Arial", 10, axisText, 1920, 1080);
            }
        }
        gl.PopMatrix();
    }
}