using System.Dynamic;

namespace Diploma.Source.Mesh;

public class MeshBuilder : IMeshBuilder
{
    private readonly MeshParameters _parameters;
    private readonly List<double> _xPoints = new();
    private readonly List<double> _yPoints = new();
    private List<Point2D> _points = default!;
    private List<Point2D> _wellPoints;
    private List<FiniteElement> _elements = default!;
    private List<FiniteElement> _wellElements;
    private Material[] _materials = default!;
    
    // private double d1 = 1;
    // private double d2 = 1;
    // private int p = 2;
    // private int m = 1;
    // private double r = 0.1;

    public MeshBuilder(MeshParameters parameters)
        => _parameters = parameters;
    
    // side = 0 -> left or lower border
    // side = 1 -> right or upper border
    private static int FindNearestIndex(IReadOnlyList<double> points, double point, int side)
    {
        for (int i = 0; i < points.Count - 1; i++)
        {
            if (Math.Abs(point - points[i]) < 1E-14) return i;
            if (Math.Abs(point - points[i + 1]) < 1E-14) return i + 1;
            
            if (point > points[i] && point < points[i + 1])
            {
                return side == 0 ? i : i + 1;
            }
        }

        return -1;
    }

    private bool IsContain(FiniteElement element, Point2D point)
    {
        var leftBottom = _points[element.Nodes[0]];
        var rightTop = _points[element.Nodes[3]];

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
    
    private void NumerateEdges()
    {
        // var connectivityList = new List<SortedSet<int>>();
        //
        // for (int i = 0; i < _points.Length; i++)
        // {
        //     connectivityList.Add(new SortedSet<int>());
        // }
        //
        // foreach (var element in _elements)
        // {
        //     var nodes = element.Nodes;
        //
        //     for (int i = 0; i < 3; i++) 
        //     {
        //         int ind1 = nodes[i];
        //
        //         for (int j = i + 1; j < 4; j++) 
        //         {
        //             int ind2 = nodes[j];
        //
        //             if (IsEdgeExist(i, j)) 
        //             {
        //                 connectivityList[ind2].Add(ind1);
        //             }
        //         }
        //     }
        // }
        //
        // var ig = new int[_points.Length + 1];
        //
        // ig[0] = 0;
        // ig[1] = 0;
        //
        // for (int i = 1; i < connectivityList.Count; i++) 
        // {
        //     ig[i + 1] = ig[i] + connectivityList[i].Count;
        // }
        //
        // var jg = new int[ig[^1]];
        //
        // for (int i = 1, j = 0; i < connectivityList.Count; i++) 
        // {
        //     foreach (var it in connectivityList[i])
        //     {
        //         jg[j++] = it;
        //     }
        // }
        //
        // foreach (var element in _elements)
        // {
        //     for (int i = 0; i < 3; i++) 
        //     {
        //         int ind1 = element.Nodes[i];
        //
        //         for (int j = i + 1; j < 4; j++) 
        //         {
        //             int ind2 = element.Nodes[j];
        //
        //             if (ind1 < ind2) 
        //             {
        //                 for (int ind = ig[ind2]; ind < ig[ind2 + 1]; ind++) 
        //                 {
        //                     if (jg[ind] == ind1) 
        //                     {
        //                         element.Edges.Add(ind);
        //                     }
        //                 }
        //             }
        //             else 
        //             {
        //                 for (int ind = ig[ind1]; ind < ig[ind1 + 1]; ind++) 
        //                 {
        //                     if (jg[ind] == ind2) 
        //                     {
        //                         element.Edges.Add(ind);
        //                     }
        //                 }
        //             }
        //         }
        //     }
        // }
    }

    public IEnumerable<Point2D> CreatePoints()
    {
        // _points = new List<Point2D>(_xPoints.Count * _yPoints.Count);
        //
        // // Taking data for the main area
        // double xStart = _parameters.Area[0].LeftBottom.X;
        // double xEnd = _parameters.Area[0].RightTop.X;
        // double yStart = _parameters.Area[0].LeftBottom.Y;
        // double yEnd = _parameters.Area[0].RightTop.Y;
        //
        // int nx = _parameters.SplitParameters.MeshNx;
        // int ny = _parameters.SplitParameters.MeshNy;
        // var kx = _parameters.SplitParameters.WellKx;
        // var ky = _parameters.SplitParameters.WellKy;
        //
        // // If a nested mesh is required
        // MeshNesting(ref nx, ref ny, ref kx, ref ky);
        //
        // // Create points for the main area 
        // double hx = (xEnd - xStart) / nx;
        // double hy = (yEnd - yStart) / ny;
        //
        // _xPoints.Add(xStart);
        // _yPoints.Add(yStart);
        //
        // for (int i = 1; i < nx + 1; i++)
        // {
        //     _xPoints.Add(_xPoints[i - 1] + hx);
        // }
        //
        // for (int i = 1; i < ny + 1; i++)
        // {
        //     _yPoints.Add(_yPoints[i - 1] + hy);
        // }
        //
        // foreach (var y in _yPoints)
        // {
        //     foreach (var x in _xPoints)
        //     {
        //         _points.Add(new Point2D(x, y));
        //     }
        // }
        
        _wellPoints = new List<Point2D>();

        foreach (var well in _parameters.Wells)
        {
            Point2D leftBottom = new(well.Center.X - well.Radius / 0.2, well.Center.Y - well.Radius / 0.2);
            Point2D rightTop = new(well.Center.X + well.Radius / 0.2, well.Center.Y + well.Radius / 0.2);
            
            _wellPoints.Clear();

            CreateWellPoints(leftBottom, rightTop, 3, 2, well.Center, well.Radius);
            _points = _wellPoints;
        }

        return _points;
    }

    private void CreateWellPoints(Point2D leftBottom, Point2D rightTop, int p, int m, Point2D center, double r)
    {
        double hx = (rightTop.X - leftBottom.X) / p;
        double hy = (rightTop.Y - leftBottom.Y) / p;
        
        // Region 1
        Point2D[] coor1 = new Point2D[(p + 1) * (m + 1)];
        
        for (int i = 1; i < p + 2; i++)
        {
            coor1[i - 1] = new Point2D(leftBottom.X + (i - 1) * hx, leftBottom.Y);
        }
        
        for (int i = 1; i < p + 2; i++)
        {
            double x = r * Math.Cos(5 * Math.PI / 4 + (i - 1) * Math.PI / 2 / p) + center.X / 2;
            double y = r * Math.Sin(5 * Math.PI / 4 + (i - 1) * Math.PI / 2 / p) + center.Y / 2;
        
            coor1[m * (p + 1) + i - 1] = new Point2D(x, y);
        }
        
        for (int i = 1; i < m; i++)
        {
            for (int j = 1; j < p + 2; j++)
            {
                double dx = (coor1[m * (p + 1) + j - 1].X - coor1[j - 1].X) / m;
                double dy = (coor1[m * (p + 1) + j - 1].Y - coor1[j - 1].Y) / m;
                double x = coor1[(i - 1) * (p + 1) + j - 1].X + dx;
                double y = coor1[(i - 1) * (p + 1) + j - 1].Y + dy;
        
                coor1[i * (p + 1) + j - 1] = new Point2D(x, y);
            }   
        }
        
        // Region 2
        Point2D[] coor2 = new Point2D[(p + 1) * (m + 1)];
        
        for (int i = 1; i < p + 2; i++)
        {
            coor2[i - 1] = new Point2D(leftBottom.X + (i - 1) * hx, rightTop.Y);
        }
        
        for (int i = 1; i < p + 2; i++)
        {
            double x = r * Math.Cos(3 * Math.PI / 4 - (i - 1) * Math.PI / 2 / p) + center.X / 2;
            double y = r * Math.Sin(3 * Math.PI / 4 - (i - 1) * Math.PI / 2 / p) + center.Y / 2;
        
            coor2[m * (p + 1) + i - 1] = new Point2D(x, y);
        }
        
        for (int i = 1; i < m; i++)
        {
            for (int j = 1; j < p + 2; j++)
            {
                double dx = (coor2[m * (p + 1) + j - 1].X - coor2[j - 1].X) / m;
                double dy = (coor2[m * (p + 1) + j - 1].Y - coor2[j - 1].Y) / m;
                double x = coor2[(i - 1) * (p + 1) + j - 1].X + dx;
                double y = coor2[(i - 1) * (p + 1) + j - 1].Y + dy;
        
                coor2[i * (p + 1) + j - 1] = new Point2D(x, y);
            }   
        }
        
        // Region 3
        Point2D[] coor3 = new Point2D[(p - 1) * (m + 1)];
        
        for (int i = 1; i < p; i++)
        {
            coor3[i - 1] = new Point2D(leftBottom.X, leftBottom.Y + i * hy);
        }
        
        for (int i = 1; i < p; i++)
        {
            double x = r * Math.Cos(5 * Math.PI / 4 - i * Math.PI / 2 / p) + center.X / 2;
            double y = r * Math.Sin(5 * Math.PI / 4 - i * Math.PI / 2 / p) + center.Y / 2;
        
            coor3[m * (p - 1) + i - 1] = new Point2D(x, y);
        }
        
        for (int i = 1; i < m; i++)
        {
            for (int j = 1; j < p; j++)
            {
                double dx = (coor3[m * (p - 1) + j - 1].X - coor3[j - 1].X) / m;
                double dy = (coor3[m * (p - 1) + j - 1].Y - coor3[j - 1].Y) / m;
                double x = coor3[(i - 1) * (p - 1) + j - 1].X + dx;
                double y = coor3[(i - 1) * (p - 1) + j - 1].Y + dy;
        
                coor3[i * (p - 1) + j - 1] = new Point2D(x, y);
            }   
        }
        
        // Region 4
        Point2D[] coor4 = new Point2D[(p - 1) * (m + 1)];
        
        for (int i = 1; i < p; i++)
        {
            coor4[i - 1] = new Point2D(rightTop.X, leftBottom.Y + i * hy);
        }
        
        for (int i = 1; i < p; i++)
        {
            double x = r * Math.Cos(7 * Math.PI / 4 + i * Math.PI / 2 / p) + center.X / 2;
            double y = r * Math.Sin(7 * Math.PI / 4 + i * Math.PI / 2 / p) + center.Y / 2;
        
            coor4[m * (p - 1) + i - 1] = new Point2D(x, y);
        }
        
        for (int i = 1; i < m; i++)
        {
            for (int j = 1; j < p; j++)
            {
                double dx = (coor4[m * (p - 1) + j - 1].X - coor4[j - 1].X) / m;
                double dy = (coor4[m * (p - 1) + j - 1].Y - coor4[j - 1].Y) / m;
                double x = coor4[(i - 1) * (p - 1) + j - 1].X + dx;
                double y = coor4[(i - 1) * (p - 1) + j - 1].Y + dy;
        
                coor4[i * (p - 1) + j - 1] = new Point2D(x, y);
            }   
        }
        
        // Reordering nodes
        for (int i = 1; i < m + 2; i++)
        {
            for (int j = (i - 1) * (p + 1); j < i * (p + 1); j++)
            {
                _wellPoints.Add(coor1[j]);
            }
            
            for (int j = (i - 1) * (p - 1); j < i * (p - 1); j++)
            {
                _wellPoints.Add(coor4[j]);
            }
            
            for (int j = i * (p + 1) - 1; j >= (i - 1) * (p + 1); j--)
            {
                _wellPoints.Add(coor2[j]);
            }
            
            for (int j = i * (p - 1) - 1; j >= (i - 1) * (p - 1); j--)
            {
                _wellPoints.Add(coor3[j]);
            }
        }
    }

    private void FindIntersections(Point2D leftBottom, Point2D rightTop, out List<int> elements)
    {
        elements = new List<int>();
        int elem1 = -1, elem2 = -1, elem3 = -1, elem4 = -1;

        int intersectCnt = 0;
        
        for (int ielem = 0; ielem < _elements.Count; ielem++)
        {
            var nodes = _elements[ielem].Nodes;
            var p1 = _points[nodes[0]];
            var p2 = _points[nodes[^1]];

            if (leftBottom.X >= p1.X && leftBottom.X <= p2.X && leftBottom.Y >= p1.Y && leftBottom.Y <= p2.Y)
            {
                intersectCnt++;
                elem1 = ielem;
            }
            if (rightTop.X >= p1.X && rightTop.X <= p2.X && leftBottom.Y >= p1.Y && leftBottom.Y <= p2.Y)
            {
                intersectCnt++;
                elem2 = ielem;
            }
            if (leftBottom.X >= p1.X && leftBottom.X <= p2.X && rightTop.Y >= p1.Y && rightTop.Y <= p2.Y)
            {
                intersectCnt++;
                elem3 = ielem;
            }
            if (rightTop.X >= p1.X && rightTop.X <= p2.X && rightTop.Y >= p1.Y && rightTop.Y <= p2.Y)
            {
                intersectCnt++;
                elem4 = ielem;
            }

            if (intersectCnt == 4) break;
        }

        if (elem1 == -1 || elem2 == -1 || elem3 == -1 || elem4 == -1) throw new Exception("Can't prepare mesh data!");

        for (int i = 0; i < (elem3 - elem1) / (_xPoints.Count - 1) + 1; i++)
        {
            for (int ielem = elem1 + i * (_xPoints.Count - 1); ielem <= elem2; ielem++)
            {
                elements.Add(ielem);
            }

            elem2 += _xPoints.Count - 1;
        }
    }

    private void CreateWellElements(int p, int m)
    {
        int[] nodes = new int[4];
        
        for (int i = 1; i < m + 1; i++)
        {
            for (int j = 1; j < 4 * p + 1; j++)
            {
                if (j == 1)
                {
                    nodes[0] = (i - 1) * 4 * p + j - 1;
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
                    nodes[0] = _wellElements[(i - 1) * 4 * p + j - 2].Nodes[1];
                    nodes[1] = nodes[0] + 1;
                    nodes[2] = _wellElements[(i - 1) * 4 * p + j - 2].Nodes[3];
                    nodes[3] = nodes[2] + 1;
                }
                
                Array.Sort(nodes);
                _wellElements.Add(new FiniteElement(nodes, 0));
            }
        }
    }
    
    public IEnumerable<FiniteElement> CreateElements()
    {
        _elements = new List<FiniteElement>((_xPoints.Count - 1) * (_yPoints.Count - 1));
        
        int[] nodes = new int[4];
        int nx = _xPoints.Count - 1;
        int ny = _yPoints.Count - 1;

        for (int i = 0; i < ny; i++)
        {
            for (int j = 0; j < nx; j++)
            {
                nodes[0] = j + i * (nx + 1);
                nodes[1] = j + i * (nx + 1) + 1;
                nodes[2] = j + i * (nx + 1) + (nx + 1);
                nodes[3] = j + i * (nx + 1) + (nx + 1) + 1;

                _elements.Add(new FiniteElement(nodes, 0));
            }
        }

        //int maxNodeNum = _elements[^1].Nodes[^1] + 1;
        
        _wellPoints = new List<Point2D>();
        _wellElements = new List<FiniteElement>();
        
        foreach (var well in _parameters.Wells)
        {
            Point2D leftBottom = new(well.Center.X - well.Radius / 0.2, well.Center.Y - well.Radius / 0.2);
            Point2D rightTop = new(well.Center.X + well.Radius / 0.2, well.Center.Y + well.Radius / 0.2);
            
            //FindIntersections(leftBottom, rightTop, out List<int> intersected);

            // leftBottom = _points[_elements[intersected[0]].Nodes[0]];
            // rightTop = _points[_elements[intersected[^1]].Nodes[^1]];
            // int p = (int)Math.Sqrt(intersected.Count);
            int p = 3;
            
            _wellPoints.Clear();
            _wellElements.Clear();
            
            //CreateWellPoints(leftBottom, rightTop, p, p - 1, well.Radius);
            CreateWellElements(p, p - 1);
            _points = _wellPoints;

            // int elem = 0;
            // //int shift = _wellElements[0].Nodes[2] - _wellElements[0].Nodes[0];
            //
            // // Renumber nodes on bottom side
            // for (int i = 0; i < p; i++)
            // {
            //     _wellElements[elem].Nodes[0] = _elements[intersected[i]].Nodes[0];
            //     _wellElements[elem].Nodes[1] = _elements[intersected[i]].Nodes[1];
            //     _wellElements[elem].Nodes[2] = maxNodeNum;
            //     _wellElements[elem].Nodes[3] = maxNodeNum + 1;
            //     elem++;
            //     maxNodeNum++;
            // }
            //
            // // Renumber nodes on right side
            // for (int i = 0, j = p - 1; i < p; i++, j++)
            // {
            //     _wellElements[elem].Nodes[1] = _elements[intersected[j]].Nodes[1];
            //     _wellElements[elem].Nodes[3] = _elements[intersected[j]].Nodes[3];
            //     _wellElements[elem].Nodes[0] = maxNodeNum;
            //     _wellElements[elem].Nodes[2] = maxNodeNum + 1;
            //     elem++;
            //     maxNodeNum++;
            // }
            //
            // // Renumber nodes on top side
            // for (int i = 0, j = p * p - 1; i < p; i++, j--)
            // {
            //     _wellElements[elem].Nodes[2] = _elements[intersected[j]].Nodes[2];
            //     _wellElements[elem].Nodes[3] = _elements[intersected[j]].Nodes[3];
            //     _wellElements[elem].Nodes[0] = maxNodeNum;
            //     _wellElements[elem].Nodes[1] = maxNodeNum + 1;
            //     elem++;
            //     maxNodeNum++;
            // }
            //
            // // Renumber nodes on left side
            // for (int i = 0, j = p * p - p; i < p; i++, j--)
            // {
            //     _wellElements[elem].Nodes[0] = _elements[intersected[j]].Nodes[0];
            //     _wellElements[elem].Nodes[2] = _elements[intersected[j]].Nodes[2];
            //     _wellElements[elem].Nodes[1] = maxNodeNum;
            //     _wellElements[elem].Nodes[3] = maxNodeNum + 1;
            //     elem++;
            //     maxNodeNum++;
            // }
            //
            // // Renumber other nodes
            // for (int i = elem; i < _wellElements.Count;)
            // {
            //     for (int j = 0; j < p; j++)
            //     {
            //         _wellElements[i].Nodes[2] = maxNodeNum;
            //         _wellElements[i].Nodes[3] = maxNodeNum + 1;
            //         i++;
            //         maxNodeNum++;
            //     }
            //     
            //     for (int j = 0; j < p; j++)
            //     {
            //         _wellElements[i].Nodes[0] = maxNodeNum;
            //         _wellElements[i].Nodes[2] = maxNodeNum + 1;
            //         i++;
            //         maxNodeNum++;
            //     }
            //     
            //     for (int j = 0; j < p; j++)
            //     {
            //         _wellElements[i].Nodes[0] = maxNodeNum;
            //         _wellElements[i].Nodes[1] = maxNodeNum + 1;
            //         i++;
            //         maxNodeNum++;
            //     }
            //     
            //     for (int j = 0; j < p; j++)
            //     {
            //         _wellElements[i].Nodes[1] = maxNodeNum;
            //         _wellElements[i].Nodes[3] = maxNodeNum + 1;
            //         i++;
            //         maxNodeNum++;
            //     }
            // }
            //
            // // Delete intersected elements
            // for (int ielem = 0; ielem < intersected.Count; ielem++)
            // {
            //     _elements.RemoveAt(intersected[ielem]);
            //
            //     for (int i = 0; i < intersected.Count; i++)
            //     {
            //         intersected[i]--;
            //     }
            // }
            //
            // _elements.AddRange(_wellElements);
            // _points.AddRange(_wellPoints);
        }

        // If the element is in a region with a different permeability,
        // we change its material number
        // if (_parameters.Area.Length > 1)
        // {
        //     var leftBottom = _parameters.Area[1].LeftBottom;
        //     var rightTop = _parameters.Area[1].RightTop;
        //
        //     foreach (var element in _elements)
        //     {
        //         var elementNodes = element.Nodes;
        //         var elementCenterX = (_points[elementNodes[3]].X + _points[elementNodes[0]].X) / 2.0;
        //         var elementCenterY = (_points[elementNodes[3]].Y + _points[elementNodes[0]].Y) / 2.0;
        //
        //         if (elementCenterX >= leftBottom.X && elementCenterX <= rightTop.X &&
        //             elementCenterY >= leftBottom.Y && elementCenterY <= rightTop.Y)
        //         {
        //             element.Area = 1;
        //         }
        //     }
        // }

        // Numerate edges of each element
        //NumerateEdges();

        // Set edges direction
        // -1 - if the outside normal doesn't coincide with the fixed
        // 1 - if the same
        // foreach (var element in _elements)
        // {
        //     element.EdgesDirect = new List<int>() { -1, -1, 1, 1 };
        // }

        return _wellElements;
    }

    public IEnumerable<DirichletCondition> CreateDirichlet()
    {
        // int nx = _xPoints.Count - 1;
        // int ny = _yPoints.Count - 1;
        // double pressure = _parameters.Area[0].PlastPressure;

        HashSet<DirichletCondition> dirichletConditions = new();

        // // lower border
        // for (int inode = 0; inode < nx + 1; inode++)
        // {
        //     dirichletConditions.Add(new (inode, pressure));
        // }
        //
        // // upper border
        // for (int inode = (nx + 1) * ny; inode < (nx + 1) * (ny + 1); inode++)
        // {
        //     dirichletConditions.Add(new (inode, pressure));
        // }
        //
        // // left border
        // //pressure = 10;
        // for (int i = 0, inode = 0; i < ny + 1; i++, inode += nx + 1)
        // {
        //     dirichletConditions.Add(new (inode, pressure));
        // }
        //
        // // right border
        // //pressure = 0;
        // for (int i = 0, inode = nx; i < ny + 1; i++, inode += nx + 1)
        // {
        //     dirichletConditions.Add(new (inode, pressure));
        // }

        return dirichletConditions;
    }

    public IEnumerable<NeumannCondition> CreateNeumann()
    {
        List<NeumannCondition> neumannConditions = new(_parameters.Wells.Length);
        
        // foreach (var well in _parameters.Wells)
        // {
        //     for (int i = 0; i < _elements.Length; i++)
        //     {
        //         if (IsContain(_elements[i], well.Center))
        //         {
        //             neumannConditions.Add(new(i, well.Power));
        //             break;
        //         }
        //     }
        // }

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