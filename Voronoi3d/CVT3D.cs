using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using Voronoi3d.RandomEngines;
using Voronoi3d.StoppingCriteria;

namespace Voronoi3d
{
    public class CVT3D
    {
        private readonly MeshGeometry3D _mesh;
        private readonly CVTSettings _settings;

        public struct Point3D
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }
            public double Error { get; }

            public Point3D(double x, double y, double z, double error = 1e-06)
            {
                X = x;
                Y = y;
                Z = z;
                Error = error;
            }

            public override string ToString()
            {
                return $"({X:F6}, {Y:F6}, {Z:F6})";
            }

            public override bool Equals(object obj)
            {
                if (obj is Point3D other)
                {
                    return Math.Abs(X - other.X) < Error &&
                           Math.Abs(Y - other.Y) < Error &&
                           Math.Abs(Z - other.Z) < Error;
                }

                return false;
            }

            public override int GetHashCode()
            {
                return HashHelper.CombineHashCodes(X, Y, Z);
            }
        }

        private IStoppingCriterion StoppingCriterion { get; }

        public CVT3D(MeshGeometry3D mesh, CVTSettings settings = default)
        {
         //    Prism.Commands.DelegateCommand DummyCommand = new Prism.Commands.DelegateCommand(null);
            _mesh = mesh;
            _settings = settings;
            StoppingCriterion = _settings?.StoppingCriterion;
        }


        public IReadOnlyCollection<Point3D> CalculateGenerators()
        {
            var numberOfPoints = _settings.NumberOfGenerators;
            var maxIterations = _settings.MaxNumberOfIterations; 
            List<CVT3D.Point3D> points = GenerateRandomPoints(numberOfPoints, _mesh);
            List<CVT3D.Point3D> generators = LloydRelaxation(points, maxIterations, _mesh);
            return generators;
        }

        protected List<Point3D> GenerateRandomPoints(int count, MeshGeometry3D mesh3d)
        {
            IRandom3D rand = RandomEngineFactory.Create(_settings.SelectedSamplingMethod);
            var points = rand
                .GetRandomNumbers(mesh3d)
                .Take(count)
                .Select(p => new Point3D(p.X, p.Y, p.Z));
            return points.ToList();
        }

        protected int FindClosestPointIndex(Point3D p, List<Point3D> points)
        {
            int closestIndex = -1;
            double closestDistance = double.MaxValue;

            for (int i = 0; i < points.Count; i++)
            {
                double distance = Math.Sqrt(
                    Math.Pow(p.X - points[i].X, 2) +
                    Math.Pow(p.Y - points[i].Y, 2) +
                    Math.Pow(p.Z - points[i].Z, 2)
                );

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        protected Point3D ComputeCentroid(List<Point3D> points)
        {
            if (points.Count == 0) return new Point3D(0, 0, 0);

            double sumX = points.Sum(p => p.X);
            double sumY = points.Sum(p => p.Y);
            double sumZ = points.Sum(p => p.Z);

            return new Point3D(sumX / points.Count, sumY / points.Count, sumZ / points.Count);
        }

        protected List<Point3D> LloydRelaxation(List<Point3D> points, int iterations, MeshGeometry3D mesh3d)
        {
            List<Point3D> previousPoints = new List<Point3D>();

            // Generate random points inside the mesh
            List<Point3D> randomPoints = GenerateRandomPoints(_settings.NumberOfSamplingPoints, mesh3d);

            for (int iter = 0; iter < iterations; iter++)
            {
                // Create empty lists for Voronoi regions
                List<List<Point3D>> voronoiRegions = new List<List<Point3D>>();
                for (int i = 0; i < points.Count; i++)
                    voronoiRegions.Add(new List<Point3D>());

                foreach (var p in randomPoints)
                {
                    int closestIndex = FindClosestPointIndex(p, points);
                    voronoiRegions[closestIndex].Add(p);
                }

                // Compute the centroid of each region and update points
                for (int i = 0; i < points.Count; i++)
                {
                    points[i] = ComputeCentroid(voronoiRegions[i]);
                }

                if (CanStop(previousPoints, points))
                    return points;

                previousPoints = points.Select(p => new Point3D(p.X, p.Y, p.Z)).ToList();
            }

            return points;
        }

        protected bool CanStop(List<Point3D> previousPoints, List<Point3D> points) =>
            StoppingCriterion.CanStop(previousPoints, points);
    }

}