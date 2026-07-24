using MAAS_SFRThelper.Models;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace MAAS_SFRThelper.Services
{
    /// <summary>
    /// Checks preconditions for influence-matrix extraction. Two tiers:
    /// blocking checks (extraction impossible) and warnings (extraction
    /// proceeds; scratch copies are static geometry-only fields, so
    /// existing fluence/dynamic MLC on the source plan is ignored).
    /// ESAPI-facing service; call on the ESAPI thread.
    /// </summary>
    public static class PlanEligibilityService
    {
        public static List<EligibilityCheck> CheckPlan(ExternalPlanSetup plan)
        {
            var checks = new List<EligibilityCheck>();

            if (plan == null)
            {
                checks.Add(new EligibilityCheck("External photon plan open", false,
                    "No external beam plan in the current context."));
                return checks; // nothing else is checkable
            }
            checks.Add(new EligibilityCheck("External photon plan open", true,
                $"Plan '{plan.Id}'."));

            // Refuses to run on a temporary working copy left behind by a
            // previous run (its name starts with the working-copy tag).
            // Running on one produces a meaningless "copy of a copy" run
            // that wastes hours (seen 2026-07-23).
            bool bLooksLikeScratch = plan.Id.StartsWith(
                PhotonCalculateInfluenceMatrix.PhotonInfluenceMatrixCalc.SCRATCH_PLAN_PREFIX,
                System.StringComparison.OrdinalIgnoreCase);
            checks.Add(new EligibilityCheck("Not a leftover working copy",
                !bLooksLikeScratch,
                bLooksLikeScratch
                    ? $"'{plan.Id}' looks like a temporary working copy from a previous run " +
                      "(the tool makes these itself and normally cleans them up). Open the " +
                      "original plan and press Refresh."
                    : "The open plan is not a leftover working copy."));

            checks.Add(new EligibilityCheck("Structure set attached",
                plan.StructureSet != null,
                plan.StructureSet != null
                    ? $"Structure set '{plan.StructureSet.Id}', {plan.StructureSet.Structures.Count()} structures."
                    : "Plan has no structure set."));

            var fields = plan.Beams.Where(b => !b.IsSetupField).ToList();
            checks.Add(new EligibilityCheck("Treatment fields present",
                fields.Count > 0,
                fields.Count > 0
                    ? $"{fields.Count} treatment field(s): {string.Join(", ", fields.Select(b => b.Id))}."
                    : "Plan contains no treatment fields (setup fields excluded)."));

            if (fields.Count > 0)
            {
                var noMlc = fields.Where(b => b.MLC == null).Select(b => b.Id).ToList();
                checks.Add(new EligibilityCheck("All fields have an MLC",
                    noMlc.Count == 0,
                    noMlc.Count == 0
                        ? "Every treatment field carries an MLC."
                        : $"No MLC on: {string.Join(", ", noMlc)}. Beamlet apertures are formed " +
                          "from MLC leaves; fit an MLC to the target in BEV to attach one."));

                var arcs = fields
                    .Where(b => b.GantryDirection != GantryDirection.None)
                    .Select(b => b.Id).ToList();
                checks.Add(new EligibilityCheck("Fixed-gantry fields (no arcs)",
                    arcs.Count == 0,
                    arcs.Count == 0
                        ? "All fields are fixed-gantry."
                        : $"Arc fields: {string.Join(", ", arcs)}. Arc/VMAT extraction " +
                          "is not supported yet (planned)."));

                var nonStatic = fields
                    .Where(b => b.GantryDirection == GantryDirection.None
                             && b.MLCPlanType != MLCPlanType.Static)
                    .Select(b => $"{b.Id} ({b.MLCPlanType})").ToList();
                checks.Add(new EligibilityCheck("Static MLC apertures",
                    nonStatic.Count == 0,
                    nonStatic.Count == 0
                        ? "All fields use a static MLC aperture."
                        : $"Non-static MLC: {string.Join(", ", nonStatic)}. Extraction will use " +
                          "static geometry-only copies of these fields; existing fluence and " +
                          "leaf motion are ignored.",
                    isBlocking: false));

                var halcyon = fields.Where(b =>
                {
                    string id = (b.TreatmentUnit?.Id ?? "").ToUpperInvariant();
                    string model = (b.TreatmentUnit?.MachineModel ?? "").ToUpperInvariant();
                    return id.Contains("HALCYON") || model.Contains("RDS");
                }).Select(b => b.Id).ToList();
                checks.Add(new EligibilityCheck("Supported machine (non-Halcyon)",
                    halcyon.Count == 0,
                    halcyon.Count == 0
                        ? "No Halcyon/RDS fields detected."
                        : $"Halcyon fields not supported: {string.Join(", ", halcyon)}."));
            }

            return checks;
        }

        /// <summary>Eligible = no blocking failures. Warnings do not block.</summary>
        public static bool IsEligible(IEnumerable<EligibilityCheck> checks)
            => checks != null && checks.Any()
               && !checks.Any(c => !c.Passed && c.IsBlocking);

        public static bool HasWarnings(IEnumerable<EligibilityCheck> checks)
            => checks != null && checks.Any(c => !c.Passed && !c.IsBlocking);
    }
}