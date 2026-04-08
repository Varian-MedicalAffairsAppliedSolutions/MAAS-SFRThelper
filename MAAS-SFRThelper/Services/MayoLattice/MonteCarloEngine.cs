using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.Types;

namespace MAAS_SFRThelper.Services.MayoLattice
{
    /// <summary>
    /// Monte Carlo engine for automated lattice point placement.
    /// 
    /// Implements the Metropolis Monte Carlo algorithm from Deufel et al. 2024,
    /// Phys. Med. Biol. 69, 075010. The algorithm finds the maximum number of
    /// lattice sphere centers that can be placed within the feasible volume
    /// while satisfying inter-sphere spacing constraints and preferring
    /// centralized placement within the GTV.
    /// 
    /// Algorithm overview:
    ///   1. Greedy initial guess respecting center-to-center spacing (Constraint 3)
    ///   2. Metropolis MC iterations to satisfy z-separation (Constraint 4)
    ///      with centralization objective (Equation 2)
    ///   3. Outer loop incrementing N until constraints can't be satisfied
    ///   4. Refinement MC pass on the maximum-N solution
    ///   5. Deterministic gradient walk toward nearest axial COMs
    /// </summary>
    public class MonteCarloEngine
    {
        private readonly List<VVector> _feasiblePoints;
        private readonly List<VVector> _coms;
        private readonly MayoConstraints _constraints;
        private readonly Random _rng;

        /// <summary>
        /// Progress callback: (statusMessage, progressFraction 0.0–1.0)
        /// </summary>
        public Action<string, double> OnProgress;

        /// <summary>
        /// Create a Monte Carlo engine for lattice placement.
        /// </summary>
        /// <param name="feasiblePoints">
        /// Array of feasible positions (PPossible). All points satisfy
        /// GTV and OAR margin constraints. Coordinates in mm.
        /// </param>
        /// <param name="coms">
        /// Axial slice centers of mass computed by SliceCOMCalculator.
        /// Used for the centralization objective. Coordinates in mm.
        /// </param>
        /// <param name="constraints">Algorithm parameters.</param>
        /// <param name="rng">Random number generator (seeded for reproducibility).</param>
        public MonteCarloEngine(List<VVector> feasiblePoints, List<VVector> coms,
            MayoConstraints constraints, Random rng)
        {
            _feasiblePoints = feasiblePoints ?? throw new ArgumentNullException(nameof(feasiblePoints));
            _coms = coms ?? throw new ArgumentNullException(nameof(coms));
            _constraints = constraints ?? throw new ArgumentNullException(nameof(constraints));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        // ═══════════════════════════════════════════════════════════════
        // Public API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Execute the full placement algorithm: find maximum N spheres,
        /// refine positions, and perform gradient walk.
        /// 
        /// Returns the final list of sphere center positions in mm.
        /// </summary>
        public List<VVector> FindOptimalPositions()
        {
            if (_feasiblePoints.Count == 0)
                return new List<VVector>();

            ReportProgress("Starting lattice placement...", 0.0);

            // ─── Phase 1: Find maximum number of spheres ───
            List<VVector> bestSolution = null;
            int maxN = 0;

            for (int n = 1; ; n++)
            {
                ReportProgress($"Trying N={n} spheres...", 0.1);

                // Build greedy initial guess for N spheres
                var initial = BuildInitialGuess(n);

                if (initial == null || initial.Count < n)
                {
                    // Can't even place N spheres with dcenter spacing alone
                    ReportProgress($"Cannot place {n} spheres. Maximum is {maxN}.", 0.3);
                    break;
                }

                // Run MC to satisfy Constraint 4 (z-separation)
                var (solution, allSatisfied) = RunMonteCarlo(initial, isRefinement: false);

                if (allSatisfied)
                {
                    bestSolution = solution;
                    maxN = n;
                    ReportProgress($"N={n}: all constraints satisfied.", 0.3);
                    // Continue to try N+1
                }
                else
                {
                    // Could not satisfy Constraint 4 for N spheres
                    // Fall back to N-1 (bestSolution)
                    ReportProgress($"N={n}: constraints not satisfiable. Using N={maxN}.", 0.3);
                    break;
                }
            }

            if (bestSolution == null || bestSolution.Count == 0)
            {
                ReportProgress("No valid placement found.", 1.0);
                return new List<VVector>();
            }

            // ─── Phase 2: Refinement MC pass ───
            ReportProgress($"Refining {maxN}-sphere solution...", 0.4);
            var (refined, _) = RunMonteCarlo(bestSolution, isRefinement: true);
            bestSolution = refined;

            // ─── Phase 3: Gradient walk toward COMs ───
            if (_constraints.UseGradientWalk)
            {
                ReportProgress("Gradient walk toward COMs...", 0.7);
                bestSolution = RunGradientWalk(bestSolution);
            }

            ReportProgress($"Placement complete: {bestSolution.Count} spheres.", 1.0);
            return bestSolution;
        }

        // ═══════════════════════════════════════════════════════════════
        // Step 1: Greedy initial guess (Equation 1 from paper)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Build an initial solution of N sphere positions using greedy
        /// sequential selection from the feasible points array.
        /// 
        /// P(1) = first element of PPossible.
        /// P(k) = first element in PPossible that is >= dcenter from all
        ///        previously placed positions P(1)..P(k-1).
        /// 
        /// This guarantees Constraint 3 (dcenter) but NOT Constraint 4
        /// (z-separation). The MC fixes Constraint 4.
        /// 
        /// Returns null if N positions cannot be placed.
        /// </summary>
        private List<VVector> BuildInitialGuess(int n)
        {
            if (n <= 0 || _feasiblePoints.Count == 0)
                return null;

            double dCenter = _constraints.DCenter;
            double dCenterSq = dCenter * dCenter;
            var positions = new List<VVector>(n);

            // P(1) = first feasible point (deterministic starting position)
            positions.Add(_feasiblePoints[0]);

            if (n == 1) return positions;

            // P(2..N): first feasible point that is >= dcenter from all existing
            for (int k = 1; k < n; k++)
            {
                bool found = false;

                for (int i = 0; i < _feasiblePoints.Count; i++)
                {
                    var candidate = _feasiblePoints[i];
                    bool valid = true;

                    // Check distance to all already-placed positions
                    for (int j = 0; j < positions.Count; j++)
                    {
                        if (DistanceSquared(candidate, positions[j]) < dCenterSq)
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (valid)
                    {
                        positions.Add(candidate);
                        found = true;
                        break; // take the first valid point (paper: "initial element in the array is always chosen")
                    }
                }

                if (!found)
                {
                    // Cannot place the k-th sphere
                    return null;
                }
            }

            return positions;
        }

        // ═══════════════════════════════════════════════════════════════
        // Step 2: Monte Carlo iterations
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Run Monte Carlo iterations to satisfy Constraint 4 and
        /// optimize centralization.
        /// 
        /// During each iteration, for each sphere:
        ///   1. Propose a random candidate position from PPossible
        ///   2. Accept if all constraints (3 and 4) are satisfied
        ///   3. Apply Metropolis criterion for centralization (Equation 2)
        /// 
        /// When isRefinement=false: terminates early when all constraints satisfied.
        /// When isRefinement=true: always runs full iteration count (centralization only).
        /// 
        /// Returns (positions, allConstraintsSatisfied).
        /// </summary>
        private (List<VVector>, bool) RunMonteCarlo(List<VVector> initialPositions,
            bool isRefinement)
        {
            int n = initialPositions.Count;
            int maxIter = _constraints.MaxIterations;
            int feasibleCount = _feasiblePoints.Count;

            // Work on a mutable copy
            var positions = new List<VVector>(initialPositions);

            bool allSatisfied = CheckAllConstraints(positions);

            // If already satisfied and not a refinement pass, return immediately
            if (allSatisfied && !isRefinement)
                return (positions, true);

            double progressStart = isRefinement ? 0.4 : 0.1;
            double progressEnd = isRefinement ? 0.7 : 0.3;

            for (int iter = 0; iter < maxIter; iter++)
            {
                // Progress reporting every 10,000 iterations
                if (iter % 10000 == 0)
                {
                    double frac = progressStart + (progressEnd - progressStart) * ((double)iter / maxIter);
                    ReportProgress($"MC iter {iter}/{maxIter} (N={n}, {(isRefinement ? "refinement" : "search")})", frac);
                }

                for (int i = 0; i < n; i++)
                {
                    // Propose random candidate from feasible points
                    int randIndex = _rng.Next(feasibleCount);
                    var candidate = _feasiblePoints[randIndex];

                    // ─── Check Constraint 3: dcenter ───
                    if (!SatisfiesDCenter(candidate, positions, i))
                        continue;

                    // ─── Check Constraint 4: z-separation / co-axial ───
                    if (!SatisfiesZSeparation(candidate, positions, i))
                        continue;

                    // ─── Centralization objective (Equation 2) ───
                    if (!AcceptByCentralization(candidate, positions[i]))
                        continue;

                    // Accept the move
                    positions[i] = candidate;
                }

                // Check for early termination (constraint satisfaction pass only)
                if (!isRefinement)
                {
                    allSatisfied = CheckAllConstraints(positions);
                    if (allSatisfied)
                        return (positions, true);
                }
            }

            // End of iterations
            if (!isRefinement)
            {
                allSatisfied = CheckAllConstraints(positions);
            }
            else
            {
                allSatisfied = true; // refinement assumes constraints are already met
            }

            return (positions, allSatisfied);
        }

        // ═══════════════════════════════════════════════════════════════
        // Step 3: Gradient walk toward COMs
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Deterministic final step that pushes each sphere as close to
        /// its nearest axial COM as possible without violating constraints.
        /// 
        /// For each sphere, the path to its nearest COM is divided into
        /// discrete steps. At each step, the sphere moves toward the COM
        /// if the move keeps all constraints satisfied.
        /// 
        /// Paper: "The vectors were then divided into 10 discrete steps.
        /// The elements in P step towards their closest COM as long as
        /// the constraints in table 1 are not violated."
        /// 
        /// The step order is sequential: sphere 1 moves one step, then
        /// sphere 2, etc. Then repeat for all steps. Earlier spheres get
        /// priority for central positions.
        /// </summary>
        private List<VVector> RunGradientWalk(List<VVector> positions)
        {
            int n = positions.Count;
            int numSteps = _constraints.GradientWalkSteps;

            // Work on mutable copy
            var result = new List<VVector>(positions);

            if (_coms.Count == 0 || n == 0)
                return result;

            for (int step = 0; step < numSteps; step++)
            {
                double frac = 0.7 + 0.25 * ((double)step / numSteps);
                ReportProgress($"Gradient walk step {step + 1}/{numSteps}", frac);

                for (int i = 0; i < n; i++)
                {
                    var current = result[i];

                    // Find nearest COM
                    var nearestCOM = FindNearestCOM(current);
                    if (nearestCOM == null) continue;
                    var com = nearestCOM.Value;

                    // Compute direction vector from current position to COM
                    double dx = com.x - current.x;
                    double dy = com.y - current.y;
                    double dz = com.z - current.z;

                    // One step = 1/numSteps of the total distance
                    double stepFrac = 1.0 / numSteps;
                    var candidate = new VVector(
                        current.x + dx * stepFrac,
                        current.y + dy * stepFrac,
                        current.z + dz * stepFrac);

                    // Snap candidate to nearest feasible point
                    // (ensures the path stays within the feasible volume)
                    var snapped = FindNearestFeasiblePoint(candidate);
                    if (snapped == null) continue;
                    candidate = snapped.Value;

                    // Accept only if all constraints remain satisfied
                    if (SatisfiesDCenter(candidate, result, i) &&
                        SatisfiesZSeparation(candidate, result, i))
                    {
                        result[i] = candidate;
                    }
                    // If constraints would be violated, sphere stays put
                }
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        // Constraint checking
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Check Constraint 3: candidate position must be >= dcenter
        /// from all other sphere positions (excluding the sphere at
        /// index excludeIndex).
        /// </summary>
        private bool SatisfiesDCenter(VVector candidate, List<VVector> positions, int excludeIndex)
        {
            double dCenterSq = _constraints.DCenter * _constraints.DCenter;

            for (int j = 0; j < positions.Count; j++)
            {
                if (j == excludeIndex) continue;
                if (DistanceSquared(candidate, positions[j]) < dCenterSq)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check Constraint 4: if candidate and any other sphere have
        /// z-coordinates within ZSep, their Euclidean distance must be
        /// >= DCoAxial. Excludes the sphere at excludeIndex.
        /// 
        /// Table 1: If |z1 - z2| &lt; zsep then distance >= dco-axial.
        /// </summary>
        private bool SatisfiesZSeparation(VVector candidate, List<VVector> positions, int excludeIndex)
        {
            double zSep = _constraints.ZSep;
            double dCoAxialSq = _constraints.DCoAxial * _constraints.DCoAxial;

            for (int j = 0; j < positions.Count; j++)
            {
                if (j == excludeIndex) continue;

                double dz = Math.Abs(candidate.z - positions[j].z);

                if (dz < zSep)
                {
                    // Co-planar: require stricter distance
                    if (DistanceSquared(candidate, positions[j]) < dCoAxialSq)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Check all pairwise constraints (3 and 4) for the current solution.
        /// Returns true if every pair of spheres satisfies both constraints.
        /// </summary>
        private bool CheckAllConstraints(List<VVector> positions)
        {
            int n = positions.Count;
            double dCenterSq = _constraints.DCenter * _constraints.DCenter;
            double zSep = _constraints.ZSep;
            double dCoAxialSq = _constraints.DCoAxial * _constraints.DCoAxial;

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double distSq = DistanceSquared(positions[i], positions[j]);

                    // Constraint 3
                    if (distSq < dCenterSq)
                        return false;

                    // Constraint 4
                    double dz = Math.Abs(positions[i].z - positions[j].z);
                    if (dz < zSep && distSq < dCoAxialSq)
                        return false;
                }
            }

            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // Centralization objective (Equation 2)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Metropolis acceptance criterion for centralization.
        /// 
        /// Equation 2: Accept if x > 1 - exp(-mu * deltaCOM)
        /// where x ~ Uniform(0,1) and deltaCOM is the difference in
        /// minimum distance to any COM (candidate minus current).
        /// 
        /// When candidate is closer to a COM (deltaCOM &lt; 0): always accept.
        /// When candidate is farther (deltaCOM > 0): accept with probability
        /// exp(-mu * deltaCOM), decreasing exponentially with distance.
        /// 
        /// Mu is in cm-space (paper units). deltaCOM is in mm (ESAPI units).
        /// The conversion deltaCOM_cm = deltaCOM_mm / 10 is applied internally.
        /// </summary>
        private bool AcceptByCentralization(VVector candidate, VVector current)
        {
            if (_coms.Count == 0)
                return true; // no COMs — accept any valid move

            double candidateMinDist = MinDistanceToCOM(candidate);
            double currentMinDist = MinDistanceToCOM(current);

            // deltaCOM > 0 means candidate is farther from COM
            double deltaCOM_mm = candidateMinDist - currentMinDist;

            // Convert to cm for compatibility with Mu (paper units)
            double deltaCOM_cm = deltaCOM_mm / 10.0;

            // Equation 2: x > 1 - exp(-mu * deltaCOM)
            double threshold = 1.0 - Math.Exp(-_constraints.Mu * deltaCOM_cm);
            double x = _rng.NextDouble();

            return x > threshold;
        }

        /// <summary>
        /// Compute the minimum Euclidean distance from a point to any COM.
        /// </summary>
        private double MinDistanceToCOM(VVector point)
        {
            double minDist = double.MaxValue;

            for (int i = 0; i < _coms.Count; i++)
            {
                double dist = Distance(point, _coms[i]);
                if (dist < minDist)
                    minDist = dist;
            }

            return minDist;
        }

        // ═══════════════════════════════════════════════════════════════
        // Nearest point lookups
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Find the nearest COM to a given point.
        /// Returns null if no COMs exist.
        /// </summary>
        private VVector? FindNearestCOM(VVector point)
        {
            if (_coms.Count == 0) return null;

            VVector nearest = _coms[0];
            double minDistSq = DistanceSquared(point, nearest);

            for (int i = 1; i < _coms.Count; i++)
            {
                double distSq = DistanceSquared(point, _coms[i]);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = _coms[i];
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find the feasible point nearest to the given position.
        /// Used by the gradient walk to snap moved positions back
        /// onto the feasible point array (ensuring the path stays
        /// within the feasible volume).
        /// 
        /// Returns null if no feasible points exist.
        /// </summary>
        private VVector? FindNearestFeasiblePoint(VVector target)
        {
            if (_feasiblePoints.Count == 0) return null;

            VVector nearest = _feasiblePoints[0];
            double minDistSq = DistanceSquared(target, nearest);

            for (int i = 1; i < _feasiblePoints.Count; i++)
            {
                double distSq = DistanceSquared(target, _feasiblePoints[i]);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = _feasiblePoints[i];
                }
            }

            return nearest;
        }

        // ═══════════════════════════════════════════════════════════════
        // Distance utilities
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Squared Euclidean distance between two points.
        /// Avoids the square root for constraint comparisons.
        /// </summary>
        private double DistanceSquared(VVector a, VVector b)
        {
            double dx = a.x - b.x;
            double dy = a.y - b.y;
            double dz = a.z - b.z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// Euclidean distance between two points.
        /// Used where actual distance values are needed (COM distances).
        /// </summary>
        private double Distance(VVector a, VVector b)
        {
            return Math.Sqrt(DistanceSquared(a, b));
        }

        // ═══════════════════════════════════════════════════════════════
        // Progress reporting
        // ═══════════════════════════════════════════════════════════════

        private void ReportProgress(string message, double fraction)
        {
            OnProgress?.Invoke(message, Math.Max(0.0, Math.Min(1.0, fraction)));
        }
    }
}