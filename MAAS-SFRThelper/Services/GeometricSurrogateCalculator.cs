using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MAAS_SFRThelper.Models;

namespace MAAS_SFRThelper.Services
{
    /// <summary>
    /// Represents a 2D circle in BEV (Beam's Eye View) coordinates
    /// </summary>
    internal class Circle2D
    {
        public double U { get; set; }      // crossline position (mm)
        public double V { get; set; }      // inline/sup-inf position (mm)
        public double Radius { get; set; } // mm

        public double Area => Math.PI * Radius * Radius;

        public Circle2D(double u, double v, double radius)
        {
            U = u;
            V = v;
            Radius = radius;
        }
    }

    /// <summary>
    /// Represents a 2D ellipse in BEV coordinates (for OAR projections)
    /// </summary>
    internal class Ellipse2D
    {
        public double U { get; set; }       // center crossline (mm)
        public double V { get; set; }       // center inline (mm)
        public double RadiusU { get; set; } // half-width in U direction (mm)
        public double RadiusV { get; set; } // half-height in V direction (mm)

        public double Area => Math.PI * RadiusU * RadiusV;

        public Ellipse2D(double u, double v, double radiusU, double radiusV)
        {
            U = u;
            V = v;
            RadiusU = radiusU;
            RadiusV = radiusV;
        }
    }

    /// <summary>
    /// Calculator for geometric surrogate metrics that predict PVDR
    /// without requiring full dose calculation.
    /// 
    /// Metrics:
    /// - SII (Sphere Independence Index): BEV overlap of spheres
    /// - VSI (Valley Shielding Index): Valley visibility in BEV
    /// - SSI (Sphere Spread Index): 3D distribution quality
    /// - OSI (OAR Sparing Index): OAR protection from sphere exposure
    /// </summary>
    public class GeometricSurrogateCalculator
    {
        #region Configuration

        // Gantry angle sampling
        private readonly double[] _gantryAngles;
        public double GantryAngleStep { get; }
        public double GantryStart { get; }
        public double GantryEnd { get; }

        // Weights for combined score (public so they can be adjusted)
        public double WeightSII { get; set; } = 0.25;
        public double WeightVSI { get; set; } = 0.25;
        public double WeightSSI { get; set; } = 0.25;
        public double WeightOSI { get; set; } = 0.25;

        #endregion

        #region Constructor

        /// <summary>
        /// Create calculator with specified gantry angle sampling
        /// </summary>
        /// <param name="gantryAngleStep">Angle step in degrees (default 5°)</param>
        /// <param name="gantryStart">Start angle in degrees (default 0°)</param>
        /// <param name="gantryEnd">End angle in degrees, exclusive (default 360°)</param>
        public GeometricSurrogateCalculator(
            double gantryAngleStep = 5.0,
            double gantryStart = 0.0,
            double gantryEnd = 360.0)
        {
            GantryAngleStep = gantryAngleStep;
            GantryStart = gantryStart;
            GantryEnd = gantryEnd;

            // Generate gantry angles
            var angles = new List<double>();
            for (double angle = gantryStart; angle < gantryEnd; angle += gantryAngleStep)
            {
                angles.Add(angle);
            }
            _gantryAngles = angles.ToArray();
        }

        #endregion

        #region Main Calculation Method

        /// <summary>
        /// Calculate all geometric surrogate metrics
        /// </summary>
        /// <param name="spheres">List of extracted spheres from SphereExtractor</param>
        /// <param name="target">Target volume information</param>
        /// <param name="oars">List of OARs to analyze (only those with IsSelected=true will be used)</param>
        /// <returns>Complete geometric results</returns>
        public GeometricResults Calculate(
            List<ExtractedSphere> spheres,
            TargetInfo target,
            List<OARInfo> oars = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var results = new GeometricResults();

            try
            {
                // Validate inputs
                if (spheres == null || spheres.Count == 0)
                {
                    results.Success = false;
                    results.Message = "No spheres provided";
                    return results;
                }

                if (target == null)
                {
                    results.Success = false;
                    results.Message = "No target provided";
                    return results;
                }

                // Store input summary
                results.SphereCount = spheres.Count;
                results.SphereRadius = spheres.Average(s => s.Radius);
                results.TargetName = target.Name;
                results.GantryAngleCount = _gantryAngles.Length;
                results.GantryAngleStep = GantryAngleStep;

                // Filter to selected OARs only
                var selectedOARs = oars?.Where(o => o.IsSelected).ToList() ?? new List<OARInfo>();
                results.OARNames = selectedOARs.Select(o => o.Name).ToList();

                // Calculate angle-dependent metrics (SII, VSI, OSI)
                var siiValues = new List<double>();
                var vsiValues = new List<double>();
                var osiValuesPerOAR = new Dictionary<string, List<double>>();

                foreach (var oar in selectedOARs)
                {
                    osiValuesPerOAR[oar.Name] = new List<double>();
                }

                // Loop through all gantry angles
                foreach (double gantryAngle in _gantryAngles)
                {
                    var perAngle = new PerAngleData { GantryAngle = gantryAngle };

                    // Project spheres to BEV
                    var projectedSpheres = ProjectSpheresToBEV(spheres, gantryAngle);

                    // Project target to BEV
                    var projectedTarget = ProjectTargetToBEV(target, gantryAngle);

                    // Calculate SII for this angle
                    double sii = CalculateSII(projectedSpheres);
                    siiValues.Add(sii);
                    perAngle.SII = sii;

                    // Calculate VSI for this angle
                    double vsi = CalculateVSI(projectedSpheres, projectedTarget);
                    vsiValues.Add(vsi);
                    perAngle.VSI = vsi;

                    // Calculate OSI for each selected OAR
                    foreach (var oar in selectedOARs)
                    {
                        var projectedOAR = ProjectOARToBEV(oar, gantryAngle);
                        double osi = CalculateOSI(projectedSpheres, projectedOAR);
                        osiValuesPerOAR[oar.Name].Add(osi);
                        perAngle.OSI_PerOAR[oar.Name] = osi;
                    }

                    results.PerAngleResults.Add(perAngle);
                }

                // Aggregate SII results
                results.SII_Mean = siiValues.Average();
                results.SII_Min = siiValues.Min();
                results.SII_Max = siiValues.Max();
                results.SII_StdDev = CalculateStdDev(siiValues);

                // Aggregate VSI results
                results.VSI_Mean = vsiValues.Average();
                results.VSI_Min = vsiValues.Min();
                results.VSI_Max = vsiValues.Max();
                results.VSI_StdDev = CalculateStdDev(vsiValues);

                // Calculate SSI (angle-independent)
                var (ssi, volComponent, alignComponent) = CalculateSSI(spheres, target);
                results.SSI = ssi;
                results.SSI_VolumeComponent = volComponent;
                results.SSI_AlignmentComponent = alignComponent;

                // Aggregate OSI results per OAR
                foreach (var oar in selectedOARs)
                {
                    var osiValues = osiValuesPerOAR[oar.Name];
                    results.OAR_Results.Add(new OARResult
                    {
                        Name = oar.Name,
                        OSI_Mean = osiValues.Average(),
                        OSI_Min = osiValues.Min(),
                        OSI_Max = osiValues.Max()
                    });
                }

                // Calculate combined OSI metrics
                if (results.OAR_Results.Count > 0)
                {
                    // Combined = average of all OAR means (could weight by volume later)
                    results.OSI_Combined = results.OAR_Results.Average(r => r.OSI_Mean);

                    // Worst = minimum mean OSI across all OARs
                    var worstOAR = results.OAR_Results.OrderBy(r => r.OSI_Mean).First();
                    results.OSI_Worst = worstOAR.OSI_Mean;
                    results.OSI_WorstOARName = worstOAR.Name;
                }
                else
                {
                    // No OARs selected - set OSI to 1.0 (perfect, no OAR concern)
                    results.OSI_Combined = 1.0;
                    results.OSI_Worst = 1.0;
                    results.OSI_WorstOARName = "N/A";
                }

                // Calculate combined score
                results.Weight_SII = WeightSII;
                results.Weight_VSI = WeightVSI;
                results.Weight_SSI = WeightSSI;
                results.Weight_OSI = WeightOSI;

                double totalWeight = WeightSII + WeightVSI + WeightSSI + WeightOSI;
                results.CombinedScore = (
                    WeightSII * results.SII_Mean +
                    WeightVSI * results.VSI_Mean +
                    WeightSSI * results.SSI +
                    WeightOSI * results.OSI_Combined
                ) / totalWeight;

                stopwatch.Stop();
                results.ComputationTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                results.Success = true;
                results.Message = $"Calculated metrics for {spheres.Count} spheres across {_gantryAngles.Length} angles";
            }
            catch (Exception ex)
            {
                results.Success = false;
                results.Message = $"Error: {ex.Message}";
            }

            return results;
        }

        #endregion

        #region BEV Projection Methods

        /// <summary>
        /// Project 3D sphere centers onto 2D BEV plane
        /// 
        /// For gantry angle θ (couch = 0°):
        ///   u = x·cos(θ) + y·sin(θ)  (crossline)
        ///   v = z                     (inline, sup-inf)
        /// </summary>
        private List<Circle2D> ProjectSpheresToBEV(List<ExtractedSphere> spheres, double gantryAngleDeg)
        {
            double theta = gantryAngleDeg * Math.PI / 180.0;
            double cosTheta = Math.Cos(theta);
            double sinTheta = Math.Sin(theta);

            var projected = new List<Circle2D>();

            foreach (var sphere in spheres)
            {
                double u = sphere.CenterX * cosTheta + sphere.CenterY * sinTheta;
                double v = sphere.CenterZ;

                projected.Add(new Circle2D(u, v, sphere.Radius));
            }

            return projected;
        }

        /// <summary>
        /// Project target volume onto BEV plane (approximated as circle)
        /// </summary>
        private Circle2D ProjectTargetToBEV(TargetInfo target, double gantryAngleDeg)
        {
            double theta = gantryAngleDeg * Math.PI / 180.0;
            double cosTheta = Math.Cos(theta);
            double sinTheta = Math.Sin(theta);

            double u = target.CenterX * cosTheta + target.CenterY * sinTheta;
            double v = target.CenterZ;

            // Target projects as circle with same radius (spherical approximation)
            return new Circle2D(u, v, target.Radius);
        }

        /// <summary>
        /// Project OAR onto BEV plane (approximated as ellipse)
        /// 
        /// OAR is approximated as cylinder with:
        /// - RadiusXY: lateral extent
        /// - LengthZ: superior-inferior extent
        /// 
        /// In BEV:
        /// - RadiusU depends on viewing angle (simplified to RadiusXY)
        /// - RadiusV = LengthZ / 2
        /// </summary>
        private Ellipse2D ProjectOARToBEV(OARInfo oar, double gantryAngleDeg)
        {
            double theta = gantryAngleDeg * Math.PI / 180.0;
            double cosTheta = Math.Cos(theta);
            double sinTheta = Math.Sin(theta);

            double u = oar.CenterX * cosTheta + oar.CenterY * sinTheta;
            double v = oar.CenterZ;

            // OAR lateral extent in BEV (simplified - could be more sophisticated)
            double radiusU = oar.RadiusXY;
            double radiusV = oar.LengthZ / 2.0;

            return new Ellipse2D(u, v, radiusU, radiusV);
        }

        #endregion

        #region SII Calculation (Sphere Independence Index)

        /// <summary>
        /// Calculate Sphere Independence Index for projected circles
        /// 
        /// SII = 1 - (overlap_area / total_area)
        /// 
        /// Higher SII = less overlap = spheres more independently targetable
        /// Range: [1/N, 1.0]
        /// </summary>
        private double CalculateSII(List<Circle2D> circles)
        {
            if (circles.Count == 0) return 1.0;
            if (circles.Count == 1) return 1.0;

            // Total area = sum of all circle areas
            double totalArea = circles.Sum(c => c.Area);

            // Calculate pairwise overlap
            double overlapArea = 0;
            for (int i = 0; i < circles.Count; i++)
            {
                for (int j = i + 1; j < circles.Count; j++)
                {
                    overlapArea += CircleIntersectionArea(circles[i], circles[j]);
                }
            }

            // SII = 1 - (overlap / total)
            double sii = 1.0 - (overlapArea / totalArea);

            // Clamp to valid range
            return Math.Max(0.0, Math.Min(1.0, sii));
        }

        /// <summary>
        /// Calculate intersection area of two circles
        /// 
        /// Formula for two circles with equal radii r, center distance d:
        /// - If d >= 2r: A = 0 (no overlap)
        /// - If d = 0:   A = πr² (complete overlap)
        /// - Otherwise:  A = 2r²·arccos(d/2r) - (d/2)·√(4r² - d²)
        /// 
        /// For unequal radii, we use the general lens formula.
        /// </summary>
        private double CircleIntersectionArea(Circle2D c1, Circle2D c2)
        {
            double dx = c2.U - c1.U;
            double dy = c2.V - c1.V;
            double d = Math.Sqrt(dx * dx + dy * dy);

            double r1 = c1.Radius;
            double r2 = c2.Radius;

            // No overlap - circles don't touch
            if (d >= r1 + r2)
                return 0.0;

            // Complete overlap - one circle inside the other
            if (d <= Math.Abs(r1 - r2))
            {
                double smallerRadius = Math.Min(r1, r2);
                return Math.PI * smallerRadius * smallerRadius;
            }

            // Partial overlap - use lens formula
            // A = r1²·arccos((d² + r1² - r2²)/(2·d·r1)) 
            //   + r2²·arccos((d² + r2² - r1²)/(2·d·r2))
            //   - 0.5·√((r1+r2+d)(-r1+r2+d)(r1-r2+d)(r1+r2-d))

            double d2 = d * d;
            double r1_2 = r1 * r1;
            double r2_2 = r2 * r2;

            double arg1 = (d2 + r1_2 - r2_2) / (2.0 * d * r1);
            double arg2 = (d2 + r2_2 - r1_2) / (2.0 * d * r2);

            // Clamp to avoid numerical issues with arccos
            arg1 = Math.Max(-1.0, Math.Min(1.0, arg1));
            arg2 = Math.Max(-1.0, Math.Min(1.0, arg2));

            double part1 = r1_2 * Math.Acos(arg1);
            double part2 = r2_2 * Math.Acos(arg2);

            // Triangle area using Heron's formula component
            double s1 = r1 + r2 + d;
            double s2 = -r1 + r2 + d;
            double s3 = r1 - r2 + d;
            double s4 = r1 + r2 - d;
            double trianglePart = 0.5 * Math.Sqrt(Math.Max(0, s1 * s2 * s3 * s4));

            return part1 + part2 - trianglePart;
        }

        #endregion

        #region VSI Calculation (Valley Shielding Index)

        /// <summary>
        /// Calculate Valley Shielding Index
        /// 
        /// VSI = 1 - (sphere_coverage / target_area)
        /// 
        /// Measures fraction of target that is "valley" (not covered by spheres)
        /// Higher VSI = more valley visible = better for low valley dose
        /// Range: [0, 1]
        /// </summary>
        private double CalculateVSI(List<Circle2D> spheres, Circle2D target)
        {
            if (spheres.Count == 0) return 1.0;

            double targetArea = target.Area;

            // Calculate union area of sphere projections
            // First, get total sphere area
            double totalSphereArea = spheres.Sum(s => s.Area);

            // Subtract pairwise overlaps to approximate union
            double overlapArea = 0;
            for (int i = 0; i < spheres.Count; i++)
            {
                for (int j = i + 1; j < spheres.Count; j++)
                {
                    overlapArea += CircleIntersectionArea(spheres[i], spheres[j]);
                }
            }

            double sphereUnionArea = totalSphereArea - overlapArea;

            // Clamp sphere coverage to target area (can't cover more than 100%)
            double sphereCoverage = Math.Min(sphereUnionArea, targetArea);

            // VSI = fraction of target that is valley
            double vsi = 1.0 - (sphereCoverage / targetArea);

            return Math.Max(0.0, Math.Min(1.0, vsi));
        }

        #endregion

        #region SSI Calculation (Sphere Spread Index)

        /// <summary>
        /// Calculate Sphere Spread Index (angle-independent)
        /// 
        /// SSI = 0.5 × VolumeScore + 0.5 × AlignmentScore
        /// 
        /// - VolumeScore: How well do spheres fill the target volume?
        /// - AlignmentScore: Are spheres centered in target?
        /// 
        /// Range: [0, 1]
        /// </summary>
        private (double ssi, double volumeComponent, double alignmentComponent) CalculateSSI(
            List<ExtractedSphere> spheres,
            TargetInfo target)
        {
            if (spheres.Count == 0)
                return (0.0, 0.0, 0.0);

            // Volume component
            double avgRadius = spheres.Average(s => s.Radius);
            double singleSphereVolume = (4.0 / 3.0) * Math.PI * Math.Pow(avgRadius, 3);
            double totalSphereVolume_mm3 = spheres.Count * singleSphereVolume;
            double totalSphereVolume_cc = totalSphereVolume_mm3 / 1000.0;

            double volumeRatio = totalSphereVolume_cc / target.Volume;

            // Normalize volume ratio: optimal is around 0.2-0.4 for SFRT
            // Map [0, 0.5] -> [0, 1], cap at 1.0
            double volumeScore = Math.Min(volumeRatio / 0.4, 1.0);

            // Alignment component
            // Calculate centroid of all spheres
            double centroidX = spheres.Average(s => s.CenterX);
            double centroidY = spheres.Average(s => s.CenterY);
            double centroidZ = spheres.Average(s => s.CenterZ);

            // Distance from sphere centroid to target center
            double dx = centroidX - target.CenterX;
            double dy = centroidY - target.CenterY;
            double dz = centroidZ - target.CenterZ;
            double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // Normalize by target radius
            double alignmentScore = 1.0 - Math.Min(distance / target.Radius, 1.0);

            // Combined SSI
            double ssi = 0.5 * volumeScore + 0.5 * alignmentScore;

            return (ssi, volumeScore, alignmentScore);
        }

        #endregion

        #region OSI Calculation (OAR Sparing Index)

        /// <summary>
        /// Calculate OAR Sparing Index
        /// 
        /// OSI = 1 - (sphere_OAR_overlap / OAR_area)
        /// 
        /// Measures how much of OAR is NOT overlapped by sphere projections
        /// Higher OSI = OAR avoids spheres = better sparing
        /// Range: [0, 1]
        /// </summary>
        private double CalculateOSI(List<Circle2D> spheres, Ellipse2D oar)
        {
            if (spheres.Count == 0) return 1.0;

            double oarArea = oar.Area;
            if (oarArea <= 0) return 1.0;

            // Calculate total overlap between spheres and OAR
            double totalOverlap = 0;

            foreach (var sphere in spheres)
            {
                totalOverlap += CircleEllipseIntersectionArea(sphere, oar);
            }

            // Cap overlap at OAR area
            totalOverlap = Math.Min(totalOverlap, oarArea);

            // OSI = fraction of OAR NOT overlapped by spheres
            double osi = 1.0 - (totalOverlap / oarArea);

            return Math.Max(0.0, Math.Min(1.0, osi));
        }

        /// <summary>
        /// Approximate intersection area between circle and ellipse
        /// 
        /// Uses a simplified approach: treat ellipse as circle with equivalent radius
        /// for overlap calculation. This is an approximation but sufficient for
        /// comparative optimization.
        /// </summary>
        private double CircleEllipseIntersectionArea(Circle2D circle, Ellipse2D ellipse)
        {
            // Distance between centers
            double dx = circle.U - ellipse.U;
            double dy = circle.V - ellipse.V;

            // Normalize distance by ellipse radii to check if circle center is "inside" ellipse
            double normalizedDist = Math.Sqrt(
                (dx * dx) / (ellipse.RadiusU * ellipse.RadiusU) +
                (dy * dy) / (ellipse.RadiusV * ellipse.RadiusV)
            );

            // Approximate ellipse as circle with geometric mean radius for intersection calc
            double ellipseEquivRadius = Math.Sqrt(ellipse.RadiusU * ellipse.RadiusV);

            // Actual center-to-center distance
            double centerDist = Math.Sqrt(dx * dx + dy * dy);

            // Use circle-circle intersection with equivalent ellipse radius
            double r1 = circle.Radius;
            double r2 = ellipseEquivRadius;

            // No overlap
            if (centerDist >= r1 + r2)
                return 0.0;

            // Circle completely inside ellipse (approximately)
            if (normalizedDist + circle.Radius / ellipseEquivRadius <= 1.0)
                return circle.Area;

            // Ellipse completely inside circle (rare for OARs)
            if (centerDist + r2 <= r1)
                return ellipse.Area;

            // Partial overlap - use circle-circle approximation
            // Scale result by ellipse aspect ratio correction
            double aspectRatio = ellipse.RadiusU / ellipse.RadiusV;
            double aspectCorrection = 2.0 / (aspectRatio + 1.0 / aspectRatio);

            // Calculate as two circles
            var circleAsCircle = new Circle2D(circle.U, circle.V, r1);
            var ellipseAsCircle = new Circle2D(ellipse.U, ellipse.V, r2);

            double rawOverlap = CircleIntersectionArea(circleAsCircle, ellipseAsCircle);

            return rawOverlap * aspectCorrection;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Calculate standard deviation of a list of values
        /// </summary>
        private double CalculateStdDev(List<double> values)
        {
            if (values.Count < 2) return 0.0;

            double mean = values.Average();
            double sumSquaredDiff = values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSquaredDiff / (values.Count - 1));
        }

        #endregion
    }
}