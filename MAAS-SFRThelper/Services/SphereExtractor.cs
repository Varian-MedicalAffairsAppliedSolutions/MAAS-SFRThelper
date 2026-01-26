using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace MAAS_SFRThelper.Services
{
    /// <summary>
    /// Represents an extracted sphere with center position and radius
    /// </summary>
    public class ExtractedSphere
    {
        public double CenterX { get; set; }  // mm (DICOM coordinates)
        public double CenterY { get; set; }  // mm
        public double CenterZ { get; set; }  // mm
        public double Radius { get; set; }   // mm

        public override string ToString()
        {
            return $"({CenterX:F1}, {CenterY:F1}, {CenterZ:F1}) r={Radius:F1}mm";
        }
    }

    /// <summary>
    /// Results from sphere extraction analysis
    /// </summary>
    public class SphereExtractionResult
    {
        public List<ExtractedSphere> Spheres { get; set; } = new List<ExtractedSphere>();
        public int SphereCount { get; set; }
        public double MeanRadius { get; set; }
        public double TotalVolume { get; set; }
        public string Message { get; set; }
        public bool Success { get; set; }
    }

    /// <summary>
    /// Service to extract sphere positions and sizes from lattice structure
    /// </summary>
    public class SphereExtractor
    {
        /// <summary>
        /// Extract spheres from a lattice structure
        /// </summary>
        public SphereExtractionResult ExtractSpheres(Structure latticeStructure, Image image, StringBuilder log = null)
        {
            var result = new SphereExtractionResult();

            try
            {
                if (latticeStructure == null || latticeStructure.IsEmpty)
                {
                    result.Message = "Structure is null or empty";
                    result.Success = false;
                    return result;
                }

                if (image == null)
                {
                    result.Message = "Image is null";
                    result.Success = false;
                    return result;
                }

                // Step 1: Get sphere count from ESAPI
                int sphereCount = latticeStructure.GetNumberOfSeparateParts();
                result.SphereCount = sphereCount;
                log?.AppendLine($"  GetNumberOfSeparateParts() = {sphereCount}");

                if (sphereCount == 0)
                {
                    result.Message = "No separate parts found in structure";
                    result.Success = false;
                    return result;
                }

                // Step 2: Calculate radius from total volume
                double totalVolume_cc = latticeStructure.Volume;
                result.TotalVolume = totalVolume_cc;
                double avgVolume_cc = totalVolume_cc / sphereCount;
                double avgVolume_mm3 = avgVolume_cc * 1000.0;  // cc to mm³

                // V = (4/3)πr³  →  r = ∛(3V/4π)
                double radius = Math.Pow((3.0 * avgVolume_mm3) / (4.0 * Math.PI), 1.0 / 3.0);
                result.MeanRadius = radius;
                log?.AppendLine($"  Total volume = {totalVolume_cc:F2} cc");
                log?.AppendLine($"  Avg sphere volume = {avgVolume_cc:F3} cc");
                log?.AppendLine($"  Calculated radius = {radius:F1} mm");

                // Step 3: Extract sphere centers from contours
                var sphereCenters = ExtractSphereCentersFromContours(latticeStructure, image, radius, log);

                if (sphereCenters.Count == 0)
                {
                    result.Message = $"Found {sphereCount} parts but could not extract centers from contours";
                    result.Success = false;
                    return result;
                }

                // Create sphere objects
                foreach (var center in sphereCenters)
                {
                    result.Spheres.Add(new ExtractedSphere
                    {
                        CenterX = center.x,
                        CenterY = center.y,
                        CenterZ = center.z,
                        Radius = radius
                    });
                }

                result.Success = true;
                result.Message = $"Extracted {result.Spheres.Count} spheres (expected {sphereCount}), radius ≈ {radius:F1} mm";
            }
            catch (Exception ex)
            {
                result.Message = $"Error: {ex.Message}";
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// Calculate polygon area using shoelace formula
        /// </summary>
        private double CalculateContourArea(VVector[] contour)
        {
            if (contour == null || contour.Length < 3)
                return 0;

            double signedArea = 0;
            for (int i = 0; i < contour.Length; i++)
            {
                var p1 = contour[i];
                var p2 = contour[(i + 1) % contour.Length];
                signedArea += (p1.x * p2.y - p2.x * p1.y);
            }
            return Math.Abs(signedArea * 0.5);
        }

        /// <summary>
        /// Calculate centroid of polygon using shoelace-derived formula
        /// </summary>
        private (double x, double y) CalculateContourCentroid(VVector[] contour)
        {
            if (contour == null || contour.Length < 3)
                return (0, 0);

            double signedArea = 0;
            double sumX = 0, sumY = 0;

            for (int i = 0; i < contour.Length; i++)
            {
                var p1 = contour[i];
                var p2 = contour[(i + 1) % contour.Length];

                double cross = p1.x * p2.y - p2.x * p1.y;
                signedArea += cross;
                sumX += (p1.x + p2.x) * cross;
                sumY += (p1.y + p2.y) * cross;
            }

            signedArea *= 0.5;

            if (Math.Abs(signedArea) < 1e-10)
            {
                // Fallback for degenerate cases
                return (contour.Average(p => p.x), contour.Average(p => p.y));
            }

            return (sumX / (6.0 * signedArea), sumY / (6.0 * signedArea));
        }

        /// <summary>
        /// Extract sphere centers by analyzing contours slice by slice
        /// Uses area-weighted centroid for sub-slice accuracy
        /// </summary>
        private List<VVector> ExtractSphereCentersFromContours(Structure structure, Image image, double expectedRadius, StringBuilder log)
        {
            var sphereCenters = new List<VVector>();

            try
            {
                if (image == null)
                {
                    log?.AppendLine("  ERROR: Image is null");
                    return sphereCenters;
                }

                // Get structure bounds
                var bounds = structure.MeshGeometry.Bounds;
                double zStart = bounds.Z;
                double zEnd = bounds.Z + bounds.SizeZ;
                double zRes = image.ZRes;

                log?.AppendLine($"  Structure Z range: {zStart:F1} to {zEnd:F1} mm");
                log?.AppendLine($"  Slice spacing: {zRes:F1} mm");

                // Collect all contour data: center, Z position, and AREA
                var allContourData = new List<(double x, double y, double z, double area)>();

                // Iterate through slices
                for (double z = zStart; z <= zEnd; z += zRes)
                {
                    int sliceIndex = (int)Math.Round((z - image.Origin.z) / zRes);
                    if (sliceIndex < 0 || sliceIndex >= image.ZSize) continue;

                    var contours = structure.GetContoursOnImagePlane(sliceIndex);
                    if (contours == null || contours.Length == 0) continue;

                    foreach (var contour in contours)
                    {
                        if (contour == null || contour.Length < 4) continue;

                        // Calculate centroid and area using proper formulas
                        var (centerX, centerY) = CalculateContourCentroid(contour);
                        double area = CalculateContourArea(contour);

                        // Use actual slice Z position
                        double actualSliceZ = image.Origin.z + sliceIndex * zRes;
                        allContourData.Add((centerX, centerY, actualSliceZ, area));
                    }
                }

                log?.AppendLine($"  Found {allContourData.Count} contours across all slices");

                if (allContourData.Count == 0)
                {
                    return sphereCenters;
                }

                // Cluster contours into spheres based on XY proximity AND Z continuity
                var assigned = new bool[allContourData.Count];
                double xyTolerance = expectedRadius * 0.5;
                double maxZGap = expectedRadius * 2.5;

                // Sort by X, then Y, then Z for consistent processing
                var sortedIndices = Enumerable.Range(0, allContourData.Count)
                    .OrderBy(i => allContourData[i].x)
                    .ThenBy(i => allContourData[i].y)
                    .ThenBy(i => allContourData[i].z)
                    .ToList();

                for (int idx = 0; idx < sortedIndices.Count; idx++)
                {
                    int i = sortedIndices[idx];
                    if (assigned[i]) continue;

                    // Start a new cluster
                    var cluster = new List<(double x, double y, double z, double area)>();
                    cluster.Add(allContourData[i]);
                    assigned[i] = true;

                    // Find all contours that belong to this sphere
                    for (int jdx = idx + 1; jdx < sortedIndices.Count; jdx++)
                    {
                        int j = sortedIndices[jdx];
                        if (assigned[j]) continue;

                        double dx = allContourData[j].x - allContourData[i].x;
                        double dy = allContourData[j].y - allContourData[i].y;
                        double xyDist = Math.Sqrt(dx * dx + dy * dy);

                        if (xyDist <= xyTolerance)
                        {
                            double newZ = allContourData[j].z;
                            double clusterMinZ = cluster.Min(c => c.z);
                            double clusterMaxZ = cluster.Max(c => c.z);

                            bool zContinuous = (newZ >= clusterMinZ - maxZGap && newZ <= clusterMaxZ + maxZGap);
                            double potentialSpan = Math.Max(clusterMaxZ, newZ) - Math.Min(clusterMinZ, newZ);
                            bool spanOk = potentialSpan <= expectedRadius * 2.5;

                            if (zContinuous && spanOk)
                            {
                                cluster.Add(allContourData[j]);
                                assigned[j] = true;
                            }
                        }
                    }

                    // Calculate sphere center using AREA-WEIGHTED CENTROID
                    if (cluster.Count >= 2)
                    {
                        double sumArea = 0;
                        double sumAreaX = 0;
                        double sumAreaY = 0;
                        double sumAreaZ = 0;

                        foreach (var c in cluster)
                        {
                            sumArea += c.area;
                            sumAreaX += c.area * c.x;
                            sumAreaY += c.area * c.y;
                            sumAreaZ += c.area * c.z;
                        }

                        // Area-weighted centroid gives sub-slice accuracy
                        double centerX = sumAreaX / sumArea;
                        double centerY = sumAreaY / sumArea;
                        double centerZ = sumAreaZ / sumArea;

                        sphereCenters.Add(new VVector(centerX, centerY, centerZ));
                    }
                }

                log?.AppendLine($"  Clustered into {sphereCenters.Count} sphere centers");

                for (int i = 0; i < sphereCenters.Count; i++)
                {
                    var c = sphereCenters[i];
                    log?.AppendLine($"    Sphere {i + 1}: ({c.x:F1}, {c.y:F1}, {c.z:F1})");
                }
            }
            catch (Exception ex)
            {
                log?.AppendLine($"  ERROR extracting centers: {ex.Message}");
            }

            return sphereCenters;
        }
    }
}