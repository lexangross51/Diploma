using System.Collections.Generic;
using Diploma.Source.Geometry;
using Diploma.Source.Mesh;

namespace Diploma.Source.Interfaces;

public interface IMeshBuilder
{
    IEnumerable<Point2D> CreatePoints();
    IEnumerable<FiniteElement> CreateElements();
}