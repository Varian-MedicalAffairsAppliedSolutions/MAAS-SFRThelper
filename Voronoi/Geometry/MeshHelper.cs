using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Voronoi.Geometry
{
    public static class MeshHelper
    {
        public enum InsideMeshCheckMethod
        {
            RayCasting,
            WindingNumber
        }

        public static bool IsPointInside(this MeshGeometry3D mesh, double x, double y, double z,
            InsideMeshCheckMethod method = InsideMeshCheckMethod.WindingNumber)
        {
            switch (method)
            {
                case InsideMeshCheckMethod.RayCasting:
                    return IsPointInside(mesh, new Point3D(x, y, z), method);
                case InsideMeshCheckMethod.WindingNumber:
                    return IsPointInside(mesh, new Point3D(x, y, z), method);
                default:
                    throw new ArgumentException("Invalid method selected for inside mesh check.");
            }
        }

        public static bool IsPointInside(this MeshGeometry3D mesh, Point3D point,
            InsideMeshCheckMethod method = InsideMeshCheckMethod.WindingNumber)
        {
            switch (method)
            {
                case InsideMeshCheckMethod.RayCasting:
                    return IsPointInsideMeshRayCasting(mesh, point);
                case InsideMeshCheckMethod.WindingNumber:
                    return IsPointInsideMeshWindingNumber(mesh, point);
                default:
                    throw new ArgumentException("Invalid method selected for inside mesh check.");
            }
        }

        private static bool IsPointInsideMeshRayCasting(MeshGeometry3D mesh, Point3D point)
        {
            int angleIncrement = 45;
            var directions = GetRayDirections(angleIncrement);
            int consistentIntersections = 0;

            foreach (var rayDirection in directions)
            {
                int intersectionCount = 0;
                var positions = mesh.Positions;
                var indices = mesh.TriangleIndices;

                for (int i = 0; i < indices.Count; i += 3)
                {
                    Point3D p1 = positions[indices[i]];
                    Point3D p2 = positions[indices[i + 1]];
                    Point3D p3 = positions[indices[i + 2]];

                    if (RayIntersectsTriangle(point, rayDirection, p1, p2, p3))
                    {
                        intersectionCount++;
                    }
                }

                if ((intersectionCount % 2) == 1)
                {
                    consistentIntersections++;
                }
            }

            return consistentIntersections > directions.Count / 2;
        }

        private static bool RayIntersectsTriangle(Point3D origin, Vector3D direction, Point3D v0, Point3D v1,
            Point3D v2)
        {
            Vector3D e1 = v1 - v0;
            Vector3D e2 = v2 - v0;
            Vector3D pvec = Vector3D.CrossProduct(direction, e2);
            double det = Vector3D.DotProduct(e1, pvec);

            if (Math.Abs(det) < 1e-8) return false;

            double invDet = 1.0 / det;
            Vector3D tvec = origin - v0;
            double u = Vector3D.DotProduct(tvec, pvec) * invDet;
            if (u < 0 || u > 1) return false;

            Vector3D qvec = Vector3D.CrossProduct(tvec, e1);
            double v = Vector3D.DotProduct(direction, qvec) * invDet;
            if (v < 0 || u + v > 1) return false;

            double t = Vector3D.DotProduct(e2, qvec) * invDet;

            return t >= 0;
        }

        private static List<Vector3D> GetRayDirections(int angleIncrement)
        {
            var directions = new List<Vector3D>();

            for (int azimuth = 0; azimuth < 360; azimuth += angleIncrement)
            {
                for (int elevation = -90; elevation <= 90; elevation += angleIncrement)
                {
                    double radAzimuth = DegreeToRadian(azimuth);
                    double radElevation = DegreeToRadian(elevation);

                    double x = Math.Cos(radElevation) * Math.Cos(radAzimuth);
                    double y = Math.Cos(radElevation) * Math.Sin(radAzimuth);
                    double z = Math.Sin(radElevation);

                    Vector3D direction = new Vector3D(x, y, z);
                    directions.Add(direction);
                }
            }

            return directions;
        }

        private static double DegreeToRadian(int degree)
        {
            return degree * Math.PI / 180.0;
        }

        private static bool IsPointInsideMeshWindingNumber(MeshGeometry3D mesh, Point3D point)
        {
            double totalSolidAngle = 0.0;
            var positions = mesh.Positions;
            var indices = mesh.TriangleIndices;

            for (int i = 0; i < indices.Count; i += 3)
            {
                Point3D p1 = positions[indices[i]];
                Point3D p2 = positions[indices[i + 1]];
                Point3D p3 = positions[indices[i + 2]];

                double solidAngle = CalculateSolidAngle(point, p1, p2, p3);

                totalSolidAngle += solidAngle;
            }

            return Math.Abs(totalSolidAngle) > 4 * Math.PI - 1e-5;
        }

        private static double CalculateSolidAngle(Point3D p, Point3D a, Point3D b, Point3D c)
        {
            Vector3D ap = a - p;
            Vector3D bp = b - p;
            Vector3D cp = c - p;

            double apLength = ap.Length;
            double bpLength = bp.Length;
            double cpLength = cp.Length;

            Vector3D crossProduct = Vector3D.CrossProduct(bp, cp);
            double crossProductLength = crossProduct.Length;

            double numerator = Vector3D.DotProduct(ap, crossProduct);

            double denominator = apLength * bpLength * cpLength +
                                 Vector3D.DotProduct(ap, bp) * cpLength +
                                 Vector3D.DotProduct(ap, cp) * bpLength +
                                 Vector3D.DotProduct(bp, cp) * apLength;

            double solidAngle = 2.0 * Math.Atan2(numerator, denominator);

            return solidAngle;
        }


        public static MeshGeometry3D CreateCubeMesh(double maxX, double maxY, double maxZ)
        {
            var mesh = new MeshGeometry3D();

            // Define the 8 vertices of the cube
            mesh.Positions = new Point3DCollection
            {
                new Point3D(0, 0, 0),       // Vertex 0
                new Point3D(maxX, 0, 0),    // Vertex 1
                new Point3D(maxX, maxY, 0), // Vertex 2
                new Point3D(0, maxY, 0),    // Vertex 3
                new Point3D(0, 0, maxZ),    // Vertex 4
                new Point3D(maxX, 0, maxZ), // Vertex 5
                new Point3D(maxX, maxY, maxZ), // Vertex 6
                new Point3D(0, maxY, maxZ)  // Vertex 7
            };

            // Define the 12 triangles (2 per face of the cube)
            mesh.TriangleIndices = new Int32Collection
            {
                // Front face
                0, 1, 2,
                0, 2, 3,
                // Right face
                1, 5, 6,
                1, 6, 2,
                // Back face
                5, 4, 7,
                5, 7, 6,
                // Left face
                4, 0, 3,
                4, 3, 7,
                // Top face
                3, 2, 6,
                3, 6, 7,
                // Bottom face
                4, 5, 1,
                4, 1, 0
            };

            return mesh;
        }

    }
}
