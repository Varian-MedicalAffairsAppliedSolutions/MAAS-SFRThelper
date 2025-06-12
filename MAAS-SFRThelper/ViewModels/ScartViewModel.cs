using NLog.LayoutRenderers.Wrappers;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace MAAS_SFRThelper.ViewModels
{
    public class ScartViewModel : BindableBase
    {
		// GTV - structure list
		private string _gtvId;

		public string GtvId
		{
			get { return _gtvId; }
			set { SetProperty(ref _gtvId, value); }
		}

		public ObservableCollection<string> Structures { get; set; }

		// pick superior margin
		private int _supMargin;
		public int SupMargin
		{
			get { return _supMargin; }
            set { SetProperty(ref _supMargin, value); }
        }

		public List<int> SupMargins { get; set; }

		// Pick inferior margin
        private int _infMargin;
        public int InfMargin
        {
            get { return _infMargin; }
            set { SetProperty(ref _infMargin, value); }
        }

        public List<int> InfMargins { get; set; }

		// Pick dose per fraction
		private double _dosePerFraction;

		public double DosePerFraction
		{
			get { return _dosePerFraction; }
            set { SetProperty(ref _dosePerFraction, value);

                _esapiWorker.Run(sc =>
                {
                    if (NumberOfFractions != 0)
                    {
                        ShrinkFactor = CalculateShrinkPercentage();
                        UpdateTotalDose();
                    }

                });
                //if (NumberOfFractions != 0)
                //{
                //    ShrinkFactor = CalculateShrinkPercentage();
                //}

            }
        }

        // Updated LowerDose property to trigger recalculation
        private double _lowerDose = 5.0; // Default to previous standard value

        public double LowerDose
        {
            get { return _lowerDose; }
            set
            {
                SetProperty(ref _lowerDose, value);

                // Trigger recalculation when lower dose changes (same pattern as other properties)
                _esapiWorker.Run(sc =>
                {
                    if (NumberOfFractions != 0 && DosePerFraction != 0 && !OverrideChecked)
                    {
                        ShrinkFactor = CalculateShrinkPercentage();
                    }
                });
            }
        }

        private double _shrinkFactor;

        public double ShrinkFactor
        {
            get { return _shrinkFactor; }
            set { SetProperty(ref _shrinkFactor, value); }
        }

        private bool _overrideChecked;

        public bool OverrideChecked
        {
            get { return _overrideChecked; }
            set { SetProperty(ref _overrideChecked, value); }
        }

        private string _doseUnit;

        public string DoseUnit
        {
            get { return _doseUnit; }
            set { SetProperty(ref _doseUnit, value); }
        }

        private string _totalDose;
        public string TotalDose
        {
            get { return _totalDose; }
            set { SetProperty(ref _totalDose, value); }
        }

        private string _outputText;

        public string OutputText
        {
            get { return _outputText; }
            set
            {
                SetProperty(ref _outputText, value);
            }

        }


        public ObservableCollection<int> Fractions { get; set; }

		private int _numberOfFractions;
        private StructureSet _structureSet;
        private PlanSetup _plan;
        private EsapiWorker _esapiWorker;

        public int NumberOfFractions
		{
			get { return _numberOfFractions; }
            set { SetProperty(ref _numberOfFractions, value);
                                
                if (NumberOfFractions != 0 && DosePerFraction != 0)

                {
                    _esapiWorker.Run(sc =>
                    {
                        ShrinkFactor = CalculateShrinkPercentage();
                        UpdateTotalDose();
                    });
                }

            }
        }

		public DelegateCommand GenerateSTV {  get; set; }
        public DelegateCommand RunAll { get; set; }
        public ScartViewModel(EsapiWorker esapiWorker)
        {
            _esapiWorker = esapiWorker;
            _esapiWorker.RunWithWait(sc =>
            {
                _structureSet = sc.StructureSet;
                _plan = sc.PlanSetup;

                if (_plan != null)
                {
                    DoseUnit = _plan.TotalDose.UnitAsString;
                }
                else
                {
                    if (_structureSet != null)
                    {
                        if (_structureSet.Patient.Courses.Any())
                        {
                            foreach (var course in _structureSet.Patient.Courses)
                            {
                                if (course.PlanSetups.Any())
                                {
                                    DoseUnit = course.PlanSetups.First().TotalDose.UnitAsString;
                                }
                            }
                        }
                    }
                }

            });

            Structures = new ObservableCollection<string>();
            BindingOperations.EnableCollectionSynchronization(Structures, this);
            Fractions = new ObservableCollection<int>() {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
            BindingOperations.EnableCollectionSynchronization(Fractions, this);
            InfMargins = new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20};
            SupMargins = new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20};

            InfMargin = 5;
            SupMargin = 5;
            
            SetPlanProperties();
			SetStructures();
            GenerateSTV = new DelegateCommand(CreateSTV);
            RunAll = new DelegateCommand(OnRunAll);
				
        }

        private void OnRunAll()
        {
            OutputText += "\nGenerating SCART plan";
            _esapiWorker.Run(sc =>
            {
                ContourSTV(sc);

                if (_structureSet.Structures.Any(st => st.Id.Equals("STV") && !st.IsEmpty))
                {
                    if (_plan != null)
                    {
                        SetBeams(sc);

                        if (_plan.Beams.Where(b => b.Technique.Id.Contains("ARC")).Count() >= 2)
                        {
                            Optimize(sc);
                        }

                        else
                        {
                            OutputText = "\nCould not generate beams";
                        }
                    }

                    else 
                    {
                        OutputText += "\nPlease create a plan with at least one beam and restart application";
                    }
                   
                }

                
                
            });
        }

        private void Optimize(ScriptContext sc)
        {

            // 2 optimization objectives
            // generate ring
            var gtv = _structureSet.Structures.First(st => st.Id == GtvId);
            var stv = _structureSet.Structures.First(st => st.Id == "STV");
            string ringId = "GTVring.2-1.2";
            Structure ring = null;
            if (_structureSet.Structures.Any(s => s.Id.Equals(ringId, StringComparison.OrdinalIgnoreCase)))
            {
                ring = _structureSet.Structures.First(s => s.Id.Equals(ringId, StringComparison.OrdinalIgnoreCase));
            }
            else 
            {
                OutputText += "\nGenerating ring";
                ring = _structureSet.AddStructure("CONTROL", ringId);
            }
            
            ring.SegmentVolume=gtv.Margin(12).Sub(gtv.Margin(2));
            var explan = _plan as ExternalPlanSetup;
            OutputText += $"\nUpdating plan dose to {TotalDose} in {DosePerFraction} per fraction";
            explan.SetPrescription(NumberOfFractions, new DoseValue(Convert.ToDouble(TotalDose.Split(' ').First()), explan.TotalDose.Unit), 1.0);
            explan.OptimizationSetup.AddPointObjective(stv,
                OptimizationObjectiveOperator.Lower, 
                new DoseValue(Convert.ToDouble(TotalDose.Split(' ').First()),
                explan.TotalDose.Unit), 
                99, 
                250);
            //explan.OptimizationSetup.AddPointObjective(ring, 
            //    OptimizationObjectiveOperator.Upper, 
            //    new DoseValue(Convert.ToDouble(TotalDose.Split(' ').First()) * 0.27,
            //    explan.TotalDose.Unit), 
            //    0, 
            //    100); // TDO verify objective

            // Change to lower dose
            double ringDoseLimit;
            if (explan.TotalDose.Unit == DoseValue.DoseUnit.cGy)
            {
                ringDoseLimit = LowerDose * 100.0;  // Convert Gy to cGy if needed
            }
            else
            {
                ringDoseLimit = LowerDose;  // Use directly if already in Gy
            }
            explan.OptimizationSetup.AddPointObjective(ring,
                OptimizationObjectiveOperator.Upper,
                new DoseValue(ringDoseLimit,
                explan.TotalDose.Unit),
                0,
                100); // TDO verify objective

            explan.OptimizationSetup.AddAutomaticNormalTissueObjective(100);
            if (explan.Beams.All(b => b.MLC == null))
            {
                OutputText += "\n ADD MLC TO FIELD AND RESTART APP";

            }
            else
            {
                OutputText += $"\n{explan.Beams.Where(b => b.MLC != null).First().MLC.Id}";

                explan.OptimizeVMAT(new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.UseIntermediateDose, explan.Beams.Where(b=>b.MLC!=null).First().MLC.Id));
                OutputText += "\nOptimized plan";
                explan.CalculateDose();
                OutputText += "\nCalculated dose";

            }
                
            // TODO verify lower dose 
            // TODO warn if stv overlaps oars
            // TODO build UI so user can select lower dose at edge of target.
            // TODO static field to treat mlc as global and auto select gtv and debug
        }

        private void SetBeams(ScriptContext sc)
        {
            // make sure there is a beam
            if (_plan != null && _plan.Beams.Any(b => !b.IsSetupField))
            {
                if (_plan.Beams.Where(b => !b.IsSetupField && b.Technique.Id.Contains("ARC")).Count() >= 2)                
                {
                    FitAllBeams();
                }
                Beam beam = _plan.Beams.First(b => !b.IsSetupField);
                VVector iso = new VVector(beam.IsocenterPosition.x, beam.IsocenterPosition.y, beam.IsocenterPosition.z);
                string machine = beam.TreatmentUnit.Id;
                string energy = beam.EnergyModeDisplayName;
                string machineType = beam.TreatmentUnit.MachineModel;
                int doseRate = beam.DoseRate;
                
                OutputText += "\nRemoving seed field";
                (_plan as ExternalPlanSetup).RemoveBeam( beam );
                OutputText += "\nRemoved seed field";
                int numberOfFields = machineType=="RDS"?4:2;
                var parameters = new ExternalBeamMachineParameters(machine, energy.Contains("-")?energy.Split('-').First():energy, doseRate, "SRS ARC", energy.Contains("-") ? "FFF" : null);
                OutputText += $"\nparameters = {parameters.MachineId}, {parameters.EnergyModeId}, {parameters.DoseRate}";
                double collimatorAngle = 15;
                for ( int i = 0; i < numberOfFields; i++)
                {
                    (_plan as ExternalPlanSetup).AddConformalArcBeam(parameters, 
                        i % 2 == 0 ? collimatorAngle : 360 - collimatorAngle,
                        20,
                        i % 2 == 0 ? 181 : 179,
                        i % 2 == 0 ? 179 : 181,
                        i % 2 == 0 ? GantryDirection.Clockwise : GantryDirection.CounterClockwise, 
                        0,
                        iso
                        );   
                    if (i > 1)
                    {
                        collimatorAngle = 30;
                    }
                }
                
                FitAllBeams();

            // TODO for Halcyon use collimator angles 315, 0, 45, 90
            // TODO tell user one field seed field inserting template for RDS vs non RDS
            // TODO if more than one field tell user we use their fields so setup collimator angle and gantry rotations properly

            }
            else 
            {
                OutputText = "\nPlan does not have beams to copy";
            }
        }

        private void FitAllBeams()
        {
           
            if (_plan.Beams.Any(b => b.TreatmentUnit.MachineModel.Equals("RDS")))
            {
                OutputText += "\nSkipping beam fitting for halcyon machine";
                ////if (_plan.Beams.Where(be => !be.IsSetupField).All(be => be.ControlPoints.First().CollimatorAngle == 0))
                ////{
                ////    double angle = 15.0;
                ////    int fieldsModified = 0;

                ////    foreach (var beam in _plan.Beams.Where(b => !b.IsSetupField))
                ////    {
                ////        // changing collimator rot to match template
                ////        if (beam.ControlPoints.First().CollimatorAngle==0)
                ////        {
                ////            OutputText += "\nChange collimator rotation for field";
                ////            fieldsModified++;
                ////            var edits = beam.GetEditableParameters();
                ////            edits.ControlPoints.First().CollimatorAngle = angle;
                ////        }
                        
                        
                ////    }

                //}

            }
            else
            {
                //if (_plan.Beams.Where(be => !be.IsSetupField).Any(be => be.ControlPoints.First().CollimatorAngle == 0))
                //{
                //    foreach (var beam in _plan.Beams.Where(b => !b.IsSetupField))
                //    {
                //        // fit them to stv with 0 margin
                        
                //    }

                //}
                OutputText += "\nStarting beam fitting";
                foreach (var beam in _plan.Beams.Where(b=>!b.IsSetupField))
                {
                    // fit them to stv with 0 margin
                    Structure stv = _structureSet.Structures.First(st=>st.Id == "STV");
                    beam.FitCollimatorToStructure(new FitToStructureMargins(0), stv, true, true, false); // don't want fitting to reset collimator
                    // 
                    OutputText += "\n\tFitting beam: " + beam.Id;
                }
            }
           
        }

        // Update total dose
        private void UpdateTotalDose()
        {
            if (NumberOfFractions > 0 && DosePerFraction > 0)
            {
                double total = DosePerFraction * NumberOfFractions;
                TotalDose = $"{total:F1} {DoseUnit}";
            }
            else
            {
                TotalDose = "N/A";
            }
        }

        private void CreateSTV()
        {
            if (!string.IsNullOrEmpty(GtvId))
            {
                _esapiWorker.Run(sc =>
                {
                    if (_structureSet.Structures.Any(st => st.Id.Equals("STV") && !st.IsEmpty))
                    {
                        OutputText += "\nUsing already existing STV";
                        return;
                    }
                    ContourSTV(sc);
                    OutputText += "\nGenerated STV";
                });
                
            }
        }

        private void ContourSTV(ScriptContext sc)
        {
            sc.Patient.BeginModifications();
            Structure gtv = GetGtvFromId();
            // ShrinkFactor = CalculateShrinkPercentage();
            Structure stv = CreateSTVStructure(gtv);
        }

        private Structure GetGtvFromId()
        {
            return _structureSet.Structures.FirstOrDefault(s => s.Id == GtvId);
        }

        private void SetStructures()
        {
            _esapiWorker.Run(sc =>
            {
                if (_structureSet != null)
                {
                    foreach (var structure in _structureSet.Structures.OrderByDescending(s => s.DicomType == "GTV"))
                    {
                        Structures.Add(structure.Id);
                    }

                    if (_structureSet.Structures.Any(s => s.DicomType == "GTV" && !s.IsEmpty))
                    {
                        GtvId = _structureSet.Structures.First(s => s.DicomType == "GTV" && !s.IsEmpty).Id;
                    }
                }
            });
        }

        private void SetPlanProperties()
        {
            _esapiWorker.RunWithWait(sc =>
            {
                if (_plan != null)
                {
                    if (! Double.IsNaN (_plan.DosePerFraction.Dose) )
                    {
                        DosePerFraction = _plan.DosePerFraction.Dose;
                    }
                    else
                    {
                        DosePerFraction = _plan.TotalDose.Unit == DoseValue.DoseUnit.cGy ? 2400.0: 24.0;
                    }

                    if (_plan.NumberOfFractions != null)
                    {
                        NumberOfFractions = _plan.NumberOfFractions.Value;
                    }
                    else { NumberOfFractions = 1; }

                    // Initialize TotalDose
                    UpdateTotalDose();
                }
            });
        }

        // --------------------------------------------------------------------------------
        private double CalculateShrinkPercentage()
        {
            // Calculate shrink percentage based on tables from the presentation
            // Reference values from presentation:
            // 15Gy x3 -> 36% (P/A = 33%)
            // 18Gy x3 -> 27% (P/A = 28%)
            // 21Gy x3 -> 24% (P/A = 24%)
            // 24Gy x3 -> 21% (P/A = 21%)
            double dosepfx = _plan.TotalDose.Unit == DoseValue.DoseUnit.cGy ? DosePerFraction / 100.0 : DosePerFraction; 
            double totalDose = dosepfx * NumberOfFractions;
            // double protectionDose = 5.0; // Standard protection dose per the presentation
            double protectionDose = _plan.TotalDose.Unit == DoseValue.DoseUnit.cGy ? LowerDose / 100.0 : LowerDose;
            double protectionTotal = protectionDose * NumberOfFractions;

            // P/A ratio (Protection dose / Ablation dose)
            double ratio = protectionTotal / totalDose;

            // Linear approximation based on presentation data
            return ratio;
        }

        private Structure CreateSTVStructure(Structure gtvStructure)
        {
            // Create new structure for STV
            string stvId = GetUniqueStructureId(_structureSet, "STV");
            Structure stvStructure = _structureSet.AddStructure("CONTROL", stvId);

            #if NET461 || NET48
            // Set color to red
            stvStructure.Color = (Color)ColorConverter.ConvertFromString("Red");
            #endif

            // Check slices containing the GTV structure
            var gtvContours = new List<VVector[]>();
            var gtvZValues = new List<int>();

            // Collect all GTV contours
           for (int index = 0; index < _structureSet.Image.ZSize; index++) 
            { 
                foreach (var slice in gtvStructure.GetContoursOnImagePlane(index))
                {
                    if (slice.Count() > 0)
                    {
                        gtvContours.Add(slice);
                        gtvZValues.Add(index);
                    }
                }
            }

            if (gtvContours.Count == 0)
            {
                MessageBox.Show("Selected GTV structure has no contours.");
                return null;
            }

            // Sort contours by Z-coordinate
            //var sortedZips = gtvZValues.Zip(gtvContours, (z, contour) => new { Z = z, Contour = contour })
            //                         .OrderBy(pair => pair.Z)
            //                         .ToList();

            //var sortedZValues = sortedZips.Select(pair => pair.Z).ToList();
            //var sortedContours = sortedZips.Select(pair => pair.Contour).ToList();

            // Calculate slice thickness based on consecutive Z values
            double sliceThickness = _structureSet.Image.ZRes;
            //if (sortedZValues.Count > 1)
            //{
            //    sliceThickness = Math.Abs(sortedZValues[1] - sortedZValues[0]);
            //}
            //else
            //{
            //    MessageBox.Show("Cannot determine slice thickness. Using default of 3mm.");
            //    sliceThickness = 3.0;
            //}

            // Apply superior and inferior margins
            int superiorSlicesToSkip = (int)Math.Ceiling(SupMargin / sliceThickness);
            int inferiorSlicesToSkip = (int)Math.Ceiling(InfMargin / sliceThickness);

            // Process each contour (excluding margins)
            for (int i = 0 + inferiorSlicesToSkip; i < gtvContours.Count - superiorSlicesToSkip; i++)
            {
                VVector[] gtvContour = gtvContours[i]; // sortedContours[i];
                int zValue = gtvZValues[i];

                // Create points list for this contour
                List<VVector> points = new List<VVector>();
                for (int j = 0; j < gtvContour.GetLength(0); j++)
                {
                    points.Add(new VVector(gtvContour[j].x, gtvContour[j].y, gtvContour[j].z));
                }

                // Calculate centroid
                double centroidX = 0, centroidY = 0;
                foreach (var point in points)
                {
                    centroidX += point.x;
                    centroidY += point.y;
                }
                centroidX /= points.Count;
                centroidY /= points.Count;

                // Convert points to polar coordinates
                List<Tuple<double, double, VVector>> polarPoints = new List<Tuple<double, double, VVector>>();
                foreach (var point in points)
                {
                    double dx = point.x - centroidX;
                    double dy = point.y - centroidY;
                    double theta = Math.Atan2(dy, dx);
                    double rho = Math.Sqrt(dx * dx + dy * dy);
                    polarPoints.Add(new Tuple<double, double, VVector>(theta, rho, point));
                }

                // Perform centroid optimization similar to MATLAB code
                for (int s = 0; s < 360; s++) // 360 samples as in original code
                {
                    // Find minimum distance point
                    var minRhoPair = polarPoints.OrderBy(p => p.Item2).First();
                    double minRho = minRhoPair.Item2;
                    double minTheta = minRhoPair.Item1;

                    // Find approximate opposite point
                    double maxTheta = minTheta + Math.PI; // 180 degrees in radians
                    if (maxTheta > Math.PI) maxTheta -= 2 * Math.PI;

                    // Find the closest point to the opposite angle
                    var maxIndex = 0;
                    var minDiff = double.MaxValue;
                    for (int j = 0; j < polarPoints.Count; j++)
                    {
                        var diff = Math.Abs(polarPoints[j].Item1 - maxTheta);
                        if (diff < minDiff)
                        {
                            minDiff = diff;
                            maxIndex = j;
                        }
                    }

                    double maxRho = polarPoints[maxIndex].Item2;

                    // Adjust centroid slightly toward max point (0.01 factor like original code)
                    double avgRho = 0.01 * (maxRho - (maxRho + minRho) / 2);
                    double dX = avgRho * Math.Cos(maxTheta);
                    double dY = avgRho * Math.Sin(maxTheta);

                    centroidX += dX;
                    centroidY += dY;

                    // Recalculate polar coordinates
                    for (int j = 0; j < points.Count; j++)
                    {
                        double dx = points[j].x - centroidX;
                        double dy = points[j].y - centroidY;
                        double theta = Math.Atan2(dy, dx);
                        double rho = Math.Sqrt(dx * dx + dy * dy);
                        polarPoints[j] = new Tuple<double, double, VVector>(theta, rho, points[j]);
                    }
                }

                // Apply shrinkage to create STV contour
                List<VVector> stvPoints = new List<VVector>();
                foreach (var polarPoint in polarPoints)
                {
                    double theta = polarPoint.Item1;
                    double rho = polarPoint.Item2 * ShrinkFactor; // Apply shrinkage

                    // Convert back to Cartesian
                    double newX = centroidX + rho * Math.Cos(theta);
                    double newY = centroidY + rho * Math.Sin(theta);

                    stvPoints.Add(new VVector(newX, newY, zValue));
                }

                // Add contour to structure
                if (stvPoints.Count > 2) // Need at least 3 points for a valid contour
                {
                    stvStructure.AddContourOnImagePlane(stvPoints.ToArray(), zValue);
                }
            }

            return stvStructure;
        }

        private string GetUniqueStructureId(StructureSet structureSet, string baseId)
        {
            string id = baseId;
            int counter = 1;

            while (structureSet.Structures.Any(s => s.Id == id))
            {
                id = $"{baseId}_{counter}";
                counter++;
            }

            return id;
        }

    }
}
