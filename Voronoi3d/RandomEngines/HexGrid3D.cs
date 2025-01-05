using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;
using Voronoi3d.Geometry;

namespace Voronoi3d.RandomEngines
{
    public class HexGrid3D : IRandom3D
    {
        public List<Point3D> Points { get; set; }
        public HexGrid3D(List<Point3D> points)
        {
            Points = new List<Point3D>();
            foreach (var point in points)
            {
                Points.Add(point);
            }
           
        }
        public IEnumerable<CVT3D.Point3D> GetRandomNumbers()
        {
            List<CVT3D.Point3D> CVTpoints = new List<CVT3D.Point3D> ();
            foreach (var point in Points) 
            { 
                CVTpoints.Add(new CVT3D.Point3D(point.X, point.Y, point.Z));
            }
            return CVTpoints;
        }

        public IEnumerable<CVT3D.Point3D> GetRandomNumbers(MeshGeometry3D mesh3d)
        {
            var vertices = mesh3d.Positions;
            List<CVT3D.Point3D> CVTpoints = new List<CVT3D.Point3D>();
            foreach (var point in Points)
            {
                CVTpoints.Add(new CVT3D.Point3D(point.X, point.Y, point.Z));
            }
            foreach (var vertex in vertices)
            {
                CVTpoints.Add(new CVT3D.Point3D(vertex.X, vertex.Y, vertex.Z));
            }
            return CVTpoints;
        }
    }

}
