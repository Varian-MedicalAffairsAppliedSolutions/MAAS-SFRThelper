using System;

namespace MAAS_SFRThelper.Models
{
    /// <summary>
    /// Represents a template for SFRT optimization objectives
    /// Contains all dose values and priorities for peak and valley structures
    /// </summary>
    public class OptimizationTemplate
    {
        // Template identification
        public string Name { get; set; }

        // Peak (Lattice) objectives - we want HIGH dose here
        public double PeakLowerDose { get; set; }          // Minimum dose to deliver (Gy)
        public double PeakLowerVolume { get; set; }        // Volume % that must receive this dose
        public int PeakLowerPriority { get; set; }         // How important this constraint is

        public double PeakMeanDose { get; set; }           // Target mean dose (Gy)
        public int PeakMeanPriority { get; set; }          // Priority for mean dose objective

        // Valley (Low dose) objectives - we want LOW dose here
        public double ValleyUpperDose { get; set; }        // Maximum dose allowed (Gy)
        public double ValleyUpperVolume { get; set; }      // Volume % (0 = max dose point)
        public int ValleyUpperPriority { get; set; }       // Priority for keeping dose low

        public double ValleyMeanDose { get; set; }         // Target mean dose (Gy)
        public int ValleyMeanPriority { get; set; }        // Priority for mean dose objective

        /// <summary>
        /// Default constructor - creates empty template
        /// </summary>
        public OptimizationTemplate()
        {
            // Initialize with safe defaults
            Name = "Custom";
            PeakLowerDose = 0;
            PeakLowerVolume = 100;
            PeakLowerPriority = 100;
            PeakMeanDose = 0;
            PeakMeanPriority = 100;
            ValleyUpperDose = 0;
            ValleyUpperVolume = 0;
            ValleyUpperPriority = 100;
            ValleyMeanDose = 0;
            ValleyMeanPriority = 100;
        }

        /// <summary>
        /// Creates the standard prostate SFRT template based on literature
        /// Reference: Ethos IOE paper - PVDR ~3.7, sphere coverage 98%
        /// </summary>
        public static OptimizationTemplate CreateStandardProstateTemplate()
        {
            return new OptimizationTemplate
            {
                Name = "Standard Prostate SFRT",

                // Peak objectives (Spheres/Lattice - HIGH dose)
                PeakLowerDose = 15.0,          // At least 15 Gy to ensure coverage
                PeakLowerVolume = 100.0,       // To 100% of sphere volume
                PeakLowerPriority = 140,       // High priority - must cover spheres

                PeakMeanDose = 20.0,           // Target ~20 Gy mean (literature: 17.2 Gy achieved)
                PeakMeanPriority = 150,        // Highest priority - drive high dose

                // Valley objectives (Voids - LOW dose)
                ValleyUpperDose = 6.0,         // Keep max dose below 6 Gy
                ValleyUpperVolume = 0.0,       // At the hottest point (D0%)
                ValleyUpperPriority = 130,     // High priority - keep valleys cool

                ValleyMeanDose = 4.5,          // Target ~4.5 Gy mean (literature: 4.72 Gy achieved)
                ValleyMeanPriority = 120       // Important but slightly less than peaks
            };
        }
    }
}