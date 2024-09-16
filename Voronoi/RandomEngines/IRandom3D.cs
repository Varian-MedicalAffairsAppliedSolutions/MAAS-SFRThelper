using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace Voronoi.RandomEngines
{
    public interface IRandom3D
    {
        IEnumerable<CVT3D.Point3D> GetRandomNumbers();
        IEnumerable<CVT3D.Point3D> GetRandomNumbers(MeshGeometry3D mesh3d);
    }
}