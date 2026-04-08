using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace MAAS_SFRThelper.Services.MayoLattice
{
    /// <summary>
    /// Builds the feasible position array (PPossible) for the Mayo lattice
    /// placement algorithm.
    /// 
    /// The feasible region is the volume where lattice sphere centers can
    /// legally be placed. It is constructed by:
    ///   1. Contracting the GTV inward by (DGtv + RSphere)
    ///   2. Expanding each OAR outward by (DOar + RSphere)
    ///   3. Subtracting all expanded OARs from the contracted GTV
    /// 
    /// Any point inside the resulting volume automatically satisfies
    /// Constraints 1 (GTV margin) and 2 (OAR margin) from Table 1.
    /// 
    /// The volume is then rasterized into a discrete array of ~100,000
    /// points scanning in x → y → z order, matching the paper's approach.
    /// 
    /// Per Deufel et al. 2024, Phys. Med. Biol. 69, 075010.
    /// </summary>
    public class FeasibleRegionBuilder
    {
        /// <summary>
        /// Build the feasible position array from GTV and OAR structures.
        /// 
        /// This method creates temporary ESAPI structures for the Boolean
        /// operations, rasterizes the result, and cleans up the temporary
        /// structures afterward.
        /// </summary>
        /// <param name="structureSet">The active structure set.</param>
        /// <param name="gtv">The gross tumor volume structure.</param>
        /// <param name="oars">
        /// OAR structures to avoid. Sphere centers will be placed at least
        /// (DOar + RSphere) mm from each OAR surface.
        /// </param>
        /// <param name="image">CT image for coordinate reference.</param>
        /// <param name="constraints">Algorithm parameters (margins, target point count).</param>
        /// <param name="log">Optional StringBuilder for logging progress.</param>
        /// <returns>
        /// List of feasible positions in mm (ESAPI coordinates), ordered
        /// by raster scan (x fastest, then y, then z).
        /// </returns>
        public List<VVector> Build(StructureSet structureSet, Structure gtv,
            List<Structure> oars, Image image, MayoConstraints constraints,
            StringBuilder log = null)
        {
            var feasiblePoints = new List<VVector>();
            var tempStructures = new List<Structure>();

            try
            {
                log?.AppendLine("Building feasible region (PPossible)...");
                log?.AppendLine($"  GTV: {gtv.Id} (volume: {gtv.Volume:F1} cc)");
                log?.AppendLine($"  OARs: {string.Join(", ", oars.Select(o => o.Id))}");
                log?.AppendLine($"  Total GTV margin: {constraints.TotalGtvMargin:F1} mm");
                log?.AppendLine($"  Total OAR margin: {constraints.TotalOarMargin:F1} mm");

                // ─── Step 1: Contract GTV ───
                var gtvContracted = CreateTempStructure(structureSet, "z_MayoGtvCon",
                    tempStructures);

                double gtvMargin = -constraints.TotalGtvMargin; // negative = contraction
                gtvContracted.SegmentVolume = ApplyMargin(gtv, gtvMargin);

                if (gtvContracted.IsEmpty)
                {
                    log?.AppendLine("  ERROR: Contracted GTV is empty. GTV may be too small for the specified margins.");
                    return feasiblePoints;
                }

                log?.AppendLine($"  Contracted GTV volume: {gtvContracted.Volume:F1} cc");

                // ─── Step 2: Expand and union all OARs ───
                SegmentVolume oarUnion = null;

                if (oars.Count > 0)
                {
                    // Expand first OAR
                    var oarExpanded = CreateTempStructure(structureSet, "z_MayoOarExp",
                        tempStructures);

                    double oarMargin = constraints.TotalOarMargin; // positive = expansion
                    oarExpanded.SegmentVolume = ApplyMargin(oars[0], oarMargin);
                    oarUnion = oarExpanded.SegmentVolume;

                    log?.AppendLine($"  Expanded OAR '{oars[0].Id}' by {oarMargin:F1} mm");

                    // Expand and union remaining OARs
                    for (int i = 1; i < oars.Count; i++)
                    {
                        var tempOar = CreateTempStructure(structureSet,
                            $"z_MayoOar{i}", tempStructures);
                        tempOar.SegmentVolume = ApplyMargin(oars[i], oarMargin);
                        oarUnion = oarUnion.Or(tempOar.SegmentVolume);

                        log?.AppendLine($"  Expanded OAR '{oars[i].Id}' by {oarMargin:F1} mm");
                    }
                }

                // ─── Step 3: Subtract OAR union from contracted GTV ───
                var feasibleStructure = CreateTempStructure(structureSet, "z_MayoFeas",
                    tempStructures);

                if (oarUnion != null)
                {
                    feasibleStructure.SegmentVolume = gtvContracted.SegmentVolume.Sub(oarUnion);
                }
                else
                {
                    feasibleStructure.SegmentVolume = gtvContracted.SegmentVolume;
                }

                if (feasibleStructure.IsEmpty)
                {
                    log?.AppendLine("  ERROR: Feasible region is empty after OAR subtraction.");
                    return feasiblePoints;
                }

                double feasibleVolume = feasibleStructure.Volume; // cc
                log?.AppendLine($"  Feasible volume: {feasibleVolume:F1} cc");

                // ─── Step 4: Rasterize the feasible volume ───
                feasiblePoints = Rasterize(feasibleStructure, image, constraints, log);

                log?.AppendLine($"  Rasterized: {feasiblePoints.Count} feasible points");
            }
            catch (Exception ex)
            {
                log?.AppendLine($"  ERROR building feasible region: {ex.Message}");
            }
            finally
            {
                // ─── Cleanup temporary structures ───
                foreach (var temp in tempStructures)
                {
                    try
                    {
                        if (structureSet.Structures.Contains(temp))
                            structureSet.RemoveStructure(temp);
                    }
                    catch
                    {
                        // Best effort cleanup
                    }
                }
            }

            return feasiblePoints;
        }

        // ═══════════════════════════════════════════════════════════════
        // Rasterization
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Rasterize the feasible structure into a discrete array of points.
        /// 
        /// The voxel size is dynamically computed to yield approximately
        /// FeasiblePointsTarget points. The scan order is x → y → z
        /// (x fastest), matching the paper's PPossible construction.
        /// 
        /// If the initial voxel size produces a count far from the target,
        /// the resolution is adjusted and re-rasterized (up to 3 attempts).
        /// </summary>
        private List<VVector> Rasterize(Structure feasibleStructure, Image image,
            MayoConstraints constraints, StringBuilder log)
        {
            int targetCount = constraints.FeasiblePointsTarget;
            var bounds = feasibleStructure.MeshGeometry.Bounds;

            double xMin = bounds.X;
            double yMin = bounds.Y;
            double zMin = bounds.Z;
            double xMax = bounds.X + bounds.SizeX;
            double yMax = bounds.Y + bounds.SizeY;
            double zMax = bounds.Z + bounds.SizeZ;

            // Estimate feasible volume from structure volume (cc → mm³)
            double volumeMm3 = feasibleStructure.Volume * 1000.0;

            // Initial voxel size estimate
            double voxelSize = Math.Pow(volumeMm3 / targetCount, 1.0 / 3.0);

            // Clamp voxel size to reasonable range
            voxelSize = Math.Max(0.5, Math.Min(voxelSize, 5.0));

            log?.AppendLine($"  Initial voxel size: {voxelSize:F2} mm");

            List<VVector> points = null;

            // Iterative refinement to hit target count
            for (int attempt = 0; attempt < 3; attempt++)
            {
                points = ScanVolume(feasibleStructure, xMin, xMax, yMin, yMax,
                    zMin, zMax, voxelSize);

                log?.AppendLine($"  Attempt {attempt + 1}: voxel={voxelSize:F2} mm, points={points.Count}");

                // Check if close enough to target (within factor of 2)
                if (points.Count >= targetCount / 2 && points.Count <= targetCount * 2)
                    break;

                if (points.Count == 0)
                {
                    // Try finer resolution
                    voxelSize *= 0.7;
                    continue;
                }

                // Adjust voxel size based on ratio of actual to target
                // points ~ volume / voxelSize³, so voxelSize ~ (volume/points)^(1/3)
                double ratio = (double)points.Count / targetCount;
                voxelSize *= Math.Pow(ratio, 1.0 / 3.0);
                voxelSize = Math.Max(0.5, Math.Min(voxelSize, 5.0));
            }

            return points ?? new List<VVector>();
        }

        /// <summary>
        /// Scan a bounding box at the given voxel resolution and collect
        /// all points that are inside the feasible structure.
        /// Scan order: x (fastest) → y → z (slowest).
        /// </summary>
        private List<VVector> ScanVolume(Structure structure,
            double xMin, double xMax, double yMin, double yMax,
            double zMin, double zMax, double voxelSize)
        {
            var points = new List<VVector>();

            // Scan in x → y → z order (x fastest, matching paper)
            for (double z = zMin; z <= zMax; z += voxelSize)
            {
                for (double y = yMin; y <= yMax; y += voxelSize)
                {
                    for (double x = xMin; x <= xMax; x += voxelSize)
                    {
                        var point = new VVector(x, y, z);
                        if (structure.IsPointInsideSegment(point))
                        {
                            points.Add(point);
                        }
                    }
                }
            }

            return points;
        }

        // ═══════════════════════════════════════════════════════════════
        // Structure utilities
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Apply a margin to a structure, handling ESAPI's 50mm limit
        /// via iterative application for larger margins.
        /// Negative margin = contraction. Positive margin = expansion.
        /// </summary>
        private SegmentVolume ApplyMargin(Structure source, double marginMm)
        {
            // Use LargeMargin extension for margins exceeding 50mm
            if (Math.Abs(marginMm) > 50.0)
            {
                return source.LargeMargin(marginMm);
            }
            else
            {
                return source.SegmentVolume.Margin(marginMm);
            }
        }

        /// <summary>
        /// Create a temporary high-resolution CONTROL structure for
        /// Boolean operations. Adds it to the tracking list for cleanup.
        /// </summary>
        private Structure CreateTempStructure(StructureSet structureSet,
            string id, List<Structure> trackingList)
        {
            // Remove existing structure with same name if present
            var existing = structureSet.Structures
                .FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                structureSet.RemoveStructure(existing);

            var structure = structureSet.AddStructure("CONTROL", id);
            structure.ConvertToHighResolution();
            trackingList.Add(structure);

            return structure;
        }
    }
}