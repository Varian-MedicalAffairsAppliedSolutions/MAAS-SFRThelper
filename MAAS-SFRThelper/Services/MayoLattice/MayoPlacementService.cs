using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace MAAS_SFRThelper.Services.MayoLattice
{
    /// <summary>
    /// Results from the Mayo lattice placement algorithm.
    /// </summary>
    public class MayoPlacementResult
    {
        /// <summary>Sphere center positions in mm (ESAPI coordinates).</summary>
        public List<VVector> SpherePositions { get; set; } = new List<VVector>();

        /// <summary>Number of spheres placed.</summary>
        public int SphereCount => SpherePositions.Count;

        /// <summary>Number of feasible points in PPossible.</summary>
        public int FeasiblePointCount { get; set; }

        /// <summary>Number of axial slice COMs computed.</summary>
        public int COMCount { get; set; }

        /// <summary>Voxel resolution used for rasterization (mm).</summary>
        public double VoxelResolution { get; set; }

        /// <summary>Whether the algorithm completed successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Status/error message.</summary>
        public string Message { get; set; }

        /// <summary>Detailed execution log.</summary>
        public string Log { get; set; }

        /// <summary>Total execution time.</summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>The constraints used for this run.</summary>
        public MayoConstraints ConstraintsUsed { get; set; }
    }

    /// <summary>
    /// Orchestrator for the Mayo Monte Carlo lattice placement algorithm.
    /// 
    /// Coordinates the full pipeline:
    ///   1. FeasibleRegionBuilder: construct PPossible via ESAPI Boolean ops
    ///   2. SliceCOMCalculator: compute axial COMs via HDBSCAN
    ///   3. MonteCarloEngine: find optimal sphere positions
    /// 
    /// This service is the single entry point called from the ViewModel.
    /// Phase 1 (feasible region) requires ESAPI access and must run on
    /// the ESAPI dispatcher thread. Phase 2 and 3 are pure math.
    /// 
    /// Per Deufel et al. 2024, Phys. Med. Biol. 69, 075010.
    /// </summary>
    public class MayoPlacementService
    {
        /// <summary>
        /// Progress callback: (statusMessage, progressPercent 0–100).
        /// Wire this to the ViewModel's progress bar.
        /// </summary>
        public Action<string, double> OnProgress;

        /// <summary>
        /// Execute the full Mayo lattice placement pipeline.
        /// 
        /// IMPORTANT: This method performs ESAPI structure operations
        /// (margins, Booleans) and must be called from within the
        /// ESAPI worker dispatcher thread.
        /// </summary>
        /// <param name="structureSet">Active structure set.</param>
        /// <param name="gtv">Gross tumor volume structure.</param>
        /// <param name="oars">OAR structures to avoid.</param>
        /// <param name="image">CT image reference.</param>
        /// <param name="constraints">Algorithm parameters.</param>
        /// <returns>Placement result with sphere positions and metadata.</returns>
        public MayoPlacementResult Execute(StructureSet structureSet, Structure gtv,
            List<Structure> oars, Image image, MayoConstraints constraints)
        {
            var result = new MayoPlacementResult();
            var log = new StringBuilder();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // ─── Validate inputs ───
                string validationError = ValidateInputs(gtv, oars, constraints);
                if (validationError != null)
                {
                    result.Success = false;
                    result.Message = validationError;
                    result.Log = log.ToString();
                    return result;
                }

                result.ConstraintsUsed = constraints.Clone();
                log.AppendLine("Mayo Lattice Placement Algorithm");
                log.AppendLine("================================");
                log.AppendLine(constraints.ToString());
                log.AppendLine();

                // ─── Phase 1: Build feasible region (PPossible) ───
                ReportProgress("Building feasible region...", 5);

                var regionBuilder = new FeasibleRegionBuilder();
                var feasiblePoints = regionBuilder.Build(structureSet, gtv, oars,
                    image, constraints, log);

                if (feasiblePoints.Count == 0)
                {
                    result.Success = false;
                    result.Message = "Feasible region is empty. Check GTV size and OAR proximity.";
                    result.Log = log.ToString();
                    return result;
                }

                result.FeasiblePointCount = feasiblePoints.Count;
                log.AppendLine($"\nFeasible points: {feasiblePoints.Count}");

                ReportProgress($"Feasible region: {feasiblePoints.Count} points", 20);

                // ─── Phase 2: Compute axial slice COMs ───
                ReportProgress("Computing axial slice COMs...", 25);

                var comCalculator = new SliceCOMCalculator(constraints.MinClusterSize);
                var coms = comCalculator.ComputeCOMs(feasiblePoints);

                result.COMCount = coms.Count;
                log.AppendLine($"Axial COMs: {coms.Count}");

                ReportProgress($"Found {coms.Count} axial COMs", 30);

                // ─── Phase 3: Monte Carlo placement ───
                ReportProgress("Running Monte Carlo placement...", 35);

                var rng = constraints.RandomSeed.HasValue
                    ? new Random(constraints.RandomSeed.Value)
                    : new Random();

                var mcEngine = new MonteCarloEngine(feasiblePoints, coms, constraints, rng);

                // Wire MC progress to our progress (scale to 35–95%)
                mcEngine.OnProgress = (msg, frac) =>
                {
                    double scaled = 35 + frac * 60; // 35% to 95%
                    ReportProgress(msg, scaled);
                };

                var positions = mcEngine.FindOptimalPositions();

                result.SpherePositions = positions;
                log.AppendLine($"\nFinal placement: {positions.Count} spheres");

                // Log sphere positions
                for (int i = 0; i < positions.Count; i++)
                {
                    var p = positions[i];
                    log.AppendLine($"  Sphere {i + 1}: ({p.x:F1}, {p.y:F1}, {p.z:F1})");
                }

                // Compute and log placement density
                if (positions.Count > 0 && gtv.Volume > 0)
                {
                    double density = (positions.Count / gtv.Volume) * 100.0;
                    log.AppendLine($"\nLattice point density: {density:F3} points per 100 cc of GTV");
                    log.AppendLine($"  ({gtv.Volume / positions.Count:F1} cc per lattice point)");
                }

                // ─── Verify constraints ───
                ReportProgress("Verifying constraints...", 95);
                var violations = VerifyConstraints(positions, constraints);

                if (violations.Count > 0)
                {
                    log.AppendLine("\nWARNING: Constraint violations detected:");
                    foreach (var v in violations)
                        log.AppendLine($"  {v}");
                    result.Message = $"Placement complete with {violations.Count} constraint warning(s). See log.";
                }
                else
                {
                    log.AppendLine("\nAll constraints verified.");
                    result.Message = $"Successfully placed {positions.Count} lattice spheres.";
                }

                result.Success = true;
                ReportProgress("Placement complete.", 100);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error: {ex.Message}";
                log.AppendLine($"\nERROR: {ex.Message}");
                log.AppendLine(ex.StackTrace);
            }
            finally
            {
                stopwatch.Stop();
                result.ExecutionTime = stopwatch.Elapsed;
                log.AppendLine($"\nExecution time: {stopwatch.Elapsed.TotalSeconds:F1} seconds");
                result.Log = log.ToString();
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        // Validation
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Validate all inputs before running the algorithm.
        /// Returns null if valid, or an error message string.
        /// </summary>
        private string ValidateInputs(Structure gtv, List<Structure> oars,
            MayoConstraints constraints)
        {
            if (gtv == null || gtv.IsEmpty)
                return "GTV structure is null or empty.";

            if (oars == null)
                return "OAR list is null (pass empty list if no OARs).";

            string constraintError = constraints.Validate();
            if (constraintError != null)
                return $"Invalid constraint: {constraintError}";

            // Check if GTV is large enough for the margins
            double minGtvDimension = Math.Min(
                gtv.MeshGeometry.Bounds.SizeX,
                Math.Min(gtv.MeshGeometry.Bounds.SizeY,
                         gtv.MeshGeometry.Bounds.SizeZ));

            if (minGtvDimension < 2 * constraints.TotalGtvMargin)
                return $"GTV smallest dimension ({minGtvDimension:F1} mm) is less than " +
                       $"twice the total margin ({2 * constraints.TotalGtvMargin:F1} mm). " +
                       $"The contracted GTV would be empty.";

            return null;
        }

        // ═══════════════════════════════════════════════════════════════
        // Post-placement constraint verification (QA)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Verify that all pairwise constraints are satisfied in the
        /// final solution. Returns a list of violation descriptions.
        /// Empty list = all constraints satisfied.
        /// 
        /// This serves as the QA check recommended by the paper:
        /// "QA software was also written to ensure that the automated
        /// plans were compliant with the requirements of table 1."
        /// </summary>
        private List<string> VerifyConstraints(List<VVector> positions,
            MayoConstraints constraints)
        {
            var violations = new List<string>();
            int n = positions.Count;

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double dx = positions[i].x - positions[j].x;
                    double dy = positions[i].y - positions[j].y;
                    double dz = positions[i].z - positions[j].z;
                    double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    double dzAbs = Math.Abs(dz);

                    // Constraint 3: dcenter
                    if (dist < constraints.DCenter)
                    {
                        violations.Add(
                            $"Constraint 3 violation: spheres {i + 1} and {j + 1} " +
                            $"distance = {dist:F1} mm < dcenter = {constraints.DCenter:F1} mm");
                    }

                    // Constraint 4: z-separation
                    if (dzAbs < constraints.ZSep && dist < constraints.DCoAxial)
                    {
                        violations.Add(
                            $"Constraint 4 violation: spheres {i + 1} and {j + 1} " +
                            $"|dz| = {dzAbs:F1} mm < zsep = {constraints.ZSep:F1} mm, " +
                            $"distance = {dist:F1} mm < dco-axial = {constraints.DCoAxial:F1} mm");
                    }
                }
            }

            return violations;
        }

        // ═══════════════════════════════════════════════════════════════
        // Progress reporting
        // ═══════════════════════════════════════════════════════════════

        private void ReportProgress(string message, double percent)
        {
            OnProgress?.Invoke(message, Math.Max(0.0, Math.Min(100.0, percent)));
        }
    }
}