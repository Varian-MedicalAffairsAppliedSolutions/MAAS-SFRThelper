using MAAS_SFRThelper.Models;
using MAAS_SFRThelper.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace MAAS_SFRThelper.Services
{
    /// <summary>
    /// Creates optimization objectives for SFRT plans
    /// Handles peak (lattice) and valley (low-dose) structure objectives
    /// </summary>
    public class OptimizationObjectiveCreator
    {
        private ExternalPlanSetup _plan;
        private StructureSet _structureSet;

        /// <summary>
        /// Constructor - requires a plan and structure set to work with
        /// </summary>
        public OptimizationObjectiveCreator(ExternalPlanSetup plan, StructureSet structureSet)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _structureSet = structureSet ?? throw new ArgumentNullException(nameof(structureSet));
        }

        /// <summary>
        /// Main method: Creates all optimization objectives based on template values
        /// </summary>
        /// <param name="latticeStructureId">Name of the peak/lattice structure</param>
        /// <param name="valleyStructureId">Name of the valley structure (or null to skip)</param>
        /// <param name="ptvStructureId">Name of PTV (only used if valley needs to be created)</param>
        /// <param name="template">Template with all dose/priority values</param>
        /// <returns>Status message string</returns>
        public string CreateObjectives(
            string latticeStructureId,
            string valleyStructureId,
            string ptvStructureId,
            OptimizationTemplate template)
        {
            string result = "=== Creating SFRT Optimization Objectives ===\n";

            // Step 1: Clear existing objectives
            result += ClearExistingObjectives();

            // Step 2: Find/create valley structure if needed
            Structure valleyStructure = null;
            if (!string.IsNullOrEmpty(valleyStructureId))
            {
                if (valleyStructureId == "[Auto-create Valley]")
                {
                    // Create valley = PTV - Lattice
                    var createResult = CreateValleyStructure(latticeStructureId, ptvStructureId);
                    result += createResult.message;
                    valleyStructure = createResult.structure;
                }
                else
                {
                    // Use existing valley structure
                    valleyStructure = FindStructureById(valleyStructureId);
                    if (valleyStructure != null)
                    {
                        result += $"\nUsing existing valley structure: {valleyStructure.Id}";
                    }
                }
            }

            // Step 3: Find lattice structure
            Structure latticeStructure = FindStructureById(latticeStructureId);
            if (latticeStructure == null)
            {
                result += $"\nERROR: Lattice structure '{latticeStructureId}' not found!";
                return result;
            }
            result += $"\nUsing lattice structure: {latticeStructure.Id}";

            // Step 4: Add peak objectives (lattice/spheres)
            result += AddPeakObjectives(latticeStructure, template);

            // Step 5: Add valley objectives (if we have a valley structure)
            if (valleyStructure != null)
            {
                result += AddValleyObjectives(valleyStructure, template);
            }
            else
            {
                result += "\nSkipping valley objectives (no valley structure specified)";
            }

            // Step 6: Add OAR objectives (prostate-specific for now)
            result += AddOARObjectives();

            // Step 7: Add normal tissue objective
            result += AddNormalTissueObjective();

            result += "\n=== Objective Creation Complete ===";
            return result;
        }

        /// <summary>
        /// Remove all existing optimization objectives from the plan
        /// </summary>
        private string ClearExistingObjectives()
        {
            var objectives = _plan.OptimizationSetup.Objectives.ToList();
            foreach (var obj in objectives)
            {
                _plan.OptimizationSetup.RemoveObjective(obj);
            }
            return $"\nCleared {objectives.Count} existing objectives";
        }

        /// <summary>
        /// Find a structure by exact ID match (case-insensitive)
        /// </summary>
        private Structure FindStructureById(string structureId)
        {
            if (string.IsNullOrEmpty(structureId))
                return null;

            return _structureSet.Structures.FirstOrDefault(s =>
                s.Id.Equals(structureId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Create valley structure by subtracting lattice from PTV
        /// Follows the pattern from your sphere code for structure creation
        /// </summary>
        private (string message, Structure structure) CreateValleyStructure(string latticeId, string ptvId)
        {
            string msg = "\n--- Creating Valley Structure ---";

            // Find lattice structure
            Structure lattice = FindStructureById(latticeId);
            if (lattice == null)
            {
                msg += $"\nERROR: Cannot create valley - lattice structure '{latticeId}' not found";
                return (msg, null);
            }

            // Find PTV structure
            Structure ptv = FindStructureById(ptvId);
            if (ptv == null)
            {
                msg += $"\nERROR: Cannot create valley - PTV structure '{ptvId}' not found";
                return (msg, null);
            }

            // Check if Valley structure already exists (your pattern)
            Structure valley = null;
            if (_structureSet.Structures.Any(st => st.Id.Equals("Valley", StringComparison.OrdinalIgnoreCase)))
            {
                valley = _structureSet.Structures.First(st => st.Id.Equals("Valley", StringComparison.OrdinalIgnoreCase));
                _structureSet.RemoveStructure(valley);
                msg += "\nRemoved existing Valley structure";
            }

            // Create new Valley structure
            valley = _structureSet.AddStructure("CONTROL", "Valley");
            msg += "\nCreated new Valley structure";

            // CRITICAL: Convert valley to high-resolution to match lattice
            valley.ConvertToHighResolution();
            msg += "\nConverted Valley to high-resolution";

            // Check if PTV needs conversion (following your sphere code pattern)
            if (!ptv.IsHighResolution)
            {
                msg += $"\nWARNING: PTV '{ptv.Id}' is not high-resolution. Creating temporary high-res copy...";

                // Create temporary high-res PTV copy
                Structure ptvHighRes = _structureSet.AddStructure("PTV", "TempPTV_HiRes");

                // Copy contours from low-res to high-res (your pattern from sphere code)
                for (int z = 0; z < _structureSet.Image.ZSize; z++)
                {
                    var contours = ptv.GetContoursOnImagePlane(z);
                    foreach (var contour in contours)
                    {
                        if (contour.Length > 0)
                        {
                            ptvHighRes.AddContourOnImagePlane(contour, z);
                        }
                    }
                }

                ptvHighRes.ConvertToHighResolution();
                msg += "\nCreated high-resolution PTV copy";

                // Valley = High-res PTV - Lattice
                valley.SegmentVolume = ptvHighRes.Sub(lattice);
                msg += $"\nValley = {ptvHighRes.Id} - {lattice.Id}";

                // Clean up temporary structure
                _structureSet.RemoveStructure(ptvHighRes);
                msg += "\nRemoved temporary PTV structure";
            }
            else
            {
                // PTV is already high-res, can do boolean operation directly
                valley.SegmentVolume = ptv.Sub(lattice);
                msg += $"\nValley = {ptv.Id} - {lattice.Id}";
            }

            msg += $"\nValley volume: {valley.Volume:F2} cc";

            return (msg, valley);
        }
        /// <summary>
        /// Add optimization objectives for peak structure (lattice/spheres)
        /// Goal: Drive HIGH dose into these structures
        /// </summary>
        private string AddPeakObjectives(Structure latticeStructure, OptimizationTemplate template)
        {
            string result = "\n--- Adding Peak Objectives (Lattice) ---";

            try
            {
                // Lower dose objective: Minimum dose to ensure coverage
                _plan.OptimizationSetup.AddPointObjective(
                    latticeStructure,
                    OptimizationObjectiveOperator.Lower,
                    new DoseValue(template.PeakLowerDose, DoseValue.DoseUnit.Gy),
                    template.PeakLowerVolume,
                    template.PeakLowerPriority);

                result += $"\n  Lower: {template.PeakLowerDose} Gy @ {template.PeakLowerVolume}% (Priority {template.PeakLowerPriority})";

                // Mean dose objective: Drive high average dose
                _plan.OptimizationSetup.AddMeanDoseObjective(
                    latticeStructure,
                    new DoseValue(template.PeakMeanDose, DoseValue.DoseUnit.Gy),
                    template.PeakMeanPriority);

                result += $"\n  Mean:  {template.PeakMeanDose} Gy (Priority {template.PeakMeanPriority})";
            }
            catch (Exception ex)
            {
                result += $"\n  ERROR adding peak objectives: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Add optimization objectives for valley structure (voids/low-dose regions)
        /// Goal: Keep dose LOW in these structures
        /// </summary>
        private string AddValleyObjectives(Structure valleyStructure, OptimizationTemplate template)
        {
            string result = "\n--- Adding Valley Objectives (Low Dose) ---";

            try
            {
                // Upper dose objective: Maximum dose limit
                _plan.OptimizationSetup.AddPointObjective(
                    valleyStructure,
                    OptimizationObjectiveOperator.Upper,
                    new DoseValue(template.ValleyUpperDose, DoseValue.DoseUnit.Gy),
                    template.ValleyUpperVolume,
                    template.ValleyUpperPriority);

                result += $"\n  Upper: {template.ValleyUpperDose} Gy @ {template.ValleyUpperVolume}% (Priority {template.ValleyUpperPriority})";

                // Mean dose objective: Keep average dose low
                _plan.OptimizationSetup.AddMeanDoseObjective(
                    valleyStructure,
                    new DoseValue(template.ValleyMeanDose, DoseValue.DoseUnit.Gy),
                    template.ValleyMeanPriority);

                result += $"\n  Mean:  {template.ValleyMeanDose} Gy (Priority {template.ValleyMeanPriority})";
            }
            catch (Exception ex)
            {
                result += $"\n  ERROR adding valley objectives: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Add optimization objectives for organs at risk (OARs)
        /// Uses prostate-specific constraints for now
        /// </summary>
        private string AddOARObjectives()
        {
            string result = "\n--- Adding OAR Objectives ---";

            // Load standard prostate OAR constraints
            var constraints = ProstateOARConstraints.GetConstraints();
            int addedCount = 0;

            foreach (var constraint in constraints)
            {
                // Try to find this OAR using name variations
                var oarStructure = StructureMatchingHelper.FindStructureByNameVariations(
                    _structureSet,
                    constraint.NameVariations,
                    requireNonEmpty: true);

                if (oarStructure == null)
                {
                    result += $"\n  {constraint.StructureName}: not found (skipping)";
                    continue;
                }

                try
                {
                    // Add upper dose constraint for this OAR
                    _plan.OptimizationSetup.AddPointObjective(
                        oarStructure,
                        constraint.Operator,
                        new DoseValue(constraint.MaxDoseGy, DoseValue.DoseUnit.Gy),
                        constraint.VolumePercent,
                        constraint.Priority);

                    result += $"\n  {oarStructure.Id}: Max {constraint.MaxDoseGy} Gy (Priority {constraint.Priority})";
                    addedCount++;
                }
                catch (Exception ex)
                {
                    result += $"\n  ERROR adding {oarStructure.Id}: {ex.Message}";
                }
            }

            result += $"\nAdded {addedCount} of {constraints.Count} OAR objectives";
            return result;
        }

        /// <summary>
        /// Add automatic normal tissue objective
        /// Helps with dose falloff and conformality
        /// </summary>
        private string AddNormalTissueObjective()
        {
            string result = "\n--- Adding Normal Tissue Objective ---";

            try
            {
                _plan.OptimizationSetup.AddAutomaticNormalTissueObjective(100);
                result += "\n  Automatic NTO added (Priority 100)";
            }
            catch (Exception ex)
            {
                result += $"\n  ERROR adding NTO: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Get available lattice structures from the structure set
        /// Uses intelligent filtering to find likely lattice candidates
        /// </summary>
        public static List<string> GetAvailableLatticeStructures(StructureSet structureSet)
        {
            var latticeStructures = new List<string>();

            if (structureSet == null)
                return latticeStructures;

            // Pattern matching for lattice structures
            var patterns = new[]
            {
                "LatticeHex", "LatticeRect", "LatticeAltCubic", "CVT3D",  // Specific names from your sphere code
                "Lattice", "CVT", "Sphere"  // Broader patterns
            };

            foreach (var structure in structureSet.Structures.Where(s => !s.IsEmpty))
            {
                // Check if structure matches any pattern
                bool matchesPattern = patterns.Any(p =>
                    structure.Id.StartsWith(p, StringComparison.OrdinalIgnoreCase));

                // Also include structures with DicomType containing "PTV" or "GTV"
                bool isPTV = structure.DicomType != null &&
                    (structure.DicomType.Contains("PTV") || structure.DicomType.Contains("GTV"));

                if (matchesPattern || isPTV)
                {
                    latticeStructures.Add(structure.Id);
                }
            }

            return latticeStructures.Distinct().OrderBy(s => s).ToList();
        }

        /// <summary>
        /// Get available valley structures from the structure set
        /// Includes option to auto-create valley
        /// </summary>
        public static List<string> GetAvailableValleyStructures(StructureSet structureSet)
        {
            var valleyStructures = new List<string>();

            // Always include auto-create option first
            valleyStructures.Add("[Auto-create Valley]");

            if (structureSet == null)
                return valleyStructures;

            // Look for common void/valley structure names
            var voidNames = new[] { "coreVoid", "Voids", "Valley", "LowDose" };

            foreach (var name in voidNames)
            {
                var structure = structureSet.Structures.FirstOrDefault(s =>
                    s.Id.Equals(name, StringComparison.OrdinalIgnoreCase) && !s.IsEmpty);

                if (structure != null)
                {
                    valleyStructures.Add(structure.Id);
                }
            }

            return valleyStructures;
        }

        /// <summary>
        /// Get available PTV structures from the structure set
        /// Used when auto-creating valley structure
        /// </summary>
        public static List<string> GetAvailablePTVStructures(StructureSet structureSet)
        {
            var ptvStructures = new List<string>();

            if (structureSet == null)
                return ptvStructures;

            // Find structures with PTV in DicomType or ID
            foreach (var structure in structureSet.Structures.Where(s => !s.IsEmpty))
            {
                bool isPTV = structure.DicomType != null &&
                (structure.DicomType.IndexOf("PTV", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 structure.DicomType.IndexOf("GTV", StringComparison.OrdinalIgnoreCase) >= 0);

                if (isPTV)
                {
                    ptvStructures.Add(structure.Id);
                }
            }

            return ptvStructures.Distinct().OrderBy(s => s).ToList();
        }
    }
}