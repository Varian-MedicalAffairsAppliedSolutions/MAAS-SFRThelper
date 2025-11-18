using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.Types;

namespace MAAS_SFRThelper.Models
{
    /// <summary>
    /// Represents a single OAR dose constraint with priority
    /// </summary>
    public class OARConstraint
    {
        /// <summary>
        /// The official structure name we'll use for display
        /// </summary>
        public string StructureName { get; set; }

        /// <summary>
        /// Different ways this structure might be named in Eclipse
        /// Examples: "Rectum", "Rectum_PRV", "RectumPRV"
        /// </summary>
        public List<string> NameVariations { get; set; }

        /// <summary>
        /// Maximum dose allowed in Gray (Gy)
        /// </summary>
        public double MaxDoseGy { get; set; }

        /// <summary>
        /// Volume percentage for dose constraint
        /// 0 = D0% (max dose), 50 = D50% (median), 95 = D95%, etc.
        /// </summary>
        public double VolumePercent { get; set; } = 0;

        /// <summary>
        /// Optimization priority (higher = more important)
        /// Typical range: 70-150
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Upper or Lower constraint (usually Upper for OARs to keep dose below limit)
        /// </summary>
        public OptimizationObjectiveOperator Operator { get; set; } = OptimizationObjectiveOperator.Upper;

        /// <summary>
        /// Human-readable description of this constraint
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Constructor - initializes the NameVariations list
        /// </summary>
        public OARConstraint()
        {
            NameVariations = new List<string>();
        }
    }

    /// <summary>
    /// Library of OAR constraints specifically for Prostate SFRT cases
    /// </summary>
    public static class ProstateOARConstraints
    {
        /// <summary>
        /// Gets the standard set of OAR constraints for prostate SFRT
        /// Based on clinical experience and literature
        /// </summary>
        public static List<OARConstraint> GetConstraints()
        {
            return new List<OARConstraint>
            {
                // Rectum - most critical OAR for prostate
                new OARConstraint
                {
                    StructureName = "Rectum",
                    NameVariations = new List<string>
                    {
                        "Rectum",
                        "Rectum_PRV",
                        "RectumPRV",
                        "RECTUM",
                        "Rect"
                    },
                    MaxDoseGy = 20.0,
                    VolumePercent = 0,  // D0% (max dose)
                    Priority = 120,
                    Description = "Rectum maximum dose constraint"
                },

                // Bladder
                new OARConstraint
                {
                    StructureName = "Bladder",
                    NameVariations = new List<string>
                    {
                        "Bladder",
                        "Bladder_PRV",
                        "BladderPRV",
                        "BLADDER",
                        "Blad"
                    },
                    MaxDoseGy = 25.0,
                    VolumePercent = 0,
                    Priority = 100,
                    Description = "Bladder maximum dose constraint"
                },

                // Left Femoral Head
                new OARConstraint
                {
                    StructureName = "FemoralHead_L",
                    NameVariations = new List<string>
                    {
                        "FemoralHead_L",
                        "Femoral_Head_L",
                        "FemHead_L",
                        "Lt_Femoral_Head",
                        "L_FemHead",
                        "FemoralHead_Lt",
                        "Left_FemHead"
                    },
                    MaxDoseGy = 18.0,
                    VolumePercent = 0,
                    Priority = 90,
                    Description = "Left femoral head maximum dose"
                },

                // Right Femoral Head
                new OARConstraint
                {
                    StructureName = "FemoralHead_R",
                    NameVariations = new List<string>
                    {
                        "FemoralHead_R",
                        "Femoral_Head_R",
                        "FemHead_R",
                        "Rt_Femoral_Head",
                        "R_FemHead",
                        "FemoralHead_Rt",
                        "Right_FemHead"
                    },
                    MaxDoseGy = 18.0,
                    VolumePercent = 0,
                    Priority = 90,
                    Description = "Right femoral head maximum dose"
                },

                // Penile Bulb
                new OARConstraint
                {
                    StructureName = "PenileBulb",
                    NameVariations = new List<string>
                    {
                        "PenileBulb",
                        "Penile_Bulb",
                        "PenBulb",
                        "PENILEBULB",
                        "Bulb"
                    },
                    MaxDoseGy = 15.0,
                    VolumePercent = 0,
                    Priority = 70,
                    Description = "Penile bulb maximum dose"
                },

                // Urethra
                new OARConstraint
                {
                    StructureName = "Urethra",
                    NameVariations = new List<string>
                    {
                        "Urethra",
                        "URETHRA",
                        "Urethra_PRV",
                        "UrethPRV"
                    },
                    MaxDoseGy = 25.0,
                    VolumePercent = 0,
                    Priority = 80,
                    Description = "Urethra maximum dose constraint"
                }
            };
        }
    }
}