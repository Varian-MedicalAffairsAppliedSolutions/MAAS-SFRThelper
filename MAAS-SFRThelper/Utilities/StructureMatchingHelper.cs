using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace MAAS_SFRThelper.Utilities
{
    /// <summary>
    /// Helper class for finding structures with flexible name matching
    /// Handles variations in structure naming across different clinics
    /// </summary>
    public static class StructureMatchingHelper
    {
        /// <summary>
        /// Find a structure that matches any of the provided name variations
        /// Tries exact matches first, then partial matches
        /// </summary>
        /// <param name="structureSet">The structure set to search in</param>
        /// <param name="nameVariations">List of acceptable names (e.g., "Rectum", "RectumPRV")</param>
        /// <param name="requireNonEmpty">If true, only return structures that have contours</param>
        /// <returns>The first matching structure, or null if not found</returns>
        public static Structure FindStructureByNameVariations(
            StructureSet structureSet,
            List<string> nameVariations,
            bool requireNonEmpty = true)
        {
            // Safety checks - make sure we have valid inputs
            if (structureSet == null || nameVariations == null || !nameVariations.Any())
                return null;

            // STRATEGY 1: Try exact matches first (fastest and most accurate)
            foreach (var name in nameVariations)
            {
                var structure = structureSet.Structures.FirstOrDefault(s =>
                    s.Id.Equals(name, StringComparison.OrdinalIgnoreCase));

                // Found a match! But check if we require it to have contours
                if (structure != null && (!requireNonEmpty || !structure.IsEmpty))
                    return structure;
            }

            // STRATEGY 2: Try partial matches (contains) - more flexible
            foreach (var name in nameVariations)
            {
                var structure = structureSet.Structures.FirstOrDefault(s =>
                    s.Id.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);

                if (structure != null && (!requireNonEmpty || !structure.IsEmpty))
                    return structure;
            }

            // Nothing found
            return null;
        }
    }
}