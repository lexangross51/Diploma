using System.Windows.Input;

namespace Diploma;

public partial class MainWindow
{
    private bool _canNavigate;
    private Point2D _fulcrum;
    
    private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
            {
                if (_timeMoment < _timeEnd - 1) TimeMoment++;
                break;
            }
            case Key.Down:
            {
                if (_timeMoment > _timeStart) TimeMoment--;
                break;
            }
        }
    }

    private void PressureControl_OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(PressureControl);
        var projPoint = _viewport.ToProjectionCoordinate(pos.X, pos.Y, PressureControl.OpenGL.RenderContextProvider);
        XPos.Text = $"{projPoint.X:f2}";
        YPos.Text = $"{projPoint.Y:f2}";
        
        if (_canNavigate)
        {
            var xShift = projPoint.X - _fulcrum.X;
            var yShift = projPoint.Y - _fulcrum.Y;

            _graphArea.Left -= xShift;
            _graphArea.Right -= xShift;
            _graphArea.Bottom -= yShift;
            _graphArea.Top -= yShift;
            
            _viewport.Left = _graphArea.Left - 0.07 * _graphArea.Width;
            _viewport.Right = _graphArea.Right + 0.05 * _graphArea.Width;
            _viewport.Bottom = _graphArea.Bottom - 0.05 * _graphArea.Height;
            _viewport.Top = _graphArea.Top + 0.05 * _graphArea.Height;
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
            Scale(screenPoint, 1.1);
        }
        else
        {
            Scale(screenPoint, 1.0 / 1.1);
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
    
    private void SaturationControl_OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(SaturationControl);
        var projPoint = _viewport.ToProjectionCoordinate(pos.X, pos.Y, SaturationControl.OpenGL.RenderContextProvider);
        XPos.Text = $"{projPoint.X:f2}";
        YPos.Text = $"{projPoint.Y:f2}";
        
        if (_canNavigate)
        {
            var xShift = projPoint.X - _fulcrum.X;
            var yShift = projPoint.Y - _fulcrum.Y;

            _graphArea.Left -= xShift;
            _graphArea.Right -= xShift;
            _graphArea.Bottom -= yShift;
            _graphArea.Top -= yShift;
            
            _viewport.Left = _graphArea.Left - 0.07 * _graphArea.Width;
            _viewport.Right = _graphArea.Right + 0.05 * _graphArea.Width;
            _viewport.Bottom = _graphArea.Bottom - 0.05 * _graphArea.Height;
            _viewport.Top = _graphArea.Top + 0.05 * _graphArea.Height;
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

        _viewport.Left = _graphArea.Left - 0.07 * _graphArea.Width;
        _viewport.Right = _graphArea.Right + 0.05 * _graphArea.Width;
        _viewport.Bottom = _graphArea.Bottom - 0.05 * _graphArea.Height;
        _viewport.Top = _graphArea.Top + 0.05 * _graphArea.Height;
    }

    private void DrawAxes(OpenGL gl)
    {
        double hx = _graphArea.Width / 10.0;
        double hy = _graphArea.Height / 10.0;

        gl.Color(0, 0, 0);
        gl.Begin(OpenGL.GL_LINES);

        for (double x = _graphArea.Left; x <= _graphArea.Right + hx; x += hx)
        {
            gl.Vertex(x, _viewport.Bottom);
            gl.Vertex(x, _viewport.Bottom + _graphArea.Height * 0.01);
        }

        for (double y = _graphArea.Bottom; y <= _graphArea.Top + hy; y += hy)
        {
            gl.Vertex(_viewport.Left, y);
            gl.Vertex(_viewport.Left + _graphArea.Width * 0.01, y);
        }

        gl.End();

        // Numbers on the axes
        for (double x = _graphArea.Left; x <= _graphArea.Right + hx; x += hx)
        {
            string axisX = $"{x:f2}";
            var pos = _viewport.ToScreenCoordinates(x, _viewport.Bottom + _graphArea.Height * 0.015,
                gl.RenderContextProvider);
            var width = gl.RenderContextProvider.Width;

            gl.DrawText((int)(pos.X - width * 0.017), (int)-pos.Y, 0f, 0f, 0f, "Arial", 10, axisX);
        }

        for (double y = _graphArea.Bottom; y <= _graphArea.Top + hy; y += hy)
        {
            string axisY = $"{y:f2}";
            var pos = _viewport.ToScreenCoordinates(_viewport.Left + _graphArea.Width * 0.015, y,
                gl.RenderContextProvider);
            var height = gl.RenderContextProvider.Height;

            gl.DrawText((int)(pos.X), (int)(-pos.Y - height * 0.002), 0f, 0f, 0f, "Arial", 10, axisY);
        }
    }
}