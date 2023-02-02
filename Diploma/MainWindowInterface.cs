using System.Windows.Input;

namespace Diploma;

public sealed partial class MainWindow
{
    private bool _canNavigate;
    private Point2D _fulcrum;
    private double _xShift, _yShift;
    
    // Pressure control
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
    }

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
    
    private void PressureControl_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenGL gl = PressureControl.OpenGL;
        
        _canNavigate = true;
        var cursorPosition = e.GetPosition(PressureControl);
        double xPos = (float)cursorPosition.X;
        double yPos = (float)cursorPosition.Y;
        _fulcrum = _graphArea.ToProjectionCoordinate(xPos, yPos, gl.RenderContextProvider);
    }

    private void PressureControl_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
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

    private void SaturationControl_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenGL gl = SaturationControl.OpenGL;
        
        _canNavigate = true;
        var cursorPosition = e.GetPosition(SaturationControl);
        double xPos = (float)cursorPosition.X;
        double yPos = (float)cursorPosition.Y;
        _fulcrum = _graphArea.ToProjectionCoordinate(xPos, yPos, gl.RenderContextProvider);
    }

    private void SaturationControl_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
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
            gl.LineWidth(2);
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(-1, -1);
            gl.Vertex(1, -1);
            gl.Vertex(-1, -1);
            gl.Vertex(-1, 1);
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
        gl.LineWidth(1);
    }
}