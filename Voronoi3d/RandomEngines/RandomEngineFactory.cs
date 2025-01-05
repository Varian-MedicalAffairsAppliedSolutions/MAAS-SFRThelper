
using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace Voronoi3d.RandomEngines
{
    public class RandomEngineFactory
    {
        private static List<Point3D> hexPoints;

        public static IRandom3D Create(RandomEngine randomEngine)
        {
            switch (randomEngine)
            {
                case RandomEngine.HALTONSEQUENCE:
                    return new HaltonSequence3D();
                case RandomEngine.HEXGRID:
                    return new HexGrid3D(hexPoints);
                case RandomEngine.UNIFORMDISTRIBUTION: 
                    throw new NotImplementedException("RandomEngine.UNIFORMDISTRIBUTION not implemented, yet.");
                default:
                    throw new ArgumentException("You should never received this exception (RandomEngineFactory)");
            }
        }

        public static void initializeHex(List<Point3D> points) {

            hexPoints = new List<Point3D> ();
            foreach (Point3D point in points)
            {
                hexPoints.Add(point);
            }
        }
    }
}
