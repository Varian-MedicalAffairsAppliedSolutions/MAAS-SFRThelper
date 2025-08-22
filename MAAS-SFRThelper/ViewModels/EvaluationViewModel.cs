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

        // Beam selection with multi-beam support
        private string _selectedBeamId;
        public string SelectedBeamId
        {
            get { return _selectedBeamId; }
            set
            {
                if (SetProperty(ref _selectedBeamId, value))
                {
                    // Handle beam selection change
                    OnBeamSelectionChanged();
                }
            }
        }

        public ObservableCollection<string> TreatmentBeams { get; set; }

        // Cache All Beams checkbox property
        private bool _cacheAllBeams = false;
        public bool CacheAllBeams
        {
            get { return _cacheAllBeams; }
            set { SetProperty(ref _cacheAllBeams, value); }
        }

        // 1D CAX Multi-beam caching
        private Dictionary<string, List<double>> _beam1DDistancesCache = new Dictionary<string, List<double>>();
        private Dictionary<string, List<double>> _beam1DDoseValuesCache = new Dictionary<string, List<double>>();
        private Dictionary<string, List<bool>> _beam1DInsideFlagsCache = new Dictionary<string, List<bool>>();
        private Dictionary<string, double> _beam1DEntryDistCache = new Dictionary<string, double>();
        private Dictionary<string, double> _beam1DExitDistCache = new Dictionary<string, double>();
        private bool _has1DMultiBeamData = false;

        // 2D Multi-beam caching fields
        private Dictionary<string, List<double[,]>> _beamDoseSlicesCache = new Dictionary<string, List<double[,]>>();
        private Dictionary<string, List<bool[,]>> _beamStructSlicesCache = new Dictionary<string, List<bool[,]>>();
        private Dictionary<string, List<double[,]>> _beamUGridSlicesCache = new Dictionary<string, List<double[,]>>();
        private Dictionary<string, List<double[,]>> _beamVGridSlicesCache = new Dictionary<string, List<double[,]>>();
        private Dictionary<string, List<double>> _beamDepthValuesCache = new Dictionary<string, List<double>>();
        private Dictionary<string, List<(int nX, int nY)>> _beamSliceDimensionsCache = new Dictionary<string, List<(int, int)>>();
        private bool _hasMultiBeamData = false;
        private List<string> _computedBeams = new List<string>();

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
            set
            {
                if (SetProperty(ref _is3dEvaluationSelected, value))
                {
                    // Notify UI that selection relevance has changed
                    RaisePropertyChanged(nameof(IsBeamSelectionRelevant));
                    RaisePropertyChanged(nameof(IsEvaluationModeRelevant));

                    // User feedback
                    if (value)
                    {
                        OutputLog += "3D Dose Metrics mode selected - analyzing total dose\n";
                    }
                }
            }
        }

        //3D interpolation
        private bool _is3dInterpolationSelected;
        public bool Is3DInterpolationSelected
        {
            get { return _is3dInterpolationSelected; }
            set
            {
                if (SetProperty(ref _is3dInterpolationSelected, value))
                {
                    // Notify UI that selection relevance has changed
                    RaisePropertyChanged(nameof(IsBeamSelectionRelevant));
                    RaisePropertyChanged(nameof(IsEvaluationModeRelevant));

                    // User feedback
                    if (value)
                    {
                        OutputLog += "3D P/V Analysis mode selected - analyzing total dose\n";
                    }
                }
            }
        }

        public bool IsBeamSelectionRelevant
        {
            get
            {
                // Beam selection only matters for 1D and 2D modes
                return !Is3DEvaluationSelected && !Is3DInterpolationSelected;
            }
        }

        public bool IsEvaluationModeRelevant
        {
            get
            {
                // Fixed Beam vs VMAT only matters for 1D and 2D
                return !Is3DEvaluationSelected && !Is3DInterpolationSelected;
            }
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


        private ObservableCollection<DepthLabelItem> _depthLabels = new ObservableCollection<DepthLabelItem>();
        public ObservableCollection<DepthLabelItem> DepthLabels
        {
            get { return _depthLabels; }
            set { SetProperty(ref _depthLabels, value); }
        }

        public class DepthLabelItem : BindableBase
        {
            private string _text;
            public string Text
            {
                get { return _text; }
                set { SetProperty(ref _text, value); }
            }

            private double _yPosition;
            public double YPosition
            {
                get { return _yPosition; }
                set { SetProperty(ref _yPosition, value); }
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

        // Internal fields for the dose sampling (current beam's data)
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
        // Multi-slice storage with adaptive grids (current beam's data)
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

        //public string DepthDisplayText
        //{
        //    get
        //    {
        //        if (_depthValues.Count == 0) return "No depth data";
        //        return $"Depth: {CurrentDepthValue:F1}mm from entry (Slice {CurrentDepthIndex + 1} of {_depthValues.Count})";
        //    }
        //}

        public string DepthDisplayText
        {
            get
            {
                if (_depthValues.Count == 0) return "No data";
                return $"{CurrentDepthValue:F1}mm\n{CurrentDepthIndex + 1}/{_depthValues.Count}";
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

        // Helper class for beam dose data
        private class BeamDoseData
        {
            public List<double[,]> DoseSlices { get; set; } = new List<double[,]>();
            public List<bool[,]> StructSlices { get; set; } = new List<bool[,]>();
            public List<double[,]> UGridSlices { get; set; } = new List<double[,]>();
            public List<double[,]> VGridSlices { get; set; } = new List<double[,]>();
            public List<double> DepthValues { get; set; } = new List<double>();
            public List<(int nX, int nY)> SliceDimensions { get; set; } = new List<(int, int)>();
            public bool HasData => DoseSlices.Count > 0;
        }

        // Helper class for 1D beam dose data
        private class Beam1DDoseData
        {
            public List<double> Distances { get; set; } = new List<double>();
            public List<double> DoseValues { get; set; } = new List<double>();
            public List<bool> InsideFlags { get; set; } = new List<bool>();
            public double EntryDist { get; set; } = double.NaN;
            public double ExitDist { get; set; } = double.NaN;
            public bool HasData => Distances.Count > 0;
        }

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
                BindingOperations.EnableCollectionSynchronization(DepthLabels, new object());

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

        // Handle beam selection change
        private void OnBeamSelectionChanged()
        {
            if (string.IsNullOrEmpty(SelectedBeamId))
                return;

            // For 1D CAX
            if (Is1DCAXSelected && _has1DMultiBeamData)
            {
                if (_beam1DDistancesCache.ContainsKey(SelectedBeamId))
                {
                    // Load cached 1D data
                    Load1DCachedBeamData(SelectedBeamId);
                    OutputLog += $"Loaded cached 1D data for: {SelectedBeamId}\n";

                    // Update visualization if in plot view
                    if (bPlotVis)
                    {
                        RefreshPlot();
                    }
                }
                else
                {
                    OutputLog += $"No cached 1D data for {SelectedBeamId}. Click Compute to calculate.\n";
                }
            }

            // For 2D Planar
            if (Is2DPlanarSelected && _hasMultiBeamData)
            {
                if (_beamDoseSlicesCache.ContainsKey(SelectedBeamId))
                {
                    // Load cached 2D data
                    LoadCached2DBeamData(SelectedBeamId);
                    OutputLog += $"Loaded cached 2D data for: {SelectedBeamId}\n";

                    // Update visualization if in plot view
                    if (bPlotVis)
                    {
                        RefreshPlot();
                    }
                }
                else
                {
                    OutputLog += $"No cached 2D data for {SelectedBeamId}. Click Compute to calculate.\n";
                }
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

        // Clear 1D beam cache
        private void Clear1DBeamCache()
        {
            _beam1DDistancesCache.Clear();
            _beam1DDoseValuesCache.Clear();
            _beam1DInsideFlagsCache.Clear();
            _beam1DEntryDistCache.Clear();
            _beam1DExitDistCache.Clear();
            _has1DMultiBeamData = false;
        }

        // Clear 2D beam cache
        private void Clear2DBeamCache()
        {
            _beamDoseSlicesCache.Clear();
            _beamStructSlicesCache.Clear();
            _beamUGridSlicesCache.Clear();
            _beamVGridSlicesCache.Clear();
            _beamDepthValuesCache.Clear();
            _beamSliceDimensionsCache.Clear();
            _hasMultiBeamData = false;
            _computedBeams.Clear();
        }

        // Load cached 1D beam data for display
        private void Load1DCachedBeamData(string beamId)
        {
            if (!_beam1DDistancesCache.ContainsKey(beamId))
            {
                return;
            }

            // Load the cached data for the selected beam
            _distances = new List<double>(_beam1DDistancesCache[beamId]);
            _doseValues = new List<double>(_beam1DDoseValuesCache[beamId]);
            _insideTumorFlags = new List<bool>(_beam1DInsideFlagsCache[beamId]);
            _entryDist = _beam1DEntryDistCache[beamId];
            _exitDist = _beam1DExitDistCache[beamId];

            _hasPlotData = true;
        }

        // Load cached 2D beam data for display
        private void LoadCached2DBeamData(string beamId)
        {
            if (!_beamDoseSlicesCache.ContainsKey(beamId))
            {
                return;
            }

            // Load the cached data for the selected beam
            _doseSlices = new List<double[,]>(_beamDoseSlicesCache[beamId]);
            _structSlices = new List<bool[,]>(_beamStructSlicesCache[beamId]);
            _uGridSlices = new List<double[,]>(_beamUGridSlicesCache[beamId]);
            _vGridSlices = new List<double[,]>(_beamVGridSlicesCache[beamId]);
            _depthValues = new List<double>(_beamDepthValuesCache[beamId]);
            _sliceDimensions = new List<(int, int)>(_beamSliceDimensionsCache[beamId]);
            UpdateDepthLabels();

            // Reset to first depth
            _currentDepthIndex = 0;
            if (_depthValues.Count > 0)
            {
                CurrentDepthValue = _depthValues[0];
            }

            _has2DPlotData = true;

            // Update UI
            RaisePropertyChanged(nameof(MaxDepthIndex));
            RaisePropertyChanged(nameof(DepthDisplayText));
            RaisePropertyChanged(nameof(CanGoToPreviousDepth));
            RaisePropertyChanged(nameof(CanGoToNextDepth));
        }

        private void UpdateDepthLabels()
        {
            DepthLabels.Clear();

            if (_depthValues.Count == 0) return;

            int totalSlices = _depthValues.Count;
            double sliderHeight = 200; // Match the slider height

            // Determine how many labels to show (max 5-7 for readability)
            int labelStep = 1;
            if (totalSlices > 7)
                labelStep = (int)Math.Ceiling(totalSlices / 6.0);

            for (int i = 0; i < totalSlices; i += labelStep)
            {
                // Calculate Y position (0 at top since IsDirectionReversed="True")
                double yPos = (i / (double)(totalSlices - 1)) * sliderHeight;

                DepthLabels.Add(new DepthLabelItem
                {
                    Text = $"{_depthValues[i]:F0}",
                    YPosition = yPos
                });
            }

            // Always include the last depth if not already added
            if ((totalSlices - 1) % labelStep != 0)
            {
                DepthLabels.Add(new DepthLabelItem
                {
                    Text = $"{_depthValues[totalSlices - 1]:F0}",
                    YPosition = sliderHeight
                });
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
                            // Add "All Beams" option first
                            TreatmentBeams.Add("All Beams");

                            foreach (var beam in _plan.Beams.Where(b => !b.IsSetupField))
                            {
                                TreatmentBeams.Add(beam.Id);
                            }

                            OutputLog += $"Found {TreatmentBeams.Count - 1} treatment beams\n";

                            // Set default selection to "All Beams"
                            if (TreatmentBeams.Any())
                            {
                                SelectedBeamId = "All Beams";
                                OutputLog += "Selected: All Beams (default)\n";
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

        //private void ExecuteComputeDose()
        //{
        //    try
        //    {
        //        OutputLog += "Starting dose computation...\n";

        //        // Copy selected IDs to local variables to avoid potential thread issues
        //        string selectedBeamId = SelectedBeamId;
        //        string selectedTumorId = SelectedTumorId;
        //        string ptvAllName = SelectedTumorId;
        //        string selectedPvdrMode = SelectedPvdrMode;

        //        if (string.IsNullOrEmpty(selectedBeamId))
        //        {
        //            OutputLog += "No beam selected. Please select a beam.\n";
        //            return;
        //        }

        //        if (string.IsNullOrEmpty(selectedTumorId))
        //        {
        //            OutputLog += "No structure selected. Please select a structure.\n";
        //            return;
        //        }

        //        _esapiWorker.RunWithWait(context =>
        //        {
        //            try
        //            {
        //                var plan = _plan;
        //                if (plan == null)
        //                {
        //                    OutputLog += "No plan available. Cannot compute dose.\n";
        //                    return;
        //                }

        //                if (plan.Dose == null)
        //                {
        //                    OutputLog += "No 3D dose is calculated for this plan. Please calculate dose first.\n";
        //                    return;
        //                }

        //                if (Is1DCAXSelected)
        //                {
        //                    OutputLog += "Running 1D CAX computation...\n";
        //                    Run1DPVDRMetric(selectedTumorId, plan);
        //                    OutputLog += "1D CAX Dosimetrics complete\n";
        //                    return;
        //                }

        //                if (Is2DPlanarSelected)
        //                {
        //                    OutputLog += "Running 2D Planar computation...\n";
        //                    Run2DPVDRMetric(selectedTumorId, plan);
        //                    OutputLog += "2D Planar Dosimetrics complete\n";
        //                    return;
        //                }

        //                bool execute3DDose = Is3DEvaluationSelected;

        //                if (execute3DDose)
        //                {
        //                    update3DMetrics(selectedTumorId, ptvAllName, plan);
        //                    OutputLog += "3D Dosimetrics complete\n";
        //                    return;
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                OutputLog += $"Error during dose computation: {ex.Message}\n";
        //                if (ex.InnerException != null)
        //                {
        //                    OutputLog += $"Inner Exception: {ex.InnerException.Message}\n";
        //                }
        //            }
        //        });

        //        // Update the commands that depend on data availability
        //        SaveCsvCommand.RaiseCanExecuteChanged();
        //        ShowPlotCommand.RaiseCanExecuteChanged();
        //        RefreshPlotCommand?.RaiseCanExecuteChanged();
        //    }
        //    catch (Exception ex)
        //    {
        //        OutputLog += $"Critical error in ExecuteComputeDose: {ex.Message}\n";
        //        MessageBox.Show($"Error computing dose: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}
        private void ExecuteComputeDose()
        {
            try
            {
                OutputLog += "Starting dose computation...\n";

                // Copy selected IDs to local variables to avoid potential thread issues
                string selectedBeamId = SelectedBeamId;
                string selectedTumorId = SelectedTumorId;
                string ptvAllName = SelectedTumorId;
                string selectedPvdrMode = SelectedPvdrMode;

                // Always need a structure
                if (string.IsNullOrEmpty(selectedTumorId))
                {
                    OutputLog += "No structure selected. Please select a structure.\n";
                    return;
                }

                // Only need beam selection for 1D and 2D modes
                if (!Is3DEvaluationSelected && !Is3DInterpolationSelected)
                {
                    if (string.IsNullOrEmpty(selectedBeamId))
                    {
                        OutputLog += "No beam selected. Please select a beam.\n";
                        return;
                    }
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
                            OutputLog += "Running 1D CAX computation...\n";
                            Run1DPVDRMetric(selectedTumorId, plan);
                            OutputLog += "1D CAX Dosimetrics complete\n";
                            return;
                        }

                        if (Is2DPlanarSelected)
                        {
                            OutputLog += "Running 2D Planar computation...\n";
                            Run2DPVDRMetric(selectedTumorId, plan);
                            OutputLog += "2D Planar Dosimetrics complete\n";
                            return;
                        }

                        if (Is3DEvaluationSelected)
                        {
                            OutputLog += "Running 3D Dose Metrics...\n";
                            update3DMetrics(selectedTumorId, ptvAllName, plan);
                            OutputLog += "3D Dosimetrics complete\n";
                            return;
                        }

                        // *** NEW: Handle 3D P/V Interpolation mode ***
                        if (Is3DInterpolationSelected)
                        {
                            OutputLog += "Running 3D P/V Analysis...\n";
                            Run3DPVAnalysis(selectedTumorId, plan);
                            OutputLog += "3D P/V Analysis complete\n";
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

                    // Also show which beam is currently displayed
                    OutputLog += $"Currently showing: {SelectedBeamId}\n";
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

        // Modified 1D PVDR Metric with compute-on-demand and caching
        public void Run1DPVDRMetric(string tumorId, PlanSetup plan)
        {
            try
            {
                string selectedBeamId = SelectedBeamId;

                // Check if we already have this beam cached
                if (_beam1DDistancesCache.ContainsKey(selectedBeamId))
                {
                    OutputLog += $"Loading cached 1D data for beam: {selectedBeamId}\n";
                    Load1DCachedBeamData(selectedBeamId);
                    return;
                }

                OutputLog += $"Computing 1D CAX for beam: {selectedBeamId}\n";

                // Compute the selected beam
                Beam1DDoseData beamData = null;

                if (selectedBeamId == "All Beams")
                {
                    // Use standardized vertical beam for "All Beams"
                    beamData = Compute1DAllBeamsStandardized(tumorId, plan);
                }
                else
                {
                    // Compute individual beam
                    var beam = plan.Beams.FirstOrDefault(b => b.Id == selectedBeamId);
                    if (beam != null)
                    {
                        // Check if individual beam dose is available
                        Dose doseMatrix = (Dose)beam.Dose ?? (Dose)plan.Dose;
                        beamData = Compute1DBeamDose(beam, tumorId, doseMatrix, plan);
                    }
                }

                if (beamData != null && beamData.HasData)
                {
                    // Cache the data
                    _beam1DDistancesCache[selectedBeamId] = beamData.Distances;
                    _beam1DDoseValuesCache[selectedBeamId] = beamData.DoseValues;
                    _beam1DInsideFlagsCache[selectedBeamId] = beamData.InsideFlags;
                    _beam1DEntryDistCache[selectedBeamId] = beamData.EntryDist;
                    _beam1DExitDistCache[selectedBeamId] = beamData.ExitDist;

                    // Load into current display
                    _distances = beamData.Distances;
                    _doseValues = beamData.DoseValues;
                    _insideTumorFlags = beamData.InsideFlags;
                    _entryDist = beamData.EntryDist;
                    _exitDist = beamData.ExitDist;

                    _hasPlotData = true;
                    _has1DMultiBeamData = true;

                    OutputLog += $"Beam {selectedBeamId} computed and cached successfully\n";

                    // Print statistics
                    var tumorDoses = new List<double>();
                    for (int i = 0; i < _doseValues.Count; i++)
                    {
                        if (_insideTumorFlags[i]) tumorDoses.Add(_doseValues[i]);
                    }

                    if (tumorDoses.Count > 0)
                    {
                        OutputLog += "===== Computation Complete =====\n";
                        OutputLog += $"Beam: {selectedBeamId}\n";
                        OutputLog += $"Entry Dist: {_entryDist:F1} mm, Exit Dist: {_exitDist:F1} mm\n";
                        OutputLog += $"Tumor length along axis: {_exitDist - _entryDist:F1} mm\n";
                        OutputLog += $"Max Dose: {tumorDoses.Max():F3} Gy\n";
                        OutputLog += $"Min Dose: {tumorDoses.Min():F3} Gy\n";
                        OutputLog += $"Avg Dose: {tumorDoses.Average():F3} Gy\n";
                        OutputLog += $"Total samples: {_distances.Count}\n";
                        OutputLog += "================================\n";
                    }
                }
                else
                {
                    OutputLog += $"Failed to compute dose for beam: {selectedBeamId}\n";
                }

                // If "Cache All Beams" is checked, compute all other beams
                if (CacheAllBeams)
                {
                    OutputLog += "\nCaching all beams...\n";
                    var allBeams = plan.Beams.Where(b => !b.IsSetupField).ToList();
                    int beamCount = 0;

                    foreach (var beam in allBeams)
                    {
                        if (!_beam1DDistancesCache.ContainsKey(beam.Id))
                        {
                            beamCount++;
                            OutputLog += $"Processing beam {beamCount}/{allBeams.Count}: {beam.Id}...\n";

                            Dose doseMatrix = (Dose)beam.Dose ?? (Dose)plan.Dose;
                            var cachedData = Compute1DBeamDose(beam, tumorId, doseMatrix, plan);

                            if (cachedData != null && cachedData.HasData)
                            {
                                _beam1DDistancesCache[beam.Id] = cachedData.Distances;
                                _beam1DDoseValuesCache[beam.Id] = cachedData.DoseValues;
                                _beam1DInsideFlagsCache[beam.Id] = cachedData.InsideFlags;
                                _beam1DEntryDistCache[beam.Id] = cachedData.EntryDist;
                                _beam1DExitDistCache[beam.Id] = cachedData.ExitDist;
                            }
                        }
                    }

                    // Also cache "All Beams" if not already done
                    if (!_beam1DDistancesCache.ContainsKey("All Beams"))
                    {
                        OutputLog += "Processing All Beams (standardized)...\n";
                        var allBeamsData = Compute1DAllBeamsStandardized(tumorId, plan);
                        if (allBeamsData != null && allBeamsData.HasData)
                        {
                            _beam1DDistancesCache["All Beams"] = allBeamsData.Distances;
                            _beam1DDoseValuesCache["All Beams"] = allBeamsData.DoseValues;
                            _beam1DInsideFlagsCache["All Beams"] = allBeamsData.InsideFlags;
                            _beam1DEntryDistCache["All Beams"] = allBeamsData.EntryDist;
                            _beam1DExitDistCache["All Beams"] = allBeamsData.ExitDist;
                        }
                    }

                    OutputLog += "All beams cached successfully!\n";
                }
            }
            catch (Exception ex)
            {
                OutputLog += $"Error in Run1DPVDRMetric: {ex.Message}\n";
            }
        }

        // Helper method to compute 1D dose for a specific beam
        private Beam1DDoseData Compute1DBeamDose(Beam beam, string tumorId, Dose doseMatrix, PlanSetup plan)
        {
            var result = new Beam1DDoseData();

            try
            {
                var tumor = _structureSet.Structures.FirstOrDefault(s => s.Id == tumorId);
                if (tumor == null)
                {
                    OutputLog += $"Could not find structure with ID '{tumorId}'.\n";
                    return result;
                }

                // Calculate beam direction
                var isocenter = beam.IsocenterPosition;
                var cp0 = beam.ControlPoints.First();

                double gantryRad = cp0.GantryAngle * Math.PI / 180.0;
                double couchRad = cp0.PatientSupportAngle * Math.PI / 180.0;

                VVector dVec = new VVector(Math.Sin(gantryRad), -Math.Cos(gantryRad), 0);

                // Apply couch rotation if needed
                if (Math.Abs(cp0.PatientSupportAngle) > 0.1)
                {
                    double x = dVec.x, z = dVec.z;
                    dVec.x = x * Math.Cos(couchRad) + z * Math.Sin(couchRad);
                    dVec.z = -x * Math.Sin(couchRad) + z * Math.Cos(couchRad);
                }

                double length = Math.Sqrt(dVec.x * dVec.x + dVec.y * dVec.y + dVec.z * dVec.z);
                VVector direction = new VVector(dVec.x / length, dVec.y / length, dVec.z / length);

                // Find entry/exit
                double searchStartDist = -300.0;
                double searchEndDist = 300.0;
                double stepSize = 2.0;
                bool insideTumor = false;

                for (double dist = searchStartDist; dist <= searchEndDist; dist += stepSize)
                {
                    var point = isocenter + dist * direction;
                    bool pointInTumor = tumor.IsPointInsideSegment(point);

                    if (!insideTumor && pointInTumor)
                    {
                        result.EntryDist = dist;
                        insideTumor = true;
                    }
                    else if (insideTumor && !pointInTumor)
                    {
                        result.ExitDist = dist;
                        break;
                    }
                }

                if (double.IsNaN(result.EntryDist) || double.IsNaN(result.ExitDist))
                {
                    OutputLog += $"Beam {beam.Id} does not intersect the tumor structure.\n";
                    return result;
                }

                // Sample the dose
                double margin = 5.0;
                double startDist = result.EntryDist - margin;
                double endDist = result.ExitDist + margin;
                stepSize = 1.0;

                for (double dist = startDist; dist <= endDist; dist += stepSize)
                {
                    var samplePoint = isocenter + dist * direction;
                    bool isInside = tumor.IsPointInsideSegment(samplePoint);

                    DoseValue doseValue = doseMatrix.GetDoseToPoint(samplePoint);
                    if (doseValue != null)
                    {
                        result.Distances.Add(dist);
                        result.DoseValues.Add(doseValue.Dose);
                        result.InsideFlags.Add(isInside);
                    }
                }
            }
            catch (Exception ex)
            {
                OutputLog += $"Error computing 1D dose for beam {beam.Id}: {ex.Message}\n";
            }

            return result;
        }

        // Helper method to compute standardized "All Beams" for 1D
        private Beam1DDoseData Compute1DAllBeamsStandardized(string tumorId, PlanSetup plan)
        {
            var result = new Beam1DDoseData();

            try
            {
                var tumor = _structureSet.Structures.FirstOrDefault(s => s.Id == tumorId);
                if (tumor == null)
                {
                    OutputLog += $"Could not find structure with ID '{tumorId}'.\n";
                    return result;
                }

                // Use plan isocenter
                var isocenter = plan.Beams.First().IsocenterPosition;

                // Standardized vertical beam direction (0, -1, 0) - pointing inferior
                VVector direction = new VVector(0, -1, 0);

                OutputLog += $"Using standardized vertical beam (0°, 0°, 0°) at isocenter ({isocenter.x:F1}, {isocenter.y:F1}, {isocenter.z:F1})\n";

                // Find entry/exit
                double searchStartDist = -300.0;
                double searchEndDist = 300.0;
                double stepSize = 2.0;
                bool insideTumor = false;

                for (double dist = searchStartDist; dist <= searchEndDist; dist += stepSize)
                {
                    var point = isocenter + dist * direction;
                    bool pointInTumor = tumor.IsPointInsideSegment(point);

                    if (!insideTumor && pointInTumor)
                    {
                        result.EntryDist = dist;
                        insideTumor = true;
                    }
                    else if (insideTumor && !pointInTumor)
                    {
                        result.ExitDist = dist;
                        break;
                    }
                }

                if (double.IsNaN(result.EntryDist) || double.IsNaN(result.ExitDist))
                {
                    OutputLog += "Standardized beam does not intersect the tumor structure.\n";
                    return result;
                }

                // Sample the total plan dose
                double margin = 5.0;
                double startDist = result.EntryDist - margin;
                double endDist = result.ExitDist + margin;
                stepSize = 1.0;

                for (double dist = startDist; dist <= endDist; dist += stepSize)
                {
                    var samplePoint = isocenter + dist * direction;
                    bool isInside = tumor.IsPointInsideSegment(samplePoint);

                    DoseValue doseValue = plan.Dose.GetDoseToPoint(samplePoint);
                    if (doseValue != null)
                    {
                        result.Distances.Add(dist);
                        result.DoseValues.Add(doseValue.Dose);
                        result.InsideFlags.Add(isInside);
                    }
                }
            }
            catch (Exception ex)
            {
                OutputLog += $"Error computing standardized All Beams dose: {ex.Message}\n";
            }

            return result;
        }

        // Modified 2D PVDR Metric with compute-on-demand
        public void Run2DPVDRMetric(string tumorId, PlanSetup plan)
        {
            try
            {
                OutputLog += "\n===== 2D PVDR Analysis =====\n";
                string selectedPvdrMode = SelectedPvdrMode;

                if (selectedPvdrMode == "Fixed Beam")
                {
                    string selectedBeamId = SelectedBeamId;

                    // Check if we already have this beam cached
                    if (_beamDoseSlicesCache.ContainsKey(selectedBeamId))
                    {
                        OutputLog += $"Loading cached 2D data for beam: {selectedBeamId}\n";
                        LoadCached2DBeamData(selectedBeamId);
                        return;
                    }

                    OutputLog += $"Computing 2D planar dose for beam: {selectedBeamId}\n";

                    // Get the structure
                    var structure = plan.StructureSet.Structures.FirstOrDefault(s => s.Id == tumorId);
                    if (structure == null)
                    {
                        OutputLog += $"Structure '{tumorId}' not found.\n";
                        return;
                    }

                    // Compute the selected beam
                    BeamDoseData beamData = null;

                    if (selectedBeamId == "All Beams")
                    {
                        // Use standardized vertical beam for "All Beams"
                        beamData = Compute2DAllBeamsStandardized(structure, plan);
                    }
                    else
                    {
                        // Compute individual beam
                        var beam = plan.Beams.FirstOrDefault(b => b.Id == selectedBeamId);
                        if (beam != null)
                        {
                            Dose doseMatrix = (Dose)beam.Dose ?? (Dose)plan.Dose;
                            beamData = ComputeBeamDose(beam, structure, doseMatrix, plan);
                        }
                    }

                    if (beamData != null && beamData.HasData)
                    {
                        // Cache the data
                        _beamDoseSlicesCache[selectedBeamId] = beamData.DoseSlices;
                        _beamStructSlicesCache[selectedBeamId] = beamData.StructSlices;
                        _beamUGridSlicesCache[selectedBeamId] = beamData.UGridSlices;
                        _beamVGridSlicesCache[selectedBeamId] = beamData.VGridSlices;
                        _beamDepthValuesCache[selectedBeamId] = beamData.DepthValues;
                        _beamSliceDimensionsCache[selectedBeamId] = beamData.SliceDimensions;

                        // Load into current display
                        LoadCached2DBeamData(selectedBeamId);
                        _hasMultiBeamData = true;

                        OutputLog += $"Beam {selectedBeamId} computed and cached successfully - {beamData.DoseSlices.Count} slices\n";
                    }
                    else
                    {
                        OutputLog += $"Failed to compute dose for beam: {selectedBeamId}\n";
                    }

                    // If "Cache All Beams" is checked, compute all other beams
                    if (CacheAllBeams)
                    {
                        OutputLog += "\nCaching all beams...\n";
                        var allBeams = plan.Beams.Where(b => !b.IsSetupField).ToList();
                        int beamCount = 0;

                        foreach (var beam in allBeams)
                        {
                            if (!_beamDoseSlicesCache.ContainsKey(beam.Id))
                            {
                                beamCount++;
                                OutputLog += $"Processing beam {beamCount}/{allBeams.Count}: {beam.Id}...\n";

                                Dose doseMatrix = (Dose)beam.Dose ?? (Dose)plan.Dose;
                                var cachedData = ComputeBeamDose(beam, structure, doseMatrix, plan);

                                if (cachedData != null && cachedData.HasData)
                                {
                                    _beamDoseSlicesCache[beam.Id] = cachedData.DoseSlices;
                                    _beamStructSlicesCache[beam.Id] = cachedData.StructSlices;
                                    _beamUGridSlicesCache[beam.Id] = cachedData.UGridSlices;
                                    _beamVGridSlicesCache[beam.Id] = cachedData.VGridSlices;
                                    _beamDepthValuesCache[beam.Id] = cachedData.DepthValues;
                                    _beamSliceDimensionsCache[beam.Id] = cachedData.SliceDimensions;
                                }
                            }
                        }

                        // Also cache "All Beams" if not already done
                        if (!_beamDoseSlicesCache.ContainsKey("All Beams"))
                        {
                            OutputLog += "Processing All Beams (standardized)...\n";
                            var allBeamsData = Compute2DAllBeamsStandardized(structure, plan);
                            if (allBeamsData != null && allBeamsData.HasData)
                            {
                                _beamDoseSlicesCache["All Beams"] = allBeamsData.DoseSlices;
                                _beamStructSlicesCache["All Beams"] = allBeamsData.StructSlices;
                                _beamUGridSlicesCache["All Beams"] = allBeamsData.UGridSlices;
                                _beamVGridSlicesCache["All Beams"] = allBeamsData.VGridSlices;
                                _beamDepthValuesCache["All Beams"] = allBeamsData.DepthValues;
                                _beamSliceDimensionsCache["All Beams"] = allBeamsData.SliceDimensions;
                            }
                        }

                        OutputLog += "All beams cached successfully!\n";
                    }
                }
                else if (selectedPvdrMode == "VMAT")
                {
                    RunSphericalPVDRMetric(tumorId, plan);
                    return;
                }
            }
            catch (Exception ex)
            {
                OutputLog += $"Error during 2D PVDR analysis: {ex.Message}\n";
            }
        }

        // Helper method to compute standardized "All Beams" for 2D with fixed grid dimensions
        private BeamDoseData Compute2DAllBeamsStandardized(Structure structure, PlanSetup plan)
        {
            var result = new BeamDoseData();

            try
            {
                // Use plan isocenter
                var iso = plan.Beams.First().IsocenterPosition;

                // Standardized vertical beam direction (0, -1, 0) - pointing inferior
                VVector beamDir = new VVector(0, -1, 0);

                // Create orthogonal vectors for axial planes
                VVector uAxis = new VVector(1, 0, 0); // X-axis
                VVector vAxis = new VVector(0, 0, 1); // Z-axis

                OutputLog += $"Using standardized vertical beam for axial slices\n";
                OutputLog += $"Beam direction: ({beamDir.x:F3}, {beamDir.y:F3}, {beamDir.z:F3})\n";

                // Find entry/exit
                double searchStartDist = -300.0;
                double searchEndDist = 300.0;
                double searchStep = 2.0;
                bool insideStructure = false;
                double entryDist = double.NaN;
                double exitDist = double.NaN;

                for (double dist = searchStartDist; dist <= searchEndDist; dist += searchStep)
                {
                    var point = iso + dist * beamDir;
                    bool pointInStructure = structure.IsPointInsideSegment(point);

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
                    OutputLog += "Standardized beam does not intersect the structure.\n";
                    return result;
                }

                // Calculate depth positions
                double targetThickness = exitDist - entryDist;
                double depthSpacing = (targetThickness < 9.0) ? 2.0 : 3.0;
                int numberOfSlices = Math.Max(2, (int)Math.Ceiling(targetThickness / depthSpacing));

                OutputLog += $"Target thickness: {targetThickness:F1}mm, creating {numberOfSlices} slices\n";

                // Generate depth positions
                var depthPositions = new List<double>();
                for (int sliceIdx = 0; sliceIdx < numberOfSlices; sliceIdx++)
                {
                    double depthFromEntry = sliceIdx * (targetThickness / (numberOfSlices - 1));
                    depthPositions.Add(depthFromEntry);
                    result.DepthValues.Add(depthFromEntry);
                }

                // FIRST PASS: Find global bounds across all slices
                double globalMinU = double.MaxValue;
                double globalMaxU = double.MinValue;
                double globalMinV = double.MaxValue;
                double globalMaxV = double.MinValue;
                bool hasValidBounds = false;

                OutputLog += "Finding global bounds across all axial slices...\n";

                for (int sliceIdx = 0; sliceIdx < numberOfSlices; sliceIdx++)
                {
                    double currentDepth = entryDist + depthPositions[sliceIdx];
                    var currentPlanePosition = iso + currentDepth * beamDir;

                    var (sliceMinU, sliceMaxU, sliceMinV, sliceMaxV, structurePointCount) =
                        FindStructureBoundsAtDepth(structure, currentPlanePosition, uAxis, vAxis, beamDir);

                    if (structurePointCount > 0)
                    {
                        globalMinU = Math.Min(globalMinU, sliceMinU);
                        globalMaxU = Math.Max(globalMaxU, sliceMaxU);
                        globalMinV = Math.Min(globalMinV, sliceMinV);
                        globalMaxV = Math.Max(globalMaxV, sliceMaxV);
                        hasValidBounds = true;
                    }
                }

                if (!hasValidBounds)
                {
                    OutputLog += "No valid structure bounds found for standardized beam\n";
                    return result;
                }

                // Calculate global grid parameters
                double globalWidth = globalMaxU - globalMinU;
                double globalHeight = globalMaxV - globalMinV;
                double padding = Math.Max(3, Math.Max(globalWidth, globalHeight) * 0.1);

                double gridMinU = globalMinU - padding;
                double gridMaxU = globalMaxU + padding;
                double gridMinV = globalMinV - padding;
                double gridMaxV = globalMaxV + padding;

                double inPlaneStep = 1.5;
                int nX = (int)Math.Ceiling((gridMaxU - gridMinU) / inPlaneStep);
                int nY = (int)Math.Ceiling((gridMaxV - gridMinV) / inPlaneStep);

                OutputLog += $"Global axial grid: {nX}×{nY} covering X:[{gridMinU:F1},{gridMaxU:F1}] Z:[{gridMinV:F1},{gridMaxV:F1}]\n";

                // SECOND PASS: Process each depth slice with fixed global dimensions
                for (int sliceIdx = 0; sliceIdx < numberOfSlices; sliceIdx++)
                {
                    double currentDepth = entryDist + depthPositions[sliceIdx];
                    var currentPlanePosition = iso + currentDepth * beamDir;

                    // Initialize arrays for this slice with GLOBAL dimensions
                    var doseSlice = new double[nX, nY];
                    var structSlice = new bool[nX, nY];
                    var uGridSlice = new double[nX, nY];
                    var vGridSlice = new double[nX, nY];

                    // Sample dose at this depth using global grid and total plan dose
                    int insideCount = 0;
                    for (int ix = 0; ix < nX; ix++)
                    {
                        double paramU = gridMinU + (ix / (double)(nX - 1)) * (gridMaxU - gridMinU);

                        for (int iy = 0; iy < nY; iy++)
                        {
                            double paramV = gridMinV + (iy / (double)(nY - 1)) * (gridMaxV - gridMinV);

                            var samplePoint = currentPlanePosition + (paramU * uAxis) + (paramV * vAxis);

                            uGridSlice[ix, iy] = paramU;
                            vGridSlice[ix, iy] = paramV;

                            bool insideAtThisDepth = structure.IsPointInsideSegment(samplePoint);
                            structSlice[ix, iy] = insideAtThisDepth;

                            if (insideAtThisDepth)
                            {
                                insideCount++;
                                try
                                {
                                    var dv = plan.Dose.GetDoseToPoint(samplePoint);
                                    if (dv != null)
                                    {
                                        doseSlice[ix, iy] = dv.Dose;
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

                    // Store this slice data with global dimensions
                    result.DoseSlices.Add(doseSlice);
                    result.StructSlices.Add(structSlice);
                    result.UGridSlices.Add(uGridSlice);
                    result.VGridSlices.Add(vGridSlice);
                    result.SliceDimensions.Add((nX, nY));

                    OutputLog += $"  Axial slice {sliceIdx + 1}: {insideCount} points inside structure\n";
                }

                OutputLog += $"All Beams: Generated {result.DoseSlices.Count} axial slices with fixed {nX}×{nY} grid\n";
            }
            catch (Exception ex)
            {
                OutputLog += $"Error computing standardized All Beams dose: {ex.Message}\n";
            }

            return result;
        }

        // Helper method for computing individual beam dose with fixed grid dimensions
        private BeamDoseData ComputeBeamDose(Beam beam, Structure structure, Dose doseMatrix, PlanSetup plan)
        {
            var result = new BeamDoseData();

            try
            {
                // Setup beam geometry (same as before)
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

                // Create orthogonal vectors
                VVector up = new VVector(0, 0, 1);
                VVector uAxis = Cross(beamDir, up);
                if (uAxis.Length == 0) uAxis = new VVector(1, 0, 0);
                uAxis = uAxis / uAxis.Length;
                VVector vAxis = Cross(beamDir, uAxis);
                vAxis = vAxis / vAxis.Length;

                // Find entry/exit (same logic as before)
                double searchStartDist = -300.0;
                double searchEndDist = 300.0;
                double searchStep = 2.0;
                bool insideStructure = false;
                double entryDist = double.NaN;
                double exitDist = double.NaN;

                for (double dist = searchStartDist; dist <= searchEndDist; dist += searchStep)
                {
                    var point = iso + dist * beamDir;
                    bool pointInStructure = structure.IsPointInsideSegment(point);

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
                    OutputLog += $"  Beam {beam.Id} does not intersect structure\n";
                    return result;
                }

                // Calculate depth positions
                double targetThickness = exitDist - entryDist;
                double depthSpacing = (targetThickness < 9.0) ? 2.0 : 3.0;
                int numberOfSlices = Math.Max(2, (int)Math.Ceiling(targetThickness / depthSpacing));

                OutputLog += $"  Target thickness: {targetThickness:F1}mm, creating {numberOfSlices} slices\n";

                // Generate depth positions
                var depthPositions = new List<double>();
                for (int sliceIdx = 0; sliceIdx < numberOfSlices; sliceIdx++)
                {
                    double depthFromEntry = sliceIdx * (targetThickness / (numberOfSlices - 1));
                    depthPositions.Add(depthFromEntry);
                    result.DepthValues.Add(depthFromEntry);
                }

                // FIRST PASS: Find global bounds across all slices
                double globalMinU = double.MaxValue;
                double globalMaxU = double.MinValue;
                double globalMinV = double.MaxValue;
                double globalMaxV = double.MinValue;
                bool hasValidBounds = false;

                OutputLog += $"  Finding global bounds across all slices...\n";

                for (int sliceIdx = 0; sliceIdx < numberOfSlices; sliceIdx++)
                {
                    double currentDepth = entryDist + depthPositions[sliceIdx];
                    var currentPlanePosition = iso + currentDepth * beamDir;

                    var (sliceMinU, sliceMaxU, sliceMinV, sliceMaxV, structurePointCount) =
                        FindStructureBoundsAtDepth(structure, currentPlanePosition, uAxis, vAxis, beamDir);

                    if (structurePointCount > 0)
                    {
                        globalMinU = Math.Min(globalMinU, sliceMinU);
                        globalMaxU = Math.Max(globalMaxU, sliceMaxU);
                        globalMinV = Math.Min(globalMinV, sliceMinV);
                        globalMaxV = Math.Max(globalMaxV, sliceMaxV);
                        hasValidBounds = true;
                    }
                }

                if (!hasValidBounds)
                {
                    OutputLog += $"  No valid structure bounds found for beam {beam.Id}\n";
                    return result;
                }

                // Calculate global grid parameters
                double globalWidth = globalMaxU - globalMinU;
                double globalHeight = globalMaxV - globalMinV;
                double padding = Math.Max(3, Math.Max(globalWidth, globalHeight) * 0.1);

                double gridMinU = globalMinU - padding;
                double gridMaxU = globalMaxU + padding;
                double gridMinV = globalMinV - padding;
                double gridMaxV = globalMaxV + padding;

                double inPlaneStep = 1.5;
                int nX = (int)Math.Ceiling((gridMaxU - gridMinU) / inPlaneStep);
                int nY = (int)Math.Ceiling((gridMaxV - gridMinV) / inPlaneStep);

                OutputLog += $"  Global grid: {nX}×{nY} covering U:[{gridMinU:F1},{gridMaxU:F1}] V:[{gridMinV:F1},{gridMaxV:F1}]\n";

                // SECOND PASS: Process each depth slice with fixed global dimensions
                for (int sliceIdx = 0; sliceIdx < numberOfSlices; sliceIdx++)
                {
                    double currentDepth = entryDist + depthPositions[sliceIdx];
                    var currentPlanePosition = iso + currentDepth * beamDir;

                    // Initialize arrays for this slice with GLOBAL dimensions
                    var doseSlice = new double[nX, nY];
                    var structSlice = new bool[nX, nY];
                    var uGridSlice = new double[nX, nY];
                    var vGridSlice = new double[nX, nY];

                    // Sample dose at this depth using global grid
                    int insideCount = 0;
                    for (int ix = 0; ix < nX; ix++)
                    {
                        double paramU = gridMinU + (ix / (double)(nX - 1)) * (gridMaxU - gridMinU);

                        for (int iy = 0; iy < nY; iy++)
                        {
                            double paramV = gridMinV + (iy / (double)(nY - 1)) * (gridMaxV - gridMinV);

                            var samplePoint = currentPlanePosition + (paramU * uAxis) + (paramV * vAxis);

                            uGridSlice[ix, iy] = paramU;
                            vGridSlice[ix, iy] = paramV;

                            bool insideAtThisDepth = structure.IsPointInsideSegment(samplePoint);
                            structSlice[ix, iy] = insideAtThisDepth;

                            if (insideAtThisDepth)
                            {
                                insideCount++;
                                try
                                {
                                    var dv = doseMatrix.GetDoseToPoint(samplePoint);
                                    if (dv != null)
                                    {
                                        doseSlice[ix, iy] = dv.Dose;
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

                    // Store this slice data with global dimensions
                    result.DoseSlices.Add(doseSlice);
                    result.StructSlices.Add(structSlice);
                    result.UGridSlices.Add(uGridSlice);
                    result.VGridSlices.Add(vGridSlice);
                    result.SliceDimensions.Add((nX, nY));

                    OutputLog += $"    Slice {sliceIdx + 1}: {insideCount} points inside structure\n";
                }

                OutputLog += $"  Beam {beam.Id}: Generated {result.DoseSlices.Count} slices with fixed {nX}×{nY} grid\n";
            }
            catch (Exception ex)
            {
                OutputLog += $"  Error computing dose for beam {beam.Id}: {ex.Message}\n";
            }

            return result;
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

        // Multi-depth visualization method - updated title to show selected beam
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

            // === IMPROVED CANVAS LAYOUT ===
            // Divide canvas into logical regions
            double totalPlotWidth = canvasW * 0.72;  // Left side for plot
            double colorbarRegionWidth = canvasW * 0.28;  // Right side for colorbar

            // Define layout regions more precisely
            double titleHeight = 35;
            double yLabelWidth = 25;  // Space for rotated Y-axis label
            double xLabelHeight = 35;  // Space for X-axis label

            // Margins for the plot area itself
            double plotLeftMargin = 70;  // Space for Y-axis tick labels
            double plotRightMargin = 15;
            double plotTopMargin = 10;
            double plotBottomMargin = 40;  // Space for X-axis tick labels

            // Calculate actual plot dimensions
            double plotAreaLeft = yLabelWidth + plotLeftMargin;
            double plotAreaTop = titleHeight + plotTopMargin;
            double plotAreaWidth = totalPlotWidth - yLabelWidth - plotLeftMargin - plotRightMargin;
            double plotAreaHeight = canvasH - titleHeight - xLabelHeight - plotTopMargin - plotBottomMargin;

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
            double cellW = plotAreaWidth / nX;
            double cellH = plotAreaHeight / nY;

            // Ensure aspect ratio is maintained if needed
            double aspectRatio = (maxU - minU) / (maxV - minV);
            if (aspectRatio > 1.5 || aspectRatio < 0.67)  // If too stretched
            {
                // Adjust to maintain reasonable aspect ratio
                double targetSize = Math.Min(plotAreaWidth / nX, plotAreaHeight / nY);
                cellW = targetSize;
                cellH = targetSize;
            }

            // Calculate actual heatmap dimensions
            double actualHeatmapWidth = cellW * nX;
            double actualHeatmapHeight = cellH * nY;

            // Center the heatmap in the plot area
            double gridLeft = plotAreaLeft + (plotAreaWidth - actualHeatmapWidth) / 2;
            double gridTop = plotAreaTop + (plotAreaHeight - actualHeatmapHeight) / 2;

            // === DRAW VISUAL DIVIDER ===
            var dividerLine = new Line
            {
                X1 = totalPlotWidth,
                Y1 = titleHeight,
                X2 = totalPlotWidth,
                Y2 = canvasH - 10,
                Stroke = Brushes.Gray,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };
            targetCanvas.Children.Add(dividerLine);

            // === ADD TITLE - Centered and prominent ===
            string beamLabel = SelectedBeamId ?? "Unknown";
            string planeType = (SelectedBeamId == "All Beams") ? "Axial Plane" : "BEV Plane";
            var title = new TextBlock
            {
                Text = $"Dose Distribution [{beamLabel}] - {planeType}",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Measure text to center it properly
            title.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double titleWidth = title.DesiredSize.Width;
            // Canvas.SetLeft(title, (totalPlotWidth - titleWidth) / 2);
            Canvas.SetLeft(title, plotAreaLeft + (plotAreaWidth - titleWidth) / 2);
            Canvas.SetTop(title, 8);
            targetCanvas.Children.Add(title);

            // === DRAW AXES ===
            // Draw a border around the plot area
            var plotBorder = new Rectangle
            {
                Width = actualHeatmapWidth + 2,
                Height = actualHeatmapHeight + 2,
                Stroke = Brushes.Black,
                StrokeThickness = 1.5,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(plotBorder, gridLeft - 1);
            Canvas.SetTop(plotBorder, gridTop - 1);
            targetCanvas.Children.Add(plotBorder);

            // === Y-AXIS LABEL - Properly rotated and positioned ===
            string vAxisText = (SelectedBeamId == "All Beams") ? "Z (mm)" : "V Distance (mm)";

            var yAxisLabel = new TextBlock
            {
                Text = vAxisText,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            // Create rotation transform
            var rotateTransform = new RotateTransform(-90);
            yAxisLabel.RenderTransform = rotateTransform;

            // Position in the center of Y-label area
            // Canvas.SetLeft(yAxisLabel, yLabelWidth / 2);
            Canvas.SetLeft(yAxisLabel, 0);
            Canvas.SetTop(yAxisLabel, plotAreaTop + plotAreaHeight / 2);
            targetCanvas.Children.Add(yAxisLabel);

            // === X-AXIS LABEL - Centered below plot ===
            string uAxisText = (SelectedBeamId == "All Beams") ? "X (mm)" : "U Distance (mm)";

            var xAxisLabel = new TextBlock
            {
                Text = uAxisText,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            xAxisLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double xLabelWidth = xAxisLabel.DesiredSize.Width;
            Canvas.SetLeft(xAxisLabel, plotAreaLeft + (plotAreaWidth - xLabelWidth) / 2);
            Canvas.SetTop(xAxisLabel, canvasH - xLabelHeight + 5);
            targetCanvas.Children.Add(xAxisLabel);

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
                        Stroke = Brushes.Gray,
                        StrokeThickness = 0.1
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

            // === AXIS TICKS AND VALUES - Cleaner layout ===
            // X-axis ticks
            int numXTicks = Math.Min(7, Math.Max(3, (int)(actualHeatmapWidth / 80)));
            for (int i = 0; i <= numXTicks; i++)
            {
                double tickGridX = i * (double)nX / numXTicks;
                double tickU = minU + (maxU - minU) * i / numXTicks;
                double tickX = gridLeft + tickGridX * cellW;

                // Tick mark
                var tick = new Line
                {
                    X1 = tickX,
                    Y1 = gridTop + actualHeatmapHeight,
                    X2 = tickX,
                    Y2 = gridTop + actualHeatmapHeight + 6,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                // Tick label
                var tickLabel = new TextBlock
                {
                    Text = $"{tickU:F0}",
                    FontSize = 11
                };

                tickLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tickLabel, tickX - tickLabel.DesiredSize.Width / 2);
                Canvas.SetTop(tickLabel, gridTop + actualHeatmapHeight + 10);

                targetCanvas.Children.Add(tick);
                targetCanvas.Children.Add(tickLabel);
            }

            // Y-axis ticks
            int numYTicks = Math.Min(6, Math.Max(3, (int)(actualHeatmapHeight / 60)));
            for (int i = 0; i <= numYTicks; i++)
            {
                double tickGridY = i * (double)nY / numYTicks;
                double tickV = minV + (maxV - minV) * i / numYTicks;
                double tickY = gridTop + actualHeatmapHeight - tickGridY * cellH;

                // Tick mark
                var tick = new Line
                {
                    X1 = gridLeft - 6,
                    Y1 = tickY,
                    X2 = gridLeft,
                    Y2 = tickY,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                // Tick label
                var tickLabel = new TextBlock
                {
                    Text = $"{tickV:F0}",
                    FontSize = 11
                };

                tickLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                // Canvas.SetLeft(tickLabel, gridLeft - tickLabel.DesiredSize.Width - 10);
                Canvas.SetLeft(tickLabel, gridLeft - tickLabel.DesiredSize.Width - 5);
                Canvas.SetTop(tickLabel, tickY - tickLabel.DesiredSize.Height / 2);

                targetCanvas.Children.Add(tick);
                targetCanvas.Children.Add(tickLabel);
            }

            // === DRAW COLORBAR (RIGHT REGION) - Improved layout ===
            double colorbarLeft = totalPlotWidth + 25;
            double colorbarTop = plotAreaTop + 20;
            double colorbarBarWidth = 45;
            double colorbarHeight = plotAreaHeight - 40;

            // Colorbar title
            var colorbarTitle = new TextBlock
            {
                Text = "Dose (Gy)",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            colorbarTitle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(colorbarTitle, colorbarLeft + (colorbarBarWidth - colorbarTitle.DesiredSize.Width) / 2);
            Canvas.SetTop(colorbarTitle, colorbarTop - 25);
            targetCanvas.Children.Add(colorbarTitle);

            // Colorbar background with border
            var colorbarBorder = new Rectangle
            {
                Width = colorbarBarWidth + 2,
                Height = colorbarHeight + 2,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(colorbarBorder, colorbarLeft - 1);
            Canvas.SetTop(colorbarBorder, colorbarTop - 1);
            targetCanvas.Children.Add(colorbarBorder);

            // Colorbar gradient
            int colorSteps = 100;  // More steps for smoother gradient
            double stepHeight = colorbarHeight / colorSteps;

            for (int s = 0; s < colorSteps; s++)
            {
                double norm = (double)s / (colorSteps - 1);
                Color barColor = GetSmoothDoseColor(norm);

                var barRect = new Rectangle
                {
                    Width = colorbarBarWidth,
                    Height = stepHeight + 1,
                    Fill = new SolidColorBrush(barColor)
                };

                Canvas.SetLeft(barRect, colorbarLeft);
                Canvas.SetTop(barRect, colorbarTop + (colorSteps - 1 - s) * stepHeight);
                targetCanvas.Children.Add(barRect);
            }

            // === COLORBAR LABELS with tick marks ===
            double labelX = colorbarLeft + colorbarBarWidth + 8;
            int numColorbarTicks = 5;

            for (int i = 0; i <= numColorbarTicks; i++)
            {
                double fraction = (double)i / numColorbarTicks;
                double doseValue = structureMinDose + fraction * (structureMaxDose - structureMinDose);
                double tickY = colorbarTop + colorbarHeight * (1 - fraction);

                // Tick mark
                var colorbarTick = new Line
                {
                    X1 = colorbarLeft + colorbarBarWidth,
                    Y1 = tickY,
                    X2 = colorbarLeft + colorbarBarWidth + 4,
                    Y2 = tickY,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                targetCanvas.Children.Add(colorbarTick);

                // Label
                var colorbarLabel = new TextBlock
                {
                    Text = $"{doseValue:F1}",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold
                };

                colorbarLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(colorbarLabel, labelX);
                Canvas.SetTop(colorbarLabel, tickY - colorbarLabel.DesiredSize.Height / 2);
                targetCanvas.Children.Add(colorbarLabel);
            }

            // Add depth information subtitle
            //var depthInfo = new TextBlock
            //{
            //    Text = $"Depth: {CurrentDepthValue:F1}mm (Slice {CurrentDepthIndex + 1}/{_depthValues.Count})",
            //    FontSize = 14,
            //    FontStyle = FontStyles.Italic,
            //    Foreground = Brushes.DarkBlue
            //};
            //depthInfo.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            //Canvas.SetLeft(depthInfo, (totalPlotWidth - depthInfo.DesiredSize.Width) / 2);
            //Canvas.SetTop(depthInfo, titleHeight - 5);
            //targetCanvas.Children.Add(depthInfo);

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
            var numVertices = ptvAll.GetNumberOfSeparateParts();
            AllMetrics.Add(new MetricData
            {
                metric = "Number of Vertices",
                value = numVertices.ToString()
            });

            // D95 calculation
            try
            {
                var relativeVolume = VolumePresentation.Relative;
                var absoluteDoseValue = DoseValuePresentation.Absolute;
                var doseUnit = plan.TotalDose.Unit.ToString();
                var d95Value = plan.GetDoseAtVolume(structureForEval, 95, relativeVolume, absoluteDoseValue);

                AllMetrics.Add(new MetricData
                {
                    metric = $"Dose Covering 95% of Target (D95) ({doseUnit})",
                    value = Math.Round(d95Value.Dose, 3).ToString()
                });
                var d90Value = plan.GetDoseAtVolume(structureForEval, 90, relativeVolume, absoluteDoseValue);
                AllMetrics.Add(new MetricData
                {
                    metric = $"Dose Covering 90% of Target (D90) ({doseUnit})",
                    value = Math.Round(d90Value.Dose, 3).ToString()
                });

                var d50Value = plan.GetDoseAtVolume(structureForEval, 50, relativeVolume, absoluteDoseValue);
                AllMetrics.Add(new MetricData
                {
                    metric = $"Dose Covering 50% of Target (D50) ({doseUnit})",
                    value = Math.Round(d50Value.Dose, 3).ToString()
                });

                // d20
                var d20Value = plan.GetDoseAtVolume(structureForEval, 20, relativeVolume, absoluteDoseValue);
                AllMetrics.Add(new MetricData
                {
                    metric = $"Dose Covering 20% of Target (D20) ({doseUnit})",
                    value = Math.Round(d20Value.Dose, 3).ToString()
                });

                // d10
                var d10Value = plan.GetDoseAtVolume(structureForEval, 10, relativeVolume, absoluteDoseValue);
                AllMetrics.Add(new MetricData
                {
                    metric = $"Dose Covering 10% of Target (D10) ({doseUnit})",
                    value = Math.Round(d10Value.Dose, 3).ToString()
                });

                // d5
                var d5Value = plan.GetDoseAtVolume(structureForEval, 5, relativeVolume, absoluteDoseValue);
                AllMetrics.Add(new MetricData
                {
                    metric = $"Dose Covering 5% of Target (D5) ({doseUnit})",
                    value = Math.Round(d5Value.Dose, 3).ToString()
                });

                // d95/d5
                AllMetrics.Add(new MetricData
                {
                    metric = "D95/D5",
                    value = Math.Round(d95Value.Dose / d5Value.Dose, 3).ToString()
                });

                // d10/d90
                AllMetrics.Add(new MetricData
                {
                    metric = "D10/D90",
                    value = Math.Round(d10Value.Dose / d90Value.Dose, 3).ToString()
                });

                // High dose core number density
                AllMetrics.Add(new MetricData
                {
                    metric = "High Dose Core Number Density (HCND)",
                    value = Math.Round((numVertices * 100 / structureForEval.Volume), 3).ToString()
                });

            }
            catch (Exception ex)
            {
                OutputLog += $"Error calculating Dose: {ex.Message}\n";
                AllMetrics.Add(new MetricData
                {
                    metric = "Dose Covering x% of Target (Dx) (Gy)",
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

        public static VVector Cross(VVector a, VVector b)
        {
            return new VVector(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x
            );
        }

        private void Run3DPVAnalysis(string tumorId, PlanSetup plan)
        {
            OutputLog += "\n===== Starting Unified 3D P/V Analysis =====\n";

            try
            {
                // Get the structure
                var structure = plan.StructureSet.Structures.FirstOrDefault(s => s.Id == tumorId);
                if (structure == null)
                {
                    OutputLog += $"Structure '{tumorId}' not found.\n";
                    return;
                }

                // Use total plan dose (not beam-specific)
                // This is the KEY DIFFERENCE from 1D/2D modes
                var totalDose = plan.Dose;
                if (totalDose == null)
                {
                    OutputLog += "No dose calculated for this plan.\n";
                    return;
                }

                OutputLog += $"Analyzing total dose for structure: {tumorId}\n";
                OutputLog += $"Structure volume: {structure.Volume:F2} cc\n";
                OutputLog += $"Dose grid resolution: {totalDose.XRes:F2} x {totalDose.YRes:F2} x {totalDose.ZRes:F2} mm\n";

                // Get structure bounds for analysis
                var bounds = structure.MeshGeometry.Bounds;
                OutputLog += $"Structure bounds: X[{bounds.X:F1},{bounds.X + bounds.SizeX:F1}] ";
                OutputLog += $"Y[{bounds.Y:F1},{bounds.Y + bounds.SizeY:F1}] ";
                OutputLog += $"Z[{bounds.Z:F1},{bounds.Z + bounds.SizeZ:F1}]\n";

                // TODO: Implement 3D grid sampling
                OutputLog += "TODO: 3D grid sampling...\n";

                // TODO: Implement P/V detection
                OutputLog += "TODO: P/V detection...\n";

                // TODO: Calculate metrics
                OutputLog += "TODO: Calculate P/V metrics...\n";

                // TODO: Prepare visualization
                OutputLog += "TODO: Prepare 3D visualization...\n";

                OutputLog += "===== 3D P/V Analysis Complete (placeholder) =====\n";
            }
            catch (Exception ex)
            {
                OutputLog += $"Error in 3D P/V Analysis: {ex.Message}\n";
                if (ex.InnerException != null)
                {
                    OutputLog += $"Inner exception: {ex.InnerException.Message}\n";
                }
            }
        }

    }

}


