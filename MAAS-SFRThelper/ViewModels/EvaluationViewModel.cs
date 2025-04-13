using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
    public class EvaluationViewModel : BindableBase
    {
        // Structure selection
        private string _selectedTumorId;
        public string SelectedTumorId
        {
            get { return _selectedTumorId; }
            set { SetProperty(ref _selectedTumorId, value); }
        }

        public ObservableCollection<string> PotentialTargets { get; set; }

        // Beam selection
        private string _selectedBeamId;
        public string SelectedBeamId
        {
            get { return _selectedBeamId; }
            set { SetProperty(ref _selectedBeamId, value); }
        }

        public ObservableCollection<string> TreatmentBeams { get; set; }

        // Output log
        private string _outputLog;
        public string OutputLog
        {
            get { return _outputLog; }
            set { SetProperty(ref _outputLog, value); }
        }

        // Compute status
        private bool _canCompute;
        public bool CanCompute
        {
            get { return _canCompute; }
            set
            {
                if (SetProperty(ref _canCompute, value))
                {
                    ComputeCommand.RaiseCanExecuteChanged();
                }
            }
        }

        // Commands
        public DelegateCommand ComputeCommand { get; private set; }
        public DelegateCommand SaveCsvCommand { get; private set; }

        // Internal fields for the dose sampling
        //private readonly List<double> _distances = new List<double>();
        //private readonly List<double> _doseValues = new List<double>();
        //private readonly List<bool> _insideTumorFlags = new List<bool>();
        private List<double> _distances = new List<double>();
        private List<double> _doseValues = new List<double>();
        private List<bool> _insideTumorFlags = new List<bool>();

        private double _entryDist;
        private double _exitDist;

        // ESAPI references
        private PlanSetup _plan;
        private StructureSet _structureSet;
        private EsapiWorker _esapiWorker;

        public EvaluationViewModel(EsapiWorker esapiWorker)
        {
            try
            {
                // Initialize collections and parameters first
                _distances = new List<double>();
                _doseValues = new List<double>();
                _insideTumorFlags = new List<bool>();

                _esapiWorker = esapiWorker ?? throw new ArgumentNullException(nameof(esapiWorker), "ESAPI worker cannot be null");

                // Initialize collections
                PotentialTargets = new ObservableCollection<string>();
                TreatmentBeams = new ObservableCollection<string>();
                OutputLog = "Starting initialization...\n";

                // Enable collection synchronization for background updates
                BindingOperations.EnableCollectionSynchronization(PotentialTargets, new object());
                BindingOperations.EnableCollectionSynchronization(TreatmentBeams, new object());

                // Initialize commands
                ComputeCommand = new DelegateCommand(ExecuteComputeDose, () => CanCompute);
                SaveCsvCommand = new DelegateCommand(ExecuteSaveCsv, CanExecuteSaveCsv);

                OutputLog += "Getting ESAPI context...\n";

                // Get ESAPI context data
                _esapiWorker.RunWithWait(sc =>
                {
                    try
                    {
                        if (sc == null)
                        {
                            OutputLog += "Error: ESAPI script context is null.\n";
                            return;
                        }

                        _structureSet = sc.StructureSet;
                        _plan = sc.PlanSetup;

                        if (_structureSet == null)
                        {
                            OutputLog += "Warning: No structure set available.\n";
                        }
                        else
                        {
                            OutputLog += $"Structure set loaded: {_structureSet.Id}\n";
                        }

                        if (_plan == null)
                        {
                            OutputLog += "Warning: No plan available.\n";
                        }
                        else
                        {
                            OutputLog += $"Plan loaded: {_plan.Id}\n";
                        }
                    }
                    catch (Exception ex)
                    {
                        OutputLog += $"Error initializing ESAPI context: {ex.Message}\n";
                        if (ex.InnerException != null)
                        {
                            OutputLog += $"Inner exception: {ex.InnerException.Message}\n";
                        }
                    }
                });

                // Load structures and beams
                SetStructures();
                SetBeams();

                OutputLog += "Initialization complete.\n";
            }
            catch (Exception ex)
            {
                string errorMessage = $"Critical error initializing EvaluationViewModel: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\nInner exception: {ex.InnerException.Message}";
                }

                // Try to log to output log if it was initialized
                try
                {
                    if (OutputLog != null)
                    {
                        OutputLog += errorMessage + "\n";
                    }
                }
                catch { /* Ignore if OutputLog couldn't be used */ }

                MessageBox.Show(errorMessage, "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetStructures()
        {
            try
            {
                _esapiWorker.Run(sc =>
                {
                    try
                    {
                        if (_structureSet != null)
                        {
                            foreach (var structure in _structureSet.Structures
                                .Where(s => !s.IsEmpty && s.DicomType != "EXTERNAL" && s.DicomType != "BODY"))
                            {
                                PotentialTargets.Add(structure.Id);
                            }

                            OutputLog += $"Found {PotentialTargets.Count} structures\n";

                            // Set default selection
                            if (PotentialTargets.Any())
                            {
                                SelectedTumorId = PotentialTargets.FirstOrDefault(id => id.Equals("PTV", StringComparison.OrdinalIgnoreCase))
                                            ?? PotentialTargets.FirstOrDefault();

                                if (SelectedTumorId != null)
                                {
                                    OutputLog += $"Selected tumor: {SelectedTumorId}\n";
                                }
                            }
                        }
                        else
                        {
                            OutputLog += "No structure set available.\n";
                        }
                    }
                    catch (Exception ex)
                    {
                        OutputLog += $"Error loading structures: {ex.Message}\n";
                    }
                });
            }
            catch (Exception ex)
            {
                OutputLog += $"Critical error in SetStructures: {ex.Message}\n";
                MessageBox.Show($"Error loading structures: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetBeams()
        {
            try
            {
                _esapiWorker.Run(sc =>
                {
                    try
                    {
                        if (_plan != null)
                        {
                            foreach (var beam in _plan.Beams.Where(b => !b.IsSetupField))
                            {
                                TreatmentBeams.Add(beam.Id);
                            }

                            OutputLog += $"Found {TreatmentBeams.Count} beams\n";

                            // Set default selection
                            if (TreatmentBeams.Any())
                            {
                                SelectedBeamId = TreatmentBeams.FirstOrDefault();

                                if (SelectedBeamId != null)
                                {
                                    OutputLog += $"Selected beam: {SelectedBeamId}\n";
                                }
                            }
                        }
                        else
                        {
                            OutputLog += "No plan available.\n";
                        }

                        // Update compute flag
                        CanCompute = (_plan != null && !string.IsNullOrEmpty(SelectedBeamId) && !string.IsNullOrEmpty(SelectedTumorId));
                        OutputLog += $"Can compute: {CanCompute}\n";
                    }
                    catch (Exception ex)
                    {
                        OutputLog += $"Error loading beams: {ex.Message}\n";
                    }
                });
            }
            catch (Exception ex)
            {
                OutputLog += $"Critical error in SetBeams: {ex.Message}\n";
                MessageBox.Show($"Error loading beams: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteComputeDose()
        {
            try
            {
                // Clear previous data first
                _distances = new List<double>();
                _doseValues = new List<double>();
                _insideTumorFlags = new List<bool>();

                OutputLog += "Starting dose computation...\n";

                // Copy selected IDs to local variables to avoid potential thread issues
                string selectedBeamId = SelectedBeamId;
                string selectedTumorId = SelectedTumorId;

                if (string.IsNullOrEmpty(selectedBeamId))
                {
                    OutputLog += "No beam selected. Please select a beam.\n";
                    return;
                }

                if (string.IsNullOrEmpty(selectedTumorId))
                {
                    OutputLog += "No structure selected. Please select a structure.\n";
                    return;
                }

                _esapiWorker.RunWithWait(context =>
                {
                    try
                    {
                        var plan = _plan;
                        if (plan == null)
                        {
                            OutputLog += "No plan available. Cannot compute dose.\n";
                            return;
                        }

                        if (plan.Dose == null)
                        {
                            OutputLog += "No 3D dose is calculated for this plan. Please calculate dose first.\n";
                            return;
                        }

                        // Get actual beam and structure objects from their IDs
                        Beam beam = null;
                        Structure tumor = null;

                        beam = plan.Beams.FirstOrDefault(b => b.Id == selectedBeamId);
                        if (beam == null)
                        {
                            OutputLog += $"Could not find beam with ID '{selectedBeamId}'. Please select another beam.\n";
                            return;
                        }

                        if (_structureSet == null)
                        {
                            OutputLog += "No structure set available. Cannot compute dose.\n";
                            return;
                        }

                        tumor = _structureSet.Structures.FirstOrDefault(s => s.Id == selectedTumorId);
                        if (tumor == null)
                        {
                            OutputLog += $"Could not find structure with ID '{selectedTumorId}'. Please select another structure.\n";
                            return;
                        }

                        // Calculate beam direction
                        OutputLog += "Calculating beam direction...\n";
                        var isocenter = beam.IsocenterPosition;
                        var cp0 = beam.ControlPoints.First();
                        var direction = ComputeBeamUnitVector(cp0.GantryAngle, cp0.PatientSupportAngle);

                        // Find entry/exit by scanning
                        OutputLog += "Finding beam entry and exit points...\n";
                        double searchStartDist = -300.0;
                        double searchEndDist = 300.0;
                        double stepSize = 2.0; // Increased for stability
                        bool insideTumor = false;
                        _entryDist = double.NaN;
                        _exitDist = double.NaN;

                        // Temporary storage for computed data
                        var tempDistances = new List<double>();
                        var tempDoseValues = new List<double>();
                        var tempInsideFlags = new List<bool>();

                        for (double dist = searchStartDist; dist <= searchEndDist; dist += stepSize)
                        {
                            var point = isocenter + dist * direction;
                            bool pointInTumor = false;

                            try
                            {
                                pointInTumor = tumor.IsPointInsideSegment(point);
                            }
                            catch (Exception ex)
                            {
                                OutputLog += $"Error checking point: {ex.Message}\n";
                                continue;
                            }

                            if (!insideTumor && pointInTumor)
                            {
                                _entryDist = dist;
                                insideTumor = true;
                            }
                            else if (insideTumor && !pointInTumor)
                            {
                                _exitDist = dist;
                                break;
                            }
                        }

                        if (double.IsNaN(_entryDist) || double.IsNaN(_exitDist))
                        {
                            OutputLog += "Beam does not intersect the tumor structure.\n";
                            return;
                        }

                        // Sample the dose within the tumor region
                        OutputLog += "Sampling dose along beam path...\n";
                        double margin = 5.0; // Increased margins
                        double startDist = _entryDist - margin;
                        double endDist = _exitDist + margin;
                        stepSize = 1.0; // Normal step size for sampling

                        for (double dist = startDist; dist <= endDist; dist += stepSize)
                        {
                            try
                            {
                                var samplePoint = isocenter + dist * direction;
                                bool isInside = tumor.IsPointInsideSegment(samplePoint);

                                // Check if dose value is accessible
                                DoseValue doseValue = plan.Dose.GetDoseToPoint(samplePoint);
                                if (doseValue == null)
                                {
                                    OutputLog += $"Null dose value at distance {dist}\n";
                                    continue;
                                }

                                double doseInGy = doseValue.Dose;

                                // Store in temporary lists
                                tempDistances.Add(dist);
                                tempDoseValues.Add(doseInGy);
                                tempInsideFlags.Add(isInside);
                            }
                            catch (Exception ex)
                            {
                                OutputLog += $"Error sampling point at distance {dist}: {ex.Message}\n";
                            }
                        }

                        // If we have data, transfer to the main lists
                        if (tempDistances.Count > 0)
                        {
                            _distances = new List<double>(tempDistances);
                            _doseValues = new List<double>(tempDoseValues);
                            _insideTumorFlags = new List<bool>(tempInsideFlags);
                        }

                        if (_distances.Count == 0)
                        {
                            OutputLog += "No valid dose samples collected.\n";
                            return;
                        }

                        // Compute basic stats
                        OutputLog += "Computing statistics...\n";
                        var tumorDoses = new List<double>();
                        for (int i = 0; i < _doseValues.Count; i++)
                        {
                            if (_insideTumorFlags[i]) tumorDoses.Add(_doseValues[i]);
                        }

                        // Avoid divide by zero by checking count
                        double maxDose = tumorDoses.Count > 0 ? tumorDoses.Max() : 0.0;
                        double minDose = tumorDoses.Count > 0 ? tumorDoses.Min() : 0.0;
                        double avgDose = tumorDoses.Count > 0 ? tumorDoses.Average() : 0.0;

                        // Update output log
                        OutputLog += "===== Computation Complete =====\n";
                        OutputLog += $"Plan: {plan.Id}, Beam: {beam.Id}, Structure: {tumor.Id}\n";
                        OutputLog += $"Entry Dist: {_entryDist:F1} mm, Exit Dist: {_exitDist:F1} mm\n";
                        OutputLog += $"Tumor length along axis: {_exitDist - _entryDist:F1} mm\n";
                        OutputLog += $"Max Dose: {maxDose:F3} Gy\n";
                        OutputLog += $"Min Dose: {minDose:F3} Gy\n";
                        OutputLog += $"Avg Dose: {avgDose:F3} Gy\n";
                        OutputLog += $"Total samples: {_distances.Count}\n";
                        OutputLog += "================================\n";
                    }
                    catch (Exception ex)
                    {
                        OutputLog += $"Error during dose computation: {ex.Message}\n";
                        if (ex.InnerException != null)
                        {
                            OutputLog += $"Inner Exception: {ex.InnerException.Message}\n";
                        }
                    }
                });

                // Update the 'Save CSV' command
                SaveCsvCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                OutputLog += $"Critical error in ExecuteComputeDose: {ex.Message}\n";
                MessageBox.Show($"Error computing dose: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteSaveCsv()
        {
            try
            {
                if (_distances == null)
                    return false;

                return _distances.Count > 0;
            }
            catch
            {
                // If there's any error accessing the collection, assume we can't save
                return false;
            }
        }

        private void ExecuteSaveCsv()
        {
            try
            {
                // Use a simple direct approach with minimal ESAPI interaction
                OutputLog += "Starting CSV save operation...\n";

                if (_distances == null || _distances.Count == 0 ||
                    _doseValues == null || _doseValues.Count == 0 ||
                    _insideTumorFlags == null || _insideTumorFlags.Count == 0)
                {
                    OutputLog += "No data to save. Please compute dose first.\n";
                    MessageBox.Show("No data available to save. Please compute dose first.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Safely create filename without any ESAPI object access
                string planId = "NoPlan";
                string beamId = "NoBeam";
                string tumorId = "NoTumor";

                // Use stored IDs which are already strings, not ESAPI objects
                if (!string.IsNullOrEmpty(SelectedBeamId))
                    beamId = SelectedBeamId.Replace(' ', '_').Replace('\\', '_').Replace('/', '_');

                if (!string.IsNullOrEmpty(SelectedTumorId))
                    tumorId = SelectedTumorId.Replace(' ', '_').Replace('\\', '_').Replace('/', '_');

                // Build data in memory
                var lines = new List<string>();
                lines.Add("Distance(mm),Dose(Gy),IsInsideTumor");

                int minCount = Math.Min(Math.Min(_distances.Count, _doseValues.Count), _insideTumorFlags.Count);

                for (int i = 0; i < minCount; i++)
                {
                    string line = string.Format("{0:F1},{1:F3},{2}",
                        _distances[i],
                        _doseValues[i],
                        _insideTumorFlags[i] ? "1" : "0");
                    lines.Add(line);
                }

                // Create the file on desktop
                try
                {
                    string fileName = String.Format("TumorDose_{0}_{1}_{2}_{3:yyyyMMdd_HHmmss}.csv",
                        planId, beamId, tumorId, DateTime.Now);

                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string filePath = Path.Combine(desktopPath, fileName);

                    // Write content all at once
                    File.WriteAllLines(filePath, lines);

                    OutputLog += "Data saved to: " + filePath + "\n";
                    MessageBox.Show("Data saved to:\n" + filePath, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    OutputLog += "Error writing to desktop: " + ex.Message + "\n";

                    // Try Documents folder
                    try
                    {
                        string fileName = String.Format("TumorDose_{0}_{1}_{2}_{3:yyyyMMdd_HHmmss}.csv",
                            planId, beamId, tumorId, DateTime.Now);

                        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        string filePath = Path.Combine(docPath, fileName);

                        // Write content all at once
                        File.WriteAllLines(filePath, lines);

                        OutputLog += "Data saved to: " + filePath + "\n";
                        MessageBox.Show("Data saved to:\n" + filePath, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex2)
                    {
                        OutputLog += "Error writing to documents folder: " + ex2.Message + "\n";
                        MessageBox.Show("Could not save data. Please check the log for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                // Last resort error handling
                try
                {
                    OutputLog += "Critical error: " + ex.Message + "\n";
                    MessageBox.Show("Critical error saving data: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch
                {
                    // If even the MessageBox fails, we can't do much else
                    MessageBox.Show("A critical error occurred.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private VVector ComputeBeamUnitVector(double gantryAngleDeg, double couchAngleDeg)
        {
            double gantryRad = gantryAngleDeg * Math.PI / 180.0;
            double couchRad = couchAngleDeg * Math.PI / 180.0;

            // Basic rotation ignoring couch first
            VVector dVec = new VVector(
                Math.Sin(gantryRad),
                -Math.Cos(gantryRad),
                0
            );

            // Apply couch rotation (if needed)
            if (Math.Abs(couchAngleDeg) > 0.1)
            {
                double x = dVec.x;
                double z = dVec.z;
                dVec.x = x * Math.Cos(couchRad) + z * Math.Sin(couchRad);
                dVec.z = -x * Math.Sin(couchRad) + z * Math.Cos(couchRad);
            }

            // Normalize
            double length = Math.Sqrt(dVec.x * dVec.x + dVec.y * dVec.y + dVec.z * dVec.z);
            return new VVector(dVec.x / length, dVec.y / length, dVec.z / length);
        }
    }
}