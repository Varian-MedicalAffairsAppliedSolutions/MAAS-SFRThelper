//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using VMS.TPS.Common.Model.API;
//using VMS.TPS.Common.Model.Types;

//namespace MAAS_SFRThelper.Services
//{
//    /// <summary>
//    /// Represents an extracted sphere with center position and radius
//    /// </summary>
//    public class ExtractedSphere
//    {
//        public double CenterX { get; set; }  // mm (DICOM coordinates)
//        public double CenterY { get; set; }  // mm
//        public double CenterZ { get; set; }  // mm
//        public double Radius { get; set; }   // mm

//        public override string ToString()
//        {
//            return $"({CenterX:F1}, {CenterY:F1}, {CenterZ:F1}) r={Radius:F1}mm";
//        }
//    }

//    /// <summary>
//    /// Results from sphere extraction analysis
//    /// </summary>
//    public class SphereExtractionResult
//    {
//        public List<ExtractedSphere> Spheres { get; set; } = new List<ExtractedSphere>();
//        public int SphereCount { get; set; }
//        public double MeanRadius { get; set; }
//        public double TotalVolume { get; set; }
//        public string Message { get; set; }
//        public bool Success { get; set; }
//    }

//    /// <summary>
//    /// Service to extract sphere positions and sizes from lattice structure
//    /// </summary>
//    public class SphereExtractor
//    {
//        /// <summary>
//        /// Extract spheres from a lattice structure
//        /// </summary>
//        /// <param name="latticeStructure">The lattice structure containing spheres</param>
//        /// <param name="image">The CT image (needed for slice information)</param>
//        /// <param name="log">Optional StringBuilder for detailed logging</param>
//        public SphereExtractionResult ExtractSpheres(Structure latticeStructure, Image image, StringBuilder log = null)
//        {
//            var result = new SphereExtractionResult();

//            try
//            {
//                if (latticeStructure == null || latticeStructure.IsEmpty)
//                {
//                    result.Message = "Structure is null or empty";
//                    result.Success = false;
//                    return result;
//                }

//                if (image == null)
//                {
//                    result.Message = "Image is null";
//                    result.Success = false;
//                    return result;
//                }

//                // Step 1: Get sphere count from ESAPI
//                int sphereCount = latticeStructure.GetNumberOfSeparateParts();
//                result.SphereCount = sphereCount;
//                log?.AppendLine($"  GetNumberOfSeparateParts() = {sphereCount}");

//                if (sphereCount == 0)
//                {
//                    result.Message = "No separate parts found in structure";
//                    result.Success = false;
//                    return result;
//                }

//                // Step 2: Calculate radius from total volume
//                double totalVolume_cc = latticeStructure.Volume;
//                result.TotalVolume = totalVolume_cc;
//                double avgVolume_cc = totalVolume_cc / sphereCount;
//                double avgVolume_mm3 = avgVolume_cc * 1000.0;  // cc to mm³

//                // V = (4/3)πr³  →  r = ∛(3V/4π)
//                double radius = Math.Pow((3.0 * avgVolume_mm3) / (4.0 * Math.PI), 1.0 / 3.0);
//                result.MeanRadius = radius;
//                log?.AppendLine($"  Total volume = {totalVolume_cc:F2} cc");
//                log?.AppendLine($"  Avg sphere volume = {avgVolume_cc:F3} cc");
//                log?.AppendLine($"  Calculated radius = {radius:F1} mm");

//                // Step 3: Extract sphere centers from contours
//                var sphereCenters = ExtractSphereCentersFromContours(latticeStructure, image, radius, log);

//                if (sphereCenters.Count == 0)
//                {
//                    result.Message = $"Found {sphereCount} parts but could not extract centers from contours";
//                    result.Success = false;
//                    return result;
//                }

//                // Create sphere objects
//                foreach (var center in sphereCenters)
//                {
//                    result.Spheres.Add(new ExtractedSphere
//                    {
//                        CenterX = center.x,
//                        CenterY = center.y,
//                        CenterZ = center.z,
//                        Radius = radius
//                    });
//                }

//                result.Success = true;
//                result.Message = $"Extracted {result.Spheres.Count} spheres (expected {sphereCount}), radius ≈ {radius:F1} mm";
//            }
//            catch (Exception ex)
//            {
//                result.Message = $"Error: {ex.Message}";
//                result.Success = false;
//            }

//            return result;
//        }

//        /// <summary>
//        /// Extract sphere centers by analyzing contours slice by slice
//        /// </summary>
//        private List<VVector> ExtractSphereCentersFromContours(Structure structure, Image image, double expectedRadius, StringBuilder log)
//        {
//            var sphereCenters = new List<VVector>();

//            try
//            {
//                if (image == null)
//                {
//                    log?.AppendLine("  ERROR: Image is null");
//                    return sphereCenters;
//                }

//                // Get structure bounds
//                var bounds = structure.MeshGeometry.Bounds;
//                double zStart = bounds.Z;
//                double zEnd = bounds.Z + bounds.SizeZ;
//                double zRes = image.ZRes;

//                log?.AppendLine($"  Structure Z range: {zStart:F1} to {zEnd:F1} mm");
//                log?.AppendLine($"  Slice spacing: {zRes:F1} mm");

//                // Collect all contour centers with their Z positions
//                var allCircleCenters = new List<(double x, double y, double z, double radius)>();

//                // Iterate through slices
//                for (double z = zStart; z <= zEnd; z += zRes)
//                {
//                    int sliceIndex = (int)Math.Round((z - image.Origin.z) / zRes);
//                    if (sliceIndex < 0 || sliceIndex >= image.ZSize) continue;

//                    var contours = structure.GetContoursOnImagePlane(sliceIndex);
//                    if (contours == null || contours.Length == 0) continue;

//                    foreach (var contour in contours)
//                    {
//                        if (contour == null || contour.Length < 4) continue;

//                        // Calculate centroid of this contour
//                        double sumX = 0, sumY = 0;
//                        foreach (var pt in contour)
//                        {
//                            sumX += pt.x;
//                            sumY += pt.y;
//                        }
//                        double centerX = sumX / contour.Length;
//                        double centerY = sumY / contour.Length;

//                        // Estimate radius of this contour
//                        double avgRadius = contour.Average(pt =>
//                            Math.Sqrt(Math.Pow(pt.x - centerX, 2) + Math.Pow(pt.y - centerY, 2)));

//                        allCircleCenters.Add((centerX, centerY, z, avgRadius));
//                    }
//                }

//                log?.AppendLine($"  Found {allCircleCenters.Count} contours across all slices");

//                if (allCircleCenters.Count == 0)
//                {
//                    return sphereCenters;
//                }

//                // Cluster circles into spheres based on XY proximity AND Z continuity
//                var assigned = new bool[allCircleCenters.Count];
//                double xyTolerance = expectedRadius * 0.5;  // Tolerance for XY clustering
//                double maxZGap = expectedRadius * 2.5;  // Max gap in Z before it's a new sphere

//                // Sort by X, then Y, then Z for consistent processing
//                var sortedIndices = Enumerable.Range(0, allCircleCenters.Count)
//                    .OrderBy(i => allCircleCenters[i].x)
//                    .ThenBy(i => allCircleCenters[i].y)
//                    .ThenBy(i => allCircleCenters[i].z)
//                    .ToList();

//                for (int idx = 0; idx < sortedIndices.Count; idx++)
//                {
//                    int i = sortedIndices[idx];
//                    if (assigned[i]) continue;

//                    // Start a new cluster with this circle
//                    var cluster = new List<(double x, double y, double z, double radius)>();
//                    cluster.Add(allCircleCenters[i]);
//                    assigned[i] = true;

//                    // Find all circles that belong to this sphere
//                    // Must have similar XY AND continuous Z (no large gaps)
//                    for (int jdx = idx + 1; jdx < sortedIndices.Count; jdx++)
//                    {
//                        int j = sortedIndices[jdx];
//                        if (assigned[j]) continue;

//                        double dx = allCircleCenters[j].x - allCircleCenters[i].x;
//                        double dy = allCircleCenters[j].y - allCircleCenters[i].y;
//                        double xyDist = Math.Sqrt(dx * dx + dy * dy);

//                        if (xyDist <= xyTolerance)
//                        {
//                            // Check if Z is continuous with existing cluster
//                            double newZ = allCircleCenters[j].z;
//                            double clusterMinZ = cluster.Min(c => c.z);
//                            double clusterMaxZ = cluster.Max(c => c.z);

//                            // Check gap from either end of cluster
//                            bool zContinuous = (newZ >= clusterMinZ - maxZGap && newZ <= clusterMaxZ + maxZGap);

//                            // Also check that cluster Z span doesn't exceed expected sphere diameter
//                            double potentialSpan = Math.Max(clusterMaxZ, newZ) - Math.Min(clusterMinZ, newZ);
//                            bool spanOk = potentialSpan <= expectedRadius * 2.5;

//                            if (zContinuous && spanOk)
//                            {
//                                cluster.Add(allCircleCenters[j]);
//                                assigned[j] = true;
//                            }
//                        }
//                    }

//                    // Calculate sphere center from cluster
//                    if (cluster.Count >= 2)  // Need at least 2 slices
//                    {
//                        // Find the slice with largest radius (equator)
//                        var equator = cluster.OrderByDescending(c => c.radius).First();

//                        // Sphere center XY = equator center
//                        // Sphere center Z = midpoint of Z range
//                        double minZ = cluster.Min(c => c.z);
//                        double maxZ = cluster.Max(c => c.z);
//                        double centerZ = (minZ + maxZ) / 2.0;

//                        sphereCenters.Add(new VVector(equator.x, equator.y, centerZ));
//                    }
//                }

//                log?.AppendLine($"  Clustered into {sphereCenters.Count} sphere centers");

//                // Log all sphere centers
//                for (int i = 0; i < sphereCenters.Count; i++)
//                {
//                    var c = sphereCenters[i];
//                    log?.AppendLine($"    Sphere {i + 1}: ({c.x:F1}, {c.y:F1}, {c.z:F1})");
//                }
//            }
//            catch (Exception ex)
//            {
//                log?.AppendLine($"  ERROR extracting centers: {ex.Message}");
//            }

//            return sphereCenters;
//        }
//    }
//}

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
        /// <param name="latticeStructure">The lattice structure containing spheres</param>
        /// <param name="image">The CT image (needed for slice information)</param>
        /// <param name="log">Optional StringBuilder for detailed logging</param>
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
        /// Extract sphere centers by analyzing contours slice by slice
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

                // Collect all contour centers with their Z positions
                var allCircleCenters = new List<(double x, double y, double z, double radius)>();

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

                        // Calculate centroid of this contour
                        double sumX = 0, sumY = 0;
                        foreach (var pt in contour)
                        {
                            sumX += pt.x;
                            sumY += pt.y;
                        }
                        double centerX = sumX / contour.Length;
                        double centerY = sumY / contour.Length;

                        // Estimate radius of this contour
                        double avgRadius = contour.Average(pt =>
                            Math.Sqrt(Math.Pow(pt.x - centerX, 2) + Math.Pow(pt.y - centerY, 2)));

                        // Use actual slice Z position (not loop variable)
                        double actualSliceZ = image.Origin.z + sliceIndex * zRes;
                        allCircleCenters.Add((centerX, centerY, actualSliceZ, avgRadius));
                    }
                }

                log?.AppendLine($"  Found {allCircleCenters.Count} contours across all slices");

                if (allCircleCenters.Count == 0)
                {
                    return sphereCenters;
                }

                // Cluster circles into spheres based on XY proximity AND Z continuity
                var assigned = new bool[allCircleCenters.Count];
                double xyTolerance = expectedRadius * 0.5;  // Tolerance for XY clustering
                double maxZGap = expectedRadius * 2.5;  // Max gap in Z before it's a new sphere

                // Sort by X, then Y, then Z for consistent processing
                var sortedIndices = Enumerable.Range(0, allCircleCenters.Count)
                    .OrderBy(i => allCircleCenters[i].x)
                    .ThenBy(i => allCircleCenters[i].y)
                    .ThenBy(i => allCircleCenters[i].z)
                    .ToList();

                for (int idx = 0; idx < sortedIndices.Count; idx++)
                {
                    int i = sortedIndices[idx];
                    if (assigned[i]) continue;

                    // Start a new cluster with this circle
                    var cluster = new List<(double x, double y, double z, double radius)>();
                    cluster.Add(allCircleCenters[i]);
                    assigned[i] = true;

                    // Find all circles that belong to this sphere
                    // Must have similar XY AND continuous Z (no large gaps)
                    for (int jdx = idx + 1; jdx < sortedIndices.Count; jdx++)
                    {
                        int j = sortedIndices[jdx];
                        if (assigned[j]) continue;

                        double dx = allCircleCenters[j].x - allCircleCenters[i].x;
                        double dy = allCircleCenters[j].y - allCircleCenters[i].y;
                        double xyDist = Math.Sqrt(dx * dx + dy * dy);

                        if (xyDist <= xyTolerance)
                        {
                            // Check if Z is continuous with existing cluster
                            double newZ = allCircleCenters[j].z;
                            double clusterMinZ = cluster.Min(c => c.z);
                            double clusterMaxZ = cluster.Max(c => c.z);

                            // Check gap from either end of cluster
                            bool zContinuous = (newZ >= clusterMinZ - maxZGap && newZ <= clusterMaxZ + maxZGap);

                            // Also check that cluster Z span doesn't exceed expected sphere diameter
                            double potentialSpan = Math.Max(clusterMaxZ, newZ) - Math.Min(clusterMinZ, newZ);
                            bool spanOk = potentialSpan <= expectedRadius * 2.5;

                            if (zContinuous && spanOk)
                            {
                                cluster.Add(allCircleCenters[j]);
                                assigned[j] = true;
                            }
                        }
                    }

                    // Calculate sphere center from cluster
                    if (cluster.Count >= 2)  // Need at least 2 slices
                    {
                        // Find the slice with largest radius (equator)
                        var equator = cluster.OrderByDescending(c => c.radius).First();

                        // Sphere center = equator position (largest slice is closest to true center)
                        sphereCenters.Add(new VVector(equator.x, equator.y, equator.z));
                    }
                }

                log?.AppendLine($"  Clustered into {sphereCenters.Count} sphere centers");

                // Log all sphere centers
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