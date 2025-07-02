using MAAS_SFRThelper.Models;
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
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
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
        private string _ptvAllId;
        public string PtvAllId
        {
            get { return _ptvAllId; }
            set { SetProperty(ref _ptvAllId, value); }
        }

        //3D dose datagrid
        public ObservableCollection<MetricData> AllMetrics { get; set; }
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

        //Is1DCAXSelected
        private bool _is1DCAXSelected;

        public bool Is1DCAXSelected
        {
            get { return _is1DCAXSelected; }
            set { SetProperty(ref _is1DCAXSelected, value); }
        }

        //IsIsocenterMethodSelected
        private bool _isIsocenterMethodSelected;

        public bool IsIsocenterMethodSelected
        {
            get { return _isIsocenterMethodSelected; }
            set { SetProperty(ref _isIsocenterMethodSelected, value); }
        }

        //Is2DPlanarSelected
        private bool _is2DPlanarSelected;

        public bool Is2DPlanarSelected
        {
            get { return _is2DPlanarSelected; }
            set { SetProperty(ref _is2DPlanarSelected, value); }
        }

        //Is3DEvaluationSelected
        private bool _is3dEvaluationSelected;

        public bool Is3DEvaluationSelected
        {
            get { return _is3dEvaluationSelected; }
            set { SetProperty(ref _is3dEvaluationSelected, value); }
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

        // Canvas reference (will be set from the View)
        private Canvas _plotCanvas;
        public Canvas PlotCanvas
        {
            get { return _plotCanvas; }
            set
            {
                if (SetProperty(ref _plotCanvas, value))
                {
                    // Subscribe to size changed events for redrawing
                    if (_plotCanvas != null)
                    {
                        _plotCanvas.SizeChanged += PlotCanvas_SizeChanged;
                        _plotCanvas.Loaded += PlotCanvas_Loaded;
                    }
                }
            }
        }

        // Flag to track if we have data to plot
        private bool _hasPlotData = false;

        // Visibility controls with mutual exclusivity
        private bool _bTextVis = true;  // Start with text visible
        public bool bTextVis
        {
            get { return _bTextVis; }
            set
            {
                if (SetProperty(ref _bTextVis, value))
                {
                    OutputLog += $"bTextVis changed to: {value}\n";
                    // If text view is being turned on, turn off plot view
                    if (value && _bPlotVis)
                    {
                        _bPlotVis = false;
                        OutputLog += "bPlotVis set to false (mutual exclusivity)\n";
                        RaisePropertyChanged(nameof(bPlotVis));
                    }
                }
            }
        }

        private bool _bPlotVis = false;  // Start with plot hidden
        public bool bPlotVis
        {
            get { return _bPlotVis; }
            set
            {
                if (SetProperty(ref _bPlotVis, value))
                {
                    OutputLog += $"bPlotVis changed to: {value}\n";
                    // If plot view is being turned on, turn off text view
                    if (value && _bTextVis)
                    {
                        _bTextVis = false;
                        OutputLog += "bTextVis set to false (mutual exclusivity)\n";
                        RaisePropertyChanged(nameof(bTextVis));
                    }

                    // Redraw plot when switching to plot view
                    if (value && _hasPlotData)
                    {
                        RefreshPlot();
                    }

                    // Update RefreshPlotCommand availability
                    RefreshPlotCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        // Commands
        public DelegateCommand ComputeCommand { get; private set; }
        public DelegateCommand SaveCsvCommand { get; private set; }
        public DelegateCommand ShowPlotCommand { get; private set; }
        private DelegateCommand _refreshPlotCommand;
        public DelegateCommand RefreshPlotCommand
        {
            get
            {
                if (_refreshPlotCommand == null)
                {
                    _refreshPlotCommand = new DelegateCommand(() =>
                    {
                        if (_hasPlotData && bPlotVis)
                        {
                            OutputLog += "Manual plot refresh triggered.\n";
                            RefreshPlot();
                        }
                    }, () => _hasPlotData && bPlotVis);
                }
                return _refreshPlotCommand;
            }
        }

        // Internal fields for the dose sampling
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
                AllMetrics = new ObservableCollection<MetricData>();
                OutputLog = "Starting initialization...\n";

                // Enable collection synchronization for background updates
                BindingOperations.EnableCollectionSynchronization(PotentialTargets, new object());
                BindingOperations.EnableCollectionSynchronization(TreatmentBeams, new object());
                BindingOperations.EnableCollectionSynchronization(AllMetrics, new object());

                // Initialize commands
                ComputeCommand = new DelegateCommand(ExecuteComputeDose, () => CanCompute);
                SaveCsvCommand = new DelegateCommand(ExecuteSaveCsv, CanExecuteSaveCsv);
                ShowPlotCommand = new DelegateCommand(ExecuteShowPlot, CanExecuteShowPlot);

                // Set default evaluation method to 1D CAX
                Is1DCAXSelected = true;

                // Set initial visibility states
                bTextVis = true;   // Show text log by default
                bPlotVis = false;  // Hide plot by default

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

        private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Redraw plot when canvas size changes if we're in plot view and have data
            if (bPlotVis && _hasPlotData)
            {
                // Use a small delay to ensure the canvas is properly resized
                System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    RefreshPlot();
                }));
            }
        }

        private void PlotCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            // Initial plot draw when canvas is loaded
            if (bPlotVis && _hasPlotData)
            {
                RefreshPlot();
            }
        }

        private void RefreshPlot()
        {
            if (PlotCanvas == null || !_hasPlotData) return;

            try
            {
                string tumorId = SelectedTumorId ?? "Unknown Tumor";
                string beamId = SelectedBeamId ?? "Unknown Beam";

                // Clear existing plot first
                PlotCanvas.Children.Clear();

                // Force layout update
                PlotCanvas.UpdateLayout();

                // Use Loaded priority for more reliable rendering
                PlotCanvas.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    DrawEmbeddedPlot(PlotCanvas, _distances, _doseValues, _insideTumorFlags, _entryDist, _exitDist, tumorId, beamId);
                }));
            }
            catch (Exception ex)
            {
                OutputLog += $"Error refreshing plot: {ex.Message}\n";
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
                _hasPlotData = false;

                OutputLog += "Starting dose computation...\n";

              

                // Copy selected IDs to local variables to avoid potential thread issues
                string selectedBeamId = SelectedBeamId;
                string selectedTumorId = SelectedTumorId;
                //string ptvAllName = PtvAllId;
                string ptvAllName = SelectedTumorId;




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

                        bool execute3DDose = Is3DEvaluationSelected;
                        
                        if (execute3DDose)
                        {
                            update3DMetrics(selectedTumorId,ptvAllName,plan);
                            OutputLog += "3D Dosimetrics complete\n";
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

                        // Calculate the beam direction using gantry and couch angles
                        double gantryAngle = cp0.GantryAngle;
                        double couchAngle = cp0.PatientSupportAngle;

                        // Convert angles to radians
                        double gantryRad = gantryAngle * Math.PI / 180.0;
                        double couchRad = couchAngle * Math.PI / 180.0;

                        // Calculate direction vector from gantry and couch angles
                        VVector dVec = new VVector(
                            Math.Sin(gantryRad),
                            -Math.Cos(gantryRad),
                            0
                        );

                        // Apply couch rotation if needed
                        if (Math.Abs(couchAngle) > 0.1)
                        {
                            double x = dVec.x;
                            double z = dVec.z;
                            dVec.x = x * Math.Cos(couchRad) + z * Math.Sin(couchRad);
                            dVec.z = -x * Math.Sin(couchRad) + z * Math.Cos(couchRad);
                        }

                        // Normalize to unit vector
                        double length = Math.Sqrt(dVec.x * dVec.x + dVec.y * dVec.y + dVec.z * dVec.z);
                        VVector direction = new VVector(dVec.x / length, dVec.y / length, dVec.z / length);

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
                            _hasPlotData = true;
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

                // Update the commands that depend on data availability
                SaveCsvCommand.RaiseCanExecuteChanged();
                ShowPlotCommand.RaiseCanExecuteChanged();
                RefreshPlotCommand?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                OutputLog += $"Critical error in ExecuteComputeDose: {ex.Message}\n";
                MessageBox.Show($"Error computing dose: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool CanExecuteSaveCsv()
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

        public bool CanExecuteShowPlot()
        {
            try
            {
                if (_distances == null || _doseValues == null || _insideTumorFlags == null)
                    return false;

                return _distances.Count > 0 && _doseValues.Count > 0 && _insideTumorFlags.Count > 0;
            }
            catch
            {
                // If there's any error accessing the collections, assume we can't plot
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
                    string filePath = System.IO.Path.Combine(desktopPath, fileName);

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
                        string filePath = System.IO.Path.Combine(docPath, fileName);

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

        private void ExecuteShowPlot()
        {
            try
            {
                OutputLog += "Creating embedded plot...\n";

                if (!CanExecuteShowPlot())
                {
                    MessageBox.Show("No dose data available to plot. Please compute dose first.",
                        "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                OutputLog += "Switching to plot view...\n";
                // Switch to plot view by setting the radio button states
                bTextVis = false;
                bPlotVis = true;

                // Check which evaluation method is selected
                if (Is1DCAXSelected)
                {
                    OutputLog += "Using 1D CAX evaluation method for embedded plot...\n";

                    // Get the structure and beam IDs for the plot title
                    string tumorId = SelectedTumorId ?? "Unknown Tumor";
                    string beamId = SelectedBeamId ?? "Unknown Beam";

                    // Show the 1D CAX dose plot in embedded canvas
                    ShowEmbeddedDosePlot(_distances, _doseValues, _insideTumorFlags, _entryDist, _exitDist, tumorId, beamId);

                    OutputLog += "1D CAX embedded plot creation completed.\n";
                }
                else if (IsIsocenterMethodSelected)
                {
                    OutputLog += "Using 2D Isocenter Planar evaluation method for plot...\n";
                    // TODO: Implement 2D Isocenter plotting logic
                    MessageBox.Show("2D Isocenter Planar plotting not yet implemented.",
                        "Feature Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Switch back to text view since plot isn't implemented
                    bTextVis = true;
                    bPlotVis = false;
                }
                else if (Is2DPlanarSelected)
                {
                    OutputLog += "Using 2D Normal Multiplanar P/V Interpolation evaluation method for plot...\n";
                    // TODO: Implement 2D Multiplanar plotting logic
                    MessageBox.Show("2D Normal Multiplanar P/V Interpolation plotting not yet implemented.",
                        "Feature Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Switch back to text view since plot isn't implemented
                    bTextVis = true;
                    bPlotVis = false;
                }
                else if (Is3DEvaluationSelected)
                {
                    OutputLog += "Using 3D Dose P/V Interpolation evaluation method for plot...\n";
                    // TODO: Implement 3D plotting logic
                    MessageBox.Show("3D Dose P/V Interpolation plotting not yet implemented.",
                        "Feature Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Switch back to text view since plot isn't implemented
                    bTextVis = true;
                    bPlotVis = false;
                }
                else
                {
                    OutputLog += "No evaluation method selected. Defaulting to 1D CAX...\n";

                    // Default to 1D CAX if nothing is selected
                    string tumorId = SelectedTumorId ?? "Unknown Tumor";
                    string beamId = SelectedBeamId ?? "Unknown Beam";
                    ShowEmbeddedDosePlot(_distances, _doseValues, _insideTumorFlags, _entryDist, _exitDist, tumorId, beamId);

                    OutputLog += "Default 1D CAX embedded plot creation completed.\n";
                }
            }
            catch (Exception ex)
            {
                OutputLog += $"Error creating embedded plot: {ex.Message}\n";
                MessageBox.Show($"Error creating plot: {ex.Message}", "Plot Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Switch back to text view on error
                bTextVis = true;
                bPlotVis = false;
            }
        }

        /// <summary>
        /// Creates an embedded plot of the dose along the central axis within the existing Canvas
        /// </summary>
        private void ShowEmbeddedDosePlot(List<double> distances, List<double> doseValues, List<bool> insideTumorFlags,
                                          double entryDist, double exitDist, string tumorId, string beamId)
        {
            try
            {
                OutputLog += "Starting ShowEmbeddedDosePlot...\n";

                if (PlotCanvas == null)
                {
                    OutputLog += "ERROR: PlotCanvas is null! Canvas reference not set from View.\n";
                    return;
                }

                // Clear canvas immediately
                PlotCanvas.Children.Clear();

                // Force a layout update to ensure canvas has proper dimensions
                PlotCanvas.UpdateLayout();

                // Use Loaded priority instead of ApplicationIdle for more reliable timing
                PlotCanvas.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    // Double-check canvas dimensions
                    if (PlotCanvas.ActualWidth <= 0 || PlotCanvas.ActualHeight <= 0)
                    {
                        OutputLog += "Canvas has invalid dimensions. Forcing layout update.\n";
                        PlotCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        PlotCanvas.Arrange(new Rect(0, 0, PlotCanvas.DesiredSize.Width, PlotCanvas.DesiredSize.Height));
                        PlotCanvas.UpdateLayout();
                    }

                    OutputLog += $"PlotCanvas dimensions: {PlotCanvas.ActualWidth} x {PlotCanvas.ActualHeight}\n";

                    DrawEmbeddedPlot(PlotCanvas, distances, doseValues, insideTumorFlags, entryDist, exitDist, tumorId, beamId);
                    OutputLog += $"DrawEmbeddedPlot completed. Canvas now has {PlotCanvas.Children.Count} children.\n";
                }));
            }
            catch (Exception ex)
            {
                OutputLog += $"Error in ShowEmbeddedDosePlot: {ex.Message}\n";
            }
        }

        /// <summary>
        /// Actually draws the plot on the provided canvas with improved sizing
        /// </summary>
        private void DrawEmbeddedPlot(Canvas plotCanvas, List<double> distances, List<double> doseValues,
                                     List<bool> insideTumorFlags, double entryDist, double exitDist,
                                     string tumorId, string beamId)
        {
            try
            {
                // Get actual dimensions
                double canvasWidth = plotCanvas.ActualWidth;
                double canvasHeight = plotCanvas.ActualHeight;

                OutputLog += $"DrawEmbeddedPlot - Canvas dimensions: {canvasWidth} x {canvasHeight}\n";

                // Check if canvas has valid dimensions
                if (canvasWidth <= 0 || canvasHeight <= 0 || double.IsNaN(canvasWidth) || double.IsNaN(canvasHeight))
                {
                    OutputLog += "Canvas has invalid dimensions. Cannot draw plot.\n";

                    // Try one more time to get dimensions
                    plotCanvas.UpdateLayout();
                    canvasWidth = plotCanvas.ActualWidth;
                    canvasHeight = plotCanvas.ActualHeight;

                    if (canvasWidth <= 0 || canvasHeight <= 0)
                    {
                        OutputLog += "Canvas still has invalid dimensions after UpdateLayout.\n";
                        return;
                    }
                }

                // Clear any existing content
                plotCanvas.Children.Clear();

                OutputLog += $"Using dimensions: {canvasWidth} x {canvasHeight}\n";

                // Use proportional margins that work better with smaller canvases
                //double leftMargin = Math.Max(50, canvasWidth * 0.08);
                double leftMargin = Math.Max(65, canvasWidth * 0.09);  // Just a bit more space
                double rightMargin = Math.Max(20, canvasWidth * 0.03);
                double topMargin = Math.Max(30, canvasHeight * 0.08);
                double bottomMargin = Math.Max(50, canvasHeight * 0.12);

                double plotWidth = canvasWidth - leftMargin - rightMargin;
                double plotHeight = canvasHeight - topMargin - bottomMargin;

                // Ensure we have positive plot dimensions
                if (plotWidth <= 0 || plotHeight <= 0)
                {
                    OutputLog += $"Invalid plot dimensions: {plotWidth} x {plotHeight}. Canvas too small.\n";
                    return;
                }

                OutputLog += $"Plot area: {plotWidth} x {plotHeight}, margins: L{leftMargin} R{rightMargin} T{topMargin} B{bottomMargin}\n";

                // Find min and max values for scaling
                double minDist = distances.Min();
                double maxDist = distances.Max();
                double maxDose = doseValues.Max() * 1.1; // Add 10% for margin

                OutputLog += $"Data ranges: Distance {minDist:F1} to {maxDist:F1}, Dose 0 to {maxDose:F3}\n";

                // Add title with responsive font size
                //double titleFontSize = Math.Max(10, Math.Min(16, canvasHeight * 0.04));
                double titleFontSize = Math.Max(14, Math.Min(20, canvasHeight * 0.06));

                TextBlock title = new TextBlock
                {
                    Text = $"Central Axis Dose Plot - {tumorId} - {beamId}",
                    FontSize = titleFontSize,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
                Canvas.SetLeft(title, canvasWidth / 2 - 150);
                Canvas.SetTop(title, 5);
                plotCanvas.Children.Add(title);

                // Create horizontal and vertical axes
                Line horizontalAxis = new Line
                {
                    X1 = leftMargin,
                    Y1 = canvasHeight - bottomMargin,
                    X2 = canvasWidth - rightMargin,
                    Y2 = canvasHeight - bottomMargin,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                Line verticalAxis = new Line
                {
                    X1 = leftMargin,
                    Y1 = topMargin,
                    X2 = leftMargin,
                    Y2 = canvasHeight - bottomMargin,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                plotCanvas.Children.Add(horizontalAxis);
                plotCanvas.Children.Add(verticalAxis);

                // Add axis labels with responsive font size
                //double labelFontSize = Math.Max(8, Math.Min(12, canvasHeight * 0.03));
                double labelFontSize = Math.Max(12, Math.Min(16, canvasHeight * 0.05));

                TextBlock xAxisLabel = new TextBlock
                {
                    Text = "Distance from Isocenter (mm)",
                    FontSize = labelFontSize,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Canvas.SetLeft(xAxisLabel, canvasWidth / 2 - 80);
                Canvas.SetTop(xAxisLabel, canvasHeight - 15);
                plotCanvas.Children.Add(xAxisLabel);

                TextBlock yAxisLabel = new TextBlock
                {
                    Text = "Dose (Gy)",
                    FontSize = labelFontSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    LayoutTransform = new RotateTransform(-90)
                };
                //Canvas.SetLeft(yAxisLabel, 15);
                Canvas.SetLeft(yAxisLabel, 10);
                Canvas.SetTop(yAxisLabel, canvasHeight / 2);
                plotCanvas.Children.Add(yAxisLabel);

                // Plot the data points
                Polyline doseLine = new Polyline
                {
                    Stroke = Brushes.Blue,
                    StrokeThickness = Math.Max(1, canvasWidth / 400) // Responsive line thickness
                };

                PointCollection points = new PointCollection();
                int validPointCount = 0;

                for (int i = 0; i < distances.Count; i++)
                {
                    // Convert data to canvas coordinates
                    double x = leftMargin + (distances[i] - minDist) / (maxDist - minDist) * plotWidth;
                    double y = (canvasHeight - bottomMargin) - (doseValues[i] / maxDose) * plotHeight;

                    // Check for valid coordinates
                    if (!double.IsNaN(x) && !double.IsNaN(y) && !double.IsInfinity(x) && !double.IsInfinity(y))
                    {
                        points.Add(new Point(x, y));
                        validPointCount++;

                        // If inside tumor, add a red dot with responsive size
                        if (insideTumorFlags[i])
                        {
                            double dotSize = Math.Max(2, Math.Min(6, canvasWidth / 150));
                            Ellipse dot = new Ellipse
                            {
                                Width = dotSize,
                                Height = dotSize,
                                Fill = Brushes.Red
                            };
                            Canvas.SetLeft(dot, x - dotSize / 2);
                            Canvas.SetTop(dot, y - dotSize / 2);
                            plotCanvas.Children.Add(dot);
                        }
                    }
                }

                OutputLog += $"Added {validPointCount} valid points to plot line.\n";

                doseLine.Points = points;
                plotCanvas.Children.Add(doseLine);

                // Add tumor boundary markers
                double entryX = leftMargin + (entryDist - minDist) / (maxDist - minDist) * plotWidth;
                double exitX = leftMargin + (exitDist - minDist) / (maxDist - minDist) * plotWidth;

                //double boundaryLabelFontSize = Math.Max(7, Math.Min(10, canvasHeight * 0.025));
                double boundaryLabelFontSize = Math.Max(10, Math.Min(14, canvasHeight * 0.04));

                // Entry line
                if (!double.IsNaN(entryX) && !double.IsInfinity(entryX))
                {
                    Line entryLine = new Line
                    {
                        X1 = entryX,
                        Y1 = topMargin,
                        X2 = entryX,
                        Y2 = canvasHeight - bottomMargin,
                        Stroke = Brushes.Green,
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 4, 2 }
                    };
                    plotCanvas.Children.Add(entryLine);

                    TextBlock entryLabel = new TextBlock
                    {
                        Text = "Entry",
                        Foreground = Brushes.Green,
                        FontSize = boundaryLabelFontSize
                    };
                    Canvas.SetLeft(entryLabel, entryX - 15);
                    Canvas.SetTop(entryLabel, topMargin + 5);
                    plotCanvas.Children.Add(entryLabel);
                }

                // Exit line
                if (!double.IsNaN(exitX) && !double.IsInfinity(exitX))
                {
                    Line exitLine = new Line
                    {
                        X1 = exitX,
                        Y1 = topMargin,
                        X2 = exitX,
                        Y2 = canvasHeight - bottomMargin,
                        Stroke = Brushes.Green,
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 4, 2 }
                    };
                    plotCanvas.Children.Add(exitLine);

                    TextBlock exitLabel = new TextBlock
                    {
                        Text = "Exit",
                        Foreground = Brushes.Green,
                        FontSize = boundaryLabelFontSize
                    };
                    Canvas.SetLeft(exitLabel, exitX - 10);
                    Canvas.SetTop(exitLabel, topMargin + 5);
                    plotCanvas.Children.Add(exitLabel);
                }

                // Add axis ticks and values with responsive sizing
                //double tickFontSize = Math.Max(6, Math.Min(9, canvasHeight * 0.02));
                double tickFontSize = Math.Max(10, Math.Min(14, canvasHeight * 0.035));

                // X-axis ticks - adjust number based on width
                int numXTicks = Math.Max(4, Math.Min(10, (int)(plotWidth / 60)));
                double xTickStep = (maxDist - minDist) / numXTicks;

                for (int i = 0; i <= numXTicks; i++)
                {
                    double tickValue = minDist + i * xTickStep;
                    double tickX = leftMargin + (tickValue - minDist) / (maxDist - minDist) * plotWidth;

                    Line tick = new Line
                    {
                        X1 = tickX,
                        Y1 = canvasHeight - bottomMargin,
                        X2 = tickX,
                        Y2 = canvasHeight - bottomMargin + 5,
                        Stroke = Brushes.Black,
                        StrokeThickness = 1
                    };

                    TextBlock tickLabel = new TextBlock
                    {
                        Text = string.Format("{0:F0}", tickValue),
                        FontSize = tickFontSize
                    };

                    Canvas.SetLeft(tickLabel, tickX - 15);
                    Canvas.SetTop(tickLabel, canvasHeight - bottomMargin + 8);

                    plotCanvas.Children.Add(tick);
                    plotCanvas.Children.Add(tickLabel);
                }

                // Y-axis ticks - adjust number based on height
                int numYTicks = Math.Max(3, Math.Min(7, (int)(plotHeight / 40)));
                double yTickStep = maxDose / numYTicks;

                for (int i = 0; i <= numYTicks; i++)
                {
                    double tickValue = i * yTickStep;
                    double tickY = (canvasHeight - bottomMargin) - (tickValue / maxDose) * plotHeight;

                    Line tick = new Line
                    {
                        X1 = leftMargin - 5,
                        Y1 = tickY,
                        X2 = leftMargin,
                        Y2 = tickY,
                        Stroke = Brushes.Black,
                        StrokeThickness = 1
                    };

                    TextBlock tickLabel = new TextBlock
                    {
                        Text = string.Format("{0:F1}", tickValue),
                        FontSize = tickFontSize
                    };

                    //Canvas.SetLeft(tickLabel, leftMargin - 40);
                    Canvas.SetLeft(tickLabel, leftMargin - 30);  // Move further from axis line
                    Canvas.SetTop(tickLabel, tickY - 8);

                    plotCanvas.Children.Add(tick);
                    plotCanvas.Children.Add(tickLabel);
                }

                OutputLog += $"Embedded plot drawn successfully. Total canvas children: {plotCanvas.Children.Count}\n";
            }
            catch (Exception ex)
            {
                OutputLog += $"Error drawing plot: {ex.Message}\n";
                OutputLog += $"Stack trace: {ex.StackTrace}\n";
            }
        }

        //public void update3DMetrics(string tumorName, string ptvAllName, PlanSetup plan)
        //{
        //    OutputLog += "Starting 3D Dosimetric Calculations \n";


        //    Structure structureForEval = _structureSet.Structures.FirstOrDefault(s => s.Id == tumorName);
        //    Structure ptvAll = _structureSet.Structures.FirstOrDefault(s => s.Id == ptvAllName);



        //    MetricData dosePerFrac = new MetricData();
        //    dosePerFrac.metric = "Prescription Dose / fraction ";
        //    dosePerFrac.value = plan.DosePerFraction.ToString();

        //    AllMetrics.Add(dosePerFrac);

        //    MetricData gtv = new MetricData();
        //    gtv.metric = "Gross Target Volume (cc)";
        //    gtv.value = Math.Round(structureForEval.Volume, 2).ToString();



        //    // Volume of vertices
        //    MetricData V_vertices = new MetricData();
        //    V_vertices.metric = "Volume of Vertices (cc) ";
        //    V_vertices.value = Math.Round(ptvAll.Volume, 2).ToString();

        //    // number of vertices
        //    MetricData ptvCount = new MetricData();
        //    ptvCount.metric = "Number of Vertices";
        //    ptvCount.value = ptvAll.GetNumberOfSeparateParts().ToString(); ;

        //    // percent of gtv that is total lattice volume
        //    MetricData latticeVolPercent = new MetricData();
        //    latticeVolPercent.metric = "percent of gtv that is total lattice volume";
        //    latticeVolPercent.value = Math.Round((100 * ptvAll.Volume / structureForEval.Volume), 2).ToString();

        //    // assume absolute dosevalue/volume presentations
        //    var absoluteVolume = VolumePresentation.AbsoluteCm3;
        //    var absoluteDoseValue = DoseValuePresentation.Absolute;

        //    AllMetrics.Clear();

        //    AllMetrics.Add(gtv);
        //    AllMetrics.Add(ptvCount); 
        //    AllMetrics.Add(V_vertices);
        //    AllMetrics.Add(latticeVolPercent);

        //    //D95
        //    MetricData d95 = new MetricData();
        //    d95.metric = "Dose covering 95% of target (D95)";
        //    d95.value = plan.GetDoseAtVolume(structureForEval, 95, absoluteVolume, absoluteDoseValue).ToString();

        //    AllMetrics.Add(d95);


        //}

        public void update3DMetrics(string tumorName, string ptvAllName, PlanSetup plan)
        {
            OutputLog += "Starting 3D Dosimetric Calculations \n";
            OutputLog += "Starting 3D Dosimetric Calculations \n";
            OutputLog += $"Selected tumor: {tumorName}, PTV All: {ptvAllName}\n";

            Structure structureForEval = _structureSet.Structures.FirstOrDefault(s => s.Id == tumorName);
            Structure ptvAll = _structureSet.Structures.FirstOrDefault(s => s.Id == ptvAllName);

            if (structureForEval == null)
            {
                OutputLog += $"Error: Could not find structure '{tumorName}'\n";
                return;
            }

            if (ptvAll == null)
            {
                OutputLog += $"Error: Could not find PTV ALL structure '{ptvAllName}'\n";
                return;
            }

            // Clear all previous metrics first
            AllMetrics.Clear();

            // Dose per fraction
            AllMetrics.Add(new MetricData
            {
                metric = "Prescription Dose per Fraction (Gy)",
                value = plan.DosePerFraction.ToString()
            });

            // Gross Target Volume
            AllMetrics.Add(new MetricData
            {
                metric = "Gross Target Volume (cc)",
                value = Math.Round(structureForEval.Volume, 2).ToString()
            });

            // Number of vertices (separate parts)
            AllMetrics.Add(new MetricData
            {
                metric = "Number of Vertices",
                value = ptvAll.GetNumberOfSeparateParts().ToString()
            });

            // Volume of vertices
            AllMetrics.Add(new MetricData
            {
                metric = "Total Volume of Vertices (cc)",
                value = Math.Round(ptvAll.Volume, 2).ToString()
            });

            // Percent of GTV that is total lattice volume
            double latticePercent = Math.Round((100 * ptvAll.Volume / structureForEval.Volume), 2);
            AllMetrics.Add(new MetricData
            {
                metric = "Percent of GTV Covered by Lattice (%)",
                value = latticePercent.ToString()
            });

            // D95 calculation
            try
            {
                var absoluteVolume = VolumePresentation.AbsoluteCm3;
                var absoluteDoseValue = DoseValuePresentation.Absolute;
                var d95Value = plan.GetDoseAtVolume(structureForEval, 95, absoluteVolume, absoluteDoseValue);

                AllMetrics.Add(new MetricData
                {
                    metric = "Dose Covering 95% of Target (D95) (Gy)",
                    value = Math.Round(d95Value.Dose, 3).ToString()
                });
                var d90Value = plan.GetDoseAtVolume(structureForEval, 90, absoluteVolume, absoluteDoseValue);
                AllMetrics.Add(new MetricData
                {
                    metric = "Dose Covering 90% of Target (D95) (Gy)",
                    value = Math.Round(d90Value.Dose, 3).ToString()
                });

                var d50Value = plan.GetDoseAtVolume(structureForEval, 50, absoluteVolume, absoluteDoseValue);
                AllMetrics.Add(new MetricData
                {
                    metric = "Dose Covering 50% of Target (D50) (Gy)",
                    value = Math.Round(d50Value.Dose, 3).ToString()
                });

                // d20
                var d20Value = plan.GetDoseAtVolume(structureForEval, 20, absoluteVolume, absoluteDoseValue);
                AllMetrics.Add(new MetricData
                {
                    metric = "Dose Covering 20% of Target (D20) (Gy)",
                    value = Math.Round(d20Value.Dose, 3).ToString()
                });
                
                // d10
                var d10Value = plan.GetDoseAtVolume(structureForEval, 50, absoluteVolume, absoluteDoseValue);
                AllMetrics.Add(new MetricData
                {
                    metric = "Dose Covering 10% of Target (D10) (Gy)",
                    value = Math.Round(d10Value.Dose, 3).ToString()
                });
                
                // d5
                var d5Value= plan.GetDoseAtVolume(structureForEval, 50, absoluteVolume, absoluteDoseValue);
                AllMetrics.Add(new MetricData
                {
                    metric = "Dose Covering 5% of Target (D5) (Gy)",
                    value = Math.Round(d5Value.Dose, 3).ToString()
                });

                // d95/d5
                AllMetrics.Add(new MetricData
                {
                    metric = "D95/D5",
                    value = Math.Round(d95Value.Dose/d5Value.Dose, 3).ToString()
                });

                // d10/d90
                AllMetrics.Add(new MetricData
                {
                    metric = "D10/D90",
                    value = Math.Round(d10Value.Dose/d90Value.Dose, 3).ToString()
                });

                // High dose core number density
                AllMetrics.Add(new MetricData
                {
                    metric = "High Dose Core Number Density (HCND)",
                    value = Math.Round(d10Value.Dose / d90Value.Dose, 3).ToString()
                });

            }
            catch (Exception ex)
            {
                OutputLog += $"Error calculating Dose: {ex.Message}\n";
                AllMetrics.Add(new MetricData
                {
                    metric = "Dose Covering x% of Target (Dx) (Gy)", // with the added doses I'm not sure how to report this best
                    value = "Error - Unable to calculate Dose/DVH"
                });
            }

            // Additional useful metrics
            AllMetrics.Add(new MetricData
            {
                metric = "Average Vertex Volume (cc)",
                value = Math.Round(ptvAll.Volume / ptvAll.GetNumberOfSeparateParts(), 3).ToString()
            });

            OutputLog += $"Added {AllMetrics.Count} metrics to collection\n";

            // Force UI update
            RaisePropertyChanged(nameof(AllMetrics));

            OutputLog += $"3D Dosimetric Calculations complete. {AllMetrics.Count} metrics calculated.\n";
        }

        /// <summary>
        /// Creates and displays a simple plot of the dose along the central axis (original popup window version)
        /// </summary>
        private void ShowDosePlot(List<double> distances, List<double> doseValues, List<bool> insideTumorFlags,
                                 double entryDist, double exitDist, string tumorId, string beamId)
        {
            try
            {
                // Create the plot window
                System.Windows.Window plotWindow = new System.Windows.Window
                {
                    Title = string.Format("Central Axis Dose Plot - {0} - {1}", tumorId, beamId),
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
                };

                // Create a grid to hold the plot components
                System.Windows.Controls.Grid mainGrid = new System.Windows.Controls.Grid();
                plotWindow.Content = mainGrid;

                // Add row definitions for the plot and legend
                mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(40, System.Windows.GridUnitType.Pixel) });

                // Create a canvas for the plot
                System.Windows.Controls.Canvas plotCanvas = new System.Windows.Controls.Canvas
                {
                    Background = System.Windows.Media.Brushes.White
                };
                System.Windows.Controls.Grid.SetRow(plotCanvas, 0);
                mainGrid.Children.Add(plotCanvas);

                // Create a panel for the legend
                System.Windows.Controls.StackPanel legendPanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new System.Windows.Thickness(0, 10, 0, 10)
                };
                System.Windows.Controls.Grid.SetRow(legendPanel, 1);
                mainGrid.Children.Add(legendPanel);

                // Use fixed margins for the plot area
                double leftMargin = 60;
                double rightMargin = 20;
                double topMargin = 30;
                double bottomMargin = 50;

                // Find min and max values for scaling
                double minDist = distances.Min();
                double maxDist = distances.Max();
                double maxDose = doseValues.Max() * 1.1; // Add 10% for margin

                // Wait for the canvas to be properly sized
                plotWindow.Loaded += (sender, e) =>
                {
                    try
                    {
                        // Calculate plot dimensions
                        double plotWidth = plotCanvas.ActualWidth - leftMargin - rightMargin;
                        double plotHeight = plotCanvas.ActualHeight - topMargin - bottomMargin;

                        // Create horizontal and vertical axes
                        System.Windows.Shapes.Line horizontalAxis = new System.Windows.Shapes.Line
                        {
                            X1 = leftMargin,
                            Y1 = plotCanvas.ActualHeight - bottomMargin,
                            X2 = plotCanvas.ActualWidth - rightMargin,
                            Y2 = plotCanvas.ActualHeight - bottomMargin,
                            Stroke = System.Windows.Media.Brushes.Black,
                            StrokeThickness = 1
                        };

                        System.Windows.Shapes.Line verticalAxis = new System.Windows.Shapes.Line
                        {
                            X1 = leftMargin,
                            Y1 = topMargin,
                            X2 = leftMargin,
                            Y2 = plotCanvas.ActualHeight - bottomMargin,
                            Stroke = System.Windows.Media.Brushes.Black,
                            StrokeThickness = 1
                        };

                        plotCanvas.Children.Add(horizontalAxis);
                        plotCanvas.Children.Add(verticalAxis);

                        // Add axis labels
                        System.Windows.Controls.TextBlock xAxisLabel = new System.Windows.Controls.TextBlock
                        {
                            Text = "Distance from Isocenter (mm)",
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            VerticalAlignment = System.Windows.VerticalAlignment.Bottom
                        };
                        System.Windows.Controls.Canvas.SetLeft(xAxisLabel, plotCanvas.ActualWidth / 2 - 80);
                        System.Windows.Controls.Canvas.SetTop(xAxisLabel, plotCanvas.ActualHeight - 20);
                        plotCanvas.Children.Add(xAxisLabel);

                        System.Windows.Controls.TextBlock yAxisLabel = new System.Windows.Controls.TextBlock
                        {
                            Text = "Dose (Gy)",
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            VerticalAlignment = System.Windows.VerticalAlignment.Center,
                            LayoutTransform = new System.Windows.Media.RotateTransform(-90)
                        };
                        System.Windows.Controls.Canvas.SetLeft(yAxisLabel, 5);
                        System.Windows.Controls.Canvas.SetTop(yAxisLabel, plotCanvas.ActualHeight / 2 - 20);
                        plotCanvas.Children.Add(yAxisLabel);

                        // Plot the data points
                        System.Windows.Shapes.Polyline doseLine = new System.Windows.Shapes.Polyline
                        {
                            Stroke = System.Windows.Media.Brushes.Blue,
                            StrokeThickness = 2
                        };

                        System.Windows.Media.PointCollection points = new System.Windows.Media.PointCollection();
                        bool hasValidPoints = false;

                        for (int i = 0; i < distances.Count; i++)
                        {
                            // Convert data to canvas coordinates
                            double x = leftMargin + (distances[i] - minDist) / (maxDist - minDist) * plotWidth;
                            double y = (plotCanvas.ActualHeight - bottomMargin) - (doseValues[i] / maxDose) * plotHeight;

                            // Check for valid coordinates before adding the point
                            if (!double.IsNaN(x) && !double.IsNaN(y) && !double.IsInfinity(x) && !double.IsInfinity(y))
                            {
                                points.Add(new System.Windows.Point(x, y));
                                hasValidPoints = true;

                                // If inside tumor, add a red dot
                                if (insideTumorFlags[i])
                                {
                                    System.Windows.Shapes.Ellipse dot = new System.Windows.Shapes.Ellipse
                                    {
                                        Width = 4,
                                        Height = 4,
                                        Fill = System.Windows.Media.Brushes.Red
                                    };
                                    System.Windows.Controls.Canvas.SetLeft(dot, x - 2);
                                    System.Windows.Controls.Canvas.SetTop(dot, y - 2);
                                    plotCanvas.Children.Add(dot);
                                }
                            }
                        }

                        if (hasValidPoints)
                        {
                            doseLine.Points = points;
                            plotCanvas.Children.Add(doseLine);

                            // Add tumor boundary markers
                            double entryX = leftMargin + (entryDist - minDist) / (maxDist - minDist) * plotWidth;
                            double exitX = leftMargin + (exitDist - minDist) / (maxDist - minDist) * plotWidth;

                            // Verify boundary coordinates are valid
                            if (!double.IsNaN(entryX) && !double.IsInfinity(entryX))
                            {
                                System.Windows.Shapes.Line entryLine = new System.Windows.Shapes.Line
                                {
                                    X1 = entryX,
                                    Y1 = topMargin,
                                    X2 = entryX,
                                    Y2 = plotCanvas.ActualHeight - bottomMargin,
                                    Stroke = System.Windows.Media.Brushes.Green,
                                    StrokeThickness = 1,
                                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 2 }
                                };
                                plotCanvas.Children.Add(entryLine);

                                System.Windows.Controls.TextBlock entryLabel = new System.Windows.Controls.TextBlock
                                {
                                    Text = "Entry",
                                    Foreground = System.Windows.Media.Brushes.Green
                                };
                                System.Windows.Controls.Canvas.SetLeft(entryLabel, entryX - 15);
                                System.Windows.Controls.Canvas.SetTop(entryLabel, topMargin);
                                plotCanvas.Children.Add(entryLabel);
                            }

                            if (!double.IsNaN(exitX) && !double.IsInfinity(exitX))
                            {
                                System.Windows.Shapes.Line exitLine = new System.Windows.Shapes.Line
                                {
                                    X1 = exitX,
                                    Y1 = topMargin,
                                    X2 = exitX,
                                    Y2 = plotCanvas.ActualHeight - bottomMargin,
                                    Stroke = System.Windows.Media.Brushes.Green,
                                    StrokeThickness = 1,
                                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 2 }
                                };
                                plotCanvas.Children.Add(exitLine);

                                System.Windows.Controls.TextBlock exitLabel = new System.Windows.Controls.TextBlock
                                {
                                    Text = "Exit",
                                    Foreground = System.Windows.Media.Brushes.Green
                                };
                                System.Windows.Controls.Canvas.SetLeft(exitLabel, exitX - 10);
                                System.Windows.Controls.Canvas.SetTop(exitLabel, topMargin);
                                plotCanvas.Children.Add(exitLabel);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Could not plot the data: No valid coordinates found.");
                            return;
                        }

                        // Add axis ticks and values
                        // X-axis
                        int numXTicks = 10;
                        double xTickStep = (maxDist - minDist) / numXTicks;

                        for (int i = 0; i <= numXTicks; i++)
                        {
                            double tickValue = minDist + i * xTickStep;
                            double tickX = leftMargin + (tickValue - minDist) / (maxDist - minDist) * plotWidth;

                            System.Windows.Shapes.Line tick = new System.Windows.Shapes.Line
                            {
                                X1 = tickX,
                                Y1 = plotCanvas.ActualHeight - bottomMargin,
                                X2 = tickX,
                                Y2 = plotCanvas.ActualHeight - bottomMargin + 5,
                                Stroke = System.Windows.Media.Brushes.Black,
                                StrokeThickness = 1
                            };

                            System.Windows.Controls.TextBlock tickLabel = new System.Windows.Controls.TextBlock
                            {
                                Text = string.Format("{0:F0}", tickValue),
                                FontSize = 10
                            };

                            System.Windows.Controls.Canvas.SetLeft(tickLabel, tickX - 10);
                            System.Windows.Controls.Canvas.SetTop(tickLabel, plotCanvas.ActualHeight - bottomMargin + 8);

                            plotCanvas.Children.Add(tick);
                            plotCanvas.Children.Add(tickLabel);
                        }

                        // Y-axis
                        int numYTicks = 5;
                        double yTickStep = maxDose / numYTicks;

                        for (int i = 0; i <= numYTicks; i++)
                        {
                            double tickValue = i * yTickStep;
                            double tickY = (plotCanvas.ActualHeight - bottomMargin) - (tickValue / maxDose) * plotHeight;

                            System.Windows.Shapes.Line tick = new System.Windows.Shapes.Line
                            {
                                X1 = leftMargin - 5,
                                Y1 = tickY,
                                X2 = leftMargin,
                                Y2 = tickY,
                                Stroke = System.Windows.Media.Brushes.Black,
                                StrokeThickness = 1
                            };

                            System.Windows.Controls.TextBlock tickLabel = new System.Windows.Controls.TextBlock
                            {
                                Text = string.Format("{0:F1}", tickValue),
                                FontSize = 10
                            };

                            System.Windows.Controls.Canvas.SetLeft(tickLabel, leftMargin - 30);
                            System.Windows.Controls.Canvas.SetTop(tickLabel, tickY - 7);

                            plotCanvas.Children.Add(tick);
                            plotCanvas.Children.Add(tickLabel);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            string.Format("Error rendering plot: {0}", ex.Message),
                            "Plot Rendering Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    }
                };

                // Add legend items
                // Dose line
                System.Windows.Shapes.Rectangle doseLineItem = new System.Windows.Shapes.Rectangle
                {
                    Width = 20,
                    Height = 2,
                    Fill = System.Windows.Media.Brushes.Blue,
                    Margin = new System.Windows.Thickness(5)
                };
                System.Windows.Controls.TextBlock doseLineLabel = new System.Windows.Controls.TextBlock
                {
                    Text = "Dose",
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new System.Windows.Thickness(5, 0, 15, 0)
                };

                // Inside tumor point
                System.Windows.Shapes.Ellipse insideTumorItem = new System.Windows.Shapes.Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = System.Windows.Media.Brushes.Red,
                    Margin = new System.Windows.Thickness(5)
                };
                System.Windows.Controls.TextBlock insideTumorLabel = new System.Windows.Controls.TextBlock
                {
                    Text = "Inside Tumor",
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new System.Windows.Thickness(5, 0, 15, 0)
                };

                // Tumor boundaries
                System.Windows.Shapes.Rectangle boundaryItem = new System.Windows.Shapes.Rectangle
                {
                    Width = 20,
                    Height = 2,
                    Fill = System.Windows.Media.Brushes.Green,
                    Margin = new System.Windows.Thickness(5)
                };
                System.Windows.Controls.TextBlock boundaryLabel = new System.Windows.Controls.TextBlock
                {
                    Text = "Tumor Boundaries",
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new System.Windows.Thickness(5, 0, 5, 0)
                };

                // Add items to legend
                legendPanel.Children.Add(doseLineItem);
                legendPanel.Children.Add(doseLineLabel);
                legendPanel.Children.Add(insideTumorItem);
                legendPanel.Children.Add(insideTumorLabel);
                legendPanel.Children.Add(boundaryLabel);

                // Show the plot window
                plotWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("Error creating plot: {0}", ex.Message),
                    "Plot Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }
}