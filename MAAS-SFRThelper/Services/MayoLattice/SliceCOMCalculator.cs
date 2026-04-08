using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.Types;

namespace MAAS_SFRThelper.Services.MayoLattice
{
    /// <summary>
    /// Computes axial slice centers of mass (COMs) from the feasible position
    /// array using HDBSCAN clustering.
    /// 
    /// The feasible positions (PPossible) are grouped by z-coordinate into
    /// axial slices. On each slice, HDBSCAN identifies density-connected
    /// clusters in the (x, y) plane. The centroid of each cluster becomes
    /// a COM. This handles multi-lobed GTVs and irregular feasible volumes
    /// where a simple per-slice centroid could land outside the feasible region.
    /// 
    /// Per Deufel et al. 2024, Phys. Med. Biol. 69, 075010.
    /// </summary>
    public class SliceCOMCalculator
    {
        private readonly int _minClusterSize;

        /// <summary>
        /// Create a SliceCOMCalculator.
        /// </summary>
        /// <param name="minClusterSize">
        /// HDBSCAN minimum cluster size. Points in clusters smaller than
        /// this are treated as noise and excluded from COM computation.
        /// Paper default: 5.
        /// </param>
        public SliceCOMCalculator(int minClusterSize = 5)
        {
            _minClusterSize = Math.Max(2, minClusterSize);
        }

        /// <summary>
        /// Compute axial slice COMs from the feasible position array.
        /// </summary>
        /// <param name="feasiblePoints">
        /// Array of feasible lattice positions (PPossible). All points
        /// automatically satisfy GTV and OAR margin constraints.
        /// Coordinates in mm (ESAPI native).
        /// </param>
        /// <param name="zTolerance">
        /// Points within this distance (mm) in z are grouped into the
        /// same axial slice. Should match the rasterization voxel size.
        /// If null, automatically determined from the data.
        /// </param>
        /// <returns>
        /// List of COM positions. Each COM is the centroid of an HDBSCAN
        /// cluster on a single axial slice, with z-coordinate set to the
        /// slice z-value. Multiple COMs may exist per slice (for multi-lobed
        /// GTVs or disconnected feasible regions).
        /// </returns>
        public List<VVector> ComputeCOMs(List<VVector> feasiblePoints, double? zTolerance = null)
        {
            var coms = new List<VVector>();

            if (feasiblePoints == null || feasiblePoints.Count == 0)
                return coms;

            // Determine z-tolerance from data if not specified
            double zTol = zTolerance ?? EstimateZTolerance(feasiblePoints);

            // Group points into axial slices by z-coordinate
            var slices = GroupByZSlice(feasiblePoints, zTol);

            // Run HDBSCAN on each slice and compute cluster centroids
            var hdbscan = new HdbscanClustering(_minClusterSize);

            foreach (var slice in slices)
            {
                double sliceZ = slice.Key;
                var slicePoints = slice.Value;

                // Need at least minClusterSize points to form a cluster
                if (slicePoints.Count < _minClusterSize)
                    continue;

                // Extract 2D (x, y) coordinates for clustering
                var points2D = slicePoints
                    .Select(p => new double[] { p.x, p.y })
                    .ToList();

                // Run HDBSCAN
                var result = hdbscan.Run(points2D);

                // Compute centroid of each cluster
                if (result.NumClusters == 0)
                {
                    // No clusters found — fall back to simple centroid of all points
                    double meanX = slicePoints.Average(p => p.x);
                    double meanY = slicePoints.Average(p => p.y);
                    coms.Add(new VVector(meanX, meanY, sliceZ));
                }
                else
                {
                    for (int c = 0; c < result.NumClusters; c++)
                    {
                        // Gather points belonging to cluster c
                        var clusterPoints = new List<VVector>();
                        for (int i = 0; i < result.Labels.Length; i++)
                        {
                            if (result.Labels[i] == c)
                            {
                                clusterPoints.Add(slicePoints[i]);
                            }
                        }

                        if (clusterPoints.Count == 0) continue;

                        double centroidX = clusterPoints.Average(p => p.x);
                        double centroidY = clusterPoints.Average(p => p.y);

                        coms.Add(new VVector(centroidX, centroidY, sliceZ));
                    }
                }
            }

            return coms;
        }

        /// <summary>
        /// Group feasible points into axial slices based on z-coordinate proximity.
        /// Points within zTolerance of each other in z are assigned to the same slice.
        /// The slice key is the mean z-value of all points in the group.
        /// </summary>
        private Dictionary<double, List<VVector>> GroupByZSlice(
            List<VVector> points, double zTolerance)
        {
            // Sort by z
            var sorted = points.OrderBy(p => p.z).ToList();

            var slices = new Dictionary<double, List<VVector>>();
            var currentSlice = new List<VVector> { sorted[0] };
            double currentZ = sorted[0].z;

            for (int i = 1; i < sorted.Count; i++)
            {
                if (Math.Abs(sorted[i].z - currentZ) <= zTolerance)
                {
                    // Same slice
                    currentSlice.Add(sorted[i]);
                }
                else
                {
                    // New slice — store current and start new
                    double sliceZ = currentSlice.Average(p => p.z);
                    slices[sliceZ] = currentSlice;

                    currentSlice = new List<VVector> { sorted[i] };
                    currentZ = sorted[i].z;
                }
            }

            // Don't forget the last slice
            if (currentSlice.Count > 0)
            {
                double sliceZ = currentSlice.Average(p => p.z);
                slices[sliceZ] = currentSlice;
            }

            return slices;
        }

        /// <summary>
        /// Estimate z-tolerance from the data by finding the smallest
        /// non-zero z-gap between adjacent points (after sorting by z).
        /// Uses half of this minimum gap as the tolerance, which groups
        /// points at the same rasterization z-level together.
        /// </summary>
        private double EstimateZTolerance(List<VVector> points)
        {
            var uniqueZ = points.Select(p => p.z).Distinct().OrderBy(z => z).ToList();

            if (uniqueZ.Count <= 1)
                return 1.0; // default 1mm if only one z-level

            double minGap = double.MaxValue;
            for (int i = 1; i < uniqueZ.Count; i++)
            {
                double gap = uniqueZ[i] - uniqueZ[i - 1];
                if (gap > 1e-6 && gap < minGap)
                    minGap = gap;
            }

            // Use half the minimum gap as tolerance
            return (minGap < double.MaxValue) ? minGap * 0.5 : 1.0;
        }
    }
}