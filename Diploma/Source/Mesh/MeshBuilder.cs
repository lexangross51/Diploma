namespace Diploma.Source.Mesh;

public class MeshBuilder
{
    private readonly MeshParameters _parameters;
    private List<(Point2D Point, bool IsFictitious)> _points = default!;
    private List<Point2D>? _wellPoints;
    private SortedDictionary<int, FiniteElement> _elements = default!;
    private List<FiniteElement>? _wellElements;
    private HashSet<DirichletCondition> _dirichletConditions = default!;
    private List<(int Element, int LocalEdge)> _remoteEdges = default!;
    private List<NeumannCondition>? _neumannConditions = new();
    private Material[] _materials = default!;
    private List<int>? _intersectedElements;

    public MeshBuilder(MeshParameters parameters)
        => _parameters = parameters;

    public Mesh Build()
    {
        CreatePointsAndElements();
        CreateDirichlet();
        CreateRemoteEdgesList();
        CreateMaterials();

        return new(_points, _elements.Values, _dirichletConditions, _remoteEdges, _neumannConditions, _materials);
    }

    private void MeshNesting(ref int nx, ref int ny, ref double k)
    {
        switch (_parameters.SplitParameters.Nesting)
        {
            case 1:
                nx *= 2;
                ny *= 2;
                k = Math.Sqrt(k);
                break;
            case 2:
                nx *= 4;
                ny *= 4;
                k = Math.Pow(k, 1.0 / 4.0);
                break;
            case 3:
                nx *= 8;
                ny *= 8;
                k = Math.Pow(k, 1.0 / 8.0);
                break;
            case 4:
                nx *= 16;
                ny *= 16;
                k = Math.Pow(k, 1.0 / 16.0);
                break;
        }
    }

    private void NumerateEdges()
    {
        var connectivityList = new List<SortedSet<int>>();

        for (int i = 0; i < _points.Count; i++)
        {
            connectivityList.Add(new SortedSet<int>());
        }

        foreach (var edge in _elements.Values.Select(element => element.Edges).SelectMany(edges => edges))
        {
            if (edge.Node1 < edge.Node2)
            {
                connectivityList[edge.Node2].Add(edge.Node1);
            }
            else
            {
                connectivityList[edge.Node1].Add(edge.Node2);
            }
        }
        
        var ig = new int[_points.Count + 1];

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

        foreach (var (_, element) in _elements)
        {
            Array.Fill(element.EdgesIndices, -1);

            int idx = 0;

            for (int i = 0; i < 4; i++)
            {
                int ind1 = element.Nodes[i];

                for (int j = 0; j < 4; j++)
                {
                    int ind2 = element.Nodes[j];

                    if (ind1 < ind2)
                    {
                        for (int ind = ig[ind2]; ind < ig[ind2 + 1]; ind++)
                        {
                            if (jg[ind] == ind1)
                            {
                                if (!element.EdgesIndices.Contains(ind))
                                {
                                    element.EdgesIndices[idx++] = ind;
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int ind = ig[ind1]; ind < ig[ind1 + 1]; ind++)
                        {
                            if (jg[ind] == ind2)
                            {
                                if (!element.EdgesIndices.Contains(ind))
                                {
                                    element.EdgesIndices[idx++] = ind;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static void CreateEdges(FiniteElement element)
    {
        element.Edges[0] = new Edge(element.Nodes[1], element.Nodes[0]);
        element.Edges[1] = new Edge(element.Nodes[0], element.Nodes[2]);
        element.Edges[2] = new Edge(element.Nodes[3], element.Nodes[1]);
        element.Edges[3] = new Edge(element.Nodes[2], element.Nodes[3]);
    }

    private void CreateUniformMesh()
    {
        // Generate points --------------------------------------------------->
        // Taking data for the main area
        double xStart = _parameters.Area[0].LeftBottom.X;
        double xEnd = _parameters.Area[0].RightTop.X;
        double yStart = _parameters.Area[0].LeftBottom.Y;
        double yEnd = _parameters.Area[0].RightTop.Y;

        int nx = _parameters.SplitParameters.MeshNx;
        int ny = _parameters.SplitParameters.MeshNy;
        double k = _parameters.SplitParameters.WellsCoefficient;

        // If a nested mesh is required
        MeshNesting(ref nx, ref ny, ref k);

        // Create points for the main area 
        double hx = (xEnd - xStart) / nx;
        double hy = (yEnd - yStart) / ny;

        for (int i = 1; i < ny + 2; i++)
        {
            for (int j = 1; j < nx + 2; j++)
            {
                double x = xStart + (j - 1) * hx;
                double y = yStart + (i - 1) * hy;

                _points.Add((new Point2D(x, y), false));
            }
        }

        // Generate elements ------------------------------------------------->
        int ielem = 0;

        for (int i = 0; i < ny; i++)
        {
            for (int j = 0; j < nx; j++)
            {
                int[] nodes =
                {
                    j + i * (nx + 1),
                    j + i * (nx + 1) + 1,
                    j + i * (nx + 1) + nx + 1,
                    j + i * (nx + 1) + nx + 1 + 1
                };

                _elements.Add(ielem, new FiniteElement(nodes, 0));
                
                CreateEdges(_elements[ielem]);
                
                ielem++;
            }
        }
    }

    private void FindIntersections(Point2D leftBottom, Point2D rightTop)
    {
        int nx = _elements[0].Nodes[2] - 1;
        int elem1 = -1, elem2 = -1, elem3 = -1;

        foreach (var ielem in _elements.Keys)
        {
            var nodes = _elements[ielem].Nodes;
            var p1 = _points[nodes[0]].Point;
            var p2 = _points[nodes[^1]].Point;

            if (leftBottom.X >= p1.X && leftBottom.X <= p2.X && leftBottom.Y >= p1.Y && leftBottom.Y <= p2.Y)
            {
                elem1 = ielem;
            }

            if (rightTop.X >= p1.X && rightTop.X <= p2.X && leftBottom.Y >= p1.Y && leftBottom.Y <= p2.Y)
            {
                elem2 = ielem;
            }

            if (leftBottom.X >= p1.X && leftBottom.X <= p2.X && rightTop.Y >= p1.Y && rightTop.Y <= p2.Y)
            {
                elem3 = ielem;
            }

            if (elem1 != -1 && elem2 != -1 && elem3 != -1) break;
        }

        if (elem1 == -1 || elem2 == -1 || elem3 == -1) throw new Exception("Can't prepare mesh data!");

        int elemsByRow = elem2 - elem1 + 1;
        int elemsByCol = (elem3 - elem1) / nx + 1;
        int elems;

        if (elemsByRow > elemsByCol)
        {
            elems = elemsByRow;
        }
        else
        {
            elems = elemsByCol;
            elem2 += elemsByCol - elemsByRow;
        }

        for (int i = 0; i < elems; i++)
        {
            for (int ielem = elem1 + i * nx; ielem <= elem2; ielem++)
            {
                _intersectedElements!.Add(ielem);
            }

            elem2 += nx;
        }

        for (int i = 0, ielem = 0; i < elems - 1; i++)
        {
            for (int j = 0; j < elems - 1; j++)
            {
                int elemIndex = _intersectedElements![ielem++];
                var tmpPoint = _points[_elements[elemIndex].Nodes[3]];
                tmpPoint.IsFictitious = true;
                _points[_elements[elemIndex].Nodes[3]] = tmpPoint;
            }

            ielem++;
        }
    }

    private void CreatePointsAndElements()
    {
        int nx = _parameters.SplitParameters.MeshNx;
        int ny = _parameters.SplitParameters.MeshNy;
        double k = _parameters.SplitParameters.WellsCoefficient;
        
        MeshNesting(ref nx, ref ny, ref k);

        _points = new List<(Point2D, bool)>((nx + 1) * (ny + 1));
        _elements = new SortedDictionary<int, FiniteElement>();

        CreateUniformMesh();

        if (_parameters.Wells.Length != 0)
        {
            _wellPoints = new List<Point2D>();
            _wellElements = new List<FiniteElement>();
            _neumannConditions = new List<NeumannCondition>();
            _intersectedElements = new List<int>();

            int maxNodeNum = _elements[nx * ny - 1].Nodes[^1] + 1;
            int maxElemNum = _elements.Count;
            int totalDeleted = 0;

            foreach (var well in _parameters.Wells)
            {
                _wellPoints.Clear();
                _wellElements.Clear();
                _intersectedElements.Clear();

                Point2D leftBottom = new(well.Center.X - well.OuterRadius, well.Center.Y - well.OuterRadius);
                Point2D rightTop = new(well.Center.X + well.OuterRadius, well.Center.Y + well.OuterRadius);

                FindIntersections(leftBottom, rightTop);

                leftBottom = _points[_elements[_intersectedElements![0]].Nodes[0]].Point;
                rightTop = _points[_elements[_intersectedElements![^1]].Nodes[^1]].Point;
                int p = (int)Math.Sqrt(_intersectedElements!.Count);

                CreateWellPoints(leftBottom, rightTop, well.Center, well.Radius, p, p == 1 ? p : 4 * p);
                CreateWellElements(p, p == 1 ? p : 4 * p);

                int elem = 0;
                int shift = 4 * p;

                // Renumber nodes on bottom side
                for (int i = 0; i < p; i++)
                {
                    _wellElements[elem].Nodes[0] = _elements[_intersectedElements![i]].Nodes[0];
                    _wellElements[elem].Nodes[1] = _elements[_intersectedElements![i]].Nodes[1];
                    _wellElements[elem].Nodes[2] = maxNodeNum;
                    _wellElements[elem].Nodes[3] = maxNodeNum + 1;
                    
                    CreateEdges(_wellElements[elem]);

                    elem++;
                    maxNodeNum++;
                }

                // Renumber nodes on right side
                for (int i = 0, j = p - 1; i < p; i++, j += p)
                {
                    _wellElements[elem].Nodes[0] = _elements[_intersectedElements![j]].Nodes[1];
                    _wellElements[elem].Nodes[1] = _elements[_intersectedElements![j]].Nodes[3];
                    _wellElements[elem].Nodes[2] = maxNodeNum;
                    _wellElements[elem].Nodes[3] = maxNodeNum + 1;
                    
                    CreateEdges(_wellElements[elem]);

                    elem++;
                    maxNodeNum++;
                }

                // Renumber nodes on top side
                for (int i = 0, j = p * p - 1; i < p; i++, j--)
                {
                    _wellElements[elem].Nodes[0] = _elements[_intersectedElements![j]].Nodes[3];
                    _wellElements[elem].Nodes[1] = _elements[_intersectedElements![j]].Nodes[2];
                    _wellElements[elem].Nodes[2] = maxNodeNum;
                    _wellElements[elem].Nodes[3] = maxNodeNum + 1;
                    
                    CreateEdges(_wellElements[elem]);

                    elem++;
                    maxNodeNum++;
                }

                // Renumber nodes on left side
                for (int i = 0, j = p * p - p; i < p; i++, j -= p)
                {
                    _wellElements[elem].Nodes[0] = _elements[_intersectedElements![j]].Nodes[2];
                    _wellElements[elem].Nodes[1] = _elements[_intersectedElements![j]].Nodes[0];
                    _wellElements[elem].Nodes[2] = maxNodeNum;
                    _wellElements[elem].Nodes[3] = i == p - 1 ? maxNodeNum + 1 - 4 * p : maxNodeNum + 1;
                    
                    CreateEdges(_wellElements[elem]);

                    elem++;
                    maxNodeNum++;
                }

                // Renumber other nodes
                for (int i = elem; i < _wellElements.Count;)
                {
                    // Bottom side
                    for (int j = 0; j < p; j++)
                    {
                        _wellElements[i].Nodes[0] = maxNodeNum - shift;
                        _wellElements[i].Nodes[1] = maxNodeNum + 1 - shift;
                        _wellElements[i].Nodes[2] = maxNodeNum;
                        _wellElements[i].Nodes[3] = maxNodeNum + 1;
                        
                        CreateEdges(_wellElements[i]);

                        i++;
                        maxNodeNum++;
                    }

                    // Right side
                    for (int j = 0; j < p; j++)
                    {
                        _wellElements[i].Nodes[2] = maxNodeNum;
                        _wellElements[i].Nodes[3] = maxNodeNum + 1;
                        _wellElements[i].Nodes[0] = _wellElements[i].Nodes[2] - shift;
                        _wellElements[i].Nodes[1] = _wellElements[i].Nodes[3] - shift;

                        CreateEdges(_wellElements[i]);

                        i++;
                        maxNodeNum++;
                    }

                    // Top side
                    for (int j = 0; j < p; j++)
                    {
                        _wellElements[i].Nodes[2] = maxNodeNum;
                        _wellElements[i].Nodes[3] = maxNodeNum + 1;
                        _wellElements[i].Nodes[0] = _wellElements[i].Nodes[2] - shift;
                        _wellElements[i].Nodes[1] = _wellElements[i].Nodes[3] - shift;
                        
                        CreateEdges(_wellElements[i]);

                        i++;
                        maxNodeNum++;
                    }

                    // Left side
                    for (int j = 0; j < p; j++)
                    {
                        _wellElements[i].Nodes[2] = maxNodeNum;
                        _wellElements[i].Nodes[3] = j == p - 1 ? maxNodeNum + 1 - 4 * p : maxNodeNum + 1;
                        _wellElements[i].Nodes[0] = _wellElements[i].Nodes[2] - shift;
                        _wellElements[i].Nodes[1] = _wellElements[i].Nodes[3] - shift;
                        
                        CreateEdges(_wellElements[i]);

                        i++;
                        maxNodeNum++;
                    }
                }

                // Delete intersected elements
                foreach (var element in _intersectedElements)
                {
                    _elements.Remove(element);
                    totalDeleted++;
                }

                // Delete points from _wellPoints which already in _points
                for (int i = 0; i < 4 * p; i++)
                {
                    _wellPoints.RemoveAt(0);
                }

                foreach (var point in _wellPoints)
                {
                    _points.Add((point, false));
                }

                for (int ielem = 0; ielem < _wellElements.Count; ielem++)
                {
                    // The first near-well element
                    if (ielem == _wellElements.Count - 4 * p)
                    {
                        double totalBordersLength = 0;

                        for (int i = ielem; i < _wellElements.Count; i++)
                        {
                            var edge = _wellElements[i].Edges[3];
                            var p1 = _points[edge.Node1].Point;
                            var p2 = _points[edge.Node2].Point;

                            totalBordersLength +=
                                Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
                        }

                        int wellElem = maxElemNum;

                        for (int i = 0; i < 4 * p; i++)
                        {
                            _neumannConditions.Add(new NeumannCondition(
                                wellElem++, 3, well.Power / totalBordersLength)
                            );
                        }
                    }

                    _elements.Add(maxElemNum++, _wellElements[ielem]);
                }
            }

            for (int i = 0; i < _neumannConditions!.Count; i++)
            {
                var element = _neumannConditions[i].Element - totalDeleted;
                var edge = _neumannConditions[i].Edge;
                var power = DataConverter.FlowToCubicMetersPerSecond(_neumannConditions[i].Power);

                _neumannConditions[i] = new NeumannCondition(element, edge, power);
            }
        }

        NumerateEdges();

        // If the element is in a region with a different permeability,
        // we change its material number
        ChangeMaterial();
    }

    private void CreateWellPoints(Point2D leftBottom, Point2D rightTop, Point2D center, double r, int p, int m)
    {
        int nx = _parameters.SplitParameters.MeshNx;
        int ny = _parameters.SplitParameters.MeshNy;
        double k = _parameters.SplitParameters.WellsCoefficient;

        MeshNesting(ref nx, ref ny, ref k);
        
        double hx = (rightTop.X - leftBottom.X) / p;
        double hy = (rightTop.Y - leftBottom.Y) / p;
        
        // Region 1
        Point2D[] region1 = new Point2D[(p + 1) * (m + 1)];
        
        for (int i = 1; i < p + 2; i++)
        {
            region1[i - 1] = new Point2D(leftBottom.X + (i - 1) * hx, leftBottom.Y);
        }
        
        for (int i = 1; i < p + 2; i++)
        {
            double x = r * Math.Cos(5 * Math.PI / 4 + (i - 1) * Math.PI / 2 / p) + center.X;
            double y = r * Math.Sin(5 * Math.PI / 4 + (i - 1) * Math.PI / 2 / p) + center.Y;
        
            region1[m * (p + 1) + i - 1] = new Point2D(x, y);
        }
        
        for (int i = 1; i < m; i++)
        {
            for (int j = 1; j < p + 2; j++)
            {
                double dx, dy;
                
                if (Math.Abs(k - 1.0) < 1E-14)
                {
                    dx = (region1[m * (p + 1) + j - 1].X - region1[j - 1].X) / m;
                    dy = (region1[m * (p + 1) + j - 1].Y - region1[j - 1].Y) / m;
                }
                else
                {
                    dx = (region1[m * (p + 1) + j - 1].X - region1[j - 1].X) * (1.0 - k) / (1 - Math.Pow(k, m));
                    dy = (region1[m * (p + 1) + j - 1].Y - region1[j - 1].Y) * (1.0 - k) / (1 - Math.Pow(k, m));
                    dx *= Math.Pow(k, i - 1);
                    dy *= Math.Pow(k, i - 1);
                }
                
                double x = region1[(i - 1) * (p + 1) + j - 1].X + dx;
                double y = region1[(i - 1) * (p + 1) + j - 1].Y + dy;
        
                region1[i * (p + 1) + j - 1] = new Point2D(x, y);
            }   
        }
        
        // Region 2
        Point2D[] region2 = new Point2D[(p + 1) * (m + 1)];
        
        for (int i = 1; i < p + 2; i++)
        {
            region2[i - 1] = new Point2D(leftBottom.X + (i - 1) * hx, rightTop.Y);
        }
        
        for (int i = 1; i < p + 2; i++)
        {
            double x = r * Math.Cos(3 * Math.PI / 4 - (i - 1) * Math.PI / 2 / p) + center.X;
            double y = r * Math.Sin(3 * Math.PI / 4 - (i - 1) * Math.PI / 2 / p) + center.Y;
        
            region2[m * (p + 1) + i - 1] = new Point2D(x, y);
        }
        
        for (int i = 1; i < m; i++)
        {
            for (int j = 1; j < p + 2; j++)
            {
                double dx, dy;
                
                if (Math.Abs(k - 1.0) < 1E-14)
                {
                    dx = (region2[m * (p + 1) + j - 1].X - region2[j - 1].X) / m;
                    dy = (region2[m * (p + 1) + j - 1].Y - region2[j - 1].Y) / m;
                }
                else
                {
                    dx = (region2[m * (p + 1) + j - 1].X - region2[j - 1].X) * (1.0 - k) / (1 - Math.Pow(k, m));
                    dy = (region2[m * (p + 1) + j - 1].Y - region2[j - 1].Y) * (1.0 - k) / (1 - Math.Pow(k, m));

                    dx *= Math.Pow(k, i - 1);
                    dy *= Math.Pow(k, i - 1);
                }
                
                double x = region2[(i - 1) * (p + 1) + j - 1].X + dx;
                double y = region2[(i - 1) * (p + 1) + j - 1].Y + dy;
        
                region2[i * (p + 1) + j - 1] = new Point2D(x, y);
            }   
        }
        
        // Region 3
        Point2D[] region3 = new Point2D[(p - 1) * (m + 1)];
        
        for (int i = 1; i < p; i++)
        {
            region3[i - 1] = new Point2D(leftBottom.X, leftBottom.Y + i * hy);
        }
        
        for (int i = 1; i < p; i++)
        {
            double x = r * Math.Cos(5 * Math.PI / 4 - i * Math.PI / 2 / p) + center.X;
            double y = r * Math.Sin(5 * Math.PI / 4 - i * Math.PI / 2 / p) + center.Y;
        
            region3[m * (p - 1) + i - 1] = new Point2D(x, y);
        }
        
        for (int i = 1; i < m; i++)
        {
            for (int j = 1; j < p; j++)
            {
                double dx, dy;
                
                if (Math.Abs(k - 1.0) < 1E-14)
                {
                    dx = (region3[m * (p - 1) + j - 1].X - region3[j - 1].X) / m;
                    dy = (region3[m * (p - 1) + j - 1].Y - region3[j - 1].Y) / m;
                }
                else
                {
                    dx = (region3[m * (p - 1) + j - 1].X - region3[j - 1].X) * (1.0 - k) / (1 - Math.Pow(k, m));
                    dy = (region3[m * (p - 1) + j - 1].Y - region3[j - 1].Y) * (1.0 - k) / (1 - Math.Pow(k, m));

                    dx *= Math.Pow(k, i - 1);
                    dy *= Math.Pow(k, i - 1);
                }
                
                double x = region3[(i - 1) * (p - 1) + j - 1].X + dx;
                double y = region3[(i - 1) * (p - 1) + j - 1].Y + dy;
        
                region3[i * (p - 1) + j - 1] = new Point2D(x, y);
            }   
        }
        
        // Region 4
        Point2D[] region4 = new Point2D[(p - 1) * (m + 1)];
        
        for (int i = 1; i < p; i++)
        {
            region4[i - 1] = new Point2D(rightTop.X, leftBottom.Y + i * hy);
        }
        
        for (int i = 1; i < p; i++)
        {
            double x = r * Math.Cos(7 * Math.PI / 4 + i * Math.PI / 2 / p) + center.X;
            double y = r * Math.Sin(7 * Math.PI / 4 + i * Math.PI / 2 / p) + center.Y;
        
            region4[m * (p - 1) + i - 1] = new Point2D(x, y);
        }
        
        for (int i = 1; i < m; i++)
        {
            for (int j = 1; j < p; j++)
            {
                double dx, dy;
                
                if (Math.Abs(k - 1.0) < 1E-14)
                {
                    dx = (region4[m * (p - 1) + j - 1].X - region4[j - 1].X) / m;
                    dy = (region4[m * (p - 1) + j - 1].Y - region4[j - 1].Y) / m;
                }
                else
                {
                    dx = (region4[m * (p - 1) + j - 1].X - region4[j - 1].X) * (1.0 - k) / (1 - Math.Pow(k, m));
                    dy = (region4[m * (p - 1) + j - 1].Y - region4[j - 1].Y) * (1.0 - k) / (1 - Math.Pow(k, m));

                    dx *= Math.Pow(k, i - 1);
                    dy *= Math.Pow(k, i - 1);
                }
                
                double x = region4[(i - 1) * (p - 1) + j - 1].X + dx;
                double y = region4[(i - 1) * (p - 1) + j - 1].Y + dy;
        
                region4[i * (p - 1) + j - 1] = new Point2D(x, y);
            }   
        }

        // Reordering nodes
        for (int i = 1; i < m + 2; i++)
        {
            for (int j = (i - 1) * (p + 1); j < i * (p + 1); j++)
            {
                _wellPoints!.Add(region1[j]);
            }

            for (int j = (i - 1) * (p - 1); j < i * (p - 1); j++)
            {
                _wellPoints!.Add(region4[j]);
            }

            for (int j = i * (p + 1) - 1; j >= (i - 1) * (p + 1); j--)
            {
                _wellPoints!.Add(region2[j]);
            }

            for (int j = i * (p - 1) - 1; j >= (i - 1) * (p - 1); j--)
            {
                _wellPoints!.Add(region3[j]);
            }
        }
    }

    private void CreateWellElements(int p, int m)
    {
        for (int i = 1; i < m + 1; i++)
        {
            for (int j = 1; j < 4 * p + 1; j++)
            {
                int[] nodes = new int[4];

                if (j == 1)
                {
                    nodes[0] = (i - 1) * 4 * p;
                    nodes[1] = nodes[0] + 1;
                    nodes[3] = nodes[0] + 4 * p;
                    nodes[2] = nodes[3] + 1;
                }
                else if (j == 4 * p)
                {
                    nodes[0] = i * 4 * p - 1;
                    nodes[1] = (i - 1) * 4 * p;
                    nodes[2] = nodes[0] + 1;
                    nodes[3] = nodes[0] + 4 * p;
                }
                else
                {
                    nodes[0] = _wellElements![(i - 1) * 4 * p + j - 2].Nodes[1];
                    nodes[3] = _wellElements![(i - 1) * 4 * p + j - 2].Nodes[2];
                    nodes[2] = nodes[3] + 1;
                    nodes[1] = nodes[0] + 1;
                }

                _wellElements!.Add(new FiniteElement(nodes, 0));
            }
        }
    }

    private void CreateRemoteEdgesList()
    {
        int nx = _parameters.SplitParameters.MeshNx;
        int ny = _parameters.SplitParameters.MeshNy;
        double k = _parameters.SplitParameters.WellsCoefficient;
        
        MeshNesting(ref nx, ref ny, ref k);

        _remoteEdges = new List<(int, int)>(nx * ny);

        // Lower side
        for (int ielem = 0; ielem < nx; ielem++)
        {
            _remoteEdges.Add((_elements.Values.IndexOf(_elements[ielem]), 0));
        }
        
        // Upper side
        for (int ielem = nx * (ny - 1); ielem < nx * ny; ielem++)
        {
            _remoteEdges.Add((_elements.Values.IndexOf(_elements[ielem]), 3));
        }
        
        // Left side
        for (int ielem = 0; ielem <= nx * (ny - 1); ielem += nx)
        {
            _remoteEdges.Add((_elements.Values.IndexOf(_elements[ielem]), 1));
        }

        // Right side
        for (int ielem = nx - 1; ielem <= nx * ny; ielem += nx)
        {
            _remoteEdges.Add((_elements.Values.IndexOf(_elements[ielem]), 2));
        }
    }

    private void CreateDirichlet()
    {
        int nx = _parameters.SplitParameters.MeshNx;
        int ny = _parameters.SplitParameters.MeshNy;
        double k = _parameters.SplitParameters.WellsCoefficient;
        double pressure = DataConverter.PressureToPascal(_parameters.Area[0].PlastPressure);
        
        MeshNesting(ref nx, ref ny, ref k);

        _dirichletConditions = new HashSet<DirichletCondition>();

        // lower border
        for (int inode = 0; inode < nx + 1; inode++)
        {
            _dirichletConditions.Add(new DirichletCondition(inode, pressure));
        }
        
        // upper border
        for (int inode = (nx + 1) * ny; inode < (nx + 1) * (ny + 1); inode++)
        {
            _dirichletConditions.Add(new DirichletCondition(inode, pressure));
        }
        
        // left border
        //pressure = DataConverter.PressureToPascal(100);
        for (int i = 0, inode = 0; i < ny + 1; i++, inode += nx + 1)
        {
            _dirichletConditions.Add(new DirichletCondition(inode, pressure));
        }
        
        // // Left side
        // double power = DataConverter.FlowToCubicMetersPerSecond(400);
        // for (int ielem = 0; ielem <= nx * (ny - 1); ielem += nx)
        // {
        //     _neumannConditions!.Add(new NeumannCondition(_elements.Values.IndexOf(_elements[ielem]), 1, power));
        // }

        // right border
        //pressure = DataConverter.PressureToPascal(0);
        for (int i = 0, inode = nx; i < ny + 1; i++, inode += nx + 1)
        {
            _dirichletConditions.Add(new DirichletCondition(inode, pressure));
        }
    }

    private void CreateMaterials()
    {
        _materials = new Material[_parameters.Area.Length];

        for (int i = 0; i < _materials.Length; i++)
        {
            _materials[i] = _parameters.Area[i].Material;
            _materials[i].Permeability = DataConverter.PermeabilityToSquareMeter(_materials[i].Permeability);
        }
    }

    private void ChangeMaterial()
    {
        if (_parameters.Area.Length == 1) return;
        var leftBottom = _parameters.Area[1].LeftBottom;
        var rightTop = _parameters.Area[1].RightTop;

        foreach (var (_, element) in _elements)
        {
            var elementNodes = element.Nodes;
            var elementCenterX = (_points[elementNodes[^1]].Point.X + _points[elementNodes[0]].Point.X) / 2.0;
            var elementCenterY = (_points[elementNodes[^1]].Point.Y + _points[elementNodes[0]].Point.Y) / 2.0;

            if (elementCenterX >= leftBottom.X && elementCenterX <= rightTop.X &&
                elementCenterY >= leftBottom.Y && elementCenterY <= rightTop.Y)
            {
                element.Area = 1;
            }
        }
    }
}