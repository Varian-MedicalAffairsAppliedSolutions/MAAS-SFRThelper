using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using Voronoi3d.Geometry;

namespace Voronoi3d.RandomEngines
{
    public class HexGrid3D : IRandom3D
    {
        private int index;
        private int[] bases;

        public HexGrid3D(int startIndex = 0)
        {
            index = startIndex;
            bases = new int[] { 2, 3, 5 };
        }

        public double[] NextPoint()
        {
            double[] point = new double[3];

            for (int i = 0; i < 3; i++)
            {
                point[i] = Halton(index, bases[i]);
            }

            index++;
            return point;
        }

        private double Halton(int index, int baseNum)
        {
            double result = 0.0;
            double f = 1.0;
            int i = index;

            while (i > 0)
            {
                f = f / baseNum;
                result = result + f * (i % baseNum);
                i = i / baseNum;
            }

            return result;
        }

        public IEnumerable<CVT3D.Point3D> GetRandomNumbers()
        {
            while (true)
            {
                double[] point = NextPoint();
                yield return new CVT3D.Point3D(point[0], point[1], point[2]);
            }
        }

        public IEnumerable<CVT3D.Point3D> GetRandomNumbers(MeshGeometry3D mesh3d)
        {
            var vertices = mesh3d.Positions;
            var triangleIndices = mesh3d.TriangleIndices;

            while (true)
            {
                // Generate a random point using the Halton sequence
                double[] point = NextPoint();

                // Scale the Halton point to be within the bounds of the mesh's bounding box
                var minX = vertices.Min(v => v.X);
                var maxX = vertices.Max(v => v.X);
                var minY = vertices.Min(v => v.Y);
                var maxY = vertices.Max(v => v.Y);
                var minZ = vertices.Min(v => v.Z);
                var maxZ = vertices.Max(v => v.Z);

                double x = minX + point[0] * (maxX - minX);
                double y = minY + point[1] * (maxY - minY);
                double z = minZ + point[2] * (maxZ - minZ);

                // Check if the point is inside the mesh
                if (mesh3d.IsPointInside(x, y, z))
                {
                    yield return new CVT3D.Point3D(x, y, z);
                }
            }
        }
    }
}
