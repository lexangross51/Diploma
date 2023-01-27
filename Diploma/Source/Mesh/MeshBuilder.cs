using System.Dynamic;

namespace Diploma.Source.Mesh;

public class MeshBuilder : IMeshBuilder
{
    private readonly MeshParameters _parameters;
    private List<Point2D> _points = default!, _wellPoints;
    private List<FiniteElement> _elements = default!, _wellElements;
    private Material[] _materials = default!;

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
        var kx = _parameters.SplitParameters.WellKx;
        var ky = _parameters.SplitParameters.WellKy;
        
        // If a nested mesh is required
        MeshNesting(ref nx, ref ny, ref kx, ref ky);
        
        // Create points for the main area 
        double hx = (xEnd - xStart) / nx;
        double hy = (yEnd - yStart) / ny;

        for (int i = 1; i < ny + 2; i++)
        {
            for (int j = 1; j < nx + 2; j++)
            {
                double x = xStart + (j - 1) * hx;
                double y = yStart + (i - 1) * hy;
                
                _points.Add(new Point2D(x, y));
            }
        }
        
        // Generate elements ------------------------------------------------->
        int[] nodes = new int[4];

        for (int i = 0; i < ny; i++)
        {
            for (int j = 0; j < nx; j++)
            {
                nodes[0] = j + i * (nx + 1);
                nodes[1] = j + i * (nx + 1) + 1;
                nodes[2] = j + i * (nx + 1) + nx + 1;
                nodes[3] = j + i * (nx + 1) + nx + 1 + 1;

                _elements.Add(new FiniteElement(nodes, 0));
            }
        }
    }
    
    public (IEnumerable<Point2D>, IEnumerable<FiniteElement>) CreatePointsAndElements()
    {
        int nx = _parameters.SplitParameters.MeshNx;
        int ny = _parameters.SplitParameters.MeshNy;
        double kx = _parameters.SplitParameters.WellKx;
        double ky = _parameters.SplitParameters.WellKy;
        
        MeshNesting(ref nx, ref ny, ref kx, ref ky);
        
        _points = new List<Point2D>((nx + 1) * (ny + 1));
        _elements = new List<FiniteElement>(nx * ny);
        
        CreateUniformMesh();

        if (_parameters.Wells.Length != 0)
        {
            _wellPoints = new List<Point2D>();
            _wellElements = new List<FiniteElement>();

            int maxNodeNum = _elements[^1].Nodes[^1] + 1;

            foreach (var well in _parameters.Wells)
            {
                _wellPoints.Clear();
                _wellElements.Clear();

                Point2D leftBottom = new(well.Center.X - well.Radius / 0.2, well.Center.Y - well.Radius / 0.2);
                Point2D rightTop = new(well.Center.X + well.Radius / 0.2, well.Center.Y + well.Radius / 0.2);

                FindIntersections(leftBottom, rightTop, out List<int> intersected);

                leftBottom = _points[_elements[intersected[0]].Nodes[0]];
                rightTop = _points[_elements[intersected[^1]].Nodes[^1]];
                int p = (int)Math.Sqrt(intersected.Count);

                CreateWellPoints(leftBottom, rightTop, well.Center, well.Radius, p, p - 1);
                CreateWellElements(p, p - 1);

                int elem = 0;
                int shift = _wellElements[0].Nodes[2] - _wellElements[0].Nodes[0];
                
                // Renumber nodes on bottom side
                for (int i = 0; i < p; i++)
                {
                    _wellElements[elem].Nodes[0] = _elements[intersected[i]].Nodes[0];
                    _wellElements[elem].Nodes[1] = _elements[intersected[i]].Nodes[1];
                    _wellElements[elem].Nodes[2] = maxNodeNum;
                    _wellElements[elem].Nodes[3] = maxNodeNum + 1;
                    elem++;
                    maxNodeNum++;
                }

                // Renumber nodes on right side
                for (int i = 0, j = p - 1; i < p; i++, j += p)
                {
                    _wellElements[elem].Nodes[1] = _elements[intersected[j]].Nodes[1];
                    _wellElements[elem].Nodes[3] = _elements[intersected[j]].Nodes[3];
                    _wellElements[elem].Nodes[0] = maxNodeNum;
                    _wellElements[elem].Nodes[2] = maxNodeNum + 1;
                    elem++;
                    maxNodeNum++;
                }

                // Renumber nodes on top side
                for (int i = 0, j = p * p - 1; i < p; i++, j--)
                {
                    _wellElements[elem].Nodes[2] = _elements[intersected[j]].Nodes[2];
                    _wellElements[elem].Nodes[3] = _elements[intersected[j]].Nodes[3];
                    _wellElements[elem].Nodes[1] = maxNodeNum;
                    _wellElements[elem].Nodes[0] = maxNodeNum + 1;
                    elem++;
                    maxNodeNum++;
                }

                // Renumber nodes on left side
                for (int i = 0, j = p * p - p; i < p; i++, j -= p)
                {
                    _wellElements[elem].Nodes[0] = _elements[intersected[j]].Nodes[0];
                    _wellElements[elem].Nodes[2] = _elements[intersected[j]].Nodes[2];
                    _wellElements[elem].Nodes[3] = maxNodeNum;
                    _wellElements[elem].Nodes[1] = i == p - 1 ? maxNodeNum + 1 - shift : maxNodeNum + 1;

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
                        i++;
                        maxNodeNum++;
                    }

                    // Right side
                    for (int j = 0; j < p; j++)
                    {
                        _wellElements[i].Nodes[0] = maxNodeNum;
                        _wellElements[i].Nodes[1] = maxNodeNum - shift;
                        _wellElements[i].Nodes[2] = maxNodeNum + 1;
                        _wellElements[i].Nodes[3] = maxNodeNum + 1 - shift;
                        i++;
                        maxNodeNum++;
                    }

                    // Top side
                    for (int j = 0; j < p; j++)
                    {
                        _wellElements[i].Nodes[0] = maxNodeNum + 1;
                        _wellElements[i].Nodes[1] = maxNodeNum;
                        _wellElements[i].Nodes[2] = maxNodeNum + 1 - shift;
                        _wellElements[i].Nodes[3] = maxNodeNum - shift;
                        i++;
                        maxNodeNum++;
                    }

                    // Left side
                    for (int j = 0; j < p; j++)
                    {
                        _wellElements[i].Nodes[3] = maxNodeNum;
                        _wellElements[i].Nodes[1] = j == p - 1 ? maxNodeNum + 1 - shift : maxNodeNum + 1;
                        _wellElements[i].Nodes[2] = _wellElements[i].Nodes[3] - shift;
                        _wellElements[i].Nodes[0] = _wellElements[i].Nodes[1] - shift;
                        i++;
                        maxNodeNum++;
                    }
                }

                // Delete intersected elements
                for (int ielem = 0; ielem < intersected.Count; ielem++)
                {
                    _elements.RemoveAt(intersected[ielem]);

                    for (int i = 0; i < intersected.Count; i++)
                    {
                        intersected[i]--;
                    }
                }

                // Delete points from _wellPoints which already in _points
                for (int i = 0; i < 4 * p; i++)
                {
                    _wellPoints.RemoveAt(0);
                }

                _elements.AddRange(_wellElements);
                _points.AddRange(_wellPoints);
            }
        }

        return (_points, _elements);
    }

    private void CreateWellPoints(Point2D leftBottom, Point2D rightTop, Point2D center, double r, int p, int m)
    {
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
                double dx = (region1[m * (p + 1) + j - 1].X - region1[j - 1].X) / m;
                double dy = (region1[m * (p + 1) + j - 1].Y - region1[j - 1].Y) / m;
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
                double dx = (region2[m * (p + 1) + j - 1].X - region2[j - 1].X) / m;
                double dy = (region2[m * (p + 1) + j - 1].Y - region2[j - 1].Y) / m;
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
                double dx = (region3[m * (p - 1) + j - 1].X - region3[j - 1].X) / m;
                double dy = (region3[m * (p - 1) + j - 1].Y - region3[j - 1].Y) / m;
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
                double dx = (region4[m * (p - 1) + j - 1].X - region4[j - 1].X) / m;
                double dy = (region4[m * (p - 1) + j - 1].Y - region4[j - 1].Y) / m;
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
                _wellPoints.Add(region1[j]);
            }
            
            for (int j = (i - 1) * (p - 1); j < i * (p - 1); j++)
            {
                _wellPoints.Add(region4[j]);
            }
            
            for (int j = i * (p + 1) - 1; j >= (i - 1) * (p + 1); j--)
            {
                _wellPoints.Add(region2[j]);
            }
            
            for (int j = i * (p - 1) - 1; j >= (i - 1) * (p - 1); j--)
            {
                _wellPoints.Add(region3[j]);
            }
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
    
    private void FindIntersections(Point2D leftBottom, Point2D rightTop, out List<int> elements)
    {
        int nx = _elements[0].Nodes[2] - 1;
        
        int elem1 = -1, elem2 = -1;
        int intersectCnt = 0;
        
        elements = new List<int>();
        
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

            if (intersectCnt == 2) break;
        }

        if (elem1 == -1 || elem2 == -1) throw new Exception("Can't prepare mesh data!");

        int rows = elem2 - elem1 + 1;
        
        for (int i = 0; i < rows; i++)
        {
            for (int ielem = elem1 + i * nx; ielem <= elem2; ielem++)
            {
                elements.Add(ielem);
            }

            elem2 += nx;
        }
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