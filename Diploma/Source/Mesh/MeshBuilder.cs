    namespace Diploma.Source.Mesh;

public class MeshBuilder : IMeshBuilder
{
    private readonly MeshParameters _parameters;
    private readonly List<double> _xPoints = new();
    private readonly List<double> _yPoints = new();
    private Point2D[] _points = default!;
    private FiniteElement[] _elements = default!;
    private Material[] _materials = default!;

    public MeshBuilder(MeshParameters parameters)
        => _parameters = parameters;
    
    private static (int, int) FindNearestIndex(IReadOnlyList<double> points, double center, double radius)
    {
        int begin = -1, end = -1;
        
        for (int i = 0; i < points.Count - 1; i++)
        {
            // if (Math.Abs(point - points[i]) < 1E-14) return i;
            // if (Math.Abs(point - points[i + 1]) < 1E-14) return i + 1;
            
            // if (point > points[i] && point < points[i + 1])
            // {
            //     return side == 0 ? i : i + 1;
            // }

            if (center - radius > points[i] && center - radius < points[i + 1])
            {
                begin = i - 1;
            }
            
            if (center + radius > points[i] && center + radius < points[i + 1])
            {
                end = i + 2;
            }
        }

        // if (Math.Abs(center - points[begin]) > 2 * Math.Abs(center - points[end]))
        // {
        //     end++;
        // }
        // else if (2 * Math.Abs(center - points[begin]) < Math.Abs(center - points[end]))
        // {
        //     begin--;
        // }
        
        return (begin, end);
    }

    private bool IsContain(FiniteElement element, Point2D point)
    {
        var leftBottom = _points[element.Nodes[0]];
        var rightTop = _points[element.Nodes[^1]];

        return point.X >= leftBottom.X && point.X <= rightTop.X &&
               point.Y >= leftBottom.Y && point.Y <= rightTop.Y;
    }

    private void MeshNesting(ref int nx, ref int ny, ref double kx, ref double ky)
    {
        switch (_parameters.SplitParameters.Nesting)
        {
            case 1:
                nx *= 2;
                ny *= 2;
                kx = Math.Sqrt(kx);
                ky = Math.Sqrt(ky);
                break;
            case 2:
                nx *= 4;
                ny *= 4;
                kx = Math.Sqrt(Math.Sqrt(kx));
                ky = Math.Sqrt(Math.Sqrt(ky));
                break;
            case 3:
                nx *= 8;
                ny *= 8;
                kx = Math.Sqrt(Math.Sqrt(Math.Sqrt(kx)));
                ky = Math.Sqrt(Math.Sqrt(Math.Sqrt(ky)));
                break;
            case 4:
                nx *= 16;
                ny *= 16;
                kx = Math.Sqrt(Math.Sqrt(Math.Sqrt(Math.Sqrt(kx))));
                ky = Math.Sqrt(Math.Sqrt(Math.Sqrt(Math.Sqrt(ky))));
                break;
        }
    }

    private static bool IsEdgeExist(int i, int j)
        => (i == 0 && j == 1 || i == 0 && j == 2 || i == 1 && j == 3 || i == 2 && j == 3);

    private void CreateEdges()
    {
        foreach (var element in _elements)
        {
            element.Edges.Add(new Edge(element.Nodes[0], element.Nodes[1]));
            element.Edges.Add(new Edge(element.Nodes[0], element.Nodes[2]));
            element.Edges.Add(new Edge(element.Nodes[1], element.Nodes[3]));
            element.Edges.Add(new Edge(element.Nodes[2], element.Nodes[3]));
        }
    }
    
    private void NumerateEdges()
    {
        var connectivityList = new List<SortedSet<int>>();
        
        for (int i = 0; i < _points.Length; i++)
        {
            connectivityList.Add(new SortedSet<int>());
        }

        foreach (var element in _elements)
        {
            var nodes = element.Nodes;

            for (int i = 0; i < 3; i++) 
            {
                int ind1 = nodes[i];

                for (int j = i + 1; j < 4; j++) 
                {
                    int ind2 = nodes[j];

                    if (IsEdgeExist(i, j)) 
                    {
                        connectivityList[ind2].Add(ind1);
                    }
                }
            }
        }

        var ig = new int[_points.Length + 1];

        ig[0] = 0;
        ig[1] = 0;
        
        for (int i = 1; i < connectivityList.Count; i++) 
        {
            ig[i + 1] = ig[i] + connectivityList[i].Count;
        }

        var jg = new int[ig[^1]];

        for (int i = 1, j = 0; i < connectivityList.Count; i++) 
        {
            foreach (var it in connectivityList[i])
            {
                jg[j++] = it;
            }
        }

        foreach (var element in _elements)
        {
            for (int i = 0; i < 3; i++) 
            {
                int ind1 = element.Nodes[i];

                for (int j = i + 1; j < 4; j++) 
                {
                    int ind2 = element.Nodes[j];

                    if (ind1 < ind2) 
                    {
                        for (int ind = ig[ind2]; ind < ig[ind2 + 1]; ind++) 
                        {
                            if (jg[ind] == ind1) 
                            {
                                element.EdgesIndices.Add(ind);
                            }
                        }
                    }
                    else 
                    {
                        for (int ind = ig[ind1]; ind < ig[ind1 + 1]; ind++) 
                        {
                            if (jg[ind] == ind2) 
                            {
                                element.EdgesIndices.Add(ind);
                            }
                        }
                    }
                }
            }
        }
    }
    
    public IEnumerable<Point2D> CreatePoints()
    {
        // Taking data for the main area
        double xStart = _parameters.Area[0].LeftBottom.X;
        double xEnd = _parameters.Area[0].RightTop.X;
        double yStart = _parameters.Area[0].LeftBottom.Y;
        double yEnd = _parameters.Area[0].RightTop.Y;

        int nx = _parameters.SplitParameters.MeshNx;
        int ny = _parameters.SplitParameters.MeshNy;
        var kx = _parameters.SplitParameters.WellKx;
        var ky = _parameters.SplitParameters.WellKy;
        
        // If a nested mesh is required
        MeshNesting(ref nx, ref ny, ref kx, ref ky);
        
        // Create points for the main area 
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
        
        // Forming points in the wells area
        HashSet<double> wellsXPoints = new();
        HashSet<double> wellsYPoints = new();
        SortedSet<(int, int)> wellsXIndexes = new();
        SortedSet<(int, int)> wellsYIndexes = new();
        
        foreach (var well in _parameters.Wells)
        {
            var center = well.Center;
            var radius = well.Radius;

            var (xStartL, xEndR) = FindNearestIndex(_xPoints,center.X, 6 * radius);
            var xEndL = center.X - radius;
            var xStartR = center.X + radius;
            //var xEndR = FindNearestIndex(_xPoints,center.X + 5 * radius, 1);
            
            var (yStartB, yEndT) = FindNearestIndex(_yPoints,center.Y, 6 * radius);
            var yEndB = center.Y - radius;
            var yStartT = center.Y + radius;
            //var yEndT = FindNearestIndex(_yPoints,center.Y + 5 * radius, 1);
            
            // Whether it is necessary to additionally crush the mesh near the well
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
            
            kx = _parameters.SplitParameters.WellKx;
            ky = _parameters.SplitParameters.WellKy;
            
            MeshNesting(ref nx, ref ny, ref kx, ref ky);

            // To the left of the well
            xStart = _xPoints[xStartL];
            xEnd = xEndL;
            hx = Math.Abs(kx - 1.0) < 1E-14 
                ? (xEnd - xStart) / nx 
                : (xEnd - xStart) * (1 - kx) / (1 - Math.Pow(kx, nx));

            for (var i = 0; i < nx + 1; i++)
            {
                wellsXPoints.Add(xEnd);
                xEnd -= hx;
                hx *= kx;
            }
            
            // To the right of the well
            xStart = xStartR;
            xEnd = _xPoints[xEndR];
            hx = Math.Abs(kx - 1.0) < 1E-14 
                ? (xEnd - xStart) / nx 
                : (xEnd - xStart) * (1 - kx) / (1 - Math.Pow(kx, nx));

            for (int i = 0; i < nx + 1; i++)
            {
                wellsXPoints.Add(xStart);
                xStart += hx;
                hx *= kx;
            }
            
            // From below the well
            yStart = _yPoints[yStartB];
            yEnd = yEndB;
            hy = Math.Abs(ky - 1.0) < 1E-14 
                ? (yEnd - yStart) / ny 
                : (yEnd - yStart) * (1 - ky) / (1 - Math.Pow(ky, ny));

            for (int i = 0; i < ny + 1; i++)
            {
                wellsYPoints.Add(yEnd);
                yEnd -= hy;
                hy *= ky;
            }
            
            // From above the well
            yStart = yStartT;
            yEnd = _yPoints[yEndT];
            hy = Math.Abs(ky - 1.0) < 1E-14 
                ? (yEnd - yStart) / ny 
                : (yEnd - yStart) * (1 - ky) / (1 - Math.Pow(ky, ny));

            for (int i = 0; i < ny + 1; i++)
            {
                wellsYPoints.Add(yStart);
                yStart += hy;
                hy *= ky;
            }
            
            wellsXIndexes.Add((xStartL, xEndR - xStartL + 1));
            wellsYIndexes.Add((yStartB, yEndT - yStartB + 1));
        }
        
        var xCount = _xPoints.Count;
        var yCount = _yPoints.Count;
        
        foreach (var idx in wellsXIndexes)
        {
            _xPoints.RemoveRange(idx.Item1 - (xCount - _xPoints.Count), idx.Item2);
        }

        foreach (var idx in wellsYIndexes)
        {
            _yPoints.RemoveRange(idx.Item1 - (yCount - _yPoints.Count), idx.Item2);
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

                _elements[ielem++] = new FiniteElement(nodes, 0);
            }
        }
        
        // If the element is in a region with a different permeability,
        // we change its material number
        if (_parameters.Area.Length > 1)
        {
            var leftBottom = _parameters.Area[1].LeftBottom;
            var rightTop = _parameters.Area[1].RightTop;

            foreach (var element in _elements)
            {
                var elementNodes = element.Nodes;
                var elementCenterX = (_points[elementNodes[^1]].X + _points[elementNodes[0]].X) / 2.0;
                var elementCenterY = (_points[elementNodes[^1]].Y + _points[elementNodes[0]].Y) / 2.0;

                if (elementCenterX >= leftBottom.X && elementCenterX <= rightTop.X &&
                    elementCenterY >= leftBottom.Y && elementCenterY <= rightTop.Y)
                {
                    element.Area = 1;
                }
            }
        }

        // Create list of edges for each element
        CreateEdges();
        
        // Numerate edges of each element
        NumerateEdges();

        // Set edges direction
        // -1 - if the outside normal doesn't coincide with the fixed
        // 1 - if the same
        foreach (var element in _elements)
        {
            element.EdgesDirect = new List<int>() { -1, -1, 1, 1 };
        }

        return _elements;
    }

    public IEnumerable<DirichletCondition> CreateDirichlet()
    {
        int nx = _xPoints.Count - 1;
        int ny = _yPoints.Count - 1;
        double pressure = _parameters.Area[0].PlastPressure;

        HashSet<DirichletCondition> dirichletConditions = new(2 * (nx + ny));

        // lower border
        for (int inode = 0; inode < nx + 1; inode++)
        {
            dirichletConditions.Add(new (inode, pressure));
        }
        
        // upper border
        for (int inode = (nx + 1) * ny; inode < (nx + 1) * (ny + 1); inode++)
        {
            dirichletConditions.Add(new (inode, pressure));
        }
        
        // left border
        //pressure = 10;
        for (int i = 0, inode = 0; i < ny + 1; i++, inode += nx + 1)
        {
            dirichletConditions.Add(new (inode, pressure));
        }
        
        // right border
        //pressure = 0;
        for (int i = 0, inode = nx; i < ny + 1; i++, inode += nx + 1)
        {
            dirichletConditions.Add(new (inode, pressure));
        }

        return dirichletConditions;
    }

    public IEnumerable<NeumannCondition> CreateNeumann()
    {
        List<NeumannCondition> neumannConditions = new(_parameters.Wells.Length);
        
        foreach (var well in _parameters.Wells)
        {
            for (int i = 0; i < _elements.Length; i++)
            {
                if (IsContain(_elements[i], well.Center))
                {
                    neumannConditions.Add(new(i, well.Power));
                    break;
                }
            }
        }

        return neumannConditions;
    }

    public IEnumerable<Material> CreateMaterials()
    {
        _materials = new Material[_parameters.Area.Length];

        for (int i = 0; i < _materials.Length; i++)
        {
            _materials[i] = _parameters.Area[i].Material;
        }

        return _materials;
    }
}