namespace Diploma.Source.Mesh;

public class MeshBuilder : IMeshBuilder
{
    private readonly MeshParameters _parameters;
    private readonly List<double> _xPoints = new();
    private readonly List<double> _yPoints = new();
    private Point2D[] _points = default!;
    private FiniteElement[] _elements = default!;

    public MeshBuilder(MeshParameters parameters)
        => _parameters = parameters;

    private static int FindNearestIndex(IReadOnlyList<double> points, double point)
    {
        for (int i = 0; i < points.Count - 1; i++)
        {
            if (Math.Abs(point - points[i]) < 1E-14) return i;
            if (Math.Abs(point - points[i + 1]) < 1E-14) return i + 1;
            
            if (point > points[i] && point < points[i + 1])
            {
                return Math.Abs(point - points[i]) < Math.Abs(point - points[i + 1]) ? i : i + 1;
            }
        }

        return -1;
    }
    
    public IEnumerable<Point2D> CreatePoints()
    {
        double xStart = _parameters.Area.LeftBottom.X;
        double xEnd = _parameters.Area.RightTop.X;
        double yStart = _parameters.Area.LeftBottom.Y;
        double yEnd = _parameters.Area.RightTop.Y;

        int nx = _parameters.SplitParameters.MeshNx;
        int ny = _parameters.SplitParameters.MeshNy;

        #region Формируем равномерную сетку

        double hx = (xEnd - xStart) / nx;
        double hy = (yEnd - yStart) / ny;
        
        _xPoints.Add(xStart);
        _yPoints.Add(yStart);
        
        for (int i = 1; i < nx + 1; i++)
        {
            _xPoints.Add(_xPoints[i - 1] + hx);
        }
        
        for (int i = 1; i < ny + 1; i++)
        {
            _yPoints.Add(_yPoints[i - 1] + hy);
        }

        #endregion

        List<double> wellsXPoints = new();
        List<double> wellsYPoints = new();
        
        foreach (var well in _parameters.Wells)
        {
            var center = well.Center;
            var radius = well.Radius;

            var xStartL = FindNearestIndex(_xPoints,center.X - 3 * radius);
            var xEndL = center.X - radius;
            var xStartR = center.X + radius;
            var xEndR = FindNearestIndex(_xPoints,center.X + 3 * radius);
            
            var yStartB = FindNearestIndex(_yPoints,center.Y - 3 * radius);
            var yEndB = center.Y - radius;
            var yStartT = center.Y + radius;
            var yEndT = FindNearestIndex(_yPoints,center.Y + 3 * radius);
            
            // Нужно ли дополнительно дробить около скважины
            if (_parameters.SplitParameters.WellNx != 0 && _parameters.SplitParameters.WellNy != 0)
            {
                nx = (int)(_parameters.SplitParameters.WellNx / 2.0);
                ny = (int)(_parameters.SplitParameters.WellNy / 2.0);
            }
            else
            {
                nx = (xEndR - xStartL + 1) / 2;
                ny = (yEndT - yStartB + 1) / 2;
            }

            // Слева от скважины
            xStart = _xPoints[xStartL];
            xEnd = xEndL;
            var kx = _parameters.SplitParameters.WellKx;
            hx = Math.Abs(kx - 1.0) < 1E-14 
                ? (xEnd - xStart) / nx 
                : (xEnd - xStart) * (1 - kx) / (1 - Math.Pow(kx, nx));

            for (var i = 0; i < nx + 1; i++)
            {
                wellsXPoints.Add(xEnd);
                xEnd -= hx;
                hx *= kx;
            }
            
            // Справа от скважины
            xStart = xStartR;
            xEnd = _xPoints[xEndR];
            kx = _parameters.SplitParameters.WellKx;
            hx = Math.Abs(kx - 1.0) < 1E-14 
                ? (xEnd - xStart) / nx 
                : (xEnd - xStart) * (1 - kx) / (1 - Math.Pow(kx, nx));

            for (int i = 0; i < nx + 1; i++)
            {
                wellsXPoints.Add(xStart);
                xStart += hx;
                hx *= kx;
            }
            
            // Cнизу от скважины
            yStart = _yPoints[yStartB];
            yEnd = yEndB;
            var ky = _parameters.SplitParameters.WellKy;
            hy = Math.Abs(ky - 1.0) < 1E-14 
                ? (yEnd - yStart) / ny 
                : (yEnd - yStart) * (1 - ky) / (1 - Math.Pow(ky, ny));

            for (int i = 0; i < nx + 1; i++)
            {
                wellsYPoints.Add(yEnd);
                yEnd -= hy;
                hy *= ky;
            }
            
            // Сверху от скважины
            yStart = yStartT;
            yEnd = _yPoints[yEndT];
            ky = _parameters.SplitParameters.WellKy;
            hy = Math.Abs(kx - 1.0) < 1E-14 
                ? (yEnd - yStart) / nx 
                : (yEnd - yStart) * (1 - ky) / (1 - Math.Pow(ky, ny));

            for (int i = 0; i < ny + 1; i++)
            {
                wellsYPoints.Add(yStart);
                yStart += hy;
                hy *= ky;
            }
            
            _xPoints.RemoveRange(xStartL, xEndR - xStartL + 1);
            _yPoints.RemoveRange(yStartB, yEndT - yStartB + 1);
        }
        
        _xPoints.AddRange(wellsXPoints);
        _yPoints.AddRange(wellsYPoints);
        
        _xPoints.Sort();
        _yPoints.Sort();

        _points = new Point2D[_xPoints.Count * _yPoints.Count];

        int ip = 0;
        
        foreach (var y in _yPoints)
        {
            foreach (var x in _xPoints)
            {
                _points[ip++] = new Point2D(x, y);
            }
        }

        return _points;
    }

    public IEnumerable<FiniteElement> CreateElements()
    {
        _elements = new FiniteElement[(_xPoints.Count - 1) * (_yPoints.Count - 1)];
        int[] nodes = new int[4];
        int nx = _xPoints.Count - 1;
        int ny = _yPoints.Count - 1;
        int ielem = 0;
        
        for (int i = 0; i < ny; i++)
        {
            for (int j = 0; j < nx; j++)
            {
                nodes[0] = j + i * (nx + 1);
                nodes[1] = j + i * (nx + 1) + 1;
                nodes[2] = j + i * (nx + 1) + (nx + 1);
                nodes[3] = j + i * (nx + 1) + (nx + 1) + 1;

                _elements[ielem++] = new FiniteElement(nodes, new []{1}, new []{1}, 0);
            }
        }

        return _elements;
    }
}