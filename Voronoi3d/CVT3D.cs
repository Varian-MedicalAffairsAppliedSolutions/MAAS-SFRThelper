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
            _mesh = mesh;
            _settings = settings;
            StoppingCriterion = _settings?.StoppingCriterion;
        }


        //public IReadOnlyCollection<Point3D> CalculateGenerators()
        //{
        //    var numberOfPoints = _settings.NumberOfGenerators;
        //    var maxIterations = _settings.MaxNumberOfIterations;
        //    bool calcVoids = _settings.VoidCalc;
        //    RandomEngine initialGeneratorEngine = calcVoids ? RandomEngine.HALTONSEQUENCE : _settings.SelectedSamplingMethod;
        //    List<CVT3D.Point3D> points = GenerateRandomPoints(numberOfPoints, _mesh, _settings.SelectedSamplingMethod);
        //    List<CVT3D.Point3D> generators = LloydRelaxation(points, maxIterations, _mesh, calcVoids);
        //    return generators;
        //}

        // Change this in CVT3D.cs
        public IReadOnlyCollection<Point3D> CalculateGenerators()
        {
            var numberOfPoints = _settings.NumberOfGenerators;
            var maxIterations = _settings.MaxNumberOfIterations;
            bool calcVoids = _settings.VoidCalc;

            // Use the determined engine consistently
            RandomEngine initialGeneratorEngine = calcVoids ? RandomEngine.HALTONSEQUENCE : _settings.SelectedSamplingMethod;

            // Use the initialGeneratorEngine here
            List<CVT3D.Point3D> points = GenerateRandomPoints(numberOfPoints, _mesh, initialGeneratorEngine);

            List<CVT3D.Point3D> generators = LloydRelaxation(points, maxIterations, _mesh, calcVoids);
            return generators;
        }

        //protected List<Point3D> GenerateRandomPoints(int count, MeshGeometry3D mesh3d, RandomEngine currentEngine)
        //{
        //    if (currentEngine == RandomEngine.HEXGRID)
        //    {
        //        RandomEngineFactory.initializeHex(_settings.SphereLocations);
        //    }
        //    int toTake = currentEngine == RandomEngine.HEXGRID ? count : count;
        //    IRandom3D rand = RandomEngineFactory.Create(currentEngine);
        //    var points = rand
        //        .GetRandomNumbers(mesh3d)
        //        .Take(toTake)
        //        .Select(p => new Point3D(p.X, p.Y, p.Z));
        //        //if (currentEngine != RandomEngine.HEXGRID)
        //        //{   
        //        //    points = points.Take(count);
        //        //}
        //    return points.ToList();
        //}

        // Change this in CVT3D.cs
        protected List<Point3D> GenerateRandomPoints(int count, MeshGeometry3D mesh3d, RandomEngine currentEngine)
        {
            // Check if SphereLocations is null when using HEXGRID
            if (currentEngine == RandomEngine.HEXGRID)
            {
                if (_settings.SphereLocations == null || !_settings.SphereLocations.Any())
                {
                    // Fall back to HALTONSEQUENCE if no sphere locations are available
                    currentEngine = RandomEngine.HALTONSEQUENCE;
                }
                else
                {
                    RandomEngineFactory.initializeHex(_settings.SphereLocations);
                }
            }

            int toTake = count;
            IRandom3D rand = RandomEngineFactory.Create(currentEngine);

            var points = rand
                .GetRandomNumbers(mesh3d)
                .Take(toTake)
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

        protected List<Point3D> LloydRelaxation(List<Point3D> points, int iterations, MeshGeometry3D mesh3d, bool calcVoids)
        {
            List<Point3D> previousPoints = new List<Point3D>();
            List<Point3D> randomPoints;

            // Generate random points inside the mesh
            if (calcVoids) { randomPoints = GenerateRandomPoints(_settings.NumberOfGenerators, mesh3d, RandomEngine.HEXGRID); }
            else { randomPoints = GenerateRandomPoints(_settings.NumberOfSamplingPoints, mesh3d, RandomEngine.HALTONSEQUENCE); }

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

        // Change this in CVT3D.cs
        //protected List<Point3D> LloydRelaxation(List<Point3D> points, int iterations, MeshGeometry3D mesh3d, bool calcVoids)
        //{
        //    List<Point3D> previousPoints = new List<Point3D>();
        //    List<Point3D> randomPoints;

        //    // Generate random points inside the mesh - be consistent with engine choice
        //    if (calcVoids)
        //    {
        //        randomPoints = GenerateRandomPoints(_settings.NumberOfSamplingPoints, mesh3d, RandomEngine.HALTONSEQUENCE);
        //    }
        //    else
        //    {
        //        // Use the SphereLocations if HEXGRID is selected, otherwise use HALTONSEQUENCE
        //        randomPoints = _settings.SelectedSamplingMethod == RandomEngine.HEXGRID &&
        //                      _settings.SphereLocations != null &&
        //                      _settings.SphereLocations.Any()
        //            ? _settings.SphereLocations.ToList()
        //            : GenerateRandomPoints(_settings.NumberOfSamplingPoints, mesh3d, RandomEngine.HALTONSEQUENCE);
        //    }

        //    // Ensure we have random points
        //    if (randomPoints == null || !randomPoints.Any())
        //    {
        //        randomPoints = GenerateRandomPoints(3000, mesh3d, RandomEngine.HALTONSEQUENCE);
        //    }

        //    // Continue with the rest of the method...
        //    for (int iter = 0; iter < iterations; iter++)
        //    {
        //        // Create empty lists for Voronoi regions
        //        List<List<Point3D>> voronoiRegions = new List<List<Point3D>>();
        //        for (int i = 0; i < points.Count; i++)
        //            voronoiRegions.Add(new List<Point3D>());

        //        foreach (var p in randomPoints)
        //        {
        //            int closestIndex = FindClosestPointIndex(p, points);
        //            if (closestIndex >= 0 && closestIndex < voronoiRegions.Count)
        //                voronoiRegions[closestIndex].Add(p);
        //        }

        //        // Compute the centroid of each region and update points
        //        for (int i = 0; i < points.Count; i++)
        //        {
        //            if (voronoiRegions[i].Any()) // Only update if there are points in the region
        //                points[i] = ComputeCentroid(voronoiRegions[i]);
        //        }

        //        if (CanStop(previousPoints, points))
        //            return points;

        //        previousPoints = points.Select(p => new Point3D(p.X, p.Y, p.Z)).ToList();
        //    }

        //    return points;
        //}

        protected bool CanStop(List<Point3D> previousPoints, List<Point3D> points) =>
            StoppingCriterion.CanStop(previousPoints, points);
    }



}