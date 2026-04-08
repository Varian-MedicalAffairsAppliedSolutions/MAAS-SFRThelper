using System;

namespace MAAS_SFRThelper.Services.MayoLattice
{
    /// <summary>
    /// Parameters for the Mayo Monte Carlo lattice placement algorithm.
    /// Default values reproduce Table 1 from Deufel et al. 2024,
    /// Phys. Med. Biol. 69, 075010.
    /// 
    /// All distance parameters are stored in millimeters (ESAPI native units).
    /// Mu is stored in the paper's reported units (cm-space, range 0–1)
    /// and converted during computation.
    /// </summary>
    public class MayoConstraints
    {
        // ───────────────────────────────────────────────
        // Sphere geometry
        // ───────────────────────────────────────────────

        /// <summary>
        /// Radius of each lattice sphere in mm.
        /// Paper default: 7.5 mm (15 mm diameter).
        /// </summary>
        public double RSphere { get; set; } = 7.5;

        // ───────────────────────────────────────────────
        // Constraint 1: GTV margin
        // ───────────────────────────────────────────────

        /// <summary>
        /// Minimum distance from sphere center to GTV surface, excluding RSphere.
        /// The total GTV contraction applied is DGtv + RSphere.
        /// Paper default: 5.0 mm (0.5 cm). Table 1: dGTV >= 0.5 cm + Rsphere.
        /// </summary>
        public double DGtv { get; set; } = 5.0;

        // ───────────────────────────────────────────────
        // Constraint 2: OAR margin
        // ───────────────────────────────────────────────

        /// <summary>
        /// Minimum distance from sphere center to OAR surface, excluding RSphere.
        /// The total OAR expansion applied is DOar + RSphere.
        /// Paper default: 10.0 mm (1.0 cm). Table 1: dOAR >= 1 cm + Rsphere.
        /// </summary>
        public double DOar { get; set; } = 10.0;

        // ───────────────────────────────────────────────
        // Constraint 3: Minimum center-to-center spacing
        // ───────────────────────────────────────────────

        /// <summary>
        /// Minimum Euclidean distance between any two sphere centers in mm.
        /// Ensures MLCs can create dose valleys between adjacent spheres.
        /// Paper default: 30.0 mm (3.0 cm). Table 1: dcenter >= 3 cm.
        /// </summary>
        public double DCenter { get; set; } = 30.0;

        // ───────────────────────────────────────────────
        // Constraint 4: Longitudinal (z-axis) separation
        // ───────────────────────────────────────────────

        /// <summary>
        /// Z-axis proximity threshold in mm. If two sphere centers have
        /// |z1 - z2| less than ZSep, they are considered "co-planar" and
        /// must satisfy the stricter DCoAxial spacing requirement.
        /// Paper default: 20.0 mm (2.0 cm). Table 1: zsep >= 2 cm.
        /// </summary>
        public double ZSep { get; set; } = 20.0;

        /// <summary>
        /// Minimum Euclidean distance between sphere centers that are
        /// nearly co-planar (|z1 - z2| less than ZSep) in mm.
        /// Ensures MLCs (oriented along z-axis) can independently
        /// modulate dose to each sphere.
        /// Paper default: 80.0 mm (8.0 cm). Table 1: dco-axial >= 8 cm.
        /// </summary>
        public double DCoAxial { get; set; } = 80.0;

        // ───────────────────────────────────────────────
        // Objective: Central placement preference
        // ───────────────────────────────────────────────

        /// <summary>
        /// Centralization preference parameter (cm-space, range 0 to 1).
        /// Controls how strongly the algorithm prefers positions near
        /// the center of mass of each axial GTV slice.
        /// 
        /// The Metropolis acceptance probability for a move farther from
        /// the COM is: P(accept) = exp(-Mu * deltaCOM_cm).
        /// 
        /// Larger Mu = more lenient (more peripheral placement, higher density).
        /// Smaller Mu = stricter central pressure.
        /// 
        /// The paper's UI displays (1 - Mu) on a slider, default 0.8.
        /// Paper default: 0.2. Table 1: mu = 0.2 cm.
        /// </summary>
        public double Mu { get; set; } = 0.2;

        // ───────────────────────────────────────────────
        // Algorithm parameters
        // ───────────────────────────────────────────────

        /// <summary>
        /// Target number of points in the feasible position array.
        /// The rasterization voxel size is dynamically adjusted to produce
        /// approximately this many interior points.
        /// Paper default: 100,000.
        /// </summary>
        public int FeasiblePointsTarget { get; set; } = 100000;

        /// <summary>
        /// Maximum Monte Carlo iterations per sphere count attempt.
        /// Also used for the refinement pass after finding maximum N.
        /// Paper default: 100,000.
        /// </summary>
        public int MaxIterations { get; set; } = 100000;

        /// <summary>
        /// Whether to perform the final deterministic gradient walk
        /// that pushes spheres toward their nearest axial slice COM.
        /// Paper default: true.
        /// </summary>
        public bool UseGradientWalk { get; set; } = true;

        /// <summary>
        /// Number of discrete steps in the gradient walk toward each COM.
        /// The path from sphere to COM is divided into this many increments.
        /// Paper default: 10.
        /// </summary>
        public int GradientWalkSteps { get; set; } = 10;

        // ───────────────────────────────────────────────
        // HDBSCAN clustering parameters
        // ───────────────────────────────────────────────

        /// <summary>
        /// Minimum cluster size for HDBSCAN when computing axial slice
        /// centers of mass. Also used as the minPts parameter for
        /// core distance computation.
        /// </summary>
        public int MinClusterSize { get; set; } = 5;

        // ───────────────────────────────────────────────
        // Reproducibility
        // ───────────────────────────────────────────────

        /// <summary>
        /// Random number generator seed for reproducibility.
        /// When null, the system clock is used (non-deterministic).
        /// When set, the algorithm produces identical results across runs.
        /// </summary>
        public int? RandomSeed { get; set; } = null;

        // ───────────────────────────────────────────────
        // Computed properties
        // ───────────────────────────────────────────────

        /// <summary>
        /// Total GTV contraction margin in mm: DGtv + RSphere.
        /// Any point inside the contracted GTV automatically satisfies Constraint 1.
        /// </summary>
        public double TotalGtvMargin => DGtv + RSphere;

        /// <summary>
        /// Total OAR expansion margin in mm: DOar + RSphere.
        /// Subtracting the expanded OAR from the contracted GTV ensures Constraint 2.
        /// </summary>
        public double TotalOarMargin => DOar + RSphere;

        /// <summary>
        /// The UI-facing centralization slider value (1 - Mu).
        /// Range 0 to 1, where higher values = stronger central pressure.
        /// Paper default: 0.8 (corresponding to Mu = 0.2).
        /// </summary>
        public double CentralizationSlider
        {
            get => 1.0 - Mu;
            set => Mu = 1.0 - Math.Max(0.0, Math.Min(1.0, value));
        }

        // ───────────────────────────────────────────────
        // Validation
        // ───────────────────────────────────────────────

        /// <summary>
        /// Validates that all parameters are physically reasonable.
        /// Returns null if valid, or an error message string if invalid.
        /// </summary>
        public string Validate()
        {
            if (RSphere <= 0)
                return "Sphere radius must be positive.";

            if (DGtv < 0)
                return "GTV margin must be non-negative.";

            if (DOar < 0)
                return "OAR margin must be non-negative.";

            if (DCenter <= 0)
                return "Center-to-center spacing must be positive.";

            if (DCenter < 2 * RSphere)
                return $"Center-to-center spacing ({DCenter:F1} mm) must be at least twice the sphere radius ({2 * RSphere:F1} mm) to prevent overlap.";

            if (ZSep <= 0)
                return "Z-separation threshold must be positive.";

            if (DCoAxial <= DCenter)
                return $"Co-axial distance ({DCoAxial:F1} mm) must exceed center-to-center spacing ({DCenter:F1} mm).";

            if (Mu < 0 || Mu > 1)
                return "Centralization parameter Mu must be between 0 and 1.";

            if (FeasiblePointsTarget < 1000)
                return "Feasible points target must be at least 1,000.";

            if (MaxIterations < 1000)
                return "Max iterations must be at least 1,000.";

            if (GradientWalkSteps < 1)
                return "Gradient walk steps must be at least 1.";

            if (MinClusterSize < 2)
                return "HDBSCAN min cluster size must be at least 2.";

            return null; // valid
        }

        /// <summary>
        /// Creates a deep copy of this constraints object.
        /// </summary>
        public MayoConstraints Clone()
        {
            return new MayoConstraints
            {
                RSphere = this.RSphere,
                DGtv = this.DGtv,
                DOar = this.DOar,
                DCenter = this.DCenter,
                ZSep = this.ZSep,
                DCoAxial = this.DCoAxial,
                Mu = this.Mu,
                FeasiblePointsTarget = this.FeasiblePointsTarget,
                MaxIterations = this.MaxIterations,
                UseGradientWalk = this.UseGradientWalk,
                GradientWalkSteps = this.GradientWalkSteps,
                MinClusterSize = this.MinClusterSize,
                RandomSeed = this.RandomSeed
            };
        }

        /// <summary>
        /// Returns a summary string of all constraint values for logging.
        /// </summary>
        public override string ToString()
        {
            return $"Mayo Constraints:\n" +
                   $"  RSphere = {RSphere:F1} mm\n" +
                   $"  DGtv = {DGtv:F1} mm (total margin: {TotalGtvMargin:F1} mm)\n" +
                   $"  DOar = {DOar:F1} mm (total margin: {TotalOarMargin:F1} mm)\n" +
                   $"  DCenter = {DCenter:F1} mm\n" +
                   $"  ZSep = {ZSep:F1} mm, DCoAxial = {DCoAxial:F1} mm\n" +
                   $"  Mu = {Mu:F2} (slider: {CentralizationSlider:F2})\n" +
                   $"  FeasiblePoints = {FeasiblePointsTarget}, MaxIter = {MaxIterations}\n" +
                   $"  GradientWalk = {UseGradientWalk} ({GradientWalkSteps} steps)\n" +
                   $"  HDBSCAN MinClusterSize = {MinClusterSize}\n" +
                   $"  RandomSeed = {(RandomSeed.HasValue ? RandomSeed.Value.ToString() : "random")}";
        }
    }
}