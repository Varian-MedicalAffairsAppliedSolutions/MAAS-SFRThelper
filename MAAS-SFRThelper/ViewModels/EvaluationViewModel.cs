//using MAAS_SFRThelper.Models;
//using Prism.Commands;
//using Prism.Mvvm;
//using System;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Data;
//using System.Windows.Media;
//using System.Windows.Media.Media3D;
//using System.Windows.Shapes;
//using System.Windows.Threading;
//using VMS.TPS.Common.Model.API;
//using VMS.TPS.Common.Model.Types;

//namespace MAAS_SFRThelper.ViewModels
//{
//    public class EvaluationViewModel : BindableBase
//    {
//        // Structure selection
//        private string _selectedTumorId;
//        public string SelectedTumorId
//        {
//            get { return _selectedTumorId; }
//            set { SetProperty(ref _selectedTumorId, value); }
//        }
//        private string _ptvAllId;
//        public string PtvAllId
//        {
//            get { return _ptvAllId; }
//            set { SetProperty(ref _ptvAllId, value); }
//        }

//        //3D dose datagrid
//        public ObservableCollection<MetricData> AllMetrics { get; set; }
//        public ObservableCollection<string> PotentialTargets { get; set; }

//        // Beam selection
//        private string _selectedBeamId;
//        public string SelectedBeamId
//        {
//            get { return _selectedBeamId; }
//            set { SetProperty(ref _selectedBeamId, value); }
//        }

//        public ObservableCollection<string> TreatmentBeams { get; set; }

//        // Output log
//        private string _outputLog;
//        public string OutputLog
//        {
//            get { return _outputLog; }
//            set { SetProperty(ref _outputLog, value); }
//        }

//        //Is1DCAXSelected
//        private bool _is1DCAXSelected;

//        public bool Is1DCAXSelected
//        {
//            get { return _is1DCAXSelected; }
//            set { SetProperty(ref _is1DCAXSelected, value); }
//        }

//        //Is2DPlanarSelected
//        private bool _is2DPlanarSelected;

//        public bool Is2DPlanarSelected
//        {
//            get { return _is2DPlanarSelected; }
//            set { SetProperty(ref _is2DPlanarSelected, value); }
//        }

//        //Dose Metrics
//        private bool _is3dEvaluationSelected;

//        public bool Is3DEvaluationSelected
//        {
//            get { return _is3dEvaluationSelected; }
//            set { SetProperty(ref _is3dEvaluationSelected, value); }
//        }

//        //3D interpolation
//        private bool _is3dInterpolationSelected;

//        public bool Is3DInterpolationSelected
//        {
//            get { return _is3dInterpolationSelected; }
//            set { SetProperty(ref _is3dInterpolationSelected, value); }
//        }

//        //PVDR Options


//        private string _selectedPvdrMode;
//        public string SelectedPvdrMode
//        {
//            get => _selectedPvdrMode;
//            set => SetProperty(ref _selectedPvdrMode, value);
//        }

//        public ObservableCollection<string> PvdrModeOptions { get; set; }

//        // Compute status
//        private bool _canCompute;
//        public bool CanCompute
//        {
//            get { return _canCompute; }
//            set
//            {
//                if (SetProperty(ref _canCompute, value))
//                {
//                    ComputeCommand.RaiseCanExecuteChanged();
//                }
//            }
//        }

//        // Canvas reference (will be set from the View)
//        private Canvas _plotCanvas;
//        public Canvas PlotCanvas
//        {
//            get { return _plotCanvas; }
//            set
//            {
//                if (SetProperty(ref _plotCanvas, value))
//                {
//                    // Subscribe to size changed events for redrawing
//                    if (_plotCanvas != null)
//                    {
//                        _plotCanvas.SizeChanged += PlotCanvas_SizeChanged;
//                        _plotCanvas.Loaded += PlotCanvas_Loaded;
//                    }
//                }
//            }
//        }

//        // Flag to track if we have data to plot
//        private bool _hasPlotData = false;

//        // Visibility controls with mutual exclusivity
//        private bool _bTextVis = true;  // Start with text visible
//        public bool bTextVis
//        {
//            get { return _bTextVis; }
//            set
//            {
//                if (SetProperty(ref _bTextVis, value))
//                {
//                    OutputLog += $"bTextVis changed to: {value}\n";
//                    // If text view is being turned on, turn off plot view
//                    if (value && _bPlotVis)
//                    {
//                        _bPlotVis = false;
//                        OutputLog += "bPlotVis set to false (mutual exclusivity)\n";
//                        RaisePropertyChanged(nameof(bPlotVis));
//                    }
//                }
//            }
//        }

//        private bool _bPlotVis = false;  // Start with plot hidden
//        public bool bPlotVis
//        {
//            get { return _bPlotVis; }
//            set
//            {
//                if (SetProperty(ref _bPlotVis, value))
//                {
//                    OutputLog += $"bPlotVis changed to: {value}\n";
//                    // If plot view is being turned on, turn off text view
//                    if (value && _bTextVis)
//                    {
//                        _bTextVis = false;
//                        OutputLog += "bTextVis set to false (mutual exclusivity)\n";
//                        RaisePropertyChanged(nameof(bTextVis));
//                    }

//                    // Redraw plot when switching to plot view
//                    if (value && _hasPlotData)
//                    {
//                        RefreshPlot();
//                    }

//                    // Update RefreshPlotCommand availability
//                    RefreshPlotCommand?.RaiseCanExecuteChanged();
//                }
//            }
//        }

//        // Commands
//        public DelegateCommand ComputeCommand { get; private set; }
//        public DelegateCommand SaveCsvCommand { get; private set; }
//        public DelegateCommand ShowPlotCommand { get; private set; }
//        private DelegateCommand _refreshPlotCommand;
//        public DelegateCommand RefreshPlotCommand
//        {
//            get
//            {
//                if (_refreshPlotCommand == null)
//                {
//                    _refreshPlotCommand = new DelegateCommand(() =>
//                    {
//                        if (_hasPlotData && bPlotVis)
//                        {
//                            OutputLog += "Manual plot refresh triggered.\n";
//                            RefreshPlot();
//                        }
//                    }, () => _hasPlotData && bPlotVis);
//                }
//                return _refreshPlotCommand;
//            }
//        }

//        // Internal fields for the dose sampling
//        private List<double> _distances = new List<double>();
//        private List<double> _doseValues = new List<double>();
//        private List<bool> _insideTumorFlags = new List<bool>();

//        private double _entryDist;
//        private double _exitDist;

//        // ESAPI references
//        private PlanSetup _plan;
//        private StructureSet _structureSet;
//        private EsapiWorker _esapiWorker;

//        // For 2D grid storage and plotting
//        private double[,] _dose2DGrid;  // [nX, nY]
//        private double[,] _x2DGrid;     // [nX, nY]
//        private double[,] _y2DGrid;     // [nX, nY]
//        private bool[,] _inStruct2D;    // [nX, nY]
//        private int _nX2D, _nY2D;       // grid size
//        private bool _has2DPlotData = false;

//        public EvaluationViewModel(EsapiWorker esapiWorker)
//        {
//            try
//            {
//                // Initialize collections and parameters first
//                _distances = new List<double>();
//                _doseValues = new List<double>();
//                _insideTumorFlags = new List<bool>();


//                _esapiWorker = esapiWorker ?? throw new ArgumentNullException(nameof(esapiWorker), "ESAPI worker cannot be null");

//                // Initialize collections
//                PvdrModeOptions = new ObservableCollection<string> { "Fixed Beam", "VMAT" };
//                SelectedPvdrMode = PvdrModeOptions.First(); // default to "Fixed Beam"

//                PotentialTargets = new ObservableCollection<string>();
//                TreatmentBeams = new ObservableCollection<string>();
//                AllMetrics = new ObservableCollection<MetricData>();
//                OutputLog = "Starting initialization...\n";

//                // Enable collection synchronization for background updates
//                BindingOperations.EnableCollectionSynchronization(PotentialTargets, new object());
//                BindingOperations.EnableCollectionSynchronization(TreatmentBeams, new object());
//                BindingOperations.EnableCollectionSynchronization(AllMetrics, new object());

//                // Initialize commands
//                ComputeCommand = new DelegateCommand(ExecuteComputeDose, () => CanCompute);
//                SaveCsvCommand = new DelegateCommand(ExecuteSaveCsv, CanExecuteSaveCsv);
//                ShowPlotCommand = new DelegateCommand(ExecuteShowPlot, CanExecuteShowPlot);

//                // Set default evaluation method to 1D CAX
//                Is1DCAXSelected = true;

//                // Set initial visibility states
//                bTextVis = true;   // Show text log by default
//                bPlotVis = false;  // Hide plot by default

//                OutputLog += "Getting ESAPI context...\n";

//                // Get ESAPI context data
//                _esapiWorker.RunWithWait(sc =>
//                {
//                    try
//                    {
//                        if (sc == null)
//                        {
//                            OutputLog += "Error: ESAPI script context is null.\n";
//                            return;
//                        }

//                        _structureSet = sc.StructureSet;
//                        _plan = sc.PlanSetup;

//                        if (_structureSet == null)
//                        {
//                            OutputLog += "Warning: No structure set available.\n";
//                        }
//                        else
//                        {
//                            OutputLog += $"Structure set loaded: {_structureSet.Id}\n";
//                        }

//                        if (_plan == null)
//                        {
//                            OutputLog += "Warning: No plan available.\n";
//                        }
//                        else
//                        {
//                            OutputLog += $"Plan loaded: {_plan.Id}\n";
//                        }



//                    }
//                    catch (Exception ex)
//                    {
//                        OutputLog += $"Error initializing ESAPI context: {ex.Message}\n";
//                        if (ex.InnerException != null)
//                        {
//                            OutputLog += $"Inner exception: {ex.InnerException.Message}\n";
//                        }
//                    }
//                });

//                // Load structures and beams
//                SetStructures();
//                SetBeams();

//                OutputLog += "Initialization complete.\n";
//            }
//            catch (Exception ex)
//            {
//                string errorMessage = $"Critical error initializing EvaluationViewModel: {ex.Message}";
//                if (ex.InnerException != null)
//                {
//                    errorMessage += $"\nInner exception: {ex.InnerException.Message}";
//                }

//                // Try to log to output log if it was initialized
//                try
//                {
//                    if (OutputLog != null)
//                    {
//                        OutputLog += errorMessage + "\n";
//                    }
//                }
//                catch { /* Ignore if OutputLog couldn't be used */ }

//                MessageBox.Show(errorMessage, "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
//            }
//        }

//        private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
//        {
//            // Redraw plot when canvas size changes if we're in plot view and have data
//            if (bPlotVis && _hasPlotData)
//            {
//                // Use a small delay to ensure the canvas is properly resized
//                System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
//                {
//                    RefreshPlot();
//                }));
//            }
//        }

//        private void PlotCanvas_Loaded(object sender, RoutedEventArgs e)
//        {
//            // Initial plot draw when canvas is loaded
//            if (bPlotVis && _hasPlotData)
//            {
//                RefreshPlot();
//            }
//        }

//        private void RefreshPlot()
//        {
//            if (PlotCanvas == null || !_hasPlotData) return;

//            try
//            {
//                string tumorId = SelectedTumorId ?? "Unknown Tumor";
//                string beamId = SelectedBeamId ?? "Unknown Beam";

//                // Clear existing plot first
//                PlotCanvas.Children.Clear();

//                // Force layout update
//                PlotCanvas.UpdateLayout();

//                // Use Loaded priority for more reliable rendering
//                PlotCanvas.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
//                {
//                    DrawEmbeddedPlot(PlotCanvas, _distances, _doseValues, _insideTumorFlags, _entryDist, _exitDist, tumorId, beamId);
//                }));
//            }
//            catch (Exception ex)
//            {
//                OutputLog += $"Error refreshing plot: {ex.Message}\n";
//            }
//        }

//        private void SetStructures()
//        {
//            try
//            {
//                _esapiWorker.Run(sc =>
//                {
//                    try
//                    {
//                        if (_structureSet != null)
//                        {
//                            foreach (var structure in _structureSet.Structures
//                                .Where(s => !s.IsEmpty && s.DicomType != "EXTERNAL" && s.DicomType != "BODY"))
//                            {
//                                PotentialTargets.Add(structure.Id);
//                            }

//                            OutputLog += $"Found {PotentialTargets.Count} structures\n";

//                            // Set default selection
//                            if (PotentialTargets.Any())
//                            {
//                                SelectedTumorId = PotentialTargets.FirstOrDefault(id => id.Equals("PTV", StringComparison.OrdinalIgnoreCase))
//                                            ?? PotentialTargets.FirstOrDefault();

//                                if (SelectedTumorId != null)
//                                {
//                                    OutputLog += $"Selected tumor: {SelectedTumorId}\n";
//                                }
//                            }
//                        }
//                        else
//                        {
//                            OutputLog += "No structure set available.\n";
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        OutputLog += $"Error loading structures: {ex.Message}\n";
//                    }
//                });
//            }
//            catch (Exception ex)
//            {
//                OutputLog += $"Critical error in SetStructures: {ex.Message}\n";
//                MessageBox.Show($"Error loading structures: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//            }
//        }

//        private void SetBeams()
//        {
//            try
//            {
//                _esapiWorker.Run(sc =>
//                {
//                    try
//                    {
//                        if (_plan != null)
//                        {
//                            foreach (var beam in _plan.Beams.Where(b => !b.IsSetupField))
//                            {
//                                TreatmentBeams.Add(beam.Id);
//                            }

//                            OutputLog += $"Found {TreatmentBeams.Count} beams\n";

//                            // Set default selection
//                            if (TreatmentBeams.Any())
//                            {
//                                SelectedBeamId = TreatmentBeams.FirstOrDefault();

//                                if (SelectedBeamId != null)
//                                {
//                                    OutputLog += $"Selected beam: {SelectedBeamId}\n";
//                                }
//                            }
//                        }
//                        else
//                        {
//                            OutputLog += "No plan available.\n";
//                        }

//                        // Update compute flag
//                        CanCompute = (_plan != null && !string.IsNullOrEmpty(SelectedBeamId) && !string.IsNullOrEmpty(SelectedTumorId));
//                        OutputLog += $"Can compute: {CanCompute}\n";
//                    }
//                    catch (Exception ex)
//                    {
//                        OutputLog += $"Error loading beams: {ex.Message}\n";
//                    }
//                });
//            }
//            catch (Exception ex)
//            {
//                OutputLog += $"Critical error in SetBeams: {ex.Message}\n";
//                MessageBox.Show($"Error loading beams: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//            }
//        }

//        private void ExecuteComputeDose()
//        {
//            try
//            {

//                OutputLog += "Starting dose computation...\n";

//                // Always reset plot states before computing!
//                System.Windows.Application.Current.Dispatcher.Invoke(() => {
//                    _hasPlotData = false;
//                    _has2DPlotData = false;
//                    bTextVis = true;
//                    bPlotVis = false;
//                });


//                // Copy selected IDs to local variables to avoid potential thread issues
//                string selectedBeamId = SelectedBeamId;
//                string selectedTumorId = SelectedTumorId;
//                //string ptvAllName = PtvAllId;
//                string ptvAllName = SelectedTumorId;
//                string selectedPvdrMode = SelectedPvdrMode;


//                if (string.IsNullOrEmpty(selectedBeamId))
//                {
//                    OutputLog += "No beam selected. Please select a beam.\n";
//                    return;
//                }

//                if (string.IsNullOrEmpty(selectedTumorId))
//                {
//                    OutputLog += "No structure selected. Please select a structure.\n";
//                    return;
//                }

//                _esapiWorker.RunWithWait(context =>
//                {
//                    try
//                    {
//                        var plan = _plan;
//                        if (plan == null)
//                        {
//                            OutputLog += "No plan available. Cannot compute dose.\n";
//                            return;
//                        }

//                        if (plan.Dose == null)
//                        {
//                            OutputLog += "No 3D dose is calculated for this plan. Please calculate dose first.\n";
//                            return;
//                        }

//                        if (Is1DCAXSelected)
//                        {
//                            Run1DPVDRMetric(selectedTumorId, plan);
//                            OutputLog += "1D CAX Dosimetrics complete\n";
//                            return;

//                        }


//                        if (Is2DPlanarSelected)
//                        {
//                            OutputLog += "Running 2D Planar computation...\n";
//                            Run2DPVDRMetric(selectedTumorId, plan);
//                            OutputLog += "2D Dosimetrics complete\n";
//                            /// just added below
//                            //if (Is2DPlanarSelected && _has2DPlotData)
//                            //{
//                            //    System.Windows.Application.Current.Dispatcher.Invoke(() =>
//                            //    {
//                            //        // make sure we’re in plot view
//                            //        bTextVis = false;
//                            //        bPlotVis = true;
//                            //        Show2DDoseHeatmapOnCanvas(PlotCanvas, _dose2DGrid, _inStruct2D);
//                            //        OutputLog += "2D dose heatmap plotted.\n";
//                            //    });
//                            //}

//                            return;
//                        }


//                        bool execute3DDose = Is3DEvaluationSelected;

//                        if (execute3DDose)
//                        {
//                            update3DMetrics(selectedTumorId,ptvAllName,plan);
//                            OutputLog += "3D Dosimetrics complete\n";
//                            return;

//                        }


//                    }

//                    catch (Exception ex)
//                    {
//                        OutputLog += $"Error during dose computation: {ex.Message}\n";
//                        if (ex.InnerException != null)
//                        {
//                            OutputLog += $"Inner Exception: {ex.InnerException.Message}\n";
//                        }
//                    }
//                });

//                if (Is2DPlanarSelected && _has2DPlotData)
//                {
//                    // bTextVis = false;
//                    // bPlotVis = true;
//                    Show2DDoseHeatmapOnCanvas(PlotCanvas, _dose2DGrid, _inStruct2D);
//                    OutputLog += "2D dose heatmap plotted.\n";
//                }

//                // Update the commands that depend on data availability
//                SaveCsvCommand.RaiseCanExecuteChanged();
//                ShowPlotCommand.RaiseCanExecuteChanged();
//                RefreshPlotCommand?.RaiseCanExecuteChanged();
//            }
//            catch (Exception ex)
//            {
//                OutputLog += $"Critical error in ExecuteComputeDose: {ex.Message}\n";
//                MessageBox.Show($"Error computing dose: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//            }
//        }

//        public bool CanExecuteSaveCsv()
//        {
//            try
//            {
//                if (_distances == null)
//                    return false;

//                return _distances.Count > 0;
//            }
//            catch
//            {
//                // If there's any error accessing the collection, assume we can't save
//                return false;
//            }
//        }

//        //public bool CanExecuteShowPlot()
//        //{
//        //    try
//        //    {
//        //        if (_distances == null || _doseValues == null || _insideTumorFlags == null)
//        //            return false;

//        //        return _distances.Count > 0 && _doseValues.Count > 0 && _insideTumorFlags.Count > 0;
//        //    }
//        //    catch
//        //    {
//        //        // If there's any error accessing the collections, assume we can't plot
//        //        return false;
//        //    }
//        //}

//        public bool CanExecuteShowPlot()
//        {
//            try
//            {
//                // 1D CAX ready?
//                if (Is1DCAXSelected)
//                    return _distances?.Count > 0;

//                // 2D Planar ready?
//                if (Is2DPlanarSelected)
//                    return _has2DPlotData;

//                // (optional) later: 3D readiness
//                if (Is3DEvaluationSelected)
//                    return false; // or hook up when you implement 3D plotting

//                return false;
//            }
//            catch
//            {
//                return false;
//            }
//        }


//        private void ExecuteSaveCsv()
//        {
//            try
//            {
//                // Use a simple direct approach with minimal ESAPI interaction
//                OutputLog += "Starting CSV save operation...\n";

//                if (_distances == null || _distances.Count == 0 ||
//                    _doseValues == null || _doseValues.Count == 0 ||
//                    _insideTumorFlags == null || _insideTumorFlags.Count == 0)
//                {
//                    OutputLog += "No data to save. Please compute dose first.\n";
//                    MessageBox.Show("No data available to save. Please compute dose first.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
//                    return;
//                }

//                // Safely create filename without any ESAPI object access
//                string planId = "NoPlan";
//                string beamId = "NoBeam";
//                string tumorId = "NoTumor";

//                // Use stored IDs which are already strings, not ESAPI objects
//                if (!string.IsNullOrEmpty(SelectedBeamId))
//                    beamId = SelectedBeamId.Replace(' ', '_').Replace('\\', '_').Replace('/', '_');

//                if (!string.IsNullOrEmpty(SelectedTumorId))
//                    tumorId = SelectedTumorId.Replace(' ', '_').Replace('\\', '_').Replace('/', '_');

//                // Build data in memory
//                var lines = new List<string>();
//                lines.Add("Distance(mm),Dose(Gy),IsInsideTumor");

//                int minCount = Math.Min(Math.Min(_distances.Count, _doseValues.Count), _insideTumorFlags.Count);

//                for (int i = 0; i < minCount; i++)
//                {
//                    string line = string.Format("{0:F1},{1:F3},{2}",
//                        _distances[i],
//                        _doseValues[i],
//                        _insideTumorFlags[i] ? "1" : "0");
//                    lines.Add(line);
//                }

//                // Create the file on desktop
//                try
//                {
//                    string fileName = String.Format("TumorDose_{0}_{1}_{2}_{3:yyyyMMdd_HHmmss}.csv",
//                        planId, beamId, tumorId, DateTime.Now);

//                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
//                    string filePath = System.IO.Path.Combine(desktopPath, fileName);

//                    // Write content all at once
//                    File.WriteAllLines(filePath, lines);

//                    OutputLog += "Data saved to: " + filePath + "\n";
//                    MessageBox.Show("Data saved to:\n" + filePath, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
//                }
//                catch (Exception ex)
//                {
//                    OutputLog += "Error writing to desktop: " + ex.Message + "\n";

//                    // Try Documents folder
//                    try
//                    {
//                        string fileName = String.Format("TumorDose_{0}_{1}_{2}_{3:yyyyMMdd_HHmmss}.csv",
//                            planId, beamId, tumorId, DateTime.Now);

//                        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
//                        string filePath = System.IO.Path.Combine(docPath, fileName);

//                        // Write content all at once
//                        File.WriteAllLines(filePath, lines);

//                        OutputLog += "Data saved to: " + filePath + "\n";
//                        MessageBox.Show("Data saved to:\n" + filePath, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
//                    }
//                    catch (Exception ex2)
//                    {
//                        OutputLog += "Error writing to documents folder: " + ex2.Message + "\n";
//                        MessageBox.Show("Could not save data. Please check the log for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                // Last resort error handling
//                try
//                {
//                    OutputLog += "Critical error: " + ex.Message + "\n";
//                    MessageBox.Show("Critical error saving data: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                }
//                catch
//                {
//                    // If even the MessageBox fails, we can't do much else
//                    MessageBox.Show("A critical error occurred.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                }
//            }
//        }

//        private void ExecuteShowPlot()
//        {
//            try
//            {
//                OutputLog += "Creating embedded plot...\n";

//                if (!CanExecuteShowPlot())
//                {
//                    MessageBox.Show("No dose data available to plot. Please compute dose first.",
//                        "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
//                    return;
//                }

//                OutputLog += "Switching to plot view...\n";
//                // Switch to plot view by setting the radio button states
//                bTextVis = false;
//                bPlotVis = true;

//                // Check which evaluation method is selected
//                if (Is1DCAXSelected)
//                {
//                    OutputLog += "Using 1D CAX evaluation method for embedded plot...\n";

//                    // Get the structure and beam IDs for the plot title
//                    string tumorId = SelectedTumorId ?? "Unknown Tumor";
//                    string beamId = SelectedBeamId ?? "Unknown Beam";

//                    // Show the 1D CAX dose plot in embedded canvas
//                    ShowEmbeddedDosePlot(_distances, _doseValues, _insideTumorFlags, _entryDist, _exitDist, tumorId, beamId);

//                    OutputLog += "1D CAX embedded plot creation completed.\n";
//                }

//                else if (Is2DPlanarSelected)
//                {
//                    OutputLog += "Rendering 2D planar dose heatmap...\n";
//                    // ensure we have the data
//                    if (!_has2DPlotData || _dose2DGrid == null)
//                    {
//                        MessageBox.Show("No 2D grid data available. Run 2D computation first.",
//                                        "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
//                        bTextVis = true;
//                        bPlotVis = false;
//                        return;
//                    }
//                    // draw it
//                    Show2DDoseHeatmapOnCanvas(PlotCanvas, _dose2DGrid, _inStruct2D);
//                    OutputLog += "2D heatmap drawn successfully.\n";
//                }
//                //{
//                //    OutputLog += "Using 2D Normal Multiplanar P/V Interpolation evaluation method for plot...\n";
//                //    // TODO: Implement 2D Multiplanar plotting logic
//                //    MessageBox.Show("2D Normal Multiplanar P/V Interpolation plotting not yet implemented.",
//                //        "Feature Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
//                //    // Switch back to text view since plot isn't implemented
//                //    bTextVis = true;
//                //    bPlotVis = false;
//                //}
//                else if (Is3DEvaluationSelected)
//                {
//                    OutputLog += "Using 3D Dose P/V Interpolation evaluation method for plot...\n";
//                    // TODO: Implement 3D plotting logic
//                    MessageBox.Show("3D Dose P/V Interpolation plotting not yet implemented.",
//                        "Feature Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
//                    // Switch back to text view since plot isn't implemented
//                    bTextVis = true;
//                    bPlotVis = false;
//                }
//                else
//                {
//                    OutputLog += "No evaluation method selected. Defaulting to 1D CAX...\n";

//                    // Default to 1D CAX if nothing is selected
//                    string tumorId = SelectedTumorId ?? "Unknown Tumor";
//                    string beamId = SelectedBeamId ?? "Unknown Beam";
//                    ShowEmbeddedDosePlot(_distances, _doseValues, _insideTumorFlags, _entryDist, _exitDist, tumorId, beamId);

//                    OutputLog += "Default 1D CAX embedded plot creation completed.\n";
//                }
//            }
//            catch (Exception ex)
//            {
//                OutputLog += $"Error creating embedded plot: {ex.Message}\n";
//                MessageBox.Show($"Error creating plot: {ex.Message}", "Plot Error", MessageBoxButton.OK, MessageBoxImage.Error);
//                // Switch back to text view on error
//                bTextVis = true;
//                bPlotVis = false;
//            }
//        }

//        /// <summary>
//        /// Creates an embedded plot of the dose along the central axis within the existing Canvas
//        /// </summary>
//        private void ShowEmbeddedDosePlot(List<double> distances, List<double> doseValues, List<bool> insideTumorFlags,
//                                          double entryDist, double exitDist, string tumorId, string beamId)
//        {
//            try
//            {
//                OutputLog += "Starting ShowEmbeddedDosePlot...\n";

//                if (PlotCanvas == null)
//                {
//                    OutputLog += "ERROR: PlotCanvas is null! Canvas reference not set from View.\n";
//                    return;
//                }

//                // Clear canvas immediately
//                PlotCanvas.Children.Clear();

//                // Force a layout update to ensure canvas has proper dimensions
//                PlotCanvas.UpdateLayout();

//                // Use Loaded priority instead of ApplicationIdle for more reliable timing
//                PlotCanvas.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
//                {
//                    // Double-check canvas dimensions
//                    if (PlotCanvas.ActualWidth <= 0 || PlotCanvas.ActualHeight <= 0)
//                    {
//                        OutputLog += "Canvas has invalid dimensions. Forcing layout update.\n";
//                        PlotCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
//                        PlotCanvas.Arrange(new Rect(0, 0, PlotCanvas.DesiredSize.Width, PlotCanvas.DesiredSize.Height));
//                        PlotCanvas.UpdateLayout();
//                    }

//                    OutputLog += $"PlotCanvas dimensions: {PlotCanvas.ActualWidth} x {PlotCanvas.ActualHeight}\n";

//                    DrawEmbeddedPlot(PlotCanvas, distances, doseValues, insideTumorFlags, entryDist, exitDist, tumorId, beamId);
//                    OutputLog += $"DrawEmbeddedPlot completed. Canvas now has {PlotCanvas.Children.Count} children.\n";
//                }));
//            }
//            catch (Exception ex)
//            {
//                OutputLog += $"Error in ShowEmbeddedDosePlot: {ex.Message}\n";
//            }
//        }

//        /// <summary>
//        /// Actually draws the plot on the provided canvas with improved sizing
//        /// </summary>
//        private void DrawEmbeddedPlot(Canvas plotCanvas, List<double> distances, List<double> doseValues,
//                                     List<bool> insideTumorFlags, double entryDist, double exitDist,
//                                     string tumorId, string beamId)
//        {
//            try
//            {
//                // Get actual dimensions
//                double canvasWidth = plotCanvas.ActualWidth;
//                double canvasHeight = plotCanvas.ActualHeight;

//                OutputLog += $"DrawEmbeddedPlot - Canvas dimensions: {canvasWidth} x {canvasHeight}\n";

//                // Check if canvas has valid dimensions
//                if (canvasWidth <= 0 || canvasHeight <= 0 || double.IsNaN(canvasWidth) || double.IsNaN(canvasHeight))
//                {
//                    OutputLog += "Canvas has invalid dimensions. Cannot draw plot.\n";

//                    // Try one more time to get dimensions
//                    plotCanvas.UpdateLayout();
//                    canvasWidth = plotCanvas.ActualWidth;
//                    canvasHeight = plotCanvas.ActualHeight;

//                    if (canvasWidth <= 0 || canvasHeight <= 0)
//                    {
//                        OutputLog += "Canvas still has invalid dimensions after UpdateLayout.\n";
//                        return;
//                    }
//                }

//                // Clear any existing content
//                plotCanvas.Children.Clear();

//                OutputLog += $"Using dimensions: {canvasWidth} x {canvasHeight}\n";

//                // Use proportional margins that work better with smaller canvases
//                //double leftMargin = Math.Max(50, canvasWidth * 0.08);
//                double leftMargin = Math.Max(65, canvasWidth * 0.09);  // Just a bit more space
//                double rightMargin = Math.Max(20, canvasWidth * 0.03);
//                double topMargin = Math.Max(30, canvasHeight * 0.08);
//                double bottomMargin = Math.Max(50, canvasHeight * 0.12);

//                double plotWidth = canvasWidth - leftMargin - rightMargin;
//                double plotHeight = canvasHeight - topMargin - bottomMargin;

//                // Ensure we have positive plot dimensions
//                if (plotWidth <= 0 || plotHeight <= 0)
//                {
//                    OutputLog += $"Invalid plot dimensions: {plotWidth} x {plotHeight}. Canvas too small.\n";
//                    return;
//                }

//                OutputLog += $"Plot area: {plotWidth} x {plotHeight}, margins: L{leftMargin} R{rightMargin} T{topMargin} B{bottomMargin}\n";

//                // Find min and max values for scaling
//                double minDist = distances.Min();
//                double maxDist = distances.Max();
//                double maxDose = doseValues.Max() * 1.1; // Add 10% for margin

//                OutputLog += $"Data ranges: Distance {minDist:F1} to {maxDist:F1}, Dose 0 to {maxDose:F3}\n";

//                // Add title with responsive font size
//                //double titleFontSize = Math.Max(10, Math.Min(16, canvasHeight * 0.04));
//                double titleFontSize = Math.Max(14, Math.Min(20, canvasHeight * 0.06));

//                TextBlock title = new TextBlock
//                {
//                    Text = $"Central Axis Dose Plot - {tumorId} - {beamId}",
//                    FontSize = titleFontSize,
//                    FontWeight = FontWeights.Bold,
//                    HorizontalAlignment = HorizontalAlignment.Center,
//                    TextWrapping = TextWrapping.Wrap
//                };
//                Canvas.SetLeft(title, canvasWidth / 2 - 150);
//                Canvas.SetTop(title, 5);
//                plotCanvas.Children.Add(title);

//                // Create horizontal and vertical axes
//                Line horizontalAxis = new Line
//                {
//                    X1 = leftMargin,
//                    Y1 = canvasHeight - bottomMargin,
//                    X2 = canvasWidth - rightMargin,
//                    Y2 = canvasHeight - bottomMargin,
//                    Stroke = Brushes.Black,
//                    StrokeThickness = 1
//                };

//                Line verticalAxis = new Line
//                {
//                    X1 = leftMargin,
//                    Y1 = topMargin,
//                    X2 = leftMargin,
//                    Y2 = canvasHeight - bottomMargin,
//                    Stroke = Brushes.Black,
//                    StrokeThickness = 1
//                };

//                plotCanvas.Children.Add(horizontalAxis);
//                plotCanvas.Children.Add(verticalAxis);

//                // Add axis labels with responsive font size
//                //double labelFontSize = Math.Max(8, Math.Min(12, canvasHeight * 0.03));
//                double labelFontSize = Math.Max(12, Math.Min(16, canvasHeight * 0.05));

//                TextBlock xAxisLabel = new TextBlock
//                {
//                    Text = "Distance from Isocenter (mm)",
//                    FontSize = labelFontSize,
//                    HorizontalAlignment = HorizontalAlignment.Center
//                };
//                Canvas.SetLeft(xAxisLabel, canvasWidth / 2 - 80);
//                Canvas.SetTop(xAxisLabel, canvasHeight - 15);
//                plotCanvas.Children.Add(xAxisLabel);

//                TextBlock yAxisLabel = new TextBlock
//                {
//                    Text = "Dose (Gy)",
//                    FontSize = labelFontSize,
//                    HorizontalAlignment = HorizontalAlignment.Center,
//                    LayoutTransform = new RotateTransform(-90)
//                };
//                //Canvas.SetLeft(yAxisLabel, 15);
//                Canvas.SetLeft(yAxisLabel, 10);
//                Canvas.SetTop(yAxisLabel, canvasHeight / 2);
//                plotCanvas.Children.Add(yAxisLabel);

//                // Plot the data points
//                Polyline doseLine = new Polyline
//                {
//                    Stroke = Brushes.Blue,
//                    StrokeThickness = Math.Max(1, canvasWidth / 400) // Responsive line thickness
//                };

//                PointCollection points = new PointCollection();
//                int validPointCount = 0;

//                for (int i = 0; i < distances.Count; i++)
//                {
//                    // Convert data to canvas coordinates
//                    double x = leftMargin + (distances[i] - minDist) / (maxDist - minDist) * plotWidth;
//                    double y = (canvasHeight - bottomMargin) - (doseValues[i] / maxDose) * plotHeight;

//                    // Check for valid coordinates
//                    if (!double.IsNaN(x) && !double.IsNaN(y) && !double.IsInfinity(x) && !double.IsInfinity(y))
//                    {
//                        points.Add(new Point(x, y));
//                        validPointCount++;

//                        // If inside tumor, add a red dot with responsive size
//                        if (insideTumorFlags[i])
//                        {
//                            double dotSize = Math.Max(2, Math.Min(6, canvasWidth / 150));
//                            Ellipse dot = new Ellipse
//                            {
//                                Width = dotSize,
//                                Height = dotSize,
//                                Fill = Brushes.Red
//                            };
//                            Canvas.SetLeft(dot, x - dotSize / 2);
//                            Canvas.SetTop(dot, y - dotSize / 2);
//                            plotCanvas.Children.Add(dot);
//                        }
//                    }
//                }

//                OutputLog += $"Added {validPointCount} valid points to plot line.\n";

//                doseLine.Points = points;
//                plotCanvas.Children.Add(doseLine);

//                // Add tumor boundary markers
//                double entryX = leftMargin + (entryDist - minDist) / (maxDist - minDist) * plotWidth;
//                double exitX = leftMargin + (exitDist - minDist) / (maxDist - minDist) * plotWidth;

//                //double boundaryLabelFontSize = Math.Max(7, Math.Min(10, canvasHeight * 0.025));
//                double boundaryLabelFontSize = Math.Max(10, Math.Min(14, canvasHeight * 0.04));

//                // Entry line
//                if (!double.IsNaN(entryX) && !double.IsInfinity(entryX))
//                {
//                    Line entryLine = new Line
//                    {
//                        X1 = entryX,
//                        Y1 = topMargin,
//                        X2 = entryX,
//                        Y2 = canvasHeight - bottomMargin,
//                        Stroke = Brushes.Green,
//                        StrokeThickness = 1,
//                        StrokeDashArray = new DoubleCollection { 4, 2 }
//                    };
//                    plotCanvas.Children.Add(entryLine);

//                    TextBlock entryLabel = new TextBlock
//                    {
//                        Text = "Entry",
//                        Foreground = Brushes.Green,
//                        FontSize = boundaryLabelFontSize
//                    };
//                    Canvas.SetLeft(entryLabel, entryX - 15);
//                    Canvas.SetTop(entryLabel, topMargin + 5);
//                    plotCanvas.Children.Add(entryLabel);
//                }

//                // Exit line
//                if (!double.IsNaN(exitX) && !double.IsInfinity(exitX))
//                {
//                    Line exitLine = new Line
//                    {
//                        X1 = exitX,
//                        Y1 = topMargin,
//                        X2 = exitX,
//                        Y2 = canvasHeight - bottomMargin,
//                        Stroke = Brushes.Green,
//                        StrokeThickness = 1,
//                        StrokeDashArray = new DoubleCollection { 4, 2 }
//                    };
//                    plotCanvas.Children.Add(exitLine);

//                    TextBlock exitLabel = new TextBlock
//                    {
//                        Text = "Exit",
//                        Foreground = Brushes.Green,
//                        FontSize = boundaryLabelFontSize
//                    };
//                    Canvas.SetLeft(exitLabel, exitX - 10);
//                    Canvas.SetTop(exitLabel, topMargin + 5);
//                    plotCanvas.Children.Add(exitLabel);
//                }

//                // Add axis ticks and values with responsive sizing
//                //double tickFontSize = Math.Max(6, Math.Min(9, canvasHeight * 0.02));
//                double tickFontSize = Math.Max(10, Math.Min(14, canvasHeight * 0.035));

//                // X-axis ticks - adjust number based on width
//                int numXTicks = Math.Max(4, Math.Min(10, (int)(plotWidth / 60)));
//                double xTickStep = (maxDist - minDist) / numXTicks;

//                for (int i = 0; i <= numXTicks; i++)
//                {
//                    double tickValue = minDist + i * xTickStep;
//                    double tickX = leftMargin + (tickValue - minDist) / (maxDist - minDist) * plotWidth;

//                    Line tick = new Line
//                    {
//                        X1 = tickX,
//                        Y1 = canvasHeight - bottomMargin,
//                        X2 = tickX,
//                        Y2 = canvasHeight - bottomMargin + 5,
//                        Stroke = Brushes.Black,
//                        StrokeThickness = 1
//                    };

//                    TextBlock tickLabel = new TextBlock
//                    {
//                        Text = string.Format("{0:F0}", tickValue),
//                        FontSize = tickFontSize
//                    };

//                    Canvas.SetLeft(tickLabel, tickX - 15);
//                    Canvas.SetTop(tickLabel, canvasHeight - bottomMargin + 8);

//                    plotCanvas.Children.Add(tick);
//                    plotCanvas.Children.Add(tickLabel);
//                }

//                // Y-axis ticks - adjust number based on height
//                int numYTicks = Math.Max(3, Math.Min(7, (int)(plotHeight / 40)));
//                double yTickStep = maxDose / numYTicks;

//                for (int i = 0; i <= numYTicks; i++)
//                {
//                    double tickValue = i * yTickStep;
//                    double tickY = (canvasHeight - bottomMargin) - (tickValue / maxDose) * plotHeight;

//                    Line tick = new Line
//                    {
//                        X1 = leftMargin - 5,
//                        Y1 = tickY,
//                        X2 = leftMargin,
//                        Y2 = tickY,
//                        Stroke = Brushes.Black,
//                        StrokeThickness = 1
//                    };

//                    TextBlock tickLabel = new TextBlock
//                    {
//                        Text = string.Format("{0:F1}", tickValue),
//                        FontSize = tickFontSize
//                    };

//                    //Canvas.SetLeft(tickLabel, leftMargin - 40);
//                    Canvas.SetLeft(tickLabel, leftMargin - 30);  // Move further from axis line
//                    Canvas.SetTop(tickLabel, tickY - 8);

//                    plotCanvas.Children.Add(tick);
//                    plotCanvas.Children.Add(tickLabel);
//                }

//                OutputLog += $"Embedded plot drawn successfully. Total canvas children: {plotCanvas.Children.Count}\n";
//            }
//            catch (Exception ex)
//            {
//                OutputLog += $"Error drawing plot: {ex.Message}\n";
//                OutputLog += $"Stack trace: {ex.StackTrace}\n";
//            }
//        }

//        public void Run1DPVDRMetric(string tumorId, PlanSetup plan)
//        {
//              // Clear previous data first
//                _distances = new List<double>();
//                _doseValues = new List<double>();
//                _insideTumorFlags = new List<bool>();
//                _hasPlotData = false;

//                OutputLog += "Starting dose computation...\n";

//                string selectedBeamId = SelectedBeamId;
//                string selectedTumorId = SelectedTumorId;

//                // Get actual beam and structure objects from their IDs
//                Beam beam = null;
//                Structure tumor = null;


//                beam = plan.Beams.FirstOrDefault(b => b.Id == selectedBeamId);
//                if (beam == null)
//                {
//                    OutputLog += $"Could not find beam with ID '{selectedBeamId}'. Please select another beam.\n";
//                    return;
//                }

//                if (_structureSet == null)
//                {
//                    OutputLog += "No structure set available. Cannot compute dose.\n";
//                    return;
//                }

//                tumor = _structureSet.Structures.FirstOrDefault(s => s.Id == selectedTumorId);
//                if (tumor == null)
//                {
//                    OutputLog += $"Could not find structure with ID '{selectedTumorId}'. Please select another structure.\n";
//                    return;
//                }

//                // Calculate beam direction
//                OutputLog += "Calculating beam direction...\n";
//                var isocenter = beam.IsocenterPosition;
//                var cp0 = beam.ControlPoints.First();

//                // Calculate the beam direction using gantry and couch angles
//                double gantryAngle = cp0.GantryAngle;
//                double couchAngle = cp0.PatientSupportAngle;

//                // Convert angles to radians
//                double gantryRad = gantryAngle * Math.PI / 180.0;
//                double couchRad = couchAngle * Math.PI / 180.0;

//                // Calculate direction vector from gantry and couch angles
//                VVector dVec = new VVector(
//                    Math.Sin(gantryRad),
//                    -Math.Cos(gantryRad),
//                    0
//                );

//                // Apply couch rotation if needed
//                if (Math.Abs(couchAngle) > 0.1)
//                {
//                    double x = dVec.x;
//                    double z = dVec.z;
//                    dVec.x = x * Math.Cos(couchRad) + z * Math.Sin(couchRad);
//                    dVec.z = -x * Math.Sin(couchRad) + z * Math.Cos(couchRad);
//                }

//                // Normalize to unit vector
//                double length = Math.Sqrt(dVec.x * dVec.x + dVec.y * dVec.y + dVec.z * dVec.z);
//                VVector direction = new VVector(dVec.x / length, dVec.y / length, dVec.z / length);

//                // Find entry/exit by scanning
//                OutputLog += "Finding beam entry and exit points...\n";
//                double searchStartDist = -300.0;
//                double searchEndDist = 300.0;
//                double stepSize = 2.0; // Increased for stability
//                bool insideTumor = false;
//                _entryDist = double.NaN;
//                _exitDist = double.NaN;

//                // Temporary storage for computed data
//                var tempDistances = new List<double>();
//                var tempDoseValues = new List<double>();
//                var tempInsideFlags = new List<bool>();

//                for (double dist = searchStartDist; dist <= searchEndDist; dist += stepSize)
//                {
//                    var point = isocenter + dist * direction;
//                    bool pointInTumor = false;

//                    try
//                    {
//                        pointInTumor = tumor.IsPointInsideSegment(point);
//                    }
//                    catch (Exception ex)
//                    {
//                        OutputLog += $"Error checking point: {ex.Message}\n";
//                        continue;
//                    }

//                    if (!insideTumor && pointInTumor)
//                    {
//                        _entryDist = dist;
//                        insideTumor = true;
//                    }
//                    else if (insideTumor && !pointInTumor)
//                    {
//                        _exitDist = dist;
//                        break;
//                    }
//                }

//                if (double.IsNaN(_entryDist) || double.IsNaN(_exitDist))
//                {
//                    OutputLog += "Beam does not intersect the tumor structure.\n";
//                    return;
//                }

//                // Sample the dose within the tumor region
//                OutputLog += "Sampling dose along beam path...\n";
//                double margin = 5.0; // Increased margins
//                double startDist = _entryDist - margin;
//                double endDist = _exitDist + margin;
//                stepSize = 1.0; // Normal step size for sampling

//                for (double dist = startDist; dist <= endDist; dist += stepSize)
//                {
//                    try
//                    {
//                        var samplePoint = isocenter + dist * direction;
//                        bool isInside = tumor.IsPointInsideSegment(samplePoint);

//                        // Check if dose value is accessible
//                        DoseValue doseValue = plan.Dose.GetDoseToPoint(samplePoint);
//                        if (doseValue == null)
//                        {
//                            OutputLog += $"Null dose value at distance {dist}\n";
//                            continue;
//                        }

//                        double doseInGy = doseValue.Dose;

//                        // Store in temporary lists
//                        tempDistances.Add(dist);
//                        tempDoseValues.Add(doseInGy);
//                        tempInsideFlags.Add(isInside);
//                    }
//                    catch (Exception ex)
//                    {
//                        OutputLog += $"Error sampling point at distance {dist}: {ex.Message}\n";
//                    }
//                }

//                // If we have data, transfer to the main lists
//                if (tempDistances.Count > 0)
//                {
//                    _distances = new List<double>(tempDistances);
//                    _doseValues = new List<double>(tempDoseValues);
//                    _insideTumorFlags = new List<bool>(tempInsideFlags);
//                    _hasPlotData = true;
//                }

//                if (_distances.Count == 0)
//                {
//                    OutputLog += "No valid dose samples collected.\n";
//                    return;
//                }

//                // Compute basic stats
//                OutputLog += "Computing statistics...\n";
//                var tumorDoses = new List<double>();
//                for (int i = 0; i < _doseValues.Count; i++)
//                {
//                    if (_insideTumorFlags[i]) tumorDoses.Add(_doseValues[i]);
//                }

//                // Avoid divide by zero by checking count
//                double maxDose = tumorDoses.Count > 0 ? tumorDoses.Max() : 0.0;
//                double minDose = tumorDoses.Count > 0 ? tumorDoses.Min() : 0.0;
//                double avgDose = tumorDoses.Count > 0 ? tumorDoses.Average() : 0.0;

//                // Update output log
//                OutputLog += "===== Computation Complete =====\n";
//                OutputLog += $"Plan: {plan.Id}, Beam: {beam.Id}, Structure: {tumor.Id}\n";
//                OutputLog += $"Entry Dist: {_entryDist:F1} mm, Exit Dist: {_exitDist:F1} mm\n";
//                OutputLog += $"Tumor length along axis: {_exitDist - _entryDist:F1} mm\n";
//                OutputLog += $"Max Dose: {maxDose:F3} Gy\n";
//                OutputLog += $"Min Dose: {minDose:F3} Gy\n";
//                OutputLog += $"Avg Dose: {avgDose:F3} Gy\n";
//                OutputLog += $"Total samples: {_distances.Count}\n";
//                OutputLog += "================================\n";

//        }


//        public void Run2DPVDRMetric(string tumorId, PlanSetup plan)
//        {
//            try
//            {
//                OutputLog += "\nStarting 2D PVDR analysis...\n";
//                string selectedPvdrMode = SelectedPvdrMode;

//                if (selectedPvdrMode == "Fixed Beam")
//                {

//                    // === YOUR EXISTING FIXED BEAM PLANAR LOGIC HERE ===
//                    // (grid, projection, P/V sampling, etc.)

//                    if (selectedPvdrMode == "Fixed Beam")
//                    {
//                        OutputLog += "\n===== Enhanced Fixed Beam 2D PVDR Analysis =====\n";

//                        // 1. Get the beam and structure
//                        var beam = plan.Beams.FirstOrDefault(b => b.Id == SelectedBeamId);
//                        if (beam == null)
//                        {
//                            OutputLog += "Selected beam not found.\n";
//                            return;
//                        }
//                        var structure = plan.StructureSet.Structures.FirstOrDefault(s => s.Id == tumorId);
//                        if (structure == null)
//                        {
//                            OutputLog += $"Structure '{tumorId}' not found.\n";
//                            return;
//                        }

//                        // 2. Setup beam geometry
//                        var iso = beam.IsocenterPosition;
//                        var cp0 = beam.ControlPoints.First();

//                        // Calculate beam direction
//                        double gantryRad = cp0.GantryAngle * Math.PI / 180.0;
//                        double couchRad = cp0.PatientSupportAngle * Math.PI / 180.0;
//                        VVector beamDir = new VVector(Math.Sin(gantryRad), -Math.Cos(gantryRad), 0);

//                        // Apply couch rotation if needed
//                        if (Math.Abs(cp0.PatientSupportAngle) > 0.1)
//                        {
//                            double x = beamDir.x, z = beamDir.z;
//                            beamDir.x = x * Math.Cos(couchRad) + z * Math.Sin(couchRad);
//                            beamDir.z = -x * Math.Sin(couchRad) + z * Math.Cos(couchRad);
//                        }
//                        double norm = Math.Sqrt(beamDir.x * beamDir.x + beamDir.y * beamDir.y + beamDir.z * beamDir.z);
//                        beamDir = new VVector(beamDir.x / norm, beamDir.y / norm, beamDir.z / norm);

//                        // Create orthogonal vectors for the plane
//                        VVector up = new VVector(0, 0, 1);
//                        VVector uAxis = EvaluationViewModel.Cross(beamDir, up);
//                        if (uAxis.Length == 0) uAxis = new VVector(1, 0, 0);
//                        uAxis = uAxis / uAxis.Length;
//                        VVector vAxis = EvaluationViewModel.Cross(beamDir, uAxis);
//                        vAxis = vAxis / vAxis.Length;

//                        OutputLog += $"Beam direction: ({beamDir.x:F3}, {beamDir.y:F3}, {beamDir.z:F3})\n";

//                        // 3. Find structure bounding box in the 2D plane
//                        OutputLog += "Finding structure bounding box in beam plane...\n";

//                        double minU = double.MaxValue, maxU = double.MinValue;
//                        double minV = double.MaxValue, maxV = double.MinValue;
//                        int structureSampleCount = 0;

//                        // Sample structure mesh to find 2D bounds
//                        var mesh = structure.MeshGeometry;
//                        if (mesh != null && mesh.Positions.Count > 0)
//                        {
//                            foreach (var point3D in mesh.Positions)
//                            {
//                                var pt = new VVector(point3D.X, point3D.Y, point3D.Z);
//                                var relative = pt - iso;

//                                // Project onto the 2D plane using manual dot product
//                                double projU = relative.x * uAxis.x + relative.y * uAxis.y + relative.z * uAxis.z;
//                                double projV = relative.x * vAxis.x + relative.y * vAxis.y + relative.z * vAxis.z;

//                                minU = Math.Min(minU, projU);
//                                maxU = Math.Max(maxU, projU);
//                                minV = Math.Min(minV, projV);
//                                maxV = Math.Max(maxV, projV);
//                                structureSampleCount++;
//                            }
//                        }

//                        OutputLog += $"Structure bounds in beam plane: U=[{minU:F1}, {maxU:F1}], V=[{minV:F1}, {maxV:F1}] mm\n";
//                        OutputLog += $"Structure mesh points sampled: {structureSampleCount}\n";

//                        // 4. Adaptive grid setup - focus on structure region
//                        //double structureWidth = maxU - minU;
//                        //double structureHeight = maxV - minV;
//                        //double padding = Math.Max(10, Math.Max(structureWidth, structureHeight) * 0.3); // 30% padding

//                        //double gridMinU = minU - padding;
//                        //double gridMaxU = maxU + padding;
//                        //double gridMinV = minV - padding;
//                        //double gridMaxV = maxV + padding;

//                        //// Dense sampling inside structure
//                        //double fineStep = 1.0;    // 1mm for inside structure

//                        //// Calculate grid dimensions
//                        //double gridWidth = gridMaxU - gridMinU;
//                        //double gridHeight = gridMaxV - gridMinV;

//                        //// Use fine step for grid sizing
//                        //int nX = (int)Math.Ceiling(gridWidth / fineStep);
//                        //int nY = (int)Math.Ceiling(gridHeight / fineStep);

//                        //OutputLog += $"Adaptive grid: {nX}×{nY} points covering {gridWidth:F1}×{gridHeight:F1} mm\n";

//                        //// Initialize arrays
//                        //_nX2D = nX;
//                        //_nY2D = nY;
//                        //_dose2DGrid = new double[nX, nY];
//                        //_x2DGrid = new double[nX, nY];
//                        //_y2DGrid = new double[nX, nY];
//                        //_inStruct2D = new bool[nX, nY];

//                        // 4. Structure-focused grid setup - minimal padding, focus on target
//                        double structureWidth = maxU - minU;
//                        double structureHeight = maxV - minV;
//                        double padding = Math.Max(3, Math.Max(structureWidth, structureHeight) * 0.1); // Only 10% padding

//                        double gridMinU = minU - padding;
//                        double gridMaxU = maxU + padding;
//                        double gridMinV = minV - padding;
//                        double gridMaxV = maxV + padding;

//                        // High-resolution sampling for structure analysis
//                        double fineStep = 0.5;    // 0.5mm for detailed analysis

//                        double gridWidth = gridMaxU - gridMinU;
//                        double gridHeight = gridMaxV - gridMinV;

//                        int nX = (int)Math.Ceiling(gridWidth / fineStep);
//                        int nY = (int)Math.Ceiling(gridHeight / fineStep);

//                        OutputLog += $"Structure-focused grid: {nX}×{nY} points covering {gridWidth:F1}×{gridHeight:F1} mm\n";
//                        OutputLog += $"Grid resolution: {fineStep} mm ({1.0 / fineStep} points per mm)\n";

//                        // Initialize arrays
//                        _nX2D = nX;
//                        _nY2D = nY;
//                        _dose2DGrid = new double[nX, nY];
//                        _x2DGrid = new double[nX, nY];
//                        _y2DGrid = new double[nX, nY];
//                        _inStruct2D = new bool[nX, nY];

//                        // 5. Structure-focused sampling - only care about target dose
//                        OutputLog += "Starting structure-focused dose sampling...\n";
//                        int insideCount = 0, nearStructureCount = 0, totalSampled = 0;
//                        var structureDoseList = new List<double>(); // ONLY structure doses for statistics

//                        for (int ix = 0; ix < nX; ix++)
//                        {
//                            double paramU = gridMinU + (ix / (double)(nX - 1)) * gridWidth;

//                            for (int iy = 0; iy < nY; iy++)
//                            {
//                                double paramV = gridMinV + (iy / (double)(nY - 1)) * gridHeight;

//                                // Convert to 3D coordinates
//                                var pt = iso + (paramU * uAxis) + (paramV * vAxis);

//                                // Store coordinates
//                                _x2DGrid[ix, iy] = paramU;
//                                _y2DGrid[ix, iy] = paramV;

//                                // Check if point is inside structure
//                                bool inside = false;
//                                try
//                                {
//                                    inside = structure.IsPointInsideSegment(pt);
//                                }
//                                catch
//                                {
//                                    _dose2DGrid[ix, iy] = double.NaN;
//                                    _inStruct2D[ix, iy] = false;
//                                    continue;
//                                }

//                                _inStruct2D[ix, iy] = inside;

//                                // Sample dose - prioritize structure, minimal outside
//                                bool shouldSample = inside; // Always sample inside structure

//                                if (!inside)
//                                {
//                                    // Only sample a few points outside for visualization context
//                                    shouldSample = (ix % 4 == 0 && iy % 4 == 0); // Very sparse outside
//                                }

//                                if (shouldSample)
//                                {
//                                    try
//                                    {
//                                        var dv = plan.Dose.GetDoseToPoint(pt);
//                                        if (dv != null)
//                                        {
//                                            double doseGy = dv.Dose;
//                                            _dose2DGrid[ix, iy] = doseGy;

//                                            if (inside)
//                                            {
//                                                structureDoseList.Add(doseGy); // Only add structure doses to statistics
//                                                insideCount++;
//                                            }
//                                            else
//                                            {
//                                                nearStructureCount++;
//                                            }
//                                            totalSampled++;
//                                        }
//                                        else
//                                        {
//                                            _dose2DGrid[ix, iy] = double.NaN;
//                                        }
//                                    }
//                                    catch
//                                    {
//                                        _dose2DGrid[ix, iy] = double.NaN;
//                                    }
//                                }
//                                else
//                                {
//                                    // Don't sample - leave as NaN for clean visualization
//                                    _dose2DGrid[ix, iy] = double.NaN;
//                                }
//                            }
//                        }

//                        OutputLog += $"Structure-focused sampling: {insideCount} inside target, {nearStructureCount} context points\n";
//                        OutputLog += $"Structure dose range: {structureDoseList.Min():F1} - {structureDoseList.Max():F1} Gy\n";

//                        if (insideCount == 0)
//                        {
//                            OutputLog += "ERROR: No valid dose points inside structure!\n";
//                            return;
//                        }

//                        // Add dose distribution analysis first
//                        AnalyzeDoseDistribution(structureDoseList);

//                        // 6. Aggressive peak detection
//                        OutputLog += "Detecting peaks INSIDE target structure (aggressive mode)...\n";
//                        var peaks = DetectStructurePeaks(_dose2DGrid, _inStruct2D, nX, nY, fineStep, structureDoseList);
//                        int peakCount = peaks.Count(); // Fixed: use Count() instead of Count
//                        OutputLog += $"Found {peakCount} dose peaks in target\n";

//                        // 7. Aggressive valley detection  
//                        OutputLog += "Detecting valleys INSIDE target structure (aggressive mode)...\n";
//                        var valleys = DetectStructureValleys(_dose2DGrid, _inStruct2D, nX, nY, fineStep, structureDoseList);
//                        int valleyCount = valleys.Count(); // Fixed: use Count() instead of Count
//                        OutputLog += $"Found {valleyCount} dose valleys in target\n";


//                        // 6. Structure-only peak detection
//                        //OutputLog += "Detecting peaks INSIDE target structure...\n";
//                        //var peaks = DetectStructurePeaks(_dose2DGrid, _inStruct2D, nX, nY, fineStep, structureDoseList);
//                        //int peakCount = peaks.Count;
//                        //OutputLog += $"Found {peakCount} dose peaks in target\n";

//                        // 7. Structure-only valley detection
//                        //OutputLog += "Detecting valleys INSIDE target structure...\n";
//                        //var valleys = DetectStructureValleys(_dose2DGrid, _inStruct2D, nX, nY, fineStep, structureDoseList);
//                        //int valleyCount = valleys.Count;
//                        //OutputLog += $"Found {valleyCount} dose valleys in target\n";



//                        //OutputLog += "Starting adaptive dose sampling...\n";
//                        //int insideCount = 0, outsideCount = 0, totalSampled = 0;
//                        //var doseList = new List<double>();

//                        //for (int ix = 0; ix < nX; ix++)
//                        //{
//                        //    double paramU = gridMinU + (ix / (double)(nX - 1)) * gridWidth;

//                        //    for (int iy = 0; iy < nY; iy++)
//                        //    {
//                        //        double paramV = gridMinV + (iy / (double)(nY - 1)) * gridHeight;

//                        //        // Convert to 3D coordinates
//                        //        var pt = iso + (paramU * uAxis) + (paramV * vAxis);

//                        //        // Store coordinates
//                        //        _x2DGrid[ix, iy] = paramU;
//                        //        _y2DGrid[ix, iy] = paramV;

//                        //        // Check if point is inside structure
//                        //        bool inside = false;
//                        //        try
//                        //        {
//                        //            inside = structure.IsPointInsideSegment(pt);
//                        //        }
//                        //        catch
//                        //        {
//                        //            // Skip points that can't be evaluated
//                        //            _dose2DGrid[ix, iy] = double.NaN;
//                        //            _inStruct2D[ix, iy] = false;
//                        //            continue;
//                        //        }

//                        //        _inStruct2D[ix, iy] = inside;

//                        //        // Adaptive sampling decision
//                        //        bool shouldSample = inside; // Always sample inside
//                        //        if (!inside)
//                        //        {
//                        //            // Sample outside points more sparsely
//                        //            shouldSample = (ix % 3 == 0 && iy % 3 == 0); // Every 3rd point
//                        //        }

//                        //        if (shouldSample)
//                        //        {
//                        //            try
//                        //            {
//                        //                var dv = plan.Dose.GetDoseToPoint(pt);
//                        //                if (dv != null)
//                        //                {
//                        //                    double doseGy = dv.Dose;
//                        //                    _dose2DGrid[ix, iy] = doseGy;

//                        //                    if (inside && doseGy > 0)
//                        //                    {
//                        //                        doseList.Add(doseGy);
//                        //                        insideCount++;
//                        //                    }
//                        //                    else if (!inside)
//                        //                    {
//                        //                        outsideCount++;
//                        //                    }
//                        //                    totalSampled++;
//                        //                }
//                        //                else
//                        //                {
//                        //                    _dose2DGrid[ix, iy] = double.NaN;
//                        //                }
//                        //            }
//                        //            catch
//                        //            {
//                        //                _dose2DGrid[ix, iy] = double.NaN;
//                        //            }
//                        //        }
//                        //        else
//                        //        {
//                        //            _dose2DGrid[ix, iy] = double.NaN;
//                        //        }
//                        //    }
//                        //}

//                        //OutputLog += $"Sampling complete: {totalSampled} total, {insideCount} inside structure, {outsideCount} outside\n";

//                        //if (insideCount == 0)
//                        //{
//                        //    OutputLog += "ERROR: No valid dose points inside structure!\n";
//                        //    return;
//                        //}



//                        // 6. PEAK DETECTION
//                        //OutputLog += "Detecting dose peaks...\n";
//                        //var peaks = DetectPeaks(_dose2DGrid, _inStruct2D, nX, nY, fineStep);
//                        //int peakCount = peaks.Count;
//                        //OutputLog += $"Found {peakCount} dose peaks\n";

//                        //// 7. VALLEY DETECTION  
//                        //OutputLog += "Detecting dose valleys...\n";
//                        //var valleys = DetectValleys(_dose2DGrid, _inStruct2D, nX, nY, fineStep);
//                        //int valleyCount = valleys.Count;
//                        //OutputLog += $"Found {valleyCount} dose valleys\n";

//                        // 8. PVDR CALCULATION
//                        if (peakCount > 0 && valleyCount > 0)
//                        {
//                            double avgPeakDose = peaks.Average(p => p.dose);
//                            double avgValleyDose = valleys.Average(val => val.dose);
//                            double maxPeakDose = peaks.Max(p => p.dose);
//                            double minValleyDose = valleys.Min(val => val.dose);

//                            double avgPVDR = avgPeakDose / avgValleyDose;
//                            double maxPVDR = maxPeakDose / minValleyDose;

//                            OutputLog += "\n===== PVDR ANALYSIS RESULTS =====\n";
//                            OutputLog += $"Peak Analysis:\n";
//                            OutputLog += $"  Number of peaks: {peakCount}\n";
//                            OutputLog += $"  Average peak dose: {avgPeakDose:F2} Gy\n";
//                            OutputLog += $"  Maximum peak dose: {maxPeakDose:F2} Gy\n";

//                            OutputLog += $"Valley Analysis:\n";
//                            OutputLog += $"  Number of valleys: {valleyCount}\n";
//                            OutputLog += $"  Average valley dose: {avgValleyDose:F2} Gy\n";
//                            OutputLog += $"  Minimum valley dose: {minValleyDose:F2} Gy\n";

//                            OutputLog += $"PVDR Metrics:\n";
//                            OutputLog += $"  Average PVDR: {avgPVDR:F2}\n";
//                            OutputLog += $"  Maximum PVDR: {maxPVDR:F2}\n";
//                            OutputLog += $"  Overall dose range: {minValleyDose:F2} - {maxPeakDose:F2} Gy\n";

//                            // Add metrics to the collection for display
//                            AllMetrics.Clear();
//                            AllMetrics.Add(new MetricData { metric = "Number of Dose Peaks", value = peakCount.ToString() });
//                            AllMetrics.Add(new MetricData { metric = "Number of Dose Valleys", value = valleyCount.ToString() });
//                            AllMetrics.Add(new MetricData { metric = "Average Peak Dose (Gy)", value = avgPeakDose.ToString("F2") });
//                            AllMetrics.Add(new MetricData { metric = "Average Valley Dose (Gy)", value = avgValleyDose.ToString("F2") });
//                            AllMetrics.Add(new MetricData { metric = "Average PVDR", value = avgPVDR.ToString("F2") });
//                            AllMetrics.Add(new MetricData { metric = "Maximum PVDR", value = maxPVDR.ToString("F2") });
//                            AllMetrics.Add(new MetricData { metric = "Peak-Valley Dose Difference (Gy)", value = (avgPeakDose - avgValleyDose).ToString("F2") });
//                        }
//                        else
//                        {
//                            OutputLog += "WARNING: Could not detect sufficient peaks or valleys for PVDR calculation\n";
//                            OutputLog += $"Peaks found: {peakCount}, Valleys found: {valleyCount}\n";
//                        }

//                        // Set flag for plotting
//                        _has2DPlotData = true;
//                        OutputLog += "Enhanced 2D PVDR analysis complete\n";


//                    }
//                }
//                else if (selectedPvdrMode == "VMAT")
//                {
//                    // Step 1: Access beam
//                    var beam = plan.Beams.FirstOrDefault(b => b.Id == SelectedBeamId);
//                    if (beam == null)
//                    {
//                        OutputLog += "Selected beam not found.\n";
//                        return;
//                    }

//                    // Step 2: Access structure from the plan's structure set
//                    var structureSet = plan.StructureSet;
//                    if (structureSet == null)
//                    {
//                        OutputLog += " No StructureSet associated with the selected plan.\n";
//                        return;
//                    }

//                    // Get structure from name
//                    var structure = plan.StructureSet.Structures.FirstOrDefault(s => s.Id == tumorId);
//                    if (structure == null)
//                    {
//                        OutputLog += " Structure {tumorId} not found.\n";
//                        return;
//                    }

//                    // Use ESAPI built-in center point
//                    var targetCenter = structure.CenterPoint; // VVector in mm

//                    // target bounds
//                    try
//                    {
//                        var mesh = structure.MeshGeometry;

//                        if (mesh == null || mesh.Positions == null || mesh.Positions.Count == 0)
//                        {
//                            OutputLog += "Mesh geometry not available for structure: " + structure.Id + "\n";
//                        }
//                        else
//                        {
//                            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
//                            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

//                            foreach (var point in mesh.Positions)
//                            {
//                                if (point.X < minX) minX = point.X;
//                                if (point.Y < minY) minY = point.Y;
//                                if (point.Z < minZ) minZ = point.Z;

//                                if (point.X > maxX) maxX = point.X;
//                                if (point.Y > maxY) maxY = point.Y;
//                                if (point.Z > maxZ) maxZ = point.Z;
//                            }

//                            OutputLog += "Structure bounding box (mm):\n";
//                            OutputLog += "  Min: (" + minX.ToString("F1") + ", " + minY.ToString("F1") + ", " + minZ.ToString("F1") + ")\n";
//                            OutputLog += "  Max: (" + maxX.ToString("F1") + ", " + maxY.ToString("F1") + ", " + maxZ.ToString("F1") + ")\n";
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        OutputLog += "Error computing mesh bounds: " + ex.Message + "\n";
//                    }



//                    OutputLog += $"Target centroid: ({targetCenter.x:F1}, {targetCenter.y:F1}, {targetCenter.z:F1}) mm \n";


//                    // Step 3: Extract jaw and MLC geometry
//                    OutputLog += "Extracting jaw and MLC positions...\n";
//                    // TODO: Add actual MLC/jaw extraction logic here
//                    //LogBeamApertureGeometry(beam);
//                    RunSphericalPVDRMetric(tumorId, plan);

//                    // Step 4: Define sampling grid in plane perpendicular to beam central axis
//                    OutputLog += "Defining planar grid for dose sampling...\n";
//                    // TODO: Add ray projection to target logic here

//                    // Step 5: Sample dose at projected beamlet intersections (peaks/valleys)
//                    OutputLog += "Sampling dose at grid points...\n";
//                    // TODO: Dose sampling at grid points

//                    // Step 6: Compute PVDR metric from dose grid
//                    OutputLog += "Computing PVDR metrics...\n";
//                    // TODO: Compute peak and valley logic here

//                    // Step 7: Log results (optional for now)
//                    // AllMetrics.Add(new EvalMetric { metric = "PVDR (2D)", value = "TBD" });
//                    OutputLog += "Logging PVDR metric complete.\n";

//                    // Step 8: (optional) Show heatmap or profile
//                    // Show2DHeatmapOnCanvas(); // Optional if wired in later

//                    OutputLog += "2D PVDR analysis complete.\n";

//                    // Matt - BEV, at what surfaces does arc cover? For a full 

//                    return; // skip rest
//                }
//                else
//                {
//                    OutputLog += $"Unknown PVDR mode: {selectedPvdrMode}\n";
//                    return;
//                }

//            }
//            catch (Exception ex)
//            {
//                OutputLog += $"Error during PVDR analysis: {ex.Message}\n";
//            }
//        }

//        public void RunSphericalPVDRMetric(string tumorId, PlanSetup plan)
//        {
//            OutputLog += "\n===== Starting Spherical Shell PVDR Evaluation =====\n";

//            // Step 1: Get beam and structure
//            var beam = plan.Beams.FirstOrDefault(b => b.Id == SelectedBeamId);
//            if (beam == null)
//            {
//                OutputLog += "Selected beam not found.\n";
//                return;
//            }

//            var structure = plan.StructureSet.Structures.FirstOrDefault(s => s.Id == tumorId);
//            if (structure == null)
//            {
//                OutputLog += $"Structure '{tumorId}' not found.\n";
//                return;
//            }

//            var shellOrigin = structure.CenterPoint;
//            OutputLog += $"Target centroid (shell origin): ({shellOrigin.x:F1}, {shellOrigin.y:F1}, {shellOrigin.z:F1}) mm\n";

//            // Step 2: Loop over all control points
//            int cpIndex = 0;
//            foreach (var cp in beam.ControlPoints)
//            {
//                OutputLog += $"\n--- Control Point {cpIndex} ---\n";

//                double gantry = cp.GantryAngle;
//                double collimator = cp.CollimatorAngle;
//                double couch = cp.PatientSupportAngle;

//                OutputLog += $"Gantry: {gantry:F1}°, Collimator: {collimator:F1}°, Couch: {couch:F1}°\n";

//                var jaws = cp.JawPositions;
//                OutputLog += $"Jaws (cm): X1 = {jaws.X1}, X2 = {jaws.X2}, Y1 = {jaws.Y1}, Y2 = {jaws.Y2}\n";

//                // Convert jaws to mm
//                double jawX1 = jaws.X1 * 10.0;
//                double jawX2 = jaws.X2 * 10.0;
//                double jawY1 = jaws.Y1 * 10.0;
//                double jawY2 = jaws.Y2 * 10.0;

//                // MLC leaf positions
//                var mlc = cp.LeafPositions;
//                int nLeafPairs = mlc.GetLength(1);  // Should be 60 for Millennium 120

//                OutputLog += $"Leaf matrix shape: [{mlc.GetLength(0)} x {mlc.GetLength(1)}]\n";


//                for (int i = 0; i < nLeafPairs; i++)
//                {
//                    double x1 = mlc[0, i] * 10.0;  // bank A
//                    double x2 = mlc[1, i] * 10.0;  // bank B
//                    double gap = Math.Abs(x2 - x1);

//                    if (gap > 0.1)
//                    {
//                        double centerX = (x1 + x2) / 2.0;
//                        double centerY = GetLeafCenterY(i, nLeafPairs, jawY1, jawY2);

//                        OutputLog += $"  Leaf {i:D2}: Center = ({centerX:F1}, {centerY:F1}) mm, Gap = {gap:F1} mm\n";
//                    }
//                }

//                cpIndex++;
//            }

//            OutputLog += "\n===== Control point loop complete =====\n";
//        }

//        public double GetLeafCenterY(int leafIndex, int totalLeaves, double jawY1, double jawY2)
//        {
//            // Map leaf index to Y position assuming evenly spaced leaves
//            // We'll just center them between jawY1 and jawY2
//            double fieldHeight = jawY2 - jawY1;
//            double leafHeight = fieldHeight / totalLeaves;

//            return jawY1 + leafHeight * (leafIndex + 0.5);
//        }


//        public void LogBeamApertureGeometry(Beam beam)
//        {
//            try
//            {
//                OutputLog += $"Logging geometry for beam: {beam.Id}\n";

//                // Log gantry and collimator angle
//                OutputLog += $"  Gantry Angle: {beam.ControlPoints[0].GantryAngle}\n";
//                OutputLog += $"  Collimator Angle: {beam.ControlPoints[0].CollimatorAngle}\n";

//                // Get jaws (X1, X2, Y1, Y2) from first control point
//                var jaws = beam.ControlPoints[0].JawPositions;
//                OutputLog += $"  Jaw positions (cm): X1 = {jaws.X1}, X2 = {jaws.X2}, Y1 = {jaws.Y1}, Y2 = {jaws.Y2}\n";

//                // Get MLC leaf positions
//                var mlcLeaves = beam.ControlPoints[0].LeafPositions;
//                int numLeaves = mlcLeaves.GetLength(0);

//                OutputLog += $"  MLC Leaf Openings (cm):\n";
//                OutputLog += $"Total MLC leaves: {numLeaves}\n";

//                for (int leaf = 0; leaf < numLeaves; leaf++)
//                {
//                    double leafX1 = mlcLeaves[leaf, 0];
//                    double leafX2 = mlcLeaves[leaf, 1];
//                    double opening = Math.Abs(leafX2 - leafX1);
//                    OutputLog += $"    Leaf {leaf + 1}: X1 = {leafX1}, X2 = {leafX2}, Width = {opening}\n";
//                }


//                OutputLog += "  Beam geometry logging complete.\n";
//            }
//            catch (Exception ex)
//            {
//                OutputLog += $"Error while logging beam geometry: {ex.Message}\n";
//            }
//        }


//        public void update3DMetrics(string tumorName, string ptvAllName, PlanSetup plan)
//        {
//            OutputLog += "Starting 3D Dosimetric Calculations \n";
//            OutputLog += "Starting 3D Dosimetric Calculations \n";
//            OutputLog += $"Selected tumor: {tumorName}, PTV All: {ptvAllName}\n";

//            Structure structureForEval = _structureSet.Structures.FirstOrDefault(s => s.Id == tumorName);
//            Structure ptvAll = _structureSet.Structures.FirstOrDefault(s => s.Id == ptvAllName);

//            if (structureForEval == null)
//            {
//                OutputLog += $"Error: Could not find structure '{tumorName}'\n";
//                return;
//            }

//            if (ptvAll == null)
//            {
//                OutputLog += $"Error: Could not find PTV ALL structure '{ptvAllName}'\n";
//                return;
//            }

//            // Clear all previous metrics first
//            AllMetrics.Clear();

//            // Dose per fraction
//            AllMetrics.Add(new MetricData
//            {
//                metric = "Prescription Dose per Fraction (Gy)",
//                value = plan.DosePerFraction.ToString()
//            });

//            // Gross Target Volume
//            AllMetrics.Add(new MetricData
//            {
//                metric = "Gross Target Volume (cc)",
//                value = Math.Round(structureForEval.Volume, 2).ToString()
//            });

//            // Number of vertices (separate parts)
//            AllMetrics.Add(new MetricData
//            {
//                metric = "Number of Vertices",
//                value = ptvAll.GetNumberOfSeparateParts().ToString()
//            });

//            // Volume of vertices
//            AllMetrics.Add(new MetricData
//            {
//                metric = "Total Volume of Vertices (cc)",
//                value = Math.Round(ptvAll.Volume, 2).ToString()
//            });

//            // Percent of GTV that is total lattice volume
//            double latticePercent = Math.Round((100 * ptvAll.Volume / structureForEval.Volume), 2);
//            AllMetrics.Add(new MetricData
//            {
//                metric = "Percent of GTV Covered by Lattice (%)",
//                value = latticePercent.ToString()
//            });

//            // D95 calculation
//            try
//            {
//                var absoluteVolume = VolumePresentation.AbsoluteCm3;
//                var absoluteDoseValue = DoseValuePresentation.Absolute;
//                var d95Value = plan.GetDoseAtVolume(structureForEval, 95, absoluteVolume, absoluteDoseValue);

//                AllMetrics.Add(new MetricData
//                {
//                    metric = "Dose Covering 95% of Target (D95) (Gy)",
//                    value = Math.Round(d95Value.Dose, 3).ToString()
//                });
//            }
//            catch (Exception ex)
//            {
//                OutputLog += $"Error calculating D95: {ex.Message}\n";
//                AllMetrics.Add(new MetricData
//                {
//                    metric = "Dose Covering 95% of Target (D95) (Gy)",
//                    value = "Error - Unable to calculate"
//                });
//            }

//            // Additional useful metrics
//            AllMetrics.Add(new MetricData
//            {
//                metric = "Average Vertex Volume (cc)",
//                value = Math.Round(ptvAll.Volume / ptvAll.GetNumberOfSeparateParts(), 3).ToString()
//            });

//            OutputLog += $"Added {AllMetrics.Count} metrics to collection\n";

//            // Force UI update
//            RaisePropertyChanged(nameof(AllMetrics));

//            OutputLog += $"3D Dosimetric Calculations complete. {AllMetrics.Count} metrics calculated.\n";
//        }

//        /// <summary>
//        /// Creates and displays a simple plot of the dose along the central axis (original popup window version)
//        /// </summary>
//        private void ShowDosePlot(List<double> distances, List<double> doseValues, List<bool> insideTumorFlags,
//                                 double entryDist, double exitDist, string tumorId, string beamId)
//        {
//            try
//            {
//                // Create the plot window
//                System.Windows.Window plotWindow = new System.Windows.Window
//                {
//                    Title = string.Format("Central Axis Dose Plot - {0} - {1}", tumorId, beamId),
//                    Width = 800,
//                    Height = 600,
//                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
//                };

//                // Create a grid to hold the plot components
//                System.Windows.Controls.Grid mainGrid = new System.Windows.Controls.Grid();
//                plotWindow.Content = mainGrid;

//                // Add row definitions for the plot and legend
//                mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
//                mainGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(40, System.Windows.GridUnitType.Pixel) });

//                // Create a canvas for the plot
//                System.Windows.Controls.Canvas plotCanvas = new System.Windows.Controls.Canvas
//                {
//                    Background = System.Windows.Media.Brushes.White
//                };
//                System.Windows.Controls.Grid.SetRow(plotCanvas, 0);
//                mainGrid.Children.Add(plotCanvas);

//                // Create a panel for the legend
//                System.Windows.Controls.StackPanel legendPanel = new System.Windows.Controls.StackPanel
//                {
//                    Orientation = System.Windows.Controls.Orientation.Horizontal,
//                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
//                    Margin = new System.Windows.Thickness(0, 10, 0, 10)
//                };
//                System.Windows.Controls.Grid.SetRow(legendPanel, 1);
//                mainGrid.Children.Add(legendPanel);

//                // Use fixed margins for the plot area
//                double leftMargin = 60;
//                double rightMargin = 20;
//                double topMargin = 30;
//                double bottomMargin = 50;

//                // Find min and max values for scaling
//                double minDist = distances.Min();
//                double maxDist = distances.Max();
//                double maxDose = doseValues.Max() * 1.1; // Add 10% for margin

//                // Wait for the canvas to be properly sized
//                plotWindow.Loaded += (sender, e) =>
//                {
//                    try
//                    {
//                        // Calculate plot dimensions
//                        double plotWidth = plotCanvas.ActualWidth - leftMargin - rightMargin;
//                        double plotHeight = plotCanvas.ActualHeight - topMargin - bottomMargin;

//                        // Create horizontal and vertical axes
//                        System.Windows.Shapes.Line horizontalAxis = new System.Windows.Shapes.Line
//                        {
//                            X1 = leftMargin,
//                            Y1 = plotCanvas.ActualHeight - bottomMargin,
//                            X2 = plotCanvas.ActualWidth - rightMargin,
//                            Y2 = plotCanvas.ActualHeight - bottomMargin,
//                            Stroke = System.Windows.Media.Brushes.Black,
//                            StrokeThickness = 1
//                        };

//                        System.Windows.Shapes.Line verticalAxis = new System.Windows.Shapes.Line
//                        {
//                            X1 = leftMargin,
//                            Y1 = topMargin,
//                            X2 = leftMargin,
//                            Y2 = plotCanvas.ActualHeight - bottomMargin,
//                            Stroke = System.Windows.Media.Brushes.Black,
//                            StrokeThickness = 1
//                        };

//                        plotCanvas.Children.Add(horizontalAxis);
//                        plotCanvas.Children.Add(verticalAxis);

//                        // Add axis labels
//                        System.Windows.Controls.TextBlock xAxisLabel = new System.Windows.Controls.TextBlock
//                        {
//                            Text = "Distance from Isocenter (mm)",
//                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
//                            VerticalAlignment = System.Windows.VerticalAlignment.Bottom
//                        };
//                        System.Windows.Controls.Canvas.SetLeft(xAxisLabel, plotCanvas.ActualWidth / 2 - 80);
//                        System.Windows.Controls.Canvas.SetTop(xAxisLabel, plotCanvas.ActualHeight - 20);
//                        plotCanvas.Children.Add(xAxisLabel);

//                        System.Windows.Controls.TextBlock yAxisLabel = new System.Windows.Controls.TextBlock
//                        {
//                            Text = "Dose (Gy)",
//                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
//                            VerticalAlignment = System.Windows.VerticalAlignment.Center,
//                            LayoutTransform = new System.Windows.Media.RotateTransform(-90)
//                        };
//                        System.Windows.Controls.Canvas.SetLeft(yAxisLabel, 5);
//                        System.Windows.Controls.Canvas.SetTop(yAxisLabel, plotCanvas.ActualHeight / 2 - 20);
//                        plotCanvas.Children.Add(yAxisLabel);

//                        // Plot the data points
//                        System.Windows.Shapes.Polyline doseLine = new System.Windows.Shapes.Polyline
//                        {
//                            Stroke = System.Windows.Media.Brushes.Blue,
//                            StrokeThickness = 2
//                        };

//                        System.Windows.Media.PointCollection points = new System.Windows.Media.PointCollection();
//                        bool hasValidPoints = false;

//                        for (int i = 0; i < distances.Count; i++)
//                        {
//                            // Convert data to canvas coordinates
//                            double x = leftMargin + (distances[i] - minDist) / (maxDist - minDist) * plotWidth;
//                            double y = (plotCanvas.ActualHeight - bottomMargin) - (doseValues[i] / maxDose) * plotHeight;

//                            // Check for valid coordinates before adding the point
//                            if (!double.IsNaN(x) && !double.IsNaN(y) && !double.IsInfinity(x) && !double.IsInfinity(y))
//                            {
//                                points.Add(new System.Windows.Point(x, y));
//                                hasValidPoints = true;

//                                // If inside tumor, add a red dot
//                                if (insideTumorFlags[i])
//                                {
//                                    System.Windows.Shapes.Ellipse dot = new System.Windows.Shapes.Ellipse
//                                    {
//                                        Width = 4,
//                                        Height = 4,
//                                        Fill = System.Windows.Media.Brushes.Red
//                                    };
//                                    System.Windows.Controls.Canvas.SetLeft(dot, x - 2);
//                                    System.Windows.Controls.Canvas.SetTop(dot, y - 2);
//                                    plotCanvas.Children.Add(dot);
//                                }
//                            }
//                        }

//                        if (hasValidPoints)
//                        {
//                            doseLine.Points = points;
//                            plotCanvas.Children.Add(doseLine);

//                            // Add tumor boundary markers
//                            double entryX = leftMargin + (entryDist - minDist) / (maxDist - minDist) * plotWidth;
//                            double exitX = leftMargin + (exitDist - minDist) / (maxDist - minDist) * plotWidth;

//                            // Verify boundary coordinates are valid
//                            if (!double.IsNaN(entryX) && !double.IsInfinity(entryX))
//                            {
//                                System.Windows.Shapes.Line entryLine = new System.Windows.Shapes.Line
//                                {
//                                    X1 = entryX,
//                                    Y1 = topMargin,
//                                    X2 = entryX,
//                                    Y2 = plotCanvas.ActualHeight - bottomMargin,
//                                    Stroke = System.Windows.Media.Brushes.Green,
//                                    StrokeThickness = 1,
//                                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 2 }
//                                };
//                                plotCanvas.Children.Add(entryLine);

//                                System.Windows.Controls.TextBlock entryLabel = new System.Windows.Controls.TextBlock
//                                {
//                                    Text = "Entry",
//                                    Foreground = System.Windows.Media.Brushes.Green
//                                };
//                                System.Windows.Controls.Canvas.SetLeft(entryLabel, entryX - 15);
//                                System.Windows.Controls.Canvas.SetTop(entryLabel, topMargin);
//                                plotCanvas.Children.Add(entryLabel);
//                            }

//                            if (!double.IsNaN(exitX) && !double.IsInfinity(exitX))
//                            {
//                                System.Windows.Shapes.Line exitLine = new System.Windows.Shapes.Line
//                                {
//                                    X1 = exitX,
//                                    Y1 = topMargin,
//                                    X2 = exitX,
//                                    Y2 = plotCanvas.ActualHeight - bottomMargin,
//                                    Stroke = System.Windows.Media.Brushes.Green,
//                                    StrokeThickness = 1,
//                                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 2 }
//                                };
//                                plotCanvas.Children.Add(exitLine);

//                                System.Windows.Controls.TextBlock exitLabel = new System.Windows.Controls.TextBlock
//                                {
//                                    Text = "Exit",
//                                    Foreground = System.Windows.Media.Brushes.Green
//                                };
//                                System.Windows.Controls.Canvas.SetLeft(exitLabel, exitX - 10);
//                                System.Windows.Controls.Canvas.SetTop(exitLabel, topMargin);
//                                plotCanvas.Children.Add(exitLabel);
//                            }
//                        }
//                        else
//                        {
//                            MessageBox.Show("Could not plot the data: No valid coordinates found.");
//                            return;
//                        }

//                        // Add axis ticks and values
//                        // X-axis
//                        int numXTicks = 10;
//                        double xTickStep = (maxDist - minDist) / numXTicks;

//                        for (int i = 0; i <= numXTicks; i++)
//                        {
//                            double tickValue = minDist + i * xTickStep;
//                            double tickX = leftMargin + (tickValue - minDist) / (maxDist - minDist) * plotWidth;

//                            System.Windows.Shapes.Line tick = new System.Windows.Shapes.Line
//                            {
//                                X1 = tickX,
//                                Y1 = plotCanvas.ActualHeight - bottomMargin,
//                                X2 = tickX,
//                                Y2 = plotCanvas.ActualHeight - bottomMargin + 5,
//                                Stroke = System.Windows.Media.Brushes.Black,
//                                StrokeThickness = 1
//                            };

//                            System.Windows.Controls.TextBlock tickLabel = new System.Windows.Controls.TextBlock
//                            {
//                                Text = string.Format("{0:F0}", tickValue),
//                                FontSize = 10
//                            };

//                            System.Windows.Controls.Canvas.SetLeft(tickLabel, tickX - 10);
//                            System.Windows.Controls.Canvas.SetTop(tickLabel, plotCanvas.ActualHeight - bottomMargin + 8);

//                            plotCanvas.Children.Add(tick);
//                            plotCanvas.Children.Add(tickLabel);
//                        }

//                        // Y-axis
//                        int numYTicks = 5;
//                        double yTickStep = maxDose / numYTicks;

//                        for (int i = 0; i <= numYTicks; i++)
//                        {
//                            double tickValue = i * yTickStep;
//                            double tickY = (plotCanvas.ActualHeight - bottomMargin) - (tickValue / maxDose) * plotHeight;

//                            System.Windows.Shapes.Line tick = new System.Windows.Shapes.Line
//                            {
//                                X1 = leftMargin - 5,
//                                Y1 = tickY,
//                                X2 = leftMargin,
//                                Y2 = tickY,
//                                Stroke = System.Windows.Media.Brushes.Black,
//                                StrokeThickness = 1
//                            };

//                            System.Windows.Controls.TextBlock tickLabel = new System.Windows.Controls.TextBlock
//                            {
//                                Text = string.Format("{0:F1}", tickValue),
//                                FontSize = 10
//                            };

//                            System.Windows.Controls.Canvas.SetLeft(tickLabel, leftMargin - 30);
//                            System.Windows.Controls.Canvas.SetTop(tickLabel, tickY - 7);

//                            plotCanvas.Children.Add(tick);
//                            plotCanvas.Children.Add(tickLabel);
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        MessageBox.Show(
//                            string.Format("Error rendering plot: {0}", ex.Message),
//                            "Plot Rendering Error",
//                            MessageBoxButton.OK,
//                            MessageBoxImage.Error
//                        );
//                    }
//                };

//                // Add legend items
//                // Dose line
//                System.Windows.Shapes.Rectangle doseLineItem = new System.Windows.Shapes.Rectangle
//                {
//                    Width = 20,
//                    Height = 2,
//                    Fill = System.Windows.Media.Brushes.Blue,
//                    Margin = new System.Windows.Thickness(5)
//                };
//                System.Windows.Controls.TextBlock doseLineLabel = new System.Windows.Controls.TextBlock
//                {
//                    Text = "Dose",
//                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
//                    Margin = new System.Windows.Thickness(5, 0, 15, 0)
//                };

//                // Inside tumor point
//                System.Windows.Shapes.Ellipse insideTumorItem = new System.Windows.Shapes.Ellipse
//                {
//                    Width = 6,
//                    Height = 6,
//                    Fill = System.Windows.Media.Brushes.Red,
//                    Margin = new System.Windows.Thickness(5)
//                };
//                System.Windows.Controls.TextBlock insideTumorLabel = new System.Windows.Controls.TextBlock
//                {
//                    Text = "Inside Tumor",
//                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
//                    Margin = new System.Windows.Thickness(5, 0, 15, 0)
//                };

//                // Tumor boundaries
//                System.Windows.Shapes.Rectangle boundaryItem = new System.Windows.Shapes.Rectangle
//                {
//                    Width = 20,
//                    Height = 2,
//                    Fill = System.Windows.Media.Brushes.Green,
//                    Margin = new System.Windows.Thickness(5)
//                };
//                System.Windows.Controls.TextBlock boundaryLabel = new System.Windows.Controls.TextBlock
//                {
//                    Text = "Tumor Boundaries",
//                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
//                    Margin = new System.Windows.Thickness(5, 0, 5, 0)
//                };

//                // Add items to legend
//                legendPanel.Children.Add(doseLineItem);
//                legendPanel.Children.Add(doseLineLabel);
//                legendPanel.Children.Add(insideTumorItem);
//                legendPanel.Children.Add(insideTumorLabel);
//                legendPanel.Children.Add(boundaryLabel);

//                // Show the plot window
//                plotWindow.ShowDialog();
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show(
//                    string.Format("Error creating plot: {0}", ex.Message),
//                    "Plot Error",
//                    MessageBoxButton.OK,
//                    MessageBoxImage.Error
//                );
//            }
//        }

//        public static VVector Cross(VVector a, VVector b)
//        {
//            return new VVector(
//                a.y * b.z - a.z * b.y,
//                a.z * b.x - a.x * b.z,
//                a.x * b.y - a.y * b.x
//            );
//        }

//        // Draws 2D heatmap of dose on the Canvas ///////////////////////////////

//        private void Show2DDoseHeatmapOnCanvas(Canvas targetCanvas, double[,] doseGrid, bool[,] inStruct = null)
//        {
//            targetCanvas.Children.Clear();

//            OutputLog += "=== Show2DDoseHeatmapOnCanvas CALLED ===\n";

//            if (targetCanvas == null || doseGrid == null)
//            {
//                OutputLog += "ERROR: Canvas or doseGrid is null\n";
//                return;
//            }

//            targetCanvas.UpdateLayout();

//            int nX = doseGrid.GetLength(0);
//            int nY = doseGrid.GetLength(1);

//            double canvasW = targetCanvas.ActualWidth;
//            double canvasH = targetCanvas.ActualHeight;

//            if (canvasW <= 0 || canvasH <= 0)
//            {
//                canvasW = 600;
//                canvasH = 400;
//            }

//            // === CANVAS DIVISION ===
//            // Left side: 75% for heatmap, Right side: 25% for colorbar
//            double heatmapWidth = canvasW * 0.75;
//            double colorbarWidth = canvasW * 0.25;
//            double dividerX = heatmapWidth;

//            // Find structure-only dose range for optimal color scaling
//            var (structureMinDose, structureMaxDose) = GetStructureDoseRange(doseGrid, inStruct, nX, nY);

//            OutputLog += $"Grid: {nX}x{nY}, Structure dose range: {structureMinDose:F3} to {structureMaxDose:F3}\n";

//            if (structureMaxDose - structureMinDose < 1e-6)
//            {
//                OutputLog += "No valid structure dose data found!\n";

//                var noDataText = new TextBlock
//                {
//                    Text = "NO STRUCTURE DOSE DATA",
//                    FontSize = 20,
//                    Foreground = Brushes.Red,
//                    FontWeight = FontWeights.Bold
//                };
//                Canvas.SetLeft(noDataText, heatmapWidth / 2 - 120);
//                Canvas.SetTop(noDataText, canvasH / 2);
//                targetCanvas.Children.Add(noDataText);
//                return;
//            }

//            // Find the bounding box of structure cells only
//            int minI = nX, maxI = -1, minJ = nY, maxJ = -1;
//            int structureCount = 0;

//            for (int i = 0; i < nX; i++)
//            {
//                for (int j = 0; j < nY; j++)
//                {
//                    bool inStructure = (inStruct != null && inStruct[i, j]);
//                    double dose = doseGrid[i, j];

//                    if (inStructure && !double.IsNaN(dose))
//                    {
//                        minI = Math.Min(minI, i);
//                        maxI = Math.Max(maxI, i);
//                        minJ = Math.Min(minJ, j);
//                        maxJ = Math.Max(maxJ, j);
//                        structureCount++;
//                    }
//                }
//            }

//            OutputLog += $"Structure bounds: i=[{minI},{maxI}], j=[{minJ},{maxJ}], cells={structureCount}\n";

//            if (structureCount == 0)
//            {
//                OutputLog += "No structure cells found!\n";
//                return;
//            }

//            int plotNX = maxI - minI + 1;
//            int plotNY = maxJ - minJ + 1;

//            OutputLog += $"Focusing on structure region: {plotNX}x{plotNY}\n";

//            // === HEATMAP REGION LAYOUT (LEFT SIDE) ===
//            double heatmapMargin = 40;
//            double titleHeight = 30;
//            double bottomLabelSpace = 30;

//            double availableHeatmapWidth = heatmapWidth - 2 * heatmapMargin;
//            double availableHeatmapHeight = canvasH - titleHeight - bottomLabelSpace - heatmapMargin;

//            // Calculate cell size to fit the heatmap region
//            double cellW = availableHeatmapWidth / plotNX;
//            double cellH = availableHeatmapHeight / plotNY;

//            // Ensure minimum cell size for visibility
//            double minCellSize = 6;
//            if (cellW < minCellSize || cellH < minCellSize)
//            {
//                double scale = Math.Min(minCellSize / cellW, minCellSize / cellH);
//                cellW *= scale;
//                cellH *= scale;
//            }

//            // Center the heatmap in the left region
//            double actualHeatmapWidth = cellW * plotNX;
//            double actualHeatmapHeight = cellH * plotNY;
//            double gridLeft = (heatmapWidth - actualHeatmapWidth) / 2;
//            double gridTop = titleHeight + (availableHeatmapHeight - actualHeatmapHeight) / 2;

//            // === COLORBAR REGION LAYOUT (RIGHT SIDE) ===
//            double colorbarMargin = 20;
//            double colorbarBarWidth = 40;
//            double colorbarLeft = dividerX + colorbarMargin;
//            double colorbarTop = titleHeight + 20;
//            double colorbarHeight = canvasH - titleHeight - 60;

//            OutputLog += $"Layout: Heatmap {actualHeatmapWidth:F0}x{actualHeatmapHeight:F0}, Cells {cellW:F1}x{cellH:F1}\n";

//            // === DRAW VISUAL DIVIDER ===
//            var dividerLine = new Line
//            {
//                X1 = dividerX,
//                Y1 = 0,
//                X2 = dividerX,
//                Y2 = canvasH,
//                Stroke = Brushes.LightGray,
//                StrokeThickness = 1,
//                StrokeDashArray = new DoubleCollection { 5, 5 }
//            };
//            targetCanvas.Children.Add(dividerLine);

//            // === ADD TITLE (CENTERED IN HEATMAP REGION) ===
//            var title = new TextBlock
//            {
//                Text = $"Target Dose Distribution ({plotNX}×{plotNY} structure region)",
//                FontSize = 16,
//                FontWeight = FontWeights.Bold,
//                HorizontalAlignment = HorizontalAlignment.Center
//            };
//            Canvas.SetLeft(title, heatmapWidth / 2 - 180);
//            Canvas.SetTop(title, 5);
//            targetCanvas.Children.Add(title);

//            // === DRAW HEATMAP (LEFT REGION) - STRUCTURE ONLY ===
//            int structureCellsDrawn = 0;

//            for (int i = minI; i <= maxI; i++)
//            {
//                for (int j = minJ; j <= maxJ; j++)
//                {
//                    double dose = doseGrid[i, j];
//                    bool inStructure = (inStruct != null && inStruct[i, j]);

//                    // ONLY draw cells that are inside the structure
//                    if (!inStructure || double.IsNaN(dose))
//                        continue;

//                    // Map dose to color using structure-only range
//                    double norm = (dose - structureMinDose) / (structureMaxDose - structureMinDose);
//                    norm = Math.Max(0, Math.Min(1, norm));

//                    Color cellColor;

//                    // Clinical color scheme
//                    if (norm < 0.2)
//                    {
//                        // Dark blue to blue
//                        double t = norm / 0.2;
//                        cellColor = Color.FromRgb(0, 0, (byte)(100 + t * 155));
//                    }
//                    else if (norm < 0.4)
//                    {
//                        // Blue to cyan
//                        double t = (norm - 0.2) / 0.2;
//                        cellColor = Color.FromRgb(0, (byte)(t * 255), 255);
//                    }
//                    else if (norm < 0.6)
//                    {
//                        // Cyan to green
//                        double t = (norm - 0.4) / 0.2;
//                        cellColor = Color.FromRgb(0, 255, (byte)((1 - t) * 255));
//                    }
//                    else if (norm < 0.8)
//                    {
//                        // Green to yellow
//                        double t = (norm - 0.6) / 0.2;
//                        cellColor = Color.FromRgb((byte)(t * 255), 255, 0);
//                    }
//                    else
//                    {
//                        // Yellow to red
//                        double t = (norm - 0.8) / 0.2;
//                        cellColor = Color.FromRgb(255, (byte)((1 - t) * 255), 0);
//                    }

//                    var rect = new Rectangle
//                    {
//                        Width = cellW + 0.5,
//                        Height = cellH + 0.5,
//                        Fill = new SolidColorBrush(cellColor),
//                        Stroke = Brushes.Black,
//                        StrokeThickness = 0.3
//                    };

//                    double px = gridLeft + (i - minI) * cellW;
//                    double py = gridTop + (maxJ - j) * cellH;  // Flip Y

//                    Canvas.SetLeft(rect, px);
//                    Canvas.SetTop(rect, py);
//                    targetCanvas.Children.Add(rect);
//                    structureCellsDrawn++;
//                }
//            }

//            OutputLog += $"Drew {structureCellsDrawn} structure cells (target-only visualization)\n";

//            // === DRAW COLORBAR (RIGHT REGION) ===
//            // Colorbar background
//            var colorbarBg = new Rectangle
//            {
//                Width = colorbarBarWidth + 4,
//                Height = colorbarHeight + 4,
//                Fill = Brushes.White,
//                Stroke = Brushes.Black,
//                StrokeThickness = 1
//            };
//            Canvas.SetLeft(colorbarBg, colorbarLeft - 2);
//            Canvas.SetTop(colorbarBg, colorbarTop - 2);
//            targetCanvas.Children.Add(colorbarBg);

//            // Colorbar gradient
//            int colorSteps = 50;
//            double stepHeight = colorbarHeight / colorSteps;

//            for (int s = 0; s < colorSteps; s++)
//            {
//                double norm = (double)s / (colorSteps - 1);

//                // Same color scheme as the plot
//                Color barColor;
//                if (norm < 0.2)
//                {
//                    double t = norm / 0.2;
//                    barColor = Color.FromRgb(0, 0, (byte)(100 + t * 155));
//                }
//                else if (norm < 0.4)
//                {
//                    double t = (norm - 0.2) / 0.2;
//                    barColor = Color.FromRgb(0, (byte)(t * 255), 255);
//                }
//                else if (norm < 0.6)
//                {
//                    double t = (norm - 0.4) / 0.2;
//                    barColor = Color.FromRgb(0, 255, (byte)((1 - t) * 255));
//                }
//                else if (norm < 0.8)
//                {
//                    double t = (norm - 0.6) / 0.2;
//                    barColor = Color.FromRgb((byte)(t * 255), 255, 0);
//                }
//                else
//                {
//                    double t = (norm - 0.8) / 0.2;
//                    barColor = Color.FromRgb(255, (byte)((1 - t) * 255), 0);
//                }

//                var barRect = new Rectangle
//                {
//                    Width = colorbarBarWidth,
//                    Height = stepHeight + 1,
//                    Fill = new SolidColorBrush(barColor),
//                    Stroke = Brushes.Black,
//                    StrokeThickness = 0.5
//                };

//                Canvas.SetLeft(barRect, colorbarLeft);
//                Canvas.SetTop(barRect, colorbarTop + (colorSteps - 1 - s) * stepHeight);
//                targetCanvas.Children.Add(barRect);
//            }

//            // === COLORBAR LABELS (RIGHT REGION) ===
//            double labelX = colorbarLeft + colorbarBarWidth + 10;

//            var maxLabel = new TextBlock
//            {
//                Text = $"{structureMaxDose:F1}",
//                FontSize = 14,
//                FontWeight = FontWeights.Bold,
//                Foreground = Brushes.Black
//            };
//            Canvas.SetLeft(maxLabel, labelX);
//            Canvas.SetTop(maxLabel, colorbarTop - 5);
//            targetCanvas.Children.Add(maxLabel);

//            var midLabel = new TextBlock
//            {
//                Text = $"{(structureMinDose + structureMaxDose) / 2:F1}",
//                FontSize = 14,
//                FontWeight = FontWeights.Bold,
//                Foreground = Brushes.Black
//            };
//            Canvas.SetLeft(midLabel, labelX);
//            Canvas.SetTop(midLabel, colorbarTop + colorbarHeight / 2 - 10);
//            targetCanvas.Children.Add(midLabel);

//            var minLabel = new TextBlock
//            {
//                Text = $"{structureMinDose:F1}",
//                FontSize = 14,
//                FontWeight = FontWeights.Bold,
//                Foreground = Brushes.Black
//            };
//            Canvas.SetLeft(minLabel, labelX);
//            Canvas.SetTop(minLabel, colorbarTop + colorbarHeight - 15);
//            targetCanvas.Children.Add(minLabel);

//            // Unit label
//            var unitLabel = new TextBlock
//            {
//                Text = "Target Dose (Gy)",
//                FontSize = 16,
//                FontWeight = FontWeights.Bold,
//                Foreground = Brushes.Black
//            };
//            Canvas.SetLeft(unitLabel, colorbarLeft);
//            Canvas.SetTop(unitLabel, colorbarTop - 35);
//            targetCanvas.Children.Add(unitLabel);

//            // Add note about visualization approach
//            //var targetNote = new TextBlock
//            //{
//            //    Text = "Target Structure Only",
//            //    FontSize = 12,
//            //    FontStyle = FontStyles.Italic,
//            //    Foreground = Brushes.DarkBlue,
//            //    FontWeight = FontWeights.Bold
//            //};
//            //Canvas.SetLeft(targetNote, gridLeft);
//            //Canvas.SetTop(targetNote, gridTop + actualHeatmapHeight + 10);
//            //targetCanvas.Children.Add(targetNote);

//            OutputLog += "=== Target-Only Layout Complete ===\n";
//        }

//        // Add this helper method to calculate structure-only dose range:
//        private (double minDose, double maxDose) GetStructureDoseRange(double[,] doseGrid, bool[,] inStruct, int nX, int nY)
//        {
//            double minDose = double.MaxValue;
//            double maxDose = double.MinValue;
//            int validCount = 0;

//            for (int i = 0; i < nX; i++)
//            {
//                for (int j = 0; j < nY; j++)
//                {
//                    if (inStruct != null && inStruct[i, j] && !double.IsNaN(doseGrid[i, j]))
//                    {
//                        double dose = doseGrid[i, j];
//                        minDose = Math.Min(minDose, dose);
//                        maxDose = Math.Max(maxDose, dose);
//                        validCount++;
//                    }
//                }
//            }

//            if (validCount == 0)
//            {
//                return (0, 1); // Fallback range
//            }

//            return (minDose, maxDose);
//        }


//        // Replace your DetectStructurePeaks method with this more sensitive version:
//        //private List<(int x, int y, double dose)> DetectStructurePeaks(double[,] doseGrid, bool[,] inStruct, int nX, int nY, double gridStep, List<double> structureDoses)
//        //{
//        //    var peaks = new List<(int x, int y, double dose)>();

//        //    if (structureDoses.Count == 0) return peaks;

//        //    // More detailed statistics
//        //    double meanStructureDose = structureDoses.Average();
//        //    double maxStructureDose = structureDoses.Max();
//        //    double minStructureDose = structureDoses.Min();
//        //    var sortedDoses = structureDoses.OrderByDescending(d => d).ToList();

//        //    // Use percentile-based thresholds for more sensitivity
//        //    double dose75th = sortedDoses[(int)(sortedDoses.Count * 0.25)]; // Top 25%
//        //    double dose90th = sortedDoses[(int)(sortedDoses.Count * 0.10)]; // Top 10%

//        //    // More sensitive peak threshold - choose the lower value for more peaks
//        //    double minPeakHeight = Math.Min(
//        //        meanStructureDose + (maxStructureDose - meanStructureDose) * 0.3, // 30% above mean towards max
//        //        dose75th // Or top 25% of doses
//        //    );

//        //    int minSeparation = Math.Max(1, (int)(1.5 / gridStep)); // Reduced to 1.5mm separation

//        //    OutputLog += $"Enhanced peak detection:\n";
//        //    OutputLog += $"  Dose range: {minStructureDose:F1} - {maxStructureDose:F1} Gy\n";
//        //    OutputLog += $"  Mean: {meanStructureDose:F1}, 75th percentile: {dose75th:F1}, 90th percentile: {dose90th:F1}\n";
//        //    OutputLog += $"  Peak threshold: {minPeakHeight:F1} Gy\n";
//        //    OutputLog += $"  Min separation: {minSeparation * gridStep:F1} mm\n";

//        //    for (int ix = 1; ix < nX - 1; ix++)
//        //    {
//        //        for (int iy = 1; iy < nY - 1; iy++)
//        //        {
//        //            if (!inStruct[ix, iy]) continue;

//        //            double centerDose = doseGrid[ix, iy];
//        //            if (double.IsNaN(centerDose) || centerDose < minPeakHeight) continue;

//        //            // Check if this point is a local maximum
//        //            bool isPeak = true;
//        //            int validNeighbors = 0;
//        //            double neighborSum = 0;

//        //            // Check 3x3 neighborhood
//        //            for (int dx = -1; dx <= 1; dx++)
//        //            {
//        //                for (int dy = -1; dy <= 1; dy++)
//        //                {
//        //                    if (dx == 0 && dy == 0) continue;

//        //                    int nx = ix + dx;
//        //                    int ny = iy + dy;

//        //                    if (nx >= 0 && nx < nX && ny >= 0 && ny < nY && inStruct[nx, ny])
//        //                    {
//        //                        double neighborDose = doseGrid[nx, ny];
//        //                        if (!double.IsNaN(neighborDose))
//        //                        {
//        //                            validNeighbors++;
//        //                            neighborSum += neighborDose;

//        //                            // For peak: center must be strictly higher than ALL neighbors
//        //                            if (neighborDose >= centerDose)
//        //                            {
//        //                                isPeak = false;
//        //                                break;
//        //                            }
//        //                        }
//        //                    }
//        //                }
//        //                if (!isPeak) break;
//        //            }

//        //            // Additional check: must be significantly higher than average neighbor
//        //            double avgNeighbor = validNeighbors > 0 ? neighborSum / validNeighbors : centerDose;
//        //            double relativeDifference = (centerDose - avgNeighbor) / meanStructureDose;
//        //            bool significantPeak = relativeDifference > 0.05; // 5% of mean dose difference

//        //            if (isPeak && validNeighbors >= 3 && significantPeak)
//        //            {
//        //                // Check separation from existing peaks
//        //                bool wellSeparated = true;
//        //                foreach (var existingPeak in peaks)
//        //                {
//        //                    int distX = Math.Abs(ix - existingPeak.x);
//        //                    int distY = Math.Abs(iy - existingPeak.y);
//        //                    double dist = Math.Sqrt(distX * distX + distY * distY) * gridStep;
//        //                    if (dist < minSeparation * gridStep)
//        //                    {
//        //                        // Keep the higher peak if they're too close
//        //                        if (centerDose > existingPeak.dose)
//        //                        {
//        //                            peaks.Remove(existingPeak);
//        //                            break;
//        //                        }
//        //                        else
//        //                        {
//        //                            wellSeparated = false;
//        //                            break;
//        //                        }
//        //                    }
//        //                }

//        //                if (wellSeparated)
//        //                {
//        //                    peaks.Add((ix, iy, centerDose));
//        //                    OutputLog += $"  Peak at grid({ix},{iy}): {centerDose:F1} Gy (neighbors avg: {avgNeighbor:F1})\n";
//        //                }
//        //            }
//        //        }
//        //    }

//        //    return peaks;
//        //}

//        //// Replace your DetectStructureValleys method with this more sensitive version:
//        //private List<(int x, int y, double dose)> DetectStructureValleys(double[,] doseGrid, bool[,] inStruct, int nX, int nY, double gridStep, List<double> structureDoses)
//        //{
//        //    var valleys = new List<(int x, int y, double dose)>();

//        //    if (structureDoses.Count == 0) return valleys;

//        //    // More detailed statistics
//        //    double meanStructureDose = structureDoses.Average();
//        //    double maxStructureDose = structureDoses.Max();
//        //    double minStructureDose = structureDoses.Min();
//        //    var sortedDoses = structureDoses.OrderBy(d => d).ToList();

//        //    // Use percentile-based thresholds
//        //    double dose25th = sortedDoses[(int)(sortedDoses.Count * 0.25)]; // Bottom 25%
//        //    double dose10th = sortedDoses[(int)(sortedDoses.Count * 0.10)]; // Bottom 10%

//        //    // More sensitive valley threshold
//        //    double maxValleyHeight = Math.Max(
//        //        meanStructureDose - (meanStructureDose - minStructureDose) * 0.3, // 30% below mean towards min
//        //        dose25th // Or bottom 25% of doses
//        //    );

//        //    int minSeparation = Math.Max(1, (int)(1.5 / gridStep)); // Reduced to 1.5mm separation

//        //    OutputLog += $"Enhanced valley detection:\n";
//        //    OutputLog += $"  25th percentile: {dose25th:F1}, 10th percentile: {dose10th:F1}\n";
//        //    OutputLog += $"  Valley threshold: {maxValleyHeight:F1} Gy\n";

//        //    for (int ix = 1; ix < nX - 1; ix++)
//        //    {
//        //        for (int iy = 1; iy < nY - 1; iy++)
//        //        {
//        //            if (!inStruct[ix, iy]) continue;

//        //            double centerDose = doseGrid[ix, iy];
//        //            if (double.IsNaN(centerDose) || centerDose > maxValleyHeight) continue;

//        //            // Check if this point is a local minimum
//        //            bool isValley = true;
//        //            int validNeighbors = 0;
//        //            double neighborSum = 0;

//        //            // Check 3x3 neighborhood
//        //            for (int dx = -1; dx <= 1; dx++)
//        //            {
//        //                for (int dy = -1; dy <= 1; dy++)
//        //                {
//        //                    if (dx == 0 && dy == 0) continue;

//        //                    int nx = ix + dx;
//        //                    int ny = iy + dy;

//        //                    if (nx >= 0 && nx < nX && ny >= 0 && ny < nY && inStruct[nx, ny])
//        //                    {
//        //                        double neighborDose = doseGrid[nx, ny];
//        //                        if (!double.IsNaN(neighborDose))
//        //                        {
//        //                            validNeighbors++;
//        //                            neighborSum += neighborDose;

//        //                            // For valley: center must be strictly lower than ALL neighbors
//        //                            if (neighborDose <= centerDose)
//        //                            {
//        //                                isValley = false;
//        //                                break;
//        //                            }
//        //                        }
//        //                    }
//        //                }
//        //                if (!isValley) break;
//        //            }

//        //            // Additional check: must be significantly lower than average neighbor
//        //            double avgNeighbor = validNeighbors > 0 ? neighborSum / validNeighbors : centerDose;
//        //            double relativeDifference = (avgNeighbor - centerDose) / meanStructureDose;
//        //            bool significantValley = relativeDifference > 0.05; // 5% of mean dose difference

//        //            if (isValley && validNeighbors >= 3 && significantValley)
//        //            {
//        //                // Check separation from existing valleys
//        //                bool wellSeparated = true;
//        //                foreach (var existingValley in valleys)
//        //                {
//        //                    int distX = Math.Abs(ix - existingValley.x);
//        //                    int distY = Math.Abs(iy - existingValley.y);
//        //                    double dist = Math.Sqrt(distX * distX + distY * distY) * gridStep;
//        //                    if (dist < minSeparation * gridStep)
//        //                    {
//        //                        // Keep the lower valley if they're too close
//        //                        if (centerDose < existingValley.dose)
//        //                        {
//        //                            valleys.Remove(existingValley);
//        //                            break;
//        //                        }
//        //                        else
//        //                        {
//        //                            wellSeparated = false;
//        //                            break;
//        //                        }
//        //                    }
//        //                }

//        //                if (wellSeparated)
//        //                {
//        //                    valleys.Add((ix, iy, centerDose));
//        //                    OutputLog += $"  Valley at grid({ix},{iy}): {centerDose:F1} Gy (neighbors avg: {avgNeighbor:F1})\n";
//        //                }
//        //            }
//        //        }
//        //    }

//        //    return valleys;
//        //}

//        private void AnalyzeDoseDistribution(List<double> structureDoses)
//        {
//            if (structureDoses.Count == 0) return;

//            var sorted = structureDoses.OrderBy(d => d).ToList();
//            int count = sorted.Count;

//            OutputLog += "\n=== DOSE DISTRIBUTION ANALYSIS ===\n";
//            OutputLog += $"Total structure points: {count}\n";
//            OutputLog += $"Min: {sorted[0]:F1} Gy\n";
//            OutputLog += $"10th percentile: {sorted[(int)(count * 0.1)]:F1} Gy\n";
//            OutputLog += $"25th percentile: {sorted[(int)(count * 0.25)]:F1} Gy\n";
//            OutputLog += $"Median: {sorted[count / 2]:F1} Gy\n";
//            OutputLog += $"75th percentile: {sorted[(int)(count * 0.75)]:F1} Gy\n";
//            OutputLog += $"90th percentile: {sorted[(int)(count * 0.9)]:F1} Gy\n";
//            OutputLog += $"Max: {sorted[count - 1]:F1} Gy\n";
//            OutputLog += $"Mean: {structureDoses.Average():F1} Gy\n";
//            OutputLog += $"Std Dev: {Math.Sqrt(structureDoses.Select(d => Math.Pow(d - structureDoses.Average(), 2)).Average()):F1} Gy\n";

//            // Count doses in different ranges
//            int lowDose = sorted.Count(d => d < 20);
//            int midDose = sorted.Count(d => d >= 20 && d < 50);
//            int highDose = sorted.Count(d => d >= 50);

//            OutputLog += $"Dose distribution: <20Gy: {lowDose} ({100.0 * lowDose / count:F1}%), ";
//            OutputLog += $"20-50Gy: {midDose} ({100.0 * midDose / count:F1}%), ";
//            OutputLog += $">50Gy: {highDose} ({100.0 * highDose / count:F1}%)\n";
//        }

//        // Just replace your existing DetectStructurePeaks method with this:
//        private List<(int x, int y, double dose)> DetectStructurePeaks(double[,] doseGrid, bool[,] inStruct, int nX, int nY, double gridStep, List<double> structureDoses)
//        {
//            var peaks = new List<(int x, int y, double dose)>();

//            if (structureDoses.Count == 0) return peaks;

//            // Simple: anything above 60% of max dose
//            double maxDose = structureDoses.Max();
//            double minPeakHeight = maxDose * 0.6; // 60% of max

//            OutputLog += $"Peak detection: threshold {minPeakHeight:F1} Gy (60% of max {maxDose:F1})\n";

//            for (int ix = 1; ix < nX - 1; ix++)
//            {
//                for (int iy = 1; iy < nY - 1; iy++)
//                {
//                    if (!inStruct[ix, iy]) continue;

//                    double centerDose = doseGrid[ix, iy];
//                    if (double.IsNaN(centerDose) || centerDose < minPeakHeight) continue;

//                    // Simple local maximum check
//                    bool isPeak = true;
//                    for (int dx = -1; dx <= 1; dx++)
//                    {
//                        for (int dy = -1; dy <= 1; dy++)
//                        {
//                            if (dx == 0 && dy == 0) continue;

//                            int nx = ix + dx;
//                            int ny = iy + dy;

//                            if (nx >= 0 && nx < nX && ny >= 0 && ny < nY && inStruct[nx, ny])
//                            {
//                                double neighborDose = doseGrid[nx, ny];
//                                if (!double.IsNaN(neighborDose) && neighborDose >= centerDose)
//                                {
//                                    isPeak = false;
//                                    break;
//                                }
//                            }
//                        }
//                        if (!isPeak) break;
//                    }

//                    if (isPeak)
//                    {
//                        // Check 2mm separation
//                        bool wellSeparated = true;
//                        foreach (var existingPeak in peaks)
//                        {
//                            double dist = Math.Sqrt(Math.Pow((ix - existingPeak.x) * gridStep, 2) +
//                                                  Math.Pow((iy - existingPeak.y) * gridStep, 2));
//                            if (dist < 2.0)
//                            {
//                                if (centerDose > existingPeak.dose)
//                                {
//                                    peaks.Remove(existingPeak);
//                                    break;
//                                }
//                                else
//                                {
//                                    wellSeparated = false;
//                                    break;
//                                }
//                            }
//                        }

//                        if (wellSeparated)
//                        {
//                            peaks.Add((ix, iy, centerDose));
//                            OutputLog += $"Peak at ({ix},{iy}): {centerDose:F1} Gy\n";
//                        }
//                    }
//                }
//            }

//            return peaks;
//        }

//        // Just replace your existing DetectStructureValleys method with this:
//        private List<(int x, int y, double dose)> DetectStructureValleys(double[,] doseGrid, bool[,] inStruct, int nX, int nY, double gridStep, List<double> structureDoses)
//        {
//            var valleys = new List<(int x, int y, double dose)>();

//            if (structureDoses.Count == 0) return valleys;

//            // Simple: anything below 40% of max dose or below 25 Gy
//            double maxDose = structureDoses.Max();
//            double maxValleyHeight = Math.Min(maxDose * 0.4, 25.0); // 40% of max or 25 Gy

//            OutputLog += $"Valley detection: threshold {maxValleyHeight:F1} Gy (40% of max or 25 Gy)\n";

//            // Count how many points qualify
//            int qualifyingPoints = structureDoses.Count(d => d <= maxValleyHeight);
//            OutputLog += $"Points below threshold: {qualifyingPoints} ({100.0 * qualifyingPoints / structureDoses.Count:F1}%)\n";

//            for (int ix = 1; ix < nX - 1; ix++)
//            {
//                for (int iy = 1; iy < nY - 1; iy++)
//                {
//                    if (!inStruct[ix, iy]) continue;

//                    double centerDose = doseGrid[ix, iy];
//                    if (double.IsNaN(centerDose) || centerDose > maxValleyHeight) continue;

//                    // Simple local minimum check - must be lower than most neighbors
//                    int validNeighbors = 0;
//                    int lowerThanNeighbors = 0;

//                    for (int dx = -1; dx <= 1; dx++)
//                    {
//                        for (int dy = -1; dy <= 1; dy++)
//                        {
//                            if (dx == 0 && dy == 0) continue;

//                            int nx = ix + dx;
//                            int ny = iy + dy;

//                            if (nx >= 0 && nx < nX && ny >= 0 && ny < nY && inStruct[nx, ny])
//                            {
//                                double neighborDose = doseGrid[nx, ny];
//                                if (!double.IsNaN(neighborDose))
//                                {
//                                    validNeighbors++;
//                                    if (centerDose < neighborDose)
//                                    {
//                                        lowerThanNeighbors++;
//                                    }
//                                }
//                            }
//                        }
//                    }

//                    // Must be lower than at least half the neighbors
//                    bool isValley = (validNeighbors >= 3) && (lowerThanNeighbors >= validNeighbors / 2);

//                    if (isValley)
//                    {
//                        // Check 2mm separation
//                        bool wellSeparated = true;
//                        foreach (var existingValley in valleys)
//                        {
//                            double dist = Math.Sqrt(Math.Pow((ix - existingValley.x) * gridStep, 2) +
//                                                  Math.Pow((iy - existingValley.y) * gridStep, 2));
//                            if (dist < 2.0)
//                            {
//                                if (centerDose < existingValley.dose)
//                                {
//                                    valleys.Remove(existingValley);
//                                    break;
//                                }
//                                else
//                                {
//                                    wellSeparated = false;
//                                    break;
//                                }
//                            }
//                        }

//                        if (wellSeparated)
//                        {
//                            valleys.Add((ix, iy, centerDose));
//                            OutputLog += $"Valley at ({ix},{iy}): {centerDose:F1} Gy\n";
//                        }
//                    }
//                }
//            }

//            return valleys;
//        }

//        //// Optional: Add a dose distribution analyzer to understand your data better
//        //private void AnalyzeDoseDistribution(List<double> structureDoses)
//        //{
//        //    if (structureDoses.Count == 0) return;

//        //    var sorted = structureDoses.OrderBy(d => d).ToList();
//        //    int count = sorted.Count;

//        //    OutputLog += "\n=== DOSE DISTRIBUTION ANALYSIS ===\n";
//        //    OutputLog += $"Total structure points: {count}\n";
//        //    OutputLog += $"Min: {sorted[0]:F1} Gy\n";
//        //    OutputLog += $"10th percentile: {sorted[(int)(count * 0.1)]:F1} Gy\n";
//        //    OutputLog += $"25th percentile: {sorted[(int)(count * 0.25)]:F1} Gy\n";
//        //    OutputLog += $"Median: {sorted[count / 2]:F1} Gy\n";
//        //    OutputLog += $"75th percentile: {sorted[(int)(count * 0.75)]:F1} Gy\n";
//        //    OutputLog += $"90th percentile: {sorted[(int)(count * 0.9)]:F1} Gy\n";
//        //    OutputLog += $"Max: {sorted[count - 1]:F1} Gy\n";
//        //    OutputLog += $"Mean: {structureDoses.Average():F1} Gy\n";
//        //    OutputLog += $"Std Dev: {Math.Sqrt(structureDoses.Select(d => Math.Pow(d - structureDoses.Average(), 2)).Average()):F1} Gy\n";

//        //    // Dose histogram in 10 bins
//        //    double binSize = (sorted.Last() - sorted.First()) / 10;
//        //    OutputLog += "Dose histogram:\n";
//        //    for (int i = 0; i < 10; i++)
//        //    {
//        //        double binStart = sorted.First() + i * binSize;
//        //        double binEnd = binStart + binSize;
//        //        int binCount = sorted.Count(d => d >= binStart && d < binEnd);
//        //        if (i == 9) binCount = sorted.Count(d => d >= binStart && d <= binEnd); // Include max in last bin
//        //        OutputLog += $"  {binStart:F1}-{binEnd:F1} Gy: {binCount} points ({100.0 * binCount / count:F1}%)\n";
//        //    }
//        //}

//        // Add this helper method to your EvaluationViewModel class:
//        private void InterpolateForVisualization(double[,] doseGrid, bool[,] inStruct, int nX, int nY)
//        {
//            // Simple interpolation for missing values to create smoother visualization
//            var tempGrid = new double[nX, nY];

//            // Copy existing values
//            for (int ix = 0; ix < nX; ix++)
//            {
//                for (int iy = 0; iy < nY; iy++)
//                {
//                    tempGrid[ix, iy] = doseGrid[ix, iy];
//                }
//            }

//            // Interpolate missing outside values
//            for (int ix = 1; ix < nX - 1; ix++)
//            {
//                for (int iy = 1; iy < nY - 1; iy++)
//                {
//                    if (inStruct[ix, iy] || !double.IsNaN(doseGrid[ix, iy]))
//                        continue; // Don't interpolate structure points or existing values

//                    // Find nearby valid values for interpolation
//                    var nearbyValues = new List<double>();
//                    var distances = new List<double>();

//                    for (int dx = -2; dx <= 2; dx++)
//                    {
//                        for (int dy = -2; dy <= 2; dy++)
//                        {
//                            if (dx == 0 && dy == 0) continue;

//                            int nx = ix + dx;
//                            int ny = iy + dy;

//                            if (nx >= 0 && nx < nX && ny >= 0 && ny < nY)
//                            {
//                                double val = doseGrid[nx, ny];
//                                if (!double.IsNaN(val))
//                                {
//                                    nearbyValues.Add(val);
//                                    distances.Add(Math.Sqrt(dx * dx + dy * dy));
//                                }
//                            }
//                        }
//                    }

//                    if (nearbyValues.Count >= 3)
//                    {
//                        // Weighted interpolation based on distance
//                        double weightedSum = 0;
//                        double totalWeight = 0;

//                        for (int i = 0; i < nearbyValues.Count; i++)
//                        {
//                            double weight = 1.0 / (distances[i] + 0.1); // Avoid division by zero
//                            weightedSum += nearbyValues[i] * weight;
//                            totalWeight += weight;
//                        }

//                        if (totalWeight > 0)
//                        {
//                            tempGrid[ix, iy] = weightedSum / totalWeight;
//                        }
//                    }
//                }
//            }

//            // Copy interpolated values back
//            for (int ix = 0; ix < nX; ix++)
//            {
//                for (int iy = 0; iy < nY; iy++)
//                {
//                    if (!inStruct[ix, iy] && double.IsNaN(doseGrid[ix, iy]))
//                    {
//                        doseGrid[ix, iy] = tempGrid[ix, iy];
//                    }
//                }
//            }
//        }

//        private Color GetSmoothDoseColor(double dose, double minDose, double maxDose)
//        {
//            if (double.IsNaN(dose))
//                return Colors.LightGray;

//            double norm = Math.Max(0, Math.Min(1, (dose - minDose) / (maxDose - minDose)));

//            // Clinical color scheme: Blue -> Cyan -> Green -> Yellow -> Red
//            if (norm < 0.2)
//            {
//                // Dark blue to blue
//                double t = norm / 0.2;
//                return Color.FromRgb(0, 0, (byte)(100 + t * 155));
//            }
//            else if (norm < 0.4)
//            {
//                // Blue to cyan
//                double t = (norm - 0.2) / 0.2;
//                return Color.FromRgb(0, (byte)(t * 255), 255);
//            }
//            else if (norm < 0.6)
//            {
//                // Cyan to green
//                double t = (norm - 0.4) / 0.2;
//                return Color.FromRgb(0, 255, (byte)((1 - t) * 255));
//            }
//            else if (norm < 0.8)
//            {
//                // Green to yellow
//                double t = (norm - 0.6) / 0.2;
//                return Color.FromRgb((byte)(t * 255), 255, 0);
//            }
//            else
//            {
//                // Yellow to red
//                double t = (norm - 0.8) / 0.2;
//                return Color.FromRgb(255, (byte)((1 - t) * 255), 0);
//            }
//        }

//    }
//}

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
using System.Windows.Media.Media3D;
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

        //Is2DPlanarSelected
        private bool _is2DPlanarSelected;

        public bool Is2DPlanarSelected
        {
            get { return _is2DPlanarSelected; }
            set { SetProperty(ref _is2DPlanarSelected, value); }
        }

        //Dose Metrics
        private bool _is3dEvaluationSelected;

        public bool Is3DEvaluationSelected
        {
            get { return _is3dEvaluationSelected; }
            set { SetProperty(ref _is3dEvaluationSelected, value); }
        }

        //3D interpolation
        private bool _is3dInterpolationSelected;

        public bool Is3DInterpolationSelected
        {
            get { return _is3dInterpolationSelected; }
            set { SetProperty(ref _is3dInterpolationSelected, value); }
        }

        //PVDR Options
        private string _selectedPvdrMode;
        public string SelectedPvdrMode
        {
            get => _selectedPvdrMode;
            set => SetProperty(ref _selectedPvdrMode, value);
        }

        public ObservableCollection<string> PvdrModeOptions { get; set; }

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
                    if (value && (_hasPlotData || _has2DPlotData))
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
                        if ((_hasPlotData || _has2DPlotData) && bPlotVis)
                        {
                            OutputLog += "Manual plot refresh triggered.\n";
                            RefreshPlot();
                        }
                    }, () => (_hasPlotData || _has2DPlotData) && bPlotVis);
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

        // For 2D grid storage and plotting (legacy single-depth)
        private double[,] _dose2DGrid;  // [nX, nY]
        private double[,] _x2DGrid;     // [nX, nY]
        private double[,] _y2DGrid;     // [nX, nY]
        private bool[,] _inStruct2D;    // [nX, nY]
        private int _nX2D, _nY2D;       // grid size
        private bool _has2DPlotData = false;

        #region Multi-Depth 2D Data Structures
        // Multi-slice storage with adaptive grids
        private List<double[,]> _doseSlices = new List<double[,]>();
        private List<bool[,]> _structSlices = new List<bool[,]>();
        private List<double[,]> _uGridSlices = new List<double[,]>(); // U coordinates per slice
        private List<double[,]> _vGridSlices = new List<double[,]>(); // V coordinates per slice
        private List<double> _depthValues = new List<double>(); // actual mm from entry
        private List<(int nX, int nY)> _sliceDimensions = new List<(int, int)>();

        private int _currentDepthIndex = 0;
        public int CurrentDepthIndex
        {
            get { return _currentDepthIndex; }
            set
            {
                if (SetProperty(ref _currentDepthIndex, value))
                {
                    // Update visualization when depth changes
                    if (_depthValues.Count > 0 && value >= 0 && value < _depthValues.Count)
                    {
                        CurrentDepthValue = _depthValues[value];
                        if (bPlotVis && _has2DPlotData)
                        {
                            Show2DDoseHeatmapOnCanvas(PlotCanvas, value);
                        }
                    }

                    // Update command states
                    RaisePropertyChanged(nameof(DepthDisplayText));
                    RaisePropertyChanged(nameof(CanGoToPreviousDepth));
                    RaisePropertyChanged(nameof(CanGoToNextDepth));
                    PreviousDepthCommand?.RaiseCanExecuteChanged();
                    NextDepthCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private double _currentDepthValue = 0.0;
        public double CurrentDepthValue
        {
            get { return _currentDepthValue; }
            set { SetProperty(ref _currentDepthValue, value); }
        }

        public int MaxDepthIndex => Math.Max(0, _depthValues.Count - 1);

        public string DepthDisplayText
        {
            get
            {
                if (_depthValues.Count == 0) return "No depth data";
                return $"Depth: {CurrentDepthValue:F1}mm from entry (Slice {CurrentDepthIndex + 1} of {_depthValues.Count})";
            }
        }

        public bool CanGoToPreviousDepth => CurrentDepthIndex > 0 && _depthValues.Count > 0;
        public bool CanGoToNextDepth => CurrentDepthIndex < MaxDepthIndex && _depthValues.Count > 0;

        private DelegateCommand _previousDepthCommand;
        public DelegateCommand PreviousDepthCommand
        {
            get
            {
                if (_previousDepthCommand == null)
                {
                    _previousDepthCommand = new DelegateCommand(() =>
                    {
                        if (CurrentDepthIndex > 0)
                        {
                            CurrentDepthIndex--;
                        }
                    }, () => CanGoToPreviousDepth);
                }
                return _previousDepthCommand;
            }
        }

        private DelegateCommand _nextDepthCommand;
        public DelegateCommand NextDepthCommand
        {
            get
            {
                if (_nextDepthCommand == null)
                {
                    _nextDepthCommand = new DelegateCommand(() =>
                    {
                        if (CurrentDepthIndex < MaxDepthIndex)
                        {
                            CurrentDepthIndex++;
                        }
                    }, () => CanGoToNextDepth);
                }
                return _nextDepthCommand;
            }
        }
        #endregion

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
                PvdrModeOptions = new ObservableCollection<string> { "Fixed Beam", "VMAT" };
                SelectedPvdrMode = PvdrModeOptions.First(); // default to "Fixed Beam"

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
            if (bPlotVis && (_hasPlotData || _has2DPlotData))
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
            if (bPlotVis && (_hasPlotData || _has2DPlotData))
            {
                RefreshPlot();
            }
        }

        private void RefreshPlot()
        {
            if (PlotCanvas == null) return;

            try
            {
                if (_hasPlotData && Is1DCAXSelected)
                {
                    // 1D plot refresh
                    string tumorId = SelectedTumorId ?? "Unknown Tumor";
                    string beamId = SelectedBeamId ?? "Unknown Beam";

                    // Clear existing plot first
                    PlotCanvas.Children.Clear();
                    PlotCanvas.UpdateLayout();

                    PlotCanvas.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                    {
                        DrawEmbeddedPlot(PlotCanvas, _distances, _doseValues, _insideTumorFlags, _entryDist, _exitDist, tumorId, beamId);
                    }));
                }
                else if (_has2DPlotData && Is2DPlanarSelected)
                {
                    // 2D multi-depth plot refresh
                    Show2DDoseHeatmapOnCanvas(PlotCanvas, CurrentDepthIndex);
                }
            }
            catch (Exception ex)
            {
                OutputLog += $"Error refreshing plot: {ex.Message}\n";
            }
        }

        // Helper method to clear multi-depth data
        private void ClearMultiDepthData()
        {
            _doseSlices.Clear();
            _structSlices.Clear();
            _uGridSlices.Clear();
            _vGridSlices.Clear();
            _depthValues.Clear();
            _sliceDimensions.Clear();
            _currentDepthIndex = 0;
            CurrentDepthValue = 0.0;
            _has2DPlotData = false;

            // Notify UI of property changes
            RaisePropertyChanged(nameof(MaxDepthIndex));
            RaisePropertyChanged(nameof(DepthDisplayText));
            RaisePropertyChanged(nameof(CanGoToPreviousDepth));
            RaisePropertyChanged(nameof(CanGoToNextDepth));
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
                OutputLog += "Starting dose computation...\n";

                // Always reset plot states before computing!
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    _hasPlotData = false;
                    _has2DPlotData = false;
                    // Clear multi-depth data
                    ClearMultiDepthData();
                    bTextVis = true;
                    bPlotVis = false;
                });

                // Copy selected IDs to local variables to avoid potential thread issues
                string selectedBeamId = SelectedBeamId;
                string selectedTumorId = SelectedTumorId;
                string ptvAllName = SelectedTumorId;
                string selectedPvdrMode = SelectedPvdrMode;

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

                        if (Is1DCAXSelected)
                        {
                            Run1DPVDRMetric(selectedTumorId, plan);
                            OutputLog += "1D CAX Dosimetrics complete\n";
                            return;
                        }

                        if (Is2DPlanarSelected)
                        {
                            OutputLog += "Running Multi-Depth 2D Planar computation...\n";
                            Run2DPVDRMetric(selectedTumorId, plan);
                            OutputLog += "Multi-Depth 2D Dosimetrics complete\n";
                            return;
                        }

                        bool execute3DDose = Is3DEvaluationSelected;

                        if (execute3DDose)
                        {
                            update3DMetrics(selectedTumorId, ptvAllName, plan);
                            OutputLog += "3D Dosimetrics complete\n";
                            return;
                        }
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
                // 1D CAX ready?
                if (Is1DCAXSelected)
                    return _distances?.Count > 0;

                // 2D Planar (multi-depth) ready?
                if (Is2DPlanarSelected)
                    return _has2DPlotData && _doseSlices.Count > 0;

                // 3D evaluation ready?
                if (Is3DEvaluationSelected)
                    return false; // Not implemented yet

                return false;
            }
            catch
            {
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
                else if (Is2DPlanarSelected)
                {
                    OutputLog += "Rendering multi-depth 2D planar dose heatmap...\n";

                    if (!_has2DPlotData || _doseSlices.Count == 0)
                    {
                        MessageBox.Show("No multi-depth grid data available. Run 2D computation first.",
                                        "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                        bTextVis = true;
                        bPlotVis = false;
                        return;
                    }

                    // Show current depth slice
                    Show2DDoseHeatmapOnCanvas(PlotCanvas, CurrentDepthIndex);
                    OutputLog += $"Multi-depth heatmap drawn successfully for slice {CurrentDepthIndex + 1}\n";
                }
                else if (Is3DEvaluationSelected)
                {
                    OutputLog += "Using 3D Dose P/V Interpolation evaluation method for plot...\n";
                    MessageBox.Show("3D Dose P/V Interpolation plotting not yet implemented.",
                        "Feature Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
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

        public void Run1DPVDRMetric(string tumorId, PlanSetup plan)
        {
            // Clear previous data first
            _distances = new List<double>();
            _doseValues = new List<double>();
            _insideTumorFlags = new List<bool>();
            _hasPlotData = false;

            OutputLog += "Starting dose computation...\n";

            string selectedBeamId = SelectedBeamId;
            string selectedTumorId = SelectedTumorId;

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

        // Multi-Depth 2D PVDR Implementation
        public void Run2DPVDRMetric(string tumorId, PlanSetup plan)
        {
            try
            {
                OutputLog += "\nStarting Multi-Depth 2D PVDR analysis...\n";
                string selectedPvdrMode = SelectedPvdrMode;

                if (selectedPvdrMode == "Fixed Beam")
                {
                    OutputLog += "\n===== Enhanced Multi-Depth Fixed Beam 2D PVDR Analysis =====\n";

                    // Clear previous multi-depth data
                    ClearMultiDepthData();

                    // 1. Get the beam and structure
                    var beam = plan.Beams.FirstOrDefault(b => b.Id == SelectedBeamId);
                    if (beam == null)
                    {
                        OutputLog += "Selected beam not found.\n";
                        return;
                    }
                    var structure = plan.StructureSet.Structures.FirstOrDefault(s => s.Id == tumorId);
                    if (structure == null)
                    {
                        OutputLog += $"Structure '{tumorId}' not found.\n";
                        return;
                    }

                    // 2. Setup beam geometry
                    var iso = beam.IsocenterPosition;
                    var cp0 = beam.ControlPoints.First();

                    // Calculate beam direction
                    double gantryRad = cp0.GantryAngle * Math.PI / 180.0;
                    double couchRad = cp0.PatientSupportAngle * Math.PI / 180.0;
                    VVector beamDir = new VVector(Math.Sin(gantryRad), -Math.Cos(gantryRad), 0);

                    // Apply couch rotation if needed
                    if (Math.Abs(cp0.PatientSupportAngle) > 0.1)
                    {
                        double x = beamDir.x, z = beamDir.z;
                        beamDir.x = x * Math.Cos(couchRad) + z * Math.Sin(couchRad);
                        beamDir.z = -x * Math.Sin(couchRad) + z * Math.Cos(couchRad);
                    }
                    double norm = Math.Sqrt(beamDir.x * beamDir.x + beamDir.y * beamDir.y + beamDir.z * beamDir.z);
                    beamDir = new VVector(beamDir.x / norm, beamDir.y / norm, beamDir.z / norm);

                    // Create orthogonal vectors for the plane
                    VVector up = new VVector(0, 0, 1);
                    VVector uAxis = EvaluationViewModel.Cross(beamDir, up);
                    if (uAxis.Length == 0) uAxis = new VVector(1, 0, 0);
                    uAxis = uAxis / uAxis.Length;
                    VVector vAxis = EvaluationViewModel.Cross(beamDir, uAxis);
                    vAxis = vAxis / vAxis.Length;

                    OutputLog += $"Beam direction: ({beamDir.x:F3}, {beamDir.y:F3}, {beamDir.z:F3})\n";

                    // 3. Find beam entry/exit through structure
                    OutputLog += "Finding beam entry and exit points through structure...\n";
                    double searchStartDist = -300.0;
                    double searchEndDist = 300.0;
                    double searchStep = 2.0;
                    bool insideStructure = false;
                    double entryDist = double.NaN;
                    double exitDist = double.NaN;

                    for (double dist = searchStartDist; dist <= searchEndDist; dist += searchStep)
                    {
                        var point = iso + dist * beamDir;
                        bool pointInStructure = false;

                        try
                        {
                            pointInStructure = structure.IsPointInsideSegment(point);
                        }
                        catch (Exception ex)
                        {
                            OutputLog += $"Error checking point: {ex.Message}\n";
                            continue;
                        }

                        if (!insideStructure && pointInStructure)
                        {
                            entryDist = dist;
                            insideStructure = true;
                        }
                        else if (insideStructure && !pointInStructure)
                        {
                            exitDist = dist;
                            break;
                        }
                    }

                    if (double.IsNaN(entryDist) || double.IsNaN(exitDist))
                    {
                        OutputLog += "Beam does not intersect the structure.\n";
                        return;
                    }

                    // 4. Calculate depth positions
                    double targetThickness = exitDist - entryDist;
                    double depthSpacing = (targetThickness < 9.0) ? 2.0 : 3.0; // mm
                    int numberOfSlices = Math.Max(2, (int)Math.Ceiling(targetThickness / depthSpacing));

                    OutputLog += $"Target thickness: {targetThickness:F1}mm, using {depthSpacing:F1}mm spacing\n";
                    OutputLog += $"Computing {numberOfSlices} depth slices\n";

                    // Generate depth positions
                    for (int sliceIdx = 0; sliceIdx < numberOfSlices; sliceIdx++)
                    {
                        double depthFromEntry = sliceIdx * (targetThickness / (numberOfSlices - 1));
                        _depthValues.Add(depthFromEntry);
                    }

                    // 5. Process each depth slice
                    double inPlaneStep = 1.5; // mm resolution for proton minibeams

                    for (int sliceIdx = 0; sliceIdx < numberOfSlices; sliceIdx++)
                    {
                        double currentDepth = entryDist + _depthValues[sliceIdx];
                        var currentPlanePosition = iso + currentDepth * beamDir;

                        OutputLog += $"\nProcessing slice {sliceIdx + 1}/{numberOfSlices} at depth {_depthValues[sliceIdx]:F1}mm...\n";

                        // Find structure bounding box at this specific depth
                        var (sliceMinU, sliceMaxU, sliceMinV, sliceMaxV, structurePointCount) =
                            FindStructureBoundsAtDepth(structure, currentPlanePosition, uAxis, vAxis, beamDir);

                        if (structurePointCount == 0)
                        {
                            OutputLog += $"No structure found at depth {_depthValues[sliceIdx]:F1}mm, skipping slice\n";
                            continue;
                        }

                        // Create adaptive grid for this slice
                        double sliceWidth = sliceMaxU - sliceMinU;
                        double sliceHeight = sliceMaxV - sliceMinV;
                        double padding = Math.Max(3, Math.Max(sliceWidth, sliceHeight) * 0.1);

                        double gridMinU = sliceMinU - padding;
                        double gridMaxU = sliceMaxU + padding;
                        double gridMinV = sliceMinV - padding;
                        double gridMaxV = sliceMaxV + padding;

                        int nX = (int)Math.Ceiling((gridMaxU - gridMinU) / inPlaneStep);
                        int nY = (int)Math.Ceiling((gridMaxV - gridMinV) / inPlaneStep);

                        OutputLog += $"Slice {sliceIdx + 1} grid: {nX}×{nY} points covering {sliceWidth:F1}×{sliceHeight:F1}mm\n";

                        // Initialize arrays for this slice
                        var doseSlice = new double[nX, nY];
                        var structSlice = new bool[nX, nY];
                        var uGridSlice = new double[nX, nY];
                        var vGridSlice = new double[nX, nY];

                        // Sample dose at this depth
                        int insideCount = 0;
                        var sliceStructureDoses = new List<double>();

                        for (int ix = 0; ix < nX; ix++)
                        {
                            double paramU = gridMinU + (ix / (double)(nX - 1)) * (gridMaxU - gridMinU);

                            for (int iy = 0; iy < nY; iy++)
                            {
                                double paramV = gridMinV + (iy / (double)(nY - 1)) * (gridMaxV - gridMinV);

                                // Convert to 3D coordinates at current depth
                                var samplePoint = currentPlanePosition + (paramU * uAxis) + (paramV * vAxis);

                                // Store grid coordinates
                                uGridSlice[ix, iy] = paramU;
                                vGridSlice[ix, iy] = paramV;

                                // Check if point is inside structure AT THIS DEPTH
                                bool insideAtThisDepth = false;
                                try
                                {
                                    insideAtThisDepth = structure.IsPointInsideSegment(samplePoint);
                                }
                                catch
                                {
                                    doseSlice[ix, iy] = double.NaN;
                                    structSlice[ix, iy] = false;
                                    continue;
                                }

                                structSlice[ix, iy] = insideAtThisDepth;

                                // Only sample dose if inside structure at this depth
                                if (insideAtThisDepth)
                                {
                                    try
                                    {
                                        var dv = plan.Dose.GetDoseToPoint(samplePoint);
                                        if (dv != null)
                                        {
                                            double doseGy = dv.Dose;
                                            doseSlice[ix, iy] = doseGy;
                                            sliceStructureDoses.Add(doseGy);
                                            insideCount++;
                                        }
                                        else
                                        {
                                            doseSlice[ix, iy] = double.NaN;
                                        }
                                    }
                                    catch
                                    {
                                        doseSlice[ix, iy] = double.NaN;
                                    }
                                }
                                else
                                {
                                    doseSlice[ix, iy] = double.NaN;
                                }
                            }
                        }

                        if (insideCount > 0)
                        {
                            // Store this slice data
                            _doseSlices.Add(doseSlice);
                            _structSlices.Add(structSlice);
                            _uGridSlices.Add(uGridSlice);
                            _vGridSlices.Add(vGridSlice);
                            _sliceDimensions.Add((nX, nY));

                            OutputLog += $"Slice {sliceIdx + 1}: {insideCount} valid structure points, dose range: {sliceStructureDoses.Min():F1}-{sliceStructureDoses.Max():F1} Gy\n";
                        }
                        else
                        {
                            OutputLog += $"Slice {sliceIdx + 1}: No valid dose points, skipping\n";
                        }
                    }

                    if (_doseSlices.Count > 0)
                    {
                        // Find slice closest to isocenter (depth = 0)
                        int isocenterSliceIndex = 0;
                        double minDistanceToIso = double.MaxValue;
                        for (int i = 0; i < _depthValues.Count; i++)
                        {
                            double distanceToIso = Math.Abs(_depthValues[i] - (0 - entryDist)); // Distance from isocenter
                            if (distanceToIso < minDistanceToIso)
                            {
                                minDistanceToIso = distanceToIso;
                                isocenterSliceIndex = i;
                            }
                        }

                        _currentDepthIndex = isocenterSliceIndex;
                        CurrentDepthValue = _depthValues[isocenterSliceIndex];
                        _has2DPlotData = true;

                        OutputLog += $"\nMulti-depth computation complete!\n";
                        OutputLog += $"Total slices: {_doseSlices.Count}\n";
                        OutputLog += $"Starting at slice {isocenterSliceIndex + 1} (closest to isocenter)\n";

                        // Notify UI of property changes
                        RaisePropertyChanged(nameof(MaxDepthIndex));
                        RaisePropertyChanged(nameof(DepthDisplayText));
                    }
                    else
                    {
                        OutputLog += "ERROR: No valid slices computed!\n";
                        return;
                    }
                }
                else if (selectedPvdrMode == "VMAT")
                {
                    // VMAT mode implementation (unchanged for now)
                    RunSphericalPVDRMetric(tumorId, plan);
                    return;
                }
                else
                {
                    OutputLog += $"Unknown PVDR mode: {selectedPvdrMode}\n";
                    return;
                }
            }
            catch (Exception ex)
            {
                OutputLog += $"Error during multi-depth PVDR analysis: {ex.Message}\n";
            }
        }

        // Helper method to find structure bounds at specific depth
        private (double minU, double maxU, double minV, double maxV, int pointCount)
            FindStructureBoundsAtDepth(Structure structure, VVector planePosition, VVector uAxis, VVector vAxis, VVector beamDir)
        {
            double minU = double.MaxValue, maxU = double.MinValue;
            double minV = double.MaxValue, maxV = double.MinValue;
            int pointCount = 0;

            // Sample structure mesh and project to this plane
            var mesh = structure.MeshGeometry;
            if (mesh != null && mesh.Positions.Count > 0)
            {
                foreach (var point3D in mesh.Positions)
                {
                    var pt = new VVector(point3D.X, point3D.Y, point3D.Z);

                    // Check if this mesh point is close to our current depth plane
                    var relative = pt - planePosition;
                    double depthDistance = Math.Abs(relative.x * beamDir.x + relative.y * beamDir.y + relative.z * beamDir.z);

                    // Only consider points within 2mm of current plane
                    if (depthDistance < 2.0)
                    {
                        // Project onto the 2D plane
                        double projU = relative.x * uAxis.x + relative.y * uAxis.y + relative.z * uAxis.z;
                        double projV = relative.x * vAxis.x + relative.y * vAxis.y + relative.z * vAxis.z;

                        minU = Math.Min(minU, projU);
                        maxU = Math.Max(maxU, projU);
                        minV = Math.Min(minV, projV);
                        maxV = Math.Max(maxV, projV);
                        pointCount++;
                    }
                }
            }

            return (minU, maxU, minV, maxV, pointCount);
        }

        // Multi-depth visualization method
        private void Show2DDoseHeatmapOnCanvas(Canvas targetCanvas, int depthIndex)
        {
            targetCanvas.Children.Clear();

            OutputLog += $"=== Show2DDoseHeatmapOnCanvas for depth index {depthIndex} ===\n";

            if (targetCanvas == null || _doseSlices.Count == 0 || depthIndex < 0 || depthIndex >= _doseSlices.Count)
            {
                OutputLog += "ERROR: Invalid canvas or depth index\n";
                return;
            }

            targetCanvas.UpdateLayout();

            // Get current slice data
            var doseGrid = _doseSlices[depthIndex];
            var inStruct = _structSlices[depthIndex];
            var uGrid = _uGridSlices[depthIndex];
            var vGrid = _vGridSlices[depthIndex];
            var (nX, nY) = _sliceDimensions[depthIndex];

            double canvasW = targetCanvas.ActualWidth;
            double canvasH = targetCanvas.ActualHeight;

            if (canvasW <= 0 || canvasH <= 0)
            {
                canvasW = 600;
                canvasH = 400;
            }

            // === CANVAS LAYOUT ===
            double heatmapWidth = canvasW * 0.75;
            double colorbarWidth = canvasW * 0.25;
            double dividerX = heatmapWidth;

            // Layout margins
            double leftMargin = 60; // More space for Y-axis labels
            double rightMargin = 20;
            double topMargin = 60; // More space for title
            double bottomMargin = 80; // More space for X-axis labels and depth info

            double availableHeatmapWidth = heatmapWidth - leftMargin - rightMargin;
            double availableHeatmapHeight = canvasH - topMargin - bottomMargin;

            // Find structure-only dose range for this slice
            var (structureMinDose, structureMaxDose) = GetStructureDoseRangeForSlice(doseGrid, inStruct, nX, nY);

            OutputLog += $"Slice {depthIndex + 1}: {nX}x{nY}, structure dose range: {structureMinDose:F3} to {structureMaxDose:F3}\n";

            if (structureMaxDose - structureMinDose < 1e-6)
            {
                OutputLog += "No valid structure dose data found for this slice!\n";
                var noDataText = new TextBlock
                {
                    Text = "NO STRUCTURE DOSE DATA AT THIS DEPTH",
                    FontSize = 18,
                    Foreground = Brushes.Red,
                    FontWeight = FontWeights.Bold
                };
                Canvas.SetLeft(noDataText, canvasW / 2 - 150);
                Canvas.SetTop(noDataText, canvasH / 2);
                targetCanvas.Children.Add(noDataText);
                return;
            }

            // Find U,V coordinate ranges for this slice
            double minU = double.MaxValue, maxU = double.MinValue;
            double minV = double.MaxValue, maxV = double.MinValue;
            int structureCount = 0;

            for (int i = 0; i < nX; i++)
            {
                for (int j = 0; j < nY; j++)
                {
                    if (inStruct[i, j] && !double.IsNaN(doseGrid[i, j]))
                    {
                        minU = Math.Min(minU, uGrid[i, j]);
                        maxU = Math.Max(maxU, uGrid[i, j]);
                        minV = Math.Min(minV, vGrid[i, j]);
                        maxV = Math.Max(maxV, vGrid[i, j]);
                        structureCount++;
                    }
                }
            }

            if (structureCount == 0)
            {
                OutputLog += "No structure cells found in this slice!\n";
                return;
            }

            // Calculate cell size for visualization
            double uRange = maxU - minU;
            double vRange = maxV - minV;
            double cellW = availableHeatmapWidth / nX;
            double cellH = availableHeatmapHeight / nY;

            // Ensure minimum cell size
            double minCellSize = 3;
            if (cellW < minCellSize || cellH < minCellSize)
            {
                double scale = Math.Min(minCellSize / cellW, minCellSize / cellH);
                cellW *= scale;
                cellH *= scale;
            }

            // Center the heatmap
            double actualHeatmapWidth = cellW * nX;
            double actualHeatmapHeight = cellH * nY;
            double gridLeft = leftMargin + (availableHeatmapWidth - actualHeatmapWidth) / 2;
            double gridTop = topMargin + (availableHeatmapHeight - actualHeatmapHeight) / 2;

            // === DRAW VISUAL DIVIDER ===
            var dividerLine = new Line
            {
                X1 = dividerX,
                Y1 = 0,
                X2 = dividerX,
                Y2 = canvasH,
                Stroke = Brushes.LightGray,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 5, 5 }
            };
            targetCanvas.Children.Add(dividerLine);

            // === ADD TITLE ===
            var title = new TextBlock
            {
                Text = $"Target Dose Distribution - {DepthDisplayText}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Canvas.SetLeft(title, heatmapWidth / 2 - 200);
            Canvas.SetTop(title, 5);
            targetCanvas.Children.Add(title);

            // === DRAW AXES ===
            // Horizontal axis
            var horizontalAxis = new Line
            {
                X1 = gridLeft,
                Y1 = gridTop + actualHeatmapHeight,
                X2 = gridLeft + actualHeatmapWidth,
                Y2 = gridTop + actualHeatmapHeight,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            targetCanvas.Children.Add(horizontalAxis);

            // Vertical axis
            var verticalAxis = new Line
            {
                X1 = gridLeft,
                Y1 = gridTop,
                X2 = gridLeft,
                Y2 = gridTop + actualHeatmapHeight,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            targetCanvas.Children.Add(verticalAxis);

            // === AXIS LABELS ===
            var xAxisLabel = new TextBlock
            {
                Text = "U Distance (mm)",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Canvas.SetLeft(xAxisLabel, gridLeft + actualHeatmapWidth / 2 - 60);
            Canvas.SetTop(xAxisLabel, gridTop + actualHeatmapHeight + 45);
            targetCanvas.Children.Add(xAxisLabel);

            var yAxisLabel = new TextBlock
            {
                Text = "V Distance (mm)",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                LayoutTransform = new RotateTransform(-90)
            };
            Canvas.SetLeft(yAxisLabel, 15);
            Canvas.SetTop(yAxisLabel, gridTop + actualHeatmapHeight / 2);
            targetCanvas.Children.Add(yAxisLabel);

            // === DRAW HEATMAP ===
            int structureCellsDrawn = 0;

            for (int i = 0; i < nX; i++)
            {
                for (int j = 0; j < nY; j++)
                {
                    double dose = doseGrid[i, j];
                    bool inside = inStruct[i, j];

                    // Only draw cells that are inside the structure
                    if (!inside || double.IsNaN(dose))
                        continue;

                    // Map dose to color
                    double norm = (dose - structureMinDose) / (structureMaxDose - structureMinDose);
                    norm = Math.Max(0, Math.Min(1, norm));
                    Color cellColor = GetSmoothDoseColor(norm);

                    var rect = new Rectangle
                    {
                        Width = cellW + 0.5,
                        Height = cellH + 0.5,
                        Fill = new SolidColorBrush(cellColor),
                        Stroke = Brushes.Black,
                        StrokeThickness = 0.2
                    };

                    double px = gridLeft + i * cellW;
                    double py = gridTop + (nY - 1 - j) * cellH; // Flip Y

                    Canvas.SetLeft(rect, px);
                    Canvas.SetTop(rect, py);
                    targetCanvas.Children.Add(rect);
                    structureCellsDrawn++;
                }
            }

            OutputLog += $"Drew {structureCellsDrawn} structure cells\n";

            // === AXIS TICKS AND VALUES ===
            // X-axis ticks (U direction)
            int numXTicks = Math.Max(4, Math.Min(8, (int)(actualHeatmapWidth / 80)));
            for (int i = 0; i <= numXTicks; i++)
            {
                double tickGridX = i * (double)nX / numXTicks;
                double tickU = minU + (maxU - minU) * i / numXTicks;
                double tickX = gridLeft + tickGridX * cellW;

                var tick = new Line
                {
                    X1 = tickX,
                    Y1 = gridTop + actualHeatmapHeight,
                    X2 = tickX,
                    Y2 = gridTop + actualHeatmapHeight + 5,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                var tickLabel = new TextBlock
                {
                    Text = $"{tickU:F0}",
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                Canvas.SetLeft(tickLabel, tickX - 15);
                Canvas.SetTop(tickLabel, gridTop + actualHeatmapHeight + 8);

                targetCanvas.Children.Add(tick);
                targetCanvas.Children.Add(tickLabel);
            }

            // Y-axis ticks (V direction)
            int numYTicks = Math.Max(3, Math.Min(6, (int)(actualHeatmapHeight / 60)));
            for (int i = 0; i <= numYTicks; i++)
            {
                double tickGridY = i * (double)nY / numYTicks;
                double tickV = minV + (maxV - minV) * i / numYTicks;
                double tickY = gridTop + actualHeatmapHeight - tickGridY * cellH;

                var tick = new Line
                {
                    X1 = gridLeft - 5,
                    Y1 = tickY,
                    X2 = gridLeft,
                    Y2 = tickY,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                var tickLabel = new TextBlock
                {
                    Text = $"{tickV:F0}",
                    FontSize = 12
                };

                Canvas.SetLeft(tickLabel, gridLeft - 40);
                Canvas.SetTop(tickLabel, tickY - 8);

                targetCanvas.Children.Add(tick);
                targetCanvas.Children.Add(tickLabel);
            }

            // === DRAW COLORBAR (RIGHT REGION) ===
            double colorbarLeft = dividerX + 20;
            double colorbarTop = topMargin + 20;
            double colorbarBarWidth = 40;
            double colorbarHeight = canvasH - topMargin - 80;

            // Colorbar background
            var colorbarBg = new Rectangle
            {
                Width = colorbarBarWidth + 4,
                Height = colorbarHeight + 4,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            Canvas.SetLeft(colorbarBg, colorbarLeft - 2);
            Canvas.SetTop(colorbarBg, colorbarTop - 2);
            targetCanvas.Children.Add(colorbarBg);

            // Colorbar gradient
            int colorSteps = 50;
            double stepHeight = colorbarHeight / colorSteps;

            for (int s = 0; s < colorSteps; s++)
            {
                double norm = (double)s / (colorSteps - 1);
                Color barColor = GetSmoothDoseColor(norm);

                var barRect = new Rectangle
                {
                    Width = colorbarBarWidth,
                    Height = stepHeight + 1,
                    Fill = new SolidColorBrush(barColor),
                    Stroke = Brushes.Black,
                    StrokeThickness = 0.3
                };

                Canvas.SetLeft(barRect, colorbarLeft);
                Canvas.SetTop(barRect, colorbarTop + (colorSteps - 1 - s) * stepHeight);
                targetCanvas.Children.Add(barRect);
            }

            // === COLORBAR LABELS ===
            double labelX = colorbarLeft + colorbarBarWidth + 10;

            var maxLabel = new TextBlock
            {
                Text = $"{structureMaxDose:F1}",
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(maxLabel, labelX);
            Canvas.SetTop(maxLabel, colorbarTop - 5);
            targetCanvas.Children.Add(maxLabel);

            var midLabel = new TextBlock
            {
                Text = $"{(structureMinDose + structureMaxDose) / 2:F1}",
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(midLabel, labelX);
            Canvas.SetTop(midLabel, colorbarTop + colorbarHeight / 2 - 10);
            targetCanvas.Children.Add(midLabel);

            var minLabel = new TextBlock
            {
                Text = $"{structureMinDose:F1}",
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(minLabel, labelX);
            Canvas.SetTop(minLabel, colorbarTop + colorbarHeight - 15);
            targetCanvas.Children.Add(minLabel);

            // Unit label
            var unitLabel = new TextBlock
            {
                Text = "Dose (Gy)",
                FontSize = 16,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(unitLabel, colorbarLeft);
            Canvas.SetTop(unitLabel, colorbarTop - 35);
            targetCanvas.Children.Add(unitLabel);

            OutputLog += "=== Multi-depth visualization complete ===\n";
        }

        // Helper method for slice-specific dose range
        private (double minDose, double maxDose) GetStructureDoseRangeForSlice(double[,] doseGrid, bool[,] inStruct, int nX, int nY)
        {
            double minDose = double.MaxValue;
            double maxDose = double.MinValue;
            int validCount = 0;

            for (int i = 0; i < nX; i++)
            {
                for (int j = 0; j < nY; j++)
                {
                    if (inStruct[i, j] && !double.IsNaN(doseGrid[i, j]))
                    {
                        double dose = doseGrid[i, j];
                        minDose = Math.Min(minDose, dose);
                        maxDose = Math.Max(maxDose, dose);
                        validCount++;
                    }
                }
            }

            return validCount == 0 ? (0, 1) : (minDose, maxDose);
        }

        // Enhanced color mapping method
        private Color GetSmoothDoseColor(double norm)
        {
            norm = Math.Max(0, Math.Min(1, norm));

            // Clinical color scheme: Blue -> Cyan -> Green -> Yellow -> Red
            if (norm < 0.2)
            {
                double t = norm / 0.2;
                return Color.FromRgb(0, 0, (byte)(100 + t * 155));
            }
            else if (norm < 0.4)
            {
                double t = (norm - 0.2) / 0.2;
                return Color.FromRgb(0, (byte)(t * 255), 255);
            }
            else if (norm < 0.6)
            {
                double t = (norm - 0.4) / 0.2;
                return Color.FromRgb(0, 255, (byte)((1 - t) * 255));
            }
            else if (norm < 0.8)
            {
                double t = (norm - 0.6) / 0.2;
                return Color.FromRgb((byte)(t * 255), 255, 0);
            }
            else
            {
                double t = (norm - 0.8) / 0.2;
                return Color.FromRgb(255, (byte)((1 - t) * 255), 0);
            }
        }

        // Override the old single-depth method call for backward compatibility
        private void Show2DDoseHeatmapOnCanvas(Canvas targetCanvas, double[,] doseGrid, bool[,] inStruct = null)
        {
            // For backward compatibility, show the current depth if we have multi-depth data
            if (_has2DPlotData && _doseSlices.Count > 0)
            {
                Show2DDoseHeatmapOnCanvas(targetCanvas, CurrentDepthIndex);
            }
            else
            {
                // Fallback to error message
                OutputLog += "No multi-depth data available for visualization.\n";
            }
        }

        // Additional helper methods for VMAT mode (existing implementation)
        public void RunSphericalPVDRMetric(string tumorId, PlanSetup plan)
        {
            OutputLog += "\n===== Starting Spherical Shell PVDR Evaluation =====\n";
            var beam = plan.Beams.FirstOrDefault(b => b.Id == SelectedBeamId);
            if (beam == null)
            {
                OutputLog += "Selected beam not found.\n";
                return;
            }

            var structure = plan.StructureSet.Structures.FirstOrDefault(s => s.Id == tumorId);
            if (structure == null)
            {
                OutputLog += $"Structure '{tumorId}' not found.\n";
                return;
            }

            var shellOrigin = structure.CenterPoint;
            OutputLog += $"Target centroid (shell origin): ({shellOrigin.x:F1}, {shellOrigin.y:F1}, {shellOrigin.z:F1}) mm\n";
            OutputLog += "\n===== VMAT analysis placeholder complete =====\n";
        }

        public void update3DMetrics(string tumorName, string ptvAllName, PlanSetup plan)
        {
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
            }
            catch (Exception ex)
            {
                OutputLog += $"Error calculating D95: {ex.Message}\n";
                AllMetrics.Add(new MetricData
                {
                    metric = "Dose Covering 95% of Target (D95) (Gy)",
                    value = "Error - Unable to calculate"
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

        public static VVector Cross(VVector a, VVector b)
        {
            return new VVector(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x
            );
        }
    }
}