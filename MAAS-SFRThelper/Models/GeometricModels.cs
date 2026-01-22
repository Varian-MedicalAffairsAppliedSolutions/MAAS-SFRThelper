//using System;
//using System.Collections.Generic;

//namespace MAAS_SFRThelper.Models
//{
//    /// <summary>
//    /// Represents target volume geometry for geometric analysis
//    /// </summary>
//    public class TargetInfo
//    {
//        public string Name { get; set; }
//        public double CenterX { get; set; }  // mm (DICOM coordinates)
//        public double CenterY { get; set; }  // mm
//        public double CenterZ { get; set; }  // mm
//        public double Radius { get; set; }   // mm (approximate as sphere)
//        public double Volume { get; set; }   // cc

//        /// <summary>
//        /// Create TargetInfo from ESAPI Structure
//        /// </summary>
//        public static TargetInfo FromStructure(VMS.TPS.Common.Model.API.Structure structure)
//        {
//            if (structure == null || structure.IsEmpty)
//                return null;

//            var bounds = structure.MeshGeometry.Bounds;
//            var center = structure.CenterPoint;

//            // Approximate radius from bounding box (average of X, Y, Z extents / 2)
//            double avgDiameter = (bounds.SizeX + bounds.SizeY + bounds.SizeZ) / 3.0;
//            double radius = avgDiameter / 2.0;

//            return new TargetInfo
//            {
//                Name = structure.Id,
//                CenterX = center.x,
//                CenterY = center.y,
//                CenterZ = center.z,
//                Radius = radius,
//                Volume = structure.Volume
//            };
//        }

//        public override string ToString()
//        {
//            return $"{Name}: center=({CenterX:F1}, {CenterY:F1}, {CenterZ:F1}), r={Radius:F1}mm";
//        }
//    }

//    /// <summary>
//    /// Represents OAR geometry for geometric analysis
//    /// </summary>
//    public class OARInfo
//    {
//        public string Name { get; set; }
//        public double CenterX { get; set; }  // mm
//        public double CenterY { get; set; }  // mm
//        public double CenterZ { get; set; }  // mm
//        public double RadiusXY { get; set; } // mm (lateral extent)
//        public double LengthZ { get; set; }  // mm (sup-inf extent)
//        public double Volume { get; set; }   // cc
//        public bool IsSelected { get; set; } // for UI binding

//        /// <summary>
//        /// Create OARInfo from ESAPI Structure
//        /// </summary>
//        public static OARInfo FromStructure(VMS.TPS.Common.Model.API.Structure structure)
//        {
//            if (structure == null || structure.IsEmpty)
//                return null;

//            var bounds = structure.MeshGeometry.Bounds;
//            var center = structure.CenterPoint;

//            // Approximate as cylinder: RadiusXY from X/Y extents, LengthZ from Z extent
//            double radiusXY = (bounds.SizeX + bounds.SizeY) / 4.0;  // average radius
//            double lengthZ = bounds.SizeZ;

//            return new OARInfo
//            {
//                Name = structure.Id,
//                CenterX = center.x,
//                CenterY = center.y,
//                CenterZ = center.z,
//                RadiusXY = radiusXY,
//                LengthZ = lengthZ,
//                Volume = structure.Volume,
//                IsSelected = false
//            };
//        }

//        public override string ToString()
//        {
//            return $"{Name}: center=({CenterX:F1}, {CenterY:F1}, {CenterZ:F1}), rXY={RadiusXY:F1}mm, lenZ={LengthZ:F1}mm";
//        }
//    }

//    /// <summary>
//    /// Results for a single OAR's OSI calculation
//    /// </summary>
//    public class OARResult
//    {
//        public string Name { get; set; }
//        public double OSI_Mean { get; set; }
//        public double OSI_Min { get; set; }
//        public double OSI_Max { get; set; }
//    }

//    /// <summary>
//    /// Per-angle metric data for visualization
//    /// </summary>
//    public class PerAngleData
//    {
//        public double GantryAngle { get; set; }
//        public double SII { get; set; }
//        public double VSI { get; set; }
//        public Dictionary<string, double> OSI_PerOAR { get; set; } = new Dictionary<string, double>();
//    }

//    /// <summary>
//    /// Complete results from geometric surrogate calculation
//    /// </summary>
//    public class GeometricResults
//    {
//        // Input summary
//        public int SphereCount { get; set; }
//        public double SphereRadius { get; set; }  // mm
//        public string TargetName { get; set; }
//        public List<string> OARNames { get; set; } = new List<string>();

//        // SII metrics (Sphere Independence Index)
//        public double SII_Mean { get; set; }
//        public double SII_Min { get; set; }
//        public double SII_Max { get; set; }
//        public double SII_StdDev { get; set; }

//        // VSI metrics (Valley Shielding Index)
//        public double VSI_Mean { get; set; }
//        public double VSI_Min { get; set; }
//        public double VSI_Max { get; set; }
//        public double VSI_StdDev { get; set; }

//        // SSI metric (Sphere Spread Index) - single value, not angle-dependent
//        public double SSI { get; set; }
//        public double SSI_VolumeComponent { get; set; }
//        public double SSI_AlignmentComponent { get; set; }

//        // OSI metrics (OAR Sparing Index) - per OAR
//        public List<OARResult> OAR_Results { get; set; } = new List<OARResult>();
//        public double OSI_Combined { get; set; }  // weighted average
//        public double OSI_Worst { get; set; }     // minimum across OARs
//        public string OSI_WorstOARName { get; set; }

//        // Combined score
//        public double CombinedScore { get; set; }
//        public double Weight_SII { get; set; }
//        public double Weight_VSI { get; set; }
//        public double Weight_SSI { get; set; }
//        public double Weight_OSI { get; set; }

//        // Per-angle data for visualization
//        public List<PerAngleData> PerAngleResults { get; set; } = new List<PerAngleData>();

//        // Computation info
//        public int GantryAngleCount { get; set; }
//        public double GantryAngleStep { get; set; }
//        public double ComputationTimeMs { get; set; }
//        public bool Success { get; set; }
//        public string Message { get; set; }

//        /// <summary>
//        /// Generate a summary string for display
//        /// </summary>
//        public string GetSummary()
//        {
//            var sb = new System.Text.StringBuilder();
//            sb.AppendLine($"=== Geometric Surrogate Results ===");
//            sb.AppendLine($"Spheres: {SphereCount} x r={SphereRadius:F1}mm");
//            sb.AppendLine($"Target: {TargetName}");
//            sb.AppendLine();
//            sb.AppendLine($"SII: {SII_Mean:F3}  (range: {SII_Min:F3} - {SII_Max:F3})");
//            sb.AppendLine($"VSI: {VSI_Mean:F3}  (range: {VSI_Min:F3} - {VSI_Max:F3})");
//            sb.AppendLine($"SSI: {SSI:F3}  (vol: {SSI_VolumeComponent:F3}, align: {SSI_AlignmentComponent:F3})");

//            if (OAR_Results.Count > 0)
//            {
//                sb.AppendLine();
//                sb.AppendLine($"OSI by OAR:");
//                foreach (var oar in OAR_Results)
//                {
//                    sb.AppendLine($"  {oar.Name}: {oar.OSI_Mean:F3}  (range: {oar.OSI_Min:F3} - {oar.OSI_Max:F3})");
//                }
//                sb.AppendLine($"OSI Combined: {OSI_Combined:F3}");
//                sb.AppendLine($"OSI Worst: {OSI_Worst:F3} ({OSI_WorstOARName})");
//            }

//            sb.AppendLine();
//            sb.AppendLine($"===============================");
//            sb.AppendLine($"Combined Score: {CombinedScore:F3}");
//            sb.AppendLine($"===============================");
//            sb.AppendLine();
//            sb.AppendLine($"[{GantryAngleCount} angles, {ComputationTimeMs:F1}ms]");

//            return sb.ToString();
//        }
//    }
//}

using System;
using System.Collections.Generic;
using System.Linq;

namespace MAAS_SFRThelper.Models
{
    /// <summary>
    /// Represents target volume geometry for geometric analysis
    /// </summary>
    public class TargetInfo
    {
        public string Name { get; set; }
        public double CenterX { get; set; }  // mm (DICOM coordinates)
        public double CenterY { get; set; }  // mm
        public double CenterZ { get; set; }  // mm
        public double Radius { get; set; }   // mm (approximate as sphere)
        public double Volume { get; set; }   // cc

        /// <summary>
        /// Create TargetInfo from ESAPI Structure
        /// </summary>
        public static TargetInfo FromStructure(VMS.TPS.Common.Model.API.Structure structure)
        {
            if (structure == null || structure.IsEmpty)
                return null;

            var bounds = structure.MeshGeometry.Bounds;
            var center = structure.CenterPoint;

            // Approximate radius from bounding box (average of X, Y, Z extents / 2)
            double avgDiameter = (bounds.SizeX + bounds.SizeY + bounds.SizeZ) / 3.0;
            double radius = avgDiameter / 2.0;

            return new TargetInfo
            {
                Name = structure.Id,
                CenterX = center.x,
                CenterY = center.y,
                CenterZ = center.z,
                Radius = radius,
                Volume = structure.Volume
            };
        }

        public override string ToString()
        {
            return $"{Name}: center=({CenterX:F1}, {CenterY:F1}, {CenterZ:F1}), r={Radius:F1}mm";
        }
    }

    /// <summary>
    /// Represents OAR geometry for geometric analysis
    /// </summary>
    public class OARInfo
    {
        public string Name { get; set; }
        public double CenterX { get; set; }  // mm
        public double CenterY { get; set; }  // mm
        public double CenterZ { get; set; }  // mm
        public double RadiusXY { get; set; } // mm (lateral extent)
        public double LengthZ { get; set; }  // mm (sup-inf extent)
        public double Volume { get; set; }   // cc
        public bool IsSelected { get; set; } // for UI binding

        /// <summary>
        /// Create OARInfo from ESAPI Structure
        /// </summary>
        public static OARInfo FromStructure(VMS.TPS.Common.Model.API.Structure structure)
        {
            if (structure == null || structure.IsEmpty)
                return null;

            var bounds = structure.MeshGeometry.Bounds;
            var center = structure.CenterPoint;

            // Approximate as cylinder: RadiusXY from X/Y extents, LengthZ from Z extent
            double radiusXY = (bounds.SizeX + bounds.SizeY) / 4.0;  // average radius
            double lengthZ = bounds.SizeZ;

            return new OARInfo
            {
                Name = structure.Id,
                CenterX = center.x,
                CenterY = center.y,
                CenterZ = center.z,
                RadiusXY = radiusXY,
                LengthZ = lengthZ,
                Volume = structure.Volume,
                IsSelected = false
            };
        }

        public override string ToString()
        {
            return $"{Name}: center=({CenterX:F1}, {CenterY:F1}, {CenterZ:F1}), rXY={RadiusXY:F1}mm, lenZ={LengthZ:F1}mm";
        }
    }

    /// <summary>
    /// Results for a single OAR's OSI calculation
    /// </summary>
    public class OARResult
    {
        public string Name { get; set; }
        public double OSI_Mean { get; set; }
        public double OSI_Min { get; set; }
        public double OSI_Max { get; set; }
    }

    /// <summary>
    /// Per-angle metric data for visualization
    /// </summary>
    public class PerAngleData
    {
        public double GantryAngle { get; set; }
        public double SII { get; set; }
        public double VSI { get; set; }
        public Dictionary<string, double> OSI_PerOAR { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// Complete results from geometric surrogate calculation
    /// </summary>
    public class GeometricResults
    {
        // Input summary
        public int SphereCount { get; set; }
        public double SphereRadius { get; set; }  // mm
        public string TargetName { get; set; }
        public List<string> OARNames { get; set; } = new List<string>();

        // SII metrics (Sphere Independence Index)
        public double SII_Mean { get; set; }
        public double SII_Min { get; set; }
        public double SII_Max { get; set; }
        public double SII_StdDev { get; set; }

        // VSI metrics (Valley Shielding Index)
        public double VSI_Mean { get; set; }
        public double VSI_Min { get; set; }
        public double VSI_Max { get; set; }
        public double VSI_StdDev { get; set; }

        // SSI metric (Sphere Spread Index) - single value, not angle-dependent
        public double SSI { get; set; }
        public double SSI_VolumeComponent { get; set; }
        public double SSI_AlignmentComponent { get; set; }

        // OSI metrics (OAR Sparing Index) - per OAR
        public List<OARResult> OAR_Results { get; set; } = new List<OARResult>();
        public double OSI_Combined { get; set; }  // weighted average
        public double OSI_Worst { get; set; }     // minimum across OARs
        public string OSI_WorstOARName { get; set; }

        // Combined score
        public double CombinedScore { get; set; }
        public double Weight_SII { get; set; }
        public double Weight_VSI { get; set; }
        public double Weight_SSI { get; set; }
        public double Weight_OSI { get; set; }

        // Per-angle data for visualization
        public List<PerAngleData> PerAngleResults { get; set; } = new List<PerAngleData>();

        // Computation info
        public int GantryAngleCount { get; set; }
        public double GantryAngleStep { get; set; }
        public double ComputationTimeMs { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }

        /// <summary>
        /// Generate a summary string for display
        /// </summary>
        public string GetSummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Geometric Surrogate Results ===");
            sb.AppendLine($"Spheres: {SphereCount} x r={SphereRadius:F1}mm");
            sb.AppendLine($"Target: {TargetName}");
            sb.AppendLine();
            sb.AppendLine($"SII: {SII_Mean:F3}  (range: {SII_Min:F3} - {SII_Max:F3})");
            sb.AppendLine($"VSI: {VSI_Mean:F3}  (range: {VSI_Min:F3} - {VSI_Max:F3})");
            sb.AppendLine($"SSI: {SSI:F3}  (vol: {SSI_VolumeComponent:F3}, align: {SSI_AlignmentComponent:F3})");

            if (OAR_Results.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"OSI by OAR:");
                foreach (var oar in OAR_Results)
                {
                    sb.AppendLine($"  {oar.Name}: {oar.OSI_Mean:F3}  (range: {oar.OSI_Min:F3} - {oar.OSI_Max:F3})");
                }
                sb.AppendLine($"OSI Combined: {OSI_Combined:F3}");
                sb.AppendLine($"OSI Worst: {OSI_Worst:F3} ({OSI_WorstOARName})");
            }

            sb.AppendLine();
            sb.AppendLine($"===============================");
            sb.AppendLine($"Combined Score: {CombinedScore:F3}");
            sb.AppendLine($"===============================");
            sb.AppendLine();
            sb.AppendLine($"[{GantryAngleCount} angles, {ComputationTimeMs:F1}ms]");

            return sb.ToString();
        }
    }

    #region Grid Search Classes

    /// <summary>
    /// Result for a single grid position
    /// </summary>
    public class GridPositionResult
    {
        public double OffsetX { get; set; }  // mm
        public double OffsetY { get; set; }  // mm
        public int ValidSphereCount { get; set; }
        public int OriginalSphereCount { get; set; }
        public int SpheresRemoved => OriginalSphereCount - ValidSphereCount;

        // Metrics at this position
        public double SII { get; set; }
        public double VSI { get; set; }
        public double SSI { get; set; }
        public double OSI { get; set; }
        public double CombinedScore { get; set; }

        // Full results if needed
        public GeometricResults FullResults { get; set; }

        public string GetSummary()
        {
            return $"X={OffsetX:+0.0;-0.0}mm, Y={OffsetY:+0.0;-0.0}mm, Score={CombinedScore:F3}, Spheres={ValidSphereCount}";
        }
    }

    /// <summary>
    /// Complete results from grid search optimization
    /// </summary>
    public class GridSearchResult
    {
        // All tested positions
        public List<GridPositionResult> AllResults { get; set; } = new List<GridPositionResult>();

        // Best results
        public GridPositionResult BestOverall { get; set; }      // Highest score (may have fewer spheres)
        public GridPositionResult BestFullCount { get; set; }    // Best score keeping all spheres
        public GridPositionResult Baseline { get; set; }         // Original position (0,0)

        // Search parameters
        public double SearchRangePercent { get; set; }
        public double SearchRangeMm { get; set; }
        public int StepsPerAxis { get; set; }
        public int TotalPositionsTested { get; set; }

        // Original configuration
        public int OriginalSphereCount { get; set; }
        public double SphereRadius { get; set; }

        // Computation info
        public double ComputationTimeMs { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }

        /// <summary>
        /// Calculate improvement percentage
        /// </summary>
        public double ImprovementPercent
        {
            get
            {
                if (Baseline == null || BestOverall == null || Baseline.CombinedScore <= 0)
                    return 0;
                return ((BestOverall.CombinedScore - Baseline.CombinedScore) / Baseline.CombinedScore) * 100.0;
            }
        }

        /// <summary>
        /// Calculate improvement for full-count best
        /// </summary>
        public double ImprovementFullCountPercent
        {
            get
            {
                if (Baseline == null || BestFullCount == null || Baseline.CombinedScore <= 0)
                    return 0;
                return ((BestFullCount.CombinedScore - Baseline.CombinedScore) / Baseline.CombinedScore) * 100.0;
            }
        }

        /// <summary>
        /// Check if best overall is different from best full count
        /// </summary>
        public bool HasDifferentBestOptions
        {
            get
            {
                if (BestOverall == null || BestFullCount == null)
                    return false;
                return Math.Abs(BestOverall.OffsetX - BestFullCount.OffsetX) > 0.1 ||
                       Math.Abs(BestOverall.OffsetY - BestFullCount.OffsetY) > 0.1;
            }
        }

        /// <summary>
        /// Get formatted summary for display
        /// </summary>
        public string GetSummary()
        {
            var sb = new System.Text.StringBuilder();

            if (!Success)
            {
                sb.AppendLine($"Grid search failed: {Message}");
                return sb.ToString();
            }

            sb.AppendLine($"Grid Search Complete ({TotalPositionsTested} positions, {ComputationTimeMs:F0}ms)");
            sb.AppendLine($"Search range: ±{SearchRangeMm:F1}mm ({SearchRangePercent:F0}% of target)");
            sb.AppendLine();

            if (BestOverall != null)
            {
                sb.AppendLine($"Best Overall:  {BestOverall.GetSummary()}");
                if (BestOverall.SpheresRemoved > 0)
                    sb.AppendLine($"               ({BestOverall.SpheresRemoved} sphere(s) removed)");
                sb.AppendLine($"               Improvement: {ImprovementPercent:+0.0;-0.0}%");
            }

            if (HasDifferentBestOptions && BestFullCount != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Best (all spheres): {BestFullCount.GetSummary()}");
                sb.AppendLine($"               Improvement: {ImprovementFullCountPercent:+0.0;-0.0}%");
            }

            if (Baseline != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Baseline:      {Baseline.GetSummary()}");
            }

            return sb.ToString();
        }
    }

    #endregion
}