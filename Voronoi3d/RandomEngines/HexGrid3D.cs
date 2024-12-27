using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using Voronoi3d.Geometry;

//private List<VVector> BuildHexGrid(double progressMax, double Xstart, double Xsize, double Ystart, double Ysize, double Zstart, double Zsize, Structure ptvRetract) // this will setup coords for points on hex grid
//{
//    double A = SpacingSelected.Value * (Math.Sqrt(3) / 2.0); // what is A? why is it this value?
//                                                             // https://www.omnicalculator.com/math/hexagon
//                                                             // the height of a triangle will be h = √3/2 × a

//    var retval = new List<VVector>();
//    void CreateLayer(double zCoord, double x0, double y0)
//    {

//        // create planar hexagonal sphere packing grid
//        var yeven = Arange(y0, y0 + Ysize, 2.0 * A * LateralScalingFactor); // Tenzin - make a drop down menu and rather than having a 2.0, put some variable in it
//                                                                            // 2 is the scaling factor --- changed to 4 and tested -- Matt - 2 and 4 reduces number of spheres overall (makes sense - verified by measurements?)

//        var xeven = Arange(x0, x0 + Xsize, LateralScalingFactor * SpacingSelected.Value);
//        // int yRow = 0;

//        foreach (var y in yeven)
//        {
//            // int xSpot = yRow%2 == 0 ? 1 : 0; // start x spot counter at 1 if y is even and start x spot counter at 0 is y is odd
//            foreach (var x in xeven)
//            {

//                var pt1 = new VVector(x, y, zCoord);
//                var pt2 = new VVector(x + (SpacingSelected.Value / 2.0) * LateralScalingFactor, y + A * LateralScalingFactor, zCoord);

//                // We want to elminate partial spheres - so if we put a check in here - if the point is in ptvRetract, we add it to retval
//                // if it is not inside sphere, we don't add this point to retval

//                bool isInsideptvRetract1 = ptvRetract.IsPointInsideSegment(pt1);
//                bool isInsideptvRetract2 = ptvRetract.IsPointInsideSegment(pt2);

//                if (isInsideptvRetract1)
//                {
//                    retval.Add(pt1);
//                }

//                if (isInsideptvRetract2)
//                {
//                    retval.Add(pt2);
//                }

//                // Old code
//                // retval.Add(new VVector(x, y, zCoord));
//                // retval.Add(new VVector(x + (SpacingSelected.Value / 2.0), y + A, zCoord ));
//                // messy sphere change
//                // retval.Add(new VVector(x + (SpacingSelected.Value / 2.0), y + A, zCoord + A/4)); 

//                // xSpot++;
//                //ProgressValue += progressMax / ((double)yeven.Count() * (double)xeven.Count());
//            }
//            //  yRow++;
//        }
//    }
//    var zRange = Arange(Zstart, Zstart + Zsize, 2.0 * A);
//    foreach (var z in zRange)
//    {
//        CreateLayer(z, Xstart, Ystart);
//        CreateLayer(z + A, Xstart + (SpacingSelected.Value / 2.0), Ystart + (A / 2.0));
//        ProgressValue += progressMax / (double)zRange.Count();
//    }

//    return retval;
//}
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
