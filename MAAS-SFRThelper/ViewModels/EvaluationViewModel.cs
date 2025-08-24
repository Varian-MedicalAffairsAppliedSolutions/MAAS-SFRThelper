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
using System.Windows.Media.Media3D;
using System.Windows.Media;

namespace MAAS_SFRThelper.ViewModels
{

    // 3D Grid for dose sampling
    public class DoseGrid3D
    {
        public double[,,] Values { get; set; }
        public int NX { get; set; }
        public int NY { get; set; }
        public int NZ { get; set; }
        public VVector Origin { get; set; }
        public VVector Spacing { get; set; }
        public double MaxDose { get; set; }
        public double MinDose { get; set; }

        public DoseGrid3D(int nx, int ny, int nz)
        {
            NX = nx;
            NY = ny;
            NZ = nz;
            Values = new double[nx, ny, nz];
            MaxDose = double.MinValue;
            MinDose = double.MaxValue;
        }
    }

    // Peak or Valley location in 3D
    public class PVPoint3D
    {
        public int I { get; set; }  // Grid indices
        public int J { get; set; }
        public int K { get; set; }
        public VVector Position { get; set; }  // Physical position
        public double DoseValue { get; set; }
        public bool IsPeak { get; set; }
    }

    // P/V Analysis Results
    public class PVAnalysisResults
    {
        public List<PVPoint3D> Peaks { get; set; } = new List<PVPoint3D>();
        public List<PVPoint3D> Valleys { get; set; } = new List<PVPoint3D>();
        public double MeanPVRatio { get; set; }
        public double StdDevPVRatio { get; set; }
        public double PeakVolumePercent { get; set; }
        public double ValleyVolumePercent { get; set; }
        public double MaxDose { get; set; }
        public double MinDose { get; set; }
        public int TotalVoxels { get; set; }
        public int StructureVoxels { get; set; }
    }

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

        // 3D P/V Analysis fields
        private DoseGrid3D _dose3DGrid;
        private PVAnalysisResults _pvResults;
        private bool _has3DData = false;

        // 3D Analysis parameters (make these configurable later if needed)
        private double _gridResolution3D = 2.0; // mm
        private double _peakThresholdPercent = 0.7; // 70% of max dose
        private double _valleyThresholdPercent = 0.2; // 20% of max dose

        // 3D Visualization fields
        private Model3DGroup _3dModelGroup;
        private bool _is3DVisualizationReady = false;
        // Add this field with your other private fields
        private Structure _currentStructure;
        // Add with your other private fields
        private MeshData _currentStructureMesh;

        // Add this public property for binding to the view
        public Model3DGroup Model3DGroup
        {
            get { return _3dModelGroup; }
            set { SetProperty(ref _3dModelGroup, value); }
        }

        private bool _showOnionLayers = false;
        public bool ShowOnionLayers
        {
            get { return _showOnionLayers; }
            set
            {
                if (SetProperty(ref _showOnionLayers, value))
                {
                    // Recreate visualization when toggled
                    if (_is3DVisualizationReady)
                    {
                        Create3DVisualization();
                    }
                }
            }
        }

        private double _onionLayerOpacity = 0.5;
        public double OnionLayerOpacity
        {
            get { return _onionLayerOpacity; }
            set
            {
                if (SetProperty(ref _onionLayerOpacity, value))
                {
                    // Recreate visualization when opacity changes
                    if (_is3DVisualizationReady && _showOnionLayers)
                    {
                        Create3DVisualization();
                    }
                }
            }
        }
        // Add property to control 2D vs 3D visualization
        public bool Show3DVisualization
        {
            get { return _is3DVisualizationReady && Is3DInterpolationSelected && bPlotVis; }
        }

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

        //        // Always need a structure
        //        if (string.IsNullOrEmpty(selectedTumorId))
        //        {
        //            OutputLog += "No structure selected. Please select a structure.\n";
        //            return;
        //        }

        //        // Only need beam selection for 1D and 2D modes
        //        if (!Is3DEvaluationSelected && !Is3DInterpolationSelected)
        //        {
        //            if (string.IsNullOrEmpty(selectedBeamId))
        //            {
        //                OutputLog += "No beam selected. Please select a beam.\n";
        //                return;
        //            }
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
        //                    OutputLog += "1D CAX Dosimetrics complete\n \n \n \n";
        //                    return;
        //                }

        //                if (Is2DPlanarSelected)
        //                {
        //                    OutputLog += "Running 2D Planar computation...\n";
        //                    Run2DPVDRMetric(selectedTumorId, plan);
        //                    OutputLog += "2D Planar Dosimetrics complete\n \n \n \n";
        //                    return;
        //                }

        //                if (Is3DEvaluationSelected)
        //                {
        //                    OutputLog += "Running 3D Dose Metrics...\n";
        //                    update3DMetrics(selectedTumorId, ptvAllName, plan);
        //                    OutputLog += "3D Dosimetrics complete\n \n \n \n";
        //                    return;
        //                }

        //                // *** NEW: Handle 3D P/V Interpolation mode ***
        //                if (Is3DInterpolationSelected)
        //                {
        //                    OutputLog += "Running 3D P/V Analysis...\n";
        //                    Run3DPVAnalysis(selectedTumorId, plan);
        //                    OutputLog += "3D P/V Analysis complete\n \n \n \n";
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

                string selectedBeamId = SelectedBeamId;
                string selectedTumorId = SelectedTumorId;
                string ptvAllName = SelectedTumorId;

                if (string.IsNullOrEmpty(selectedTumorId))
                {
                    OutputLog += "No structure selected. Please select a structure.\n";
                    return;
                }

                if (!Is3DEvaluationSelected && !Is3DInterpolationSelected)
                {
                    if (string.IsNullOrEmpty(selectedBeamId))
                    {
                        OutputLog += "No beam selected. Please select a beam.\n";
                        return;
                    }
                }

                // All ESAPI work happens here
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
                            OutputLog += "1D CAX Dosimetrics complete\n \n \n \n";
                            return;
                        }

                        if (Is2DPlanarSelected)
                        {
                            OutputLog += "Running 2D Planar computation...\n";
                            Run2DPVDRMetric(selectedTumorId, plan);
                            OutputLog += "2D Planar Dosimetrics complete\n \n \n \n";
                            return;
                        }

                        if (Is3DEvaluationSelected)
                        {
                            OutputLog += "Running 3D Dose Metrics...\n";
                            update3DMetrics(selectedTumorId, ptvAllName, plan);
                            OutputLog += "3D Dosimetrics complete\n \n \n \n";
                            return;
                        }

                        if (Is3DInterpolationSelected)
                        {
                            OutputLog += "Running 3D P/V Analysis...\n";
                            Run3DPVAnalysis(selectedTumorId, plan);
                            OutputLog += "3D P/V Analysis complete\n \n \n \n";
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        OutputLog += $"Error during dose computation: {ex.Message}\n";
                    }
                });

                // After ESAPI work is done, update commands on UI thread
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

                // 3D P/V Interpolation ready?
                if (Is3DInterpolationSelected)
                    return _has3DData && _doseSlices != null && _doseSlices.Count > 0; // _has3DData && _pvResults != null && _dose3DGrid != null;  // Check data, not visualization

                // 3D evaluation ready?
                if (Is3DEvaluationSelected)
                    return false; // No plots for this

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
                else if (Is3DInterpolationSelected)
                {
                    OutputLog += "Showing 3D P/V visualization...\n";
                    if (!_has3DData || _doseSlices == null || _doseSlices.Count == 0)
                    {
                        MessageBox.Show("No 3D visualization data available. Run computation first.",
                            "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                        bTextVis = true;
                        bPlotVis = false;
                        return;
                    }

                    // NOW create the 3D visualization on UI thread
                    Create3DVisualization();

                    // The 3D viewport will automatically show due to binding
                    OutputLog += "3D visualization is ready. Use mouse to rotate, zoom, and pan.\n";
                    OutputLog += "Left-click drag: Rotate | Right-click drag: Pan | Scroll: Zoom\n";
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
            OutputLog += "\n===== Starting 3D Slice Stack Visualization =====\n";

            try
            {
                // Get the structure
                var structure = plan.StructureSet.Structures.FirstOrDefault(s => s.Id == tumorId);
                if (structure == null)
                {
                    OutputLog += $"Structure '{tumorId}' not found.\n";
                    return;
                }

                // Extract mesh for structure outline
                _currentStructureMesh = new MeshData();
                try
                {
                    if (structure.MeshGeometry != null && structure.MeshGeometry.Positions != null)
                    {
                        var positions = structure.MeshGeometry.Positions;
                        var triangles = structure.MeshGeometry.TriangleIndices;

                        OutputLog += $"Extracting mesh with {positions.Count} vertices...\n";

                        foreach (var point in positions)
                        {
                            _currentStructureMesh.Positions.Add(new Point3D(point.X, point.Y, point.Z));
                        }

                        foreach (var index in triangles)
                        {
                            _currentStructureMesh.TriangleIndices.Add(index);
                        }

                        OutputLog += $"Mesh extraction complete: {_currentStructureMesh.Positions.Count} vertices\n";
                    }
                }
                catch (Exception meshEx)
                {
                    OutputLog += $"Warning: Could not extract mesh: {meshEx.Message}\n";
                }

                OutputLog += $"Analyzing structure: {tumorId}\n";
                OutputLog += $"Structure volume: {structure.Volume:F2} cc\n";

                // DELETE ALL THE OLD CODE - no Create3DDoseGrid, no DetectPeaksAndValleys, etc.

                // JUST compute 2D slices using existing method
                OutputLog += "Computing standardized axial slices for 3D visualization...\n";

                // Use your existing 2D computation
                var beamData = Compute2DAllBeamsStandardized(structure, plan);

                if (beamData == null || !beamData.HasData)
                {
                    OutputLog += "Failed to compute 2D slices.\n";
                    return;
                }

                // Store the slice data
                _doseSlices = beamData.DoseSlices;
                _structSlices = beamData.StructSlices;
                _uGridSlices = beamData.UGridSlices;
                _vGridSlices = beamData.VGridSlices;
                _depthValues = beamData.DepthValues;
                _sliceDimensions = beamData.SliceDimensions;

                OutputLog += $"Computed {_doseSlices.Count} axial slices\n";

                // Find dose range
                double maxDose = 0;
                double minDose = double.MaxValue;

                foreach (var slice in _doseSlices)
                {
                    for (int i = 0; i < slice.GetLength(0); i++)
                    {
                        for (int j = 0; j < slice.GetLength(1); j++)
                        {
                            if (!double.IsNaN(slice[i, j]) && slice[i, j] > 0)
                            {
                                maxDose = Math.Max(maxDose, slice[i, j]);
                                minDose = Math.Min(minDose, slice[i, j]);
                            }
                        }
                    }
                }

                OutputLog += $"Dose range across all slices: {minDose:F2} - {maxDose:F2} Gy\n";

                // Set flags for visualization
                _has3DData = true;
                _has2DPlotData = true;

                // NO CALLS TO OLD METHODS - DELETE THESE LINES:
                // _dose3DGrid = Create3DDoseGrid(structure, totalDose);  // DELETE
                // _pvResults = DetectPeaksAndValleys(_dose3DGrid, structure);  // DELETE
                // CalculatePVMetrics(_pvResults, _dose3DGrid);  // DELETE
                // DisplayPVResults();  // DELETE

                OutputLog += "===== 3D Slice Stack Data Ready =====\n";
            }
            catch (Exception ex)
            {
                OutputLog += $"Error in 3D slice stack preparation: {ex.Message}\n";
            }
        }

        //private void Run3DPVAnalysis(string tumorId, PlanSetup plan)
        //{
        //    OutputLog += "\n===== Starting Unified 3D P/V Analysis =====\n";

        //    try
        //    {
        //        // Get the structure
        //        var structure = plan.StructureSet.Structures.FirstOrDefault(s => s.Id == tumorId);
        //        if (structure == null)
        //        {
        //            OutputLog += $"Structure '{tumorId}' not found.\n";
        //            return;
        //        }

        //        // Extract mesh data while in ESAPI thread - store as simple data
        //        _currentStructureMesh = new MeshData();

        //        try
        //        {
        //            if (structure.MeshGeometry != null && structure.MeshGeometry.Positions != null)
        //            {
        //                // Extract all data as simple types while in ESAPI thread
        //                var positions = structure.MeshGeometry.Positions;
        //                var triangles = structure.MeshGeometry.TriangleIndices;

        //                OutputLog += $"Extracting mesh with {positions.Count} vertices...\n";

        //                // Copy data, don't reference ESAPI objects
        //                foreach (var point in positions)
        //                {
        //                    _currentStructureMesh.Positions.Add(new Point3D(point.X, point.Y, point.Z));
        //                }

        //                foreach (var index in triangles)
        //                {
        //                    _currentStructureMesh.TriangleIndices.Add(index);
        //                }

        //                OutputLog += $"Mesh extraction complete: {_currentStructureMesh.Positions.Count} vertices\n";
        //            }
        //        }
        //        catch (Exception meshEx)
        //        {
        //            OutputLog += $"Warning: Could not extract mesh: {meshEx.Message}\n";
        //        }

        //        var totalDose = plan.Dose;
        //        if (totalDose == null)
        //        {
        //            OutputLog += "No dose calculated for this plan.\n";
        //            return;
        //        }

        //        OutputLog += $"Analyzing structure: {tumorId}\n";
        //        OutputLog += $"Structure volume: {structure.Volume:F2} cc\n";

        //        // Create 3D dose grid
        //        //OutputLog += "Creating 3D dose grid...\n";
        //        //_dose3DGrid = Create3DDoseGrid(structure, totalDose);

        //        if (_dose3DGrid == null)
        //        {
        //            OutputLog += "Failed to create 3D dose grid.\n";
        //            return;
        //        }

        //        OutputLog += $"Grid dimensions: {_dose3DGrid.NX} x {_dose3DGrid.NY} x {_dose3DGrid.NZ}\n";
        //        OutputLog += $"Dose range: {_dose3DGrid.MinDose:F2} - {_dose3DGrid.MaxDose:F2} Gy\n";

        //        // Detect peaks and valleys
        //        //OutputLog += "Detecting peaks and valleys...\n";
        //        //_pvResults = DetectPeaksAndValleys(_dose3DGrid, structure);

        //        if (_pvResults == null)
        //        {
        //            OutputLog += "Failed to detect peaks and valleys.\n";
        //            return;
        //        }

        //        OutputLog += $"Found {_pvResults.Peaks.Count} peaks and {_pvResults.Valleys.Count} valleys\n";

        //        // Calculate P/V metrics
        //        //OutputLog += "Calculating P/V metrics...\n";
        //        //CalculatePVMetrics(_pvResults, _dose3DGrid);

        //        // Display results
        //        //DisplayPVResults();

        //        _has3DData = true;
        //        OutputLog += "===== 3D P/V Analysis Complete =====\n";

        //        // DON'T use Dispatcher here - just set the flag
        //    }
        //    catch (Exception ex)
        //    {
        //        OutputLog += $"Error in 3D P/V Analysis: {ex.Message}\n";
        //    }
        //}

        // Create 3D dose grid by sampling at regular intervals
        //private DoseGrid3D Create3DDoseGrid(Structure structure, Dose dose)
        //{
        //    try
        //    {
        //        // Get structure bounds
        //        var bounds = structure.MeshGeometry.Bounds;

        //        OutputLog += $"Structure bounds: X[{bounds.X:F1}-{bounds.X + bounds.SizeX:F1}], ";
        //        OutputLog += $"Y[{bounds.Y:F1}-{bounds.Y + bounds.SizeY:F1}], ";
        //        OutputLog += $"Z[{bounds.Z:F1}-{bounds.Z + bounds.SizeZ:F1}]\n";
        //        OutputLog += $"Structure size: {bounds.SizeX:F1} x {bounds.SizeY:F1} x {bounds.SizeZ:F1} mm\n";

        //        // DEBUG: Let's check the structure center
        //        var centerPoint = structure.CenterPoint;
        //        OutputLog += $"Structure center: ({centerPoint.x:F1}, {centerPoint.y:F1}, {centerPoint.z:F1})\n";

        //        // DEBUG: Test some specific points
        //        OutputLog += "Testing specific points:\n";
        //        var testPoint1 = new VVector(centerPoint.x, centerPoint.y, centerPoint.z);
        //        bool centerInside = structure.IsPointInsideSegment(testPoint1);
        //        OutputLog += $"  Center point inside? {centerInside}\n";

        //        // Test points at different Z levels
        //        for (double z = bounds.Z; z <= bounds.Z + bounds.SizeZ; z += 20)
        //        {
        //            var testPt = new VVector(centerPoint.x, centerPoint.y, z);
        //            bool inside = structure.IsPointInsideSegment(testPt);
        //            OutputLog += $"  Point at ({centerPoint.x:F1}, {centerPoint.y:F1}, {z:F1}) inside? {inside}\n";
        //        }

        //        // Use structure bounds directly without padding first to see what we get
        //        double minX = bounds.X;
        //        double maxX = bounds.X + bounds.SizeX;
        //        double minY = bounds.Y;
        //        double maxY = bounds.Y + bounds.SizeY;
        //        double minZ = bounds.Z;
        //        double maxZ = bounds.Z + bounds.SizeZ;

        //        // Calculate grid dimensions
        //        int nx = (int)Math.Ceiling((maxX - minX) / _gridResolution3D);
        //        int ny = (int)Math.Ceiling((maxY - minY) / _gridResolution3D);
        //        int nz = (int)Math.Ceiling((maxZ - minZ) / _gridResolution3D);

        //        OutputLog += $"Grid will be {nx} x {ny} x {nz} with {_gridResolution3D}mm spacing\n";

        //        var grid = new DoseGrid3D(nx, ny, nz)
        //        {
        //            Origin = new VVector(minX, minY, minZ),
        //            Spacing = new VVector(_gridResolution3D, _gridResolution3D, _gridResolution3D)
        //        };

        //        // Sample dose at each grid point
        //        int insideCount = 0;
        //        int totalPoints = 0;

        //        // Track distribution in all dimensions
        //        Dictionary<int, int> xDistribution = new Dictionary<int, int>();
        //        Dictionary<int, int> yDistribution = new Dictionary<int, int>();
        //        Dictionary<int, int> zDistribution = new Dictionary<int, int>();

        //        for (int i = 0; i < nx; i++)
        //        {
        //            double x = minX + i * _gridResolution3D;

        //            for (int j = 0; j < ny; j++)
        //            {
        //                double y = minY + j * _gridResolution3D;

        //                for (int k = 0; k < nz; k++)
        //                {
        //                    double z = minZ + k * _gridResolution3D;
        //                    var point = new VVector(x, y, z);
        //                    totalPoints++;

        //                    // Check if point is inside structure
        //                    bool isInside = structure.IsPointInsideSegment(point);

        //                    if (isInside)
        //                    {
        //                        insideCount++;

        //                        // Track distribution
        //                        if (!xDistribution.ContainsKey(i)) xDistribution[i] = 0;
        //                        if (!yDistribution.ContainsKey(j)) yDistribution[j] = 0;
        //                        if (!zDistribution.ContainsKey(k)) zDistribution[k] = 0;

        //                        xDistribution[i]++;
        //                        yDistribution[j]++;
        //                        zDistribution[k]++;

        //                        var doseValue = dose.GetDoseToPoint(point);
        //                        if (doseValue != null)
        //                        {
        //                            double doseGy = doseValue.Dose;
        //                            grid.Values[i, j, k] = doseGy;

        //                            if (doseGy > grid.MaxDose) grid.MaxDose = doseGy;
        //                            if (doseGy < grid.MinDose) grid.MinDose = doseGy;
        //                        }
        //                        else
        //                        {
        //                            grid.Values[i, j, k] = 0; // Use 0 instead of NaN for inside but no dose
        //                        }
        //                    }
        //                    else
        //                    {
        //                        grid.Values[i, j, k] = double.NaN;
        //                    }
        //                }
        //            }
        //        }

        //        OutputLog += $"Sampled {insideCount} voxels inside structure out of {totalPoints} total\n";

        //        // Show distribution in all dimensions
        //        OutputLog += $"X distribution: {xDistribution.Count} slices with points (should be ~100 for sphere)\n";
        //        OutputLog += $"Y distribution: {yDistribution.Count} slices with points (should be ~100 for sphere)\n";
        //        OutputLog += $"Z distribution: {zDistribution.Count} slices with points (should be ~99 for sphere)\n";

        //        // Show first and last slices with points
        //        if (zDistribution.Count > 0)
        //        {
        //            int minZ_k = zDistribution.Keys.Min();
        //            int maxZ_k = zDistribution.Keys.Max();
        //            OutputLog += $"Z range with points: slice {minZ_k} to {maxZ_k} (out of {nz} total)\n";
        //            double actualMinZ = minZ + minZ_k * _gridResolution3D;
        //            double actualMaxZ = minZ + maxZ_k * _gridResolution3D;
        //            OutputLog += $"Actual Z range with points: {actualMinZ:F1} to {actualMaxZ:F1} mm\n";
        //        }

        //        return grid;
        //    }
        //    catch (Exception ex)
        //    {
        //        OutputLog += $"Error creating 3D grid: {ex.Message}\n";
        //        return null;
        //    }
        //}

        //// Detect peaks and valleys using local maxima/minima approach
        //private PVAnalysisResults DetectPeaksAndValleys(DoseGrid3D grid, Structure structure)
        //{
        //    var results = new PVAnalysisResults
        //    {
        //        MaxDose = grid.MaxDose,
        //        MinDose = grid.MinDose
        //    };

        //    try
        //    {
        //        // Calculate thresholds
        //        double peakThreshold = _peakThresholdPercent * grid.MaxDose;
        //        double valleyThreshold = _valleyThresholdPercent * grid.MaxDose;

        //        OutputLog += $"Peak threshold: > {peakThreshold:F2} Gy ({_peakThresholdPercent * 100}% of max)\n";
        //        OutputLog += $"Valley threshold: < {valleyThreshold:F2} Gy ({_valleyThresholdPercent * 100}% of max)\n";

        //        int structureVoxelCount = 0;
        //        int peakVoxelCount = 0;
        //        int valleyVoxelCount = 0;

        //        // Method 1: Simple threshold-based detection
        //        for (int i = 1; i < grid.NX - 1; i++)
        //        {
        //            for (int j = 1; j < grid.NY - 1; j++)
        //            {
        //                for (int k = 1; k < grid.NZ - 1; k++)
        //                {
        //                    double centerDose = grid.Values[i, j, k];

        //                    // Skip if outside structure
        //                    if (double.IsNaN(centerDose))
        //                        continue;

        //                    structureVoxelCount++;

        //                    // Check if local maximum (peak)
        //                    bool isLocalMax = true;
        //                    bool isLocalMin = true;

        //                    // Check 26 neighbors
        //                    for (int di = -1; di <= 1; di++)
        //                    {
        //                        for (int dj = -1; dj <= 1; dj++)
        //                        {
        //                            for (int dk = -1; dk <= 1; dk++)
        //                            {
        //                                if (di == 0 && dj == 0 && dk == 0)
        //                                    continue;

        //                                double neighborDose = grid.Values[i + di, j + dj, k + dk];
        //                                if (!double.IsNaN(neighborDose))
        //                                {
        //                                    if (neighborDose >= centerDose)
        //                                        isLocalMax = false;
        //                                    if (neighborDose <= centerDose)
        //                                        isLocalMin = false;
        //                                }
        //                            }
        //                        }
        //                    }

        //                    // Classify voxel
        //                    VVector position = new VVector(
        //                        grid.Origin.x + i * grid.Spacing.x,
        //                        grid.Origin.y + j * grid.Spacing.y,
        //                        grid.Origin.z + k * grid.Spacing.z
        //                    );

        //                    // Peak: local maximum AND above threshold
        //                    if (isLocalMax && centerDose > peakThreshold)
        //                    {
        //                        results.Peaks.Add(new PVPoint3D
        //                        {
        //                            I = i,
        //                            J = j,
        //                            K = k,
        //                            Position = position,
        //                            DoseValue = centerDose,
        //                            IsPeak = true
        //                        });
        //                        peakVoxelCount++;
        //                    }
        //                    // Valley: local minimum AND below threshold
        //                    else if (isLocalMin && centerDose < valleyThreshold)
        //                    {
        //                        results.Valleys.Add(new PVPoint3D
        //                        {
        //                            I = i,
        //                            J = j,
        //                            K = k,
        //                            Position = position,
        //                            DoseValue = centerDose,
        //                            IsPeak = false
        //                        });
        //                        valleyVoxelCount++;
        //                    }

        //                    // Count for volume statistics
        //                    if (centerDose > peakThreshold)
        //                        peakVoxelCount++;
        //                    if (centerDose < valleyThreshold)
        //                        valleyVoxelCount++;
        //                }
        //            }
        //        }

        //        results.TotalVoxels = grid.NX * grid.NY * grid.NZ;
        //        results.StructureVoxels = structureVoxelCount;
        //        results.PeakVolumePercent = 100.0 * peakVoxelCount / structureVoxelCount;
        //        results.ValleyVolumePercent = 100.0 * valleyVoxelCount / structureVoxelCount;

        //        return results;
        //    }
        //    catch (Exception ex)
        //    {
        //        OutputLog += $"Error detecting peaks/valleys: {ex.Message}\n";
        //        return results;
        //    }
        //}

        //// Calculate P/V metrics
        //private void CalculatePVMetrics(PVAnalysisResults results, DoseGrid3D grid)
        //{
        //    try
        //    {
        //        if (results.Peaks.Count == 0 || results.Valleys.Count == 0)
        //        {
        //            OutputLog += "Warning: No peaks or valleys found for P/V calculation.\n";
        //            results.MeanPVRatio = 0;
        //            return;
        //        }

        //        // Calculate P/V ratios using nearest neighbor approach
        //        List<double> pvRatios = new List<double>();

        //        foreach (var peak in results.Peaks)
        //        {
        //            // Find nearest valley
        //            double minDistance = double.MaxValue;
        //            PVPoint3D nearestValley = null;

        //            foreach (var valley in results.Valleys)
        //            {
        //                double dx = peak.Position.x - valley.Position.x;
        //                double dy = peak.Position.y - valley.Position.y;
        //                double dz = peak.Position.z - valley.Position.z;
        //                double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);

        //                if (distance < minDistance)
        //                {
        //                    minDistance = distance;
        //                    nearestValley = valley;
        //                }
        //            }

        //            if (nearestValley != null && nearestValley.DoseValue > 0)
        //            {
        //                double ratio = peak.DoseValue / nearestValley.DoseValue;
        //                pvRatios.Add(ratio);
        //            }
        //        }

        //        if (pvRatios.Count > 0)
        //        {
        //            results.MeanPVRatio = pvRatios.Average();

        //            // Calculate standard deviation
        //            double sumSquares = pvRatios.Sum(r => Math.Pow(r - results.MeanPVRatio, 2));
        //            results.StdDevPVRatio = Math.Sqrt(sumSquares / pvRatios.Count);

        //            OutputLog += $"P/V Ratios - Mean: {results.MeanPVRatio:F2}, StdDev: {results.StdDevPVRatio:F2}\n";
        //            OutputLog += $"Min P/V: {pvRatios.Min():F2}, Max P/V: {pvRatios.Max():F2}\n";
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        OutputLog += $"Error calculating P/V metrics: {ex.Message}\n";
        //    }
        //}

        //// Display results in the metrics grid
        //private void DisplayPVResults()
        //{

        //    if (_pvResults == null) return;

        //    OutputLog += "Updating metrics display...\n";

        //    // Clear all previous metrics first (same as update3DMetrics)
        //    AllMetrics.Clear();

        //    // Add P/V metrics following the same pattern as update3DMetrics
        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "Number of Peaks",
        //        value = _pvResults.Peaks.Count.ToString()
        //    });

        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "Number of Valleys",
        //        value = _pvResults.Valleys.Count.ToString()
        //    });

        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "Mean P/V Ratio",
        //        value = Math.Round(_pvResults.MeanPVRatio, 2).ToString()
        //    });

        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "P/V Ratio Std Dev",
        //        value = Math.Round(_pvResults.StdDevPVRatio, 2).ToString()
        //    });

        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "Peak Volume (%)",
        //        value = Math.Round(_pvResults.PeakVolumePercent, 1).ToString()
        //    });

        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "Valley Volume (%)",
        //        value = Math.Round(_pvResults.ValleyVolumePercent, 1).ToString()
        //    });

        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "Max Dose (Gy)",
        //        value = Math.Round(_pvResults.MaxDose, 2).ToString()
        //    });

        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "Min Dose (Gy)",
        //        value = Math.Round(_pvResults.MinDose, 2).ToString()
        //    });

        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "Structure Voxels",
        //        value = _pvResults.StructureVoxels.ToString()
        //    });

        //    // Calculate heterogeneity index
        //    double heterogeneityIndex = (_pvResults.MaxDose - _pvResults.MinDose) / _pvResults.MaxDose;
        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "Heterogeneity Index",
        //        value = Math.Round(heterogeneityIndex, 3).ToString()
        //    });

        //    OutputLog += $"Added {AllMetrics.Count} metrics to collection\n";

        //    // At the end of your metrics in Run3DPVAnalysis, add these test metrics:
        //    //AllMetrics.Add(new MetricData
        //    //{
        //    //    metric = "Peak Density (peaks/cc)",
        //    //    value = Math.Round(_pvResults.Peaks.Count / structure.Volume, 2).ToString()
        //    //});

        //    //AllMetrics.Add(new MetricData
        //    //{
        //    //    metric = "Valley Density (valleys/cc)",
        //    //    value = Math.Round(_pvResults.Valleys.Count / structure.Volume, 2).ToString()
        //    //});

        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "Grid Resolution (mm)",
        //        value = _gridResolution3D.ToString()
        //    });

        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "Peak Threshold (Gy)",
        //        value = Math.Round(_peakThresholdPercent * _pvResults.MaxDose, 2).ToString()
        //    });

        //    AllMetrics.Add(new MetricData
        //    {
        //        metric = "Valley Threshold (Gy)",
        //        value = Math.Round(_valleyThresholdPercent * _pvResults.MaxDose, 2).ToString()
        //    });

        //    // Force UI update (same as update3DMetrics)
        //    RaisePropertyChanged(nameof(AllMetrics));

        //    OutputLog += "Metrics display updated.\n";
        //}

        private void AddStructureMesh(Model3DGroup modelGroup)
        {
            try
            {
                // We need to get the structure mesh from the last computation
                // Store this during Run3DPVAnalysis
                if (_currentStructure?.MeshGeometry != null)
                {
                    var mesh = new MeshGeometry3D();

                    // Convert structure mesh to WPF mesh
                    foreach (var point in _currentStructure.MeshGeometry.Positions)
                    {
                        mesh.Positions.Add(new Point3D(point.X, point.Y, point.Z));
                    }

                    foreach (var triangle in _currentStructure.MeshGeometry.TriangleIndices)
                    {
                        mesh.TriangleIndices.Add(triangle);
                    }

                    // Semi-transparent gray material for structure
                    var structureMaterial = new DiffuseMaterial(
                        new SolidColorBrush(Color.FromArgb(50, 128, 128, 128)));

                    var structureModel = new GeometryModel3D(mesh, structureMaterial);
                    structureModel.BackMaterial = structureMaterial; // Show both sides

                    modelGroup.Children.Add(structureModel);
                    OutputLog += "Added structure mesh to visualization\n";
                }
            }
            catch (Exception ex)
            {
                OutputLog += $"Error adding structure mesh: {ex.Message}\n";
            }
        }




        // Add the 3D Legend method
        //private void Add3DLegend(Model3DGroup modelGroup, double doseMin, double doseMax)
        //{
        //    try
        //    {
        //        OutputLog += "Adding 3D dose legend...\n";

        //        // Position legend to the right side
        //        double legendX = 70;
        //        double legendY = -40;
        //        double legendZ = 0;

        //        // Create color bar
        //        double barHeight = 80;
        //        double barWidth = 8;
        //        double barDepth = 2;
        //        int colorSteps = 20;

        //        for (int i = 0; i < colorSteps; i++)
        //        {
        //            double norm = i / (double)(colorSteps - 1);
        //            Color color = GetImprovedDoseColor(norm);

        //            double yPos = legendY + (i / (double)colorSteps) * barHeight;
        //            double segmentHeight = barHeight / colorSteps * 1.1; // Slight overlap

        //            // Create a colored segment
        //            var mesh = new MeshGeometry3D();

        //            // Create a small box for this color segment
        //            mesh.Positions.Add(new Point3D(legendX, yPos, legendZ));
        //            mesh.Positions.Add(new Point3D(legendX + barWidth, yPos, legendZ));
        //            mesh.Positions.Add(new Point3D(legendX + barWidth, yPos + segmentHeight, legendZ));
        //            mesh.Positions.Add(new Point3D(legendX, yPos + segmentHeight, legendZ));

        //            mesh.Positions.Add(new Point3D(legendX, yPos, legendZ + barDepth));
        //            mesh.Positions.Add(new Point3D(legendX + barWidth, yPos, legendZ + barDepth));
        //            mesh.Positions.Add(new Point3D(legendX + barWidth, yPos + segmentHeight, legendZ + barDepth));
        //            mesh.Positions.Add(new Point3D(legendX, yPos + segmentHeight, legendZ + barDepth));

        //            // Front face
        //            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2);
        //            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(3);

        //            // Back face
        //            mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(5);
        //            mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(7); mesh.TriangleIndices.Add(6);

        //            var material = new DiffuseMaterial(new SolidColorBrush(color));
        //            modelGroup.Children.Add(new GeometryModel3D(mesh, material));
        //        }

        //        // Add tick marks at key positions
        //        var tickPositions = new[]
        //        {
        //    new { pos = 0.0, label = $"{doseMin:F0}" },
        //    new { pos = 0.25, label = $"{doseMin + 0.25*(doseMax-doseMin):F0}" },
        //    new { pos = 0.5, label = $"{doseMin + 0.5*(doseMax-doseMin):F0}" },
        //    new { pos = 0.75, label = $"{doseMin + 0.75*(doseMax-doseMin):F0}" },
        //    new { pos = 1.0, label = $"{doseMax:F0}" }
        //};

        //        foreach (var tick in tickPositions)
        //        {
        //            double yPos = legendY + tick.pos * barHeight;

        //            // Add a small tick mark
        //            var tickLine = CreateLine(
        //                new Point3D(legendX + barWidth, yPos, legendZ),
        //                new Point3D(legendX + barWidth + 3, yPos, legendZ),
        //                0.5
        //            );
        //            modelGroup.Children.Add(new GeometryModel3D(tickLine,
        //                new DiffuseMaterial(new SolidColorBrush(Colors.White))));
        //        }

        //        // Add "Dose (Gy)" label as a marker
        //        var labelMarker = CreateLine(
        //            new Point3D(legendX + barWidth / 2, legendY + barHeight + 5, legendZ),
        //            new Point3D(legendX + barWidth / 2, legendY + barHeight + 6, legendZ),
        //            2
        //        );
        //        modelGroup.Children.Add(new GeometryModel3D(labelMarker,
        //            new DiffuseMaterial(new SolidColorBrush(Colors.White))));

        //        OutputLog += $"Legend added showing range: {doseMin:F1} - {doseMax:F1} Gy\n";
        //    }
        //    catch (Exception ex)
        //    {
        //        OutputLog += $"Error adding legend: {ex.Message}\n";
        //    }
        //}

        private void CreateOnionLayers(Model3DGroup modelGroup, double doseMin, double doseMax,
                               double centerX, double centerY, double centerZ,
                               double xScale, double yScale, double zScale)
        {
            try
            {
                OutputLog += "\n=== Creating Onion Layer (Isodose) Visualization ===\n";

                // Define isodose levels
                var isodoseLevels = new[]
                {
            new { level = 0.95, color = Color.FromArgb(100, 255, 0, 0), name = "95%" },
            new { level = 0.80, color = Color.FromArgb(80, 255, 128, 0), name = "80%" },
            new { level = 0.60, color = Color.FromArgb(60, 255, 255, 0), name = "60%" },
            new { level = 0.40, color = Color.FromArgb(40, 0, 255, 0), name = "40%" },
            new { level = 0.20, color = Color.FromArgb(30, 0, 128, 255), name = "20%" }
        };

                foreach (var isoLevel in isodoseLevels)
                {
                    double targetDose = doseMin + isoLevel.level * (doseMax - doseMin);
                    var surfaceMesh = new MeshGeometry3D();

                    OutputLog += $"Creating {isoLevel.name} isodose surface at {targetDose:F1} Gy...\n";

                    var surfacePoints = new List<Point3D>();

                    // Sample through slices to find isodose contours
                    for (int sliceIdx = 0; sliceIdx < _doseSlices.Count; sliceIdx += 2) // Skip every other for performance
                    {
                        var doseSlice = _doseSlices[sliceIdx];
                        var structSlice = _structSlices[sliceIdx];
                        var uGrid = _uGridSlices[sliceIdx];
                        var vGrid = _vGridSlices[sliceIdx];
                        var (nX, nY) = _sliceDimensions[sliceIdx];

                        double originalY = _depthValues[sliceIdx];
                        double scaledY = (originalY - centerY) * yScale;

                        // Find contour at this dose level
                        for (int i = 1; i < nX - 1; i += 2) // Skip for performance
                        {
                            for (int j = 1; j < nY - 1; j += 2)
                            {
                                if (!structSlice[i, j]) continue;

                                double dose = doseSlice[i, j];
                                if (double.IsNaN(dose)) continue;

                                // Check if near the isodose level
                                double diff = Math.Abs(dose - targetDose);
                                double tolerance = (doseMax - doseMin) * 0.02;

                                if (diff < tolerance)
                                {
                                    double u = uGrid[i, j];
                                    double v = vGrid[i, j];
                                    double scaledX = (u - centerX) * xScale;
                                    double scaledZ = (v - centerZ) * zScale;

                                    surfacePoints.Add(new Point3D(scaledX, scaledY, scaledZ));
                                }
                            }
                        }
                    }

                    // Create mesh from surface points
                    if (surfacePoints.Count > 3)
                    {
                        // Limit points for performance
                        int maxPoints = Math.Min(surfacePoints.Count, 500);
                        double pointSize = 3.0;

                        for (int i = 0; i < maxPoints; i++)
                        {
                            var point = surfacePoints[i];
                            int baseIdx = surfaceMesh.Positions.Count;

                            // Add a small quad at this position
                            surfaceMesh.Positions.Add(new Point3D(point.X - pointSize, point.Y, point.Z - pointSize));
                            surfaceMesh.Positions.Add(new Point3D(point.X + pointSize, point.Y, point.Z - pointSize));
                            surfaceMesh.Positions.Add(new Point3D(point.X + pointSize, point.Y, point.Z + pointSize));
                            surfaceMesh.Positions.Add(new Point3D(point.X - pointSize, point.Y, point.Z + pointSize));

                            surfaceMesh.TriangleIndices.Add(baseIdx);
                            surfaceMesh.TriangleIndices.Add(baseIdx + 1);
                            surfaceMesh.TriangleIndices.Add(baseIdx + 2);

                            surfaceMesh.TriangleIndices.Add(baseIdx);
                            surfaceMesh.TriangleIndices.Add(baseIdx + 2);
                            surfaceMesh.TriangleIndices.Add(baseIdx + 3);
                        }

                        // Create semi-transparent material
                        var material = new DiffuseMaterial(new SolidColorBrush(isoLevel.color));
                        var geoModel = new GeometryModel3D(surfaceMesh, material);
                        geoModel.BackMaterial = material;

                        modelGroup.Children.Add(geoModel);
                        OutputLog += $"  Added {maxPoints} points to {isoLevel.name} surface\n";
                    }
                }

                OutputLog += "Onion layer visualization complete\n";
            }
            catch (Exception ex)
            {
                OutputLog += $"Error creating onion layers: {ex.Message}\n";
            }
        }
        // Improved color mapping with more gradations
        private void Create3DVisualization()
        {
            try
            {
                OutputLog += "\n=== Starting Enhanced 3D Slice Stack Visualization ===\n";

                if (_doseSlices == null || _doseSlices.Count == 0)
                {
                    OutputLog += "ERROR: No slice data available for visualization.\n";
                    return;
                }

                var modelGroup = new Model3DGroup();

                // STEP 1: ANALYZE DATA BOUNDS AND DOSE DISTRIBUTION
                OutputLog += "Analyzing data bounds and dose distribution...\n";

                double dataMinX = double.MaxValue, dataMaxX = double.MinValue;
                double dataMinY = double.MaxValue, dataMaxY = double.MinValue;
                double dataMinZ = double.MaxValue, dataMaxZ = double.MinValue;

                // Collect ALL dose values for statistical analysis
                var allDoseValues = new List<double>();
                int totalDosePoints = 0;

                // First pass: collect all data
                for (int sliceIdx = 0; sliceIdx < _doseSlices.Count; sliceIdx++)
                {
                    var doseSlice = _doseSlices[sliceIdx];
                    var structSlice = _structSlices[sliceIdx];
                    var uGrid = _uGridSlices[sliceIdx];
                    var vGrid = _vGridSlices[sliceIdx];
                    var (nX, nY) = _sliceDimensions[sliceIdx];

                    int pointsInSlice = 0;

                    for (int i = 0; i < nX; i++)
                    {
                        for (int j = 0; j < nY; j++)
                        {
                            if (!structSlice[i, j] || double.IsNaN(doseSlice[i, j]) || doseSlice[i, j] <= 0)
                                continue;

                            double dose = doseSlice[i, j];
                            double u = uGrid[i, j];
                            double v = vGrid[i, j];

                            // Track spatial bounds
                            dataMinX = Math.Min(dataMinX, u);
                            dataMaxX = Math.Max(dataMaxX, u);
                            dataMinZ = Math.Min(dataMinZ, v);
                            dataMaxZ = Math.Max(dataMaxZ, v);

                            // Collect dose values
                            allDoseValues.Add(dose);
                            totalDosePoints++;
                            pointsInSlice++;
                        }
                    }

                    if (sliceIdx < 10 || sliceIdx % 10 == 0)
                        OutputLog += $"  Slice {sliceIdx + 1}: {pointsInSlice} dose points\n";
                }

                // Y bounds from slice positions
                dataMinY = _depthValues[0];
                dataMaxY = _depthValues[_depthValues.Count - 1];

                // Calculate actual data dimensions
                double dataWidth = dataMaxX - dataMinX;
                double dataThickness = dataMaxY - dataMinY;
                double dataHeight = dataMaxZ - dataMinZ;

                OutputLog += $"\nData bounds:\n";
                OutputLog += $"  X: [{dataMinX:F1}, {dataMaxX:F1}] mm (width: {dataWidth:F1} mm)\n";
                OutputLog += $"  Y: [{dataMinY:F1}, {dataMaxY:F1}] mm (thickness: {dataThickness:F1} mm)\n";
                OutputLog += $"  Z: [{dataMinZ:F1}, {dataMaxZ:F1}] mm (height: {dataHeight:F1} mm)\n";
                OutputLog += $"  Total dose points: {totalDosePoints}\n";

                // STEP 2: CALCULATE DOSE STATISTICS AND PERCENTILES
                if (allDoseValues.Count == 0)
                {
                    OutputLog += "ERROR: No dose values found!\n";
                    return;
                }

                allDoseValues.Sort();

                // Helper function for percentiles
                double GetPercentile(List<double> sortedValues, double percentile)
                {
                    int index = (int)Math.Max(0, Math.Min(sortedValues.Count - 1,
                                              sortedValues.Count * percentile / 100.0));
                    return sortedValues[index];
                }

                double absoluteMin = allDoseValues[0];
                double absoluteMax = allDoseValues[allDoseValues.Count - 1];
                double percentile5 = GetPercentile(allDoseValues, 5);
                double percentile10 = GetPercentile(allDoseValues, 10);
                double percentile25 = GetPercentile(allDoseValues, 25);
                double percentile50 = GetPercentile(allDoseValues, 50);
                double percentile75 = GetPercentile(allDoseValues, 75);
                double percentile90 = GetPercentile(allDoseValues, 90);
                double percentile95 = GetPercentile(allDoseValues, 95);

                OutputLog += $"\n=== Dose Distribution Analysis ===\n";
                OutputLog += $"Absolute: Min={absoluteMin:F2} Gy, Max={absoluteMax:F2} Gy\n";
                OutputLog += $"Percentiles:\n";
                OutputLog += $"  5%: {percentile5:F2} Gy\n";
                OutputLog += $"  10%: {percentile10:F2} Gy\n";
                OutputLog += $"  25%: {percentile25:F2} Gy\n";
                OutputLog += $"  50% (median): {percentile50:F2} Gy\n";
                OutputLog += $"  75%: {percentile75:F2} Gy\n";
                OutputLog += $"  90%: {percentile90:F2} Gy\n";
                OutputLog += $"  95%: {percentile95:F2} Gy\n";

                // USE PERCENTILE-BASED NORMALIZATION
                double doseMin_forNormalization = percentile5;
                double doseMax_forNormalization = percentile95;

                OutputLog += $"\nUsing percentile normalization: {doseMin_forNormalization:F2} - {doseMax_forNormalization:F2} Gy\n";

                // STEP 3: CALCULATE ADAPTIVE SCALING
                const double TARGET_SIZE = 100.0;

                double maxDimension = Math.Max(Math.Max(dataWidth, dataThickness), dataHeight);
                if (maxDimension <= 0)
                {
                    OutputLog += "ERROR: Invalid data dimensions!\n";
                    return;
                }

                double baseScale = TARGET_SIZE / maxDimension;
                double xScale = baseScale;
                double yScale = baseScale;
                double zScale = baseScale;

                // Boost thin dimensions
                double minThreshold = maxDimension * 0.15;

                if (dataWidth > 0 && dataWidth < minThreshold)
                {
                    xScale = baseScale * (minThreshold / dataWidth);
                    OutputLog += $"  Boosting X by {minThreshold / dataWidth:F1}x for visibility\n";
                }

                if (dataThickness > 0 && dataThickness < minThreshold)
                {
                    yScale = baseScale * (minThreshold / dataThickness);
                    OutputLog += $"  Boosting Y by {minThreshold / dataThickness:F1}x for visibility\n";
                }

                if (dataHeight > 0 && dataHeight < minThreshold)
                {
                    zScale = baseScale * (minThreshold / dataHeight);
                    OutputLog += $"  Boosting Z by {minThreshold / dataHeight:F1}x for visibility\n";
                }

                OutputLog += $"\nScale factors: X={xScale:F2}, Y={yScale:F2}, Z={zScale:F2}\n";

                // STEP 4: IMPROVED ADAPTIVE DOWNSAMPLING
                int downsampleStep = 1;

                // Progressive downsampling based on data size
                if (totalDosePoints > 1000000)
                {
                    downsampleStep = 4;
                    OutputLog += "Very large dataset (>1M points) - using 4x downsampling for smooth visualization\n";
                }
                else if (totalDosePoints > 500000)
                {
                    downsampleStep = 3;
                    OutputLog += "Large dataset (>500K points) - using 3x downsampling\n";
                }
                else if (totalDosePoints > 100000)
                {
                    downsampleStep = 2;
                    OutputLog += "Medium dataset (>100K points) - using 2x downsampling\n";
                }
                else
                {
                    downsampleStep = 1;
                    OutputLog += "Small dataset - no downsampling needed\n";
                }

                int maxTotalCells = 100000;  // Increased limit

                // STEP 5: CREATE 3D VISUALIZATION
                double centerX = (dataMinX + dataMaxX) / 2.0;
                double centerY = (dataMinY + dataMaxY) / 2.0;
                double centerZ = (dataMinZ + dataMaxZ) / 2.0;

                OutputLog += $"\nCreating 3D geometry for {_doseSlices.Count} slices...\n";

                int totalCellsCreated = 0;
                int slicesProcessed = 0;
                double sliceThickness = 1.0 * Math.Min(xScale, Math.Min(yScale, zScale));

                // Material cache for efficiency
                var materialCache = new Dictionary<Color, Material>();
                bool useAdaptiveSampling = downsampleStep > 1;  // Enable adaptive sampling for downsampled data

                for (int sliceIdx = 0; sliceIdx < _doseSlices.Count; sliceIdx++)
                {
                    var doseSlice = _doseSlices[sliceIdx];
                    var structSlice = _structSlices[sliceIdx];
                    var uGrid = _uGridSlices[sliceIdx];
                    var vGrid = _vGridSlices[sliceIdx];
                    var (nX, nY) = _sliceDimensions[sliceIdx];

                    double originalY = _depthValues[sliceIdx];
                    double scaledY = (originalY - centerY) * yScale;

                    var colorMeshes = new Dictionary<Color, MeshGeometry3D>();
                    int cellsInSlice = 0;

                    // Process cells with improved downsampling
                    for (int i = 0; i < nX; i += downsampleStep)
                    {
                        for (int j = 0; j < nY; j += downsampleStep)
                        {
                            if (!structSlice[i, j])
                                continue;

                            double dose = doseSlice[i, j];
                            if (double.IsNaN(dose) || dose <= 0)
                                continue;

                            if (totalCellsCreated >= maxTotalCells)
                            {
                                OutputLog += $"  Reached cell limit ({maxTotalCells}) at slice {sliceIdx + 1}\n";
                                goto FinishSlices;
                            }

                            // Adaptive sampling: check if near boundary for smoother edges
                            bool nearBoundary = false;
                            if (useAdaptiveSampling)
                            {
                                // Check neighbors
                                for (int di = -1; di <= 1 && !nearBoundary; di++)
                                {
                                    for (int dj = -1; dj <= 1 && !nearBoundary; dj++)
                                    {
                                        int ni = i + di;
                                        int nj = j + dj;
                                        if (ni >= 0 && ni < nX && nj >= 0 && nj < nY)
                                        {
                                            if (!structSlice[ni, nj])
                                            {
                                                nearBoundary = true;
                                            }
                                        }
                                    }
                                }
                            }

                            // Use finer resolution near boundaries
                            double cellSizeFactor = nearBoundary ? 0.5 : 1.0;

                            double u = uGrid[i, j];
                            double v = vGrid[i, j];
                            double scaledX = (u - centerX) * xScale;
                            double scaledZ = (v - centerZ) * zScale;

                            // Calculate adaptive cell size
                            double cellWidth = 2.0 * xScale;
                            double cellHeight = 2.0 * zScale;

                            if (i + 1 < nX && !double.IsNaN(uGrid[i + 1, j]))
                                cellWidth = Math.Abs(uGrid[i + 1, j] - u) * xScale;
                            if (j + 1 < nY && !double.IsNaN(vGrid[i, j + 1]))
                                cellHeight = Math.Abs(vGrid[i, j + 1] - v) * zScale;

                            // Apply downsampling and boundary adjustment
                            cellWidth *= downsampleStep * 1.05 * cellSizeFactor;
                            cellHeight *= downsampleStep * 1.05 * cellSizeFactor;

                            // Percentile-based color normalization
                            double normalizedDose = (dose - doseMin_forNormalization) /
                                                  (doseMax_forNormalization - doseMin_forNormalization);
                            normalizedDose = Math.Max(0, Math.Min(1, normalizedDose));

                            Color cellColor = GetImprovedDoseColor(normalizedDose);

                            if (!colorMeshes.ContainsKey(cellColor))
                                colorMeshes[cellColor] = new MeshGeometry3D();

                            var mesh = colorMeshes[cellColor];
                            int baseIndex = mesh.Positions.Count;

                            // Create thin box
                            mesh.Positions.Add(new Point3D(scaledX - cellWidth / 2, scaledY - sliceThickness / 2, scaledZ - cellHeight / 2));
                            mesh.Positions.Add(new Point3D(scaledX + cellWidth / 2, scaledY - sliceThickness / 2, scaledZ - cellHeight / 2));
                            mesh.Positions.Add(new Point3D(scaledX + cellWidth / 2, scaledY - sliceThickness / 2, scaledZ + cellHeight / 2));
                            mesh.Positions.Add(new Point3D(scaledX - cellWidth / 2, scaledY - sliceThickness / 2, scaledZ + cellHeight / 2));

                            mesh.Positions.Add(new Point3D(scaledX - cellWidth / 2, scaledY + sliceThickness / 2, scaledZ - cellHeight / 2));
                            mesh.Positions.Add(new Point3D(scaledX + cellWidth / 2, scaledY + sliceThickness / 2, scaledZ - cellHeight / 2));
                            mesh.Positions.Add(new Point3D(scaledX + cellWidth / 2, scaledY + sliceThickness / 2, scaledZ + cellHeight / 2));
                            mesh.Positions.Add(new Point3D(scaledX - cellWidth / 2, scaledY + sliceThickness / 2, scaledZ + cellHeight / 2));

                            // Top face
                            mesh.TriangleIndices.Add(baseIndex + 4);
                            mesh.TriangleIndices.Add(baseIndex + 5);
                            mesh.TriangleIndices.Add(baseIndex + 6);
                            mesh.TriangleIndices.Add(baseIndex + 4);
                            mesh.TriangleIndices.Add(baseIndex + 6);
                            mesh.TriangleIndices.Add(baseIndex + 7);

                            // Bottom face
                            mesh.TriangleIndices.Add(baseIndex + 0);
                            mesh.TriangleIndices.Add(baseIndex + 2);
                            mesh.TriangleIndices.Add(baseIndex + 1);
                            mesh.TriangleIndices.Add(baseIndex + 0);
                            mesh.TriangleIndices.Add(baseIndex + 3);
                            mesh.TriangleIndices.Add(baseIndex + 2);

                            cellsInSlice++;
                            totalCellsCreated++;
                        }
                    }

                    // Add all color groups for this slice
                    foreach (var kvp in colorMeshes)
                    {
                        if (kvp.Value.Positions.Count > 0)
                        {
                            if (!materialCache.ContainsKey(kvp.Key))
                            {
                                var material = new MaterialGroup();
                                material.Children.Add(new DiffuseMaterial(new SolidColorBrush(kvp.Key)));

                                if (kvp.Key == Colors.Red || kvp.Key == Colors.OrangeRed || kvp.Key == Colors.DarkRed)
                                {
                                    material.Children.Add(new EmissiveMaterial(
                                        new SolidColorBrush(Color.FromArgb(40, kvp.Key.R, kvp.Key.G, kvp.Key.B))));
                                }
                                materialCache[kvp.Key] = material;
                            }

                            var geoModel = new GeometryModel3D(kvp.Value, materialCache[kvp.Key]);
                            geoModel.BackMaterial = materialCache[kvp.Key];
                            modelGroup.Children.Add(geoModel);
                        }
                    }

                    slicesProcessed++;

                    if (sliceIdx == 0 || (sliceIdx + 1) % 10 == 0 || sliceIdx == _doseSlices.Count - 1)
                    {
                        OutputLog += $"  Processed slice {sliceIdx + 1}/{_doseSlices.Count}: {cellsInSlice} cells\n";
                    }
                }

            FinishSlices:

                // STEP 6: ADD 3D LEGEND
                Add3DLegend(modelGroup, doseMin_forNormalization, doseMax_forNormalization);

                // STEP 7: ADD REFERENCE GEOMETRY
                OutputLog += "\nAdding reference geometry...\n";

                double axisLength = TARGET_SIZE * 0.6;

                // X-axis (Red)
                var xAxis = CreateLine(new Point3D(-axisLength, 0, 0), new Point3D(axisLength, 0, 0), 2);
                var xMaterial = new MaterialGroup();
                xMaterial.Children.Add(new DiffuseMaterial(new SolidColorBrush(Colors.Red)));
                xMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(50, 0, 0))));
                modelGroup.Children.Add(new GeometryModel3D(xAxis, xMaterial));

                // Y-axis (Green)
                var yAxis = CreateLine(new Point3D(0, -axisLength, 0), new Point3D(0, axisLength, 0), 2);
                var yMaterial = new MaterialGroup();
                yMaterial.Children.Add(new DiffuseMaterial(new SolidColorBrush(Colors.Green)));
                yMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(0, 50, 0))));
                modelGroup.Children.Add(new GeometryModel3D(yAxis, yMaterial));

                // Z-axis (Blue)
                var zAxis = CreateLine(new Point3D(0, 0, -axisLength), new Point3D(0, 0, axisLength), 2);
                var zMaterial = new MaterialGroup();
                zMaterial.Children.Add(new DiffuseMaterial(new SolidColorBrush(Colors.Blue)));
                zMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(0, 0, 50))));
                modelGroup.Children.Add(new GeometryModel3D(zAxis, zMaterial));

                // STEP 8: OPTIONALLY ADD ONION LAYERS (if you have a ShowOnionLayers property)
                // Uncomment this if you want onion layers:
                if (ShowOnionLayers)
                {
                    CreateOnionLayers(modelGroup, doseMin_forNormalization, doseMax_forNormalization,
                                     centerX, centerY, centerZ, xScale, yScale, zScale);
                }

                // STEP 9: FINAL STATISTICS
                OutputLog += $"\n=== Visualization Statistics ===\n";
                OutputLog += $"Slices processed: {slicesProcessed}/{_doseSlices.Count}\n";
                OutputLog += $"Total cells created: {totalCellsCreated}\n";
                OutputLog += $"Total 3D objects: {modelGroup.Children.Count}\n";

                int meshCount = 0, totalTriangles = 0, totalVertices = 0;
                foreach (GeometryModel3D geoModel in modelGroup.Children.OfType<GeometryModel3D>())
                {
                    var mesh = geoModel.Geometry as MeshGeometry3D;
                    if (mesh != null)
                    {
                        meshCount++;
                        totalTriangles += mesh.TriangleIndices.Count / 3;
                        totalVertices += mesh.Positions.Count;
                    }
                }

                OutputLog += $"Meshes: {meshCount}, Vertices: {totalVertices}, Triangles: {totalTriangles}\n";

                // Set the model
                Model3DGroup = modelGroup;
                _is3DVisualizationReady = true;

                RaisePropertyChanged(nameof(Show3DVisualization));
                RaisePropertyChanged(nameof(Model3DGroup));

                OutputLog += "\n=== Visualization Complete ===\n";
                OutputLog += $"Visual size: {dataWidth * xScale:F1} x {dataThickness * yScale:F1} x {dataHeight * zScale:F1} units\n";
                OutputLog += $"Actual size: {dataWidth:F1} x {dataThickness:F1} x {dataHeight:F1} mm\n";
                OutputLog += $"Dose range: {doseMin_forNormalization:F2} - {doseMax_forNormalization:F2} Gy\n";
                OutputLog += "Legend added on right side\n";
                OutputLog += "Use mouse to rotate/zoom/pan the view\n";
            }
            catch (Exception ex)
            {
                OutputLog += $"\nERROR in Create3DVisualization: {ex.Message}\n";
                OutputLog += $"Stack trace: {ex.StackTrace}\n";
            }
        }
        // Add the 3D Legend method
        private void Add3DLegend(Model3DGroup modelGroup, double doseMin, double doseMax)
        {
            try
            {
                OutputLog += "Adding 3D dose legend...\n";

                // Position legend to the right side
                double legendX = 70;
                double legendY = -40;
                double legendZ = 0;

                // Create color bar
                double barHeight = 80;
                double barWidth = 8;
                double barDepth = 2;
                int colorSteps = 20;

                for (int i = 0; i < colorSteps; i++)
                {
                    double norm = i / (double)(colorSteps - 1);
                    Color color = GetImprovedDoseColor(norm);

                    double yPos = legendY + (i / (double)colorSteps) * barHeight;
                    double segmentHeight = barHeight / colorSteps * 1.1; // Slight overlap

                    // Create a colored segment
                    var mesh = new MeshGeometry3D();

                    // Create a small box for this color segment
                    mesh.Positions.Add(new Point3D(legendX, yPos, legendZ));
                    mesh.Positions.Add(new Point3D(legendX + barWidth, yPos, legendZ));
                    mesh.Positions.Add(new Point3D(legendX + barWidth, yPos + segmentHeight, legendZ));
                    mesh.Positions.Add(new Point3D(legendX, yPos + segmentHeight, legendZ));

                    mesh.Positions.Add(new Point3D(legendX, yPos, legendZ + barDepth));
                    mesh.Positions.Add(new Point3D(legendX + barWidth, yPos, legendZ + barDepth));
                    mesh.Positions.Add(new Point3D(legendX + barWidth, yPos + segmentHeight, legendZ + barDepth));
                    mesh.Positions.Add(new Point3D(legendX, yPos + segmentHeight, legendZ + barDepth));

                    // Front face
                    mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2);
                    mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(3);

                    // Back face
                    mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(5);
                    mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(7); mesh.TriangleIndices.Add(6);

                    var material = new DiffuseMaterial(new SolidColorBrush(color));
                    modelGroup.Children.Add(new GeometryModel3D(mesh, material));
                }

                // Add tick marks at key positions
                var tickPositions = new[]
                {
            new { pos = 0.0, label = $"{doseMin:F0}" },
            new { pos = 0.25, label = $"{doseMin + 0.25*(doseMax-doseMin):F0}" },
            new { pos = 0.5, label = $"{doseMin + 0.5*(doseMax-doseMin):F0}" },
            new { pos = 0.75, label = $"{doseMin + 0.75*(doseMax-doseMin):F0}" },
            new { pos = 1.0, label = $"{doseMax:F0}" }
        };

                foreach (var tick in tickPositions)
                {
                    double yPos = legendY + tick.pos * barHeight;

                    // Add a small tick mark
                    var tickLine = CreateLine(
                        new Point3D(legendX + barWidth, yPos, legendZ),
                        new Point3D(legendX + barWidth + 3, yPos, legendZ),
                        0.5
                    );
                    modelGroup.Children.Add(new GeometryModel3D(tickLine,
                        new DiffuseMaterial(new SolidColorBrush(Colors.White))));
                }

                // Add "Dose (Gy)" label as a marker
                var labelMarker = CreateLine(
                    new Point3D(legendX + barWidth / 2, legendY + barHeight + 5, legendZ),
                    new Point3D(legendX + barWidth / 2, legendY + barHeight + 6, legendZ),
                    2
                );
                modelGroup.Children.Add(new GeometryModel3D(labelMarker,
                    new DiffuseMaterial(new SolidColorBrush(Colors.White))));

                OutputLog += $"Legend added showing range: {doseMin:F1} - {doseMax:F1} Gy\n";
            }
            catch (Exception ex)
            {
                OutputLog += $"Error adding legend: {ex.Message}\n";
            }
        }

        // Keep your existing GetImprovedDoseColor method
        private Color GetImprovedDoseColor(double normalizedDose)
        {
            normalizedDose = Math.Max(0, Math.Min(1, normalizedDose));

            if (normalizedDose >= 0.95) return Colors.DarkRed;
            if (normalizedDose >= 0.90) return Colors.Red;
            if (normalizedDose >= 0.85) return Colors.OrangeRed;
            if (normalizedDose >= 0.80) return Colors.DarkOrange;
            if (normalizedDose >= 0.75) return Colors.Orange;
            if (normalizedDose >= 0.70) return Colors.Gold;
            if (normalizedDose >= 0.65) return Colors.Goldenrod;
            if (normalizedDose >= 0.60) return Colors.Yellow;
            if (normalizedDose >= 0.55) return Colors.GreenYellow;
            if (normalizedDose >= 0.50) return Colors.LawnGreen;
            if (normalizedDose >= 0.45) return Colors.LightGreen;
            if (normalizedDose >= 0.40) return Colors.MediumSeaGreen;
            if (normalizedDose >= 0.35) return Colors.SeaGreen;
            if (normalizedDose >= 0.30) return Colors.DarkSeaGreen;
            if (normalizedDose >= 0.25) return Colors.CadetBlue;
            if (normalizedDose >= 0.20) return Colors.LightBlue;
            if (normalizedDose >= 0.15) return Colors.SkyBlue;
            if (normalizedDose >= 0.10) return Colors.DeepSkyBlue;
            if (normalizedDose >= 0.05) return Colors.DodgerBlue;
            return Colors.Blue;
        }
        // Simple sphere helper
        private MeshGeometry3D CreateSimpleSphere(double cx, double cy, double cz, double radius)
        {
            var mesh = new MeshGeometry3D();

            // Create octahedron as simple sphere
            mesh.Positions.Add(new Point3D(cx + radius, cy, cz));
            mesh.Positions.Add(new Point3D(cx - radius, cy, cz));
            mesh.Positions.Add(new Point3D(cx, cy + radius, cz));
            mesh.Positions.Add(new Point3D(cx, cy - radius, cz));
            mesh.Positions.Add(new Point3D(cx, cy, cz + radius));
            mesh.Positions.Add(new Point3D(cx, cy, cz - radius));

            // Add triangles
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(4);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(5);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(4);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(5);

            return mesh;
        }

        // Helper to add bounding box
        private void AddScaledBoundingBox(Model3DGroup modelGroup, double width, double height, double depth)
        {
            var boxMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(50, 128, 128, 128)));
            double thickness = 0.5;

            double x = width / 2;
            double y = height / 2;
            double z = depth / 2;

            // Add 12 edges of the box
            // Bottom edges
            modelGroup.Children.Add(new GeometryModel3D(
                CreateLine(new Point3D(-x, -y, -z), new Point3D(x, -y, -z), thickness), boxMaterial));
            modelGroup.Children.Add(new GeometryModel3D(
                CreateLine(new Point3D(x, -y, -z), new Point3D(x, -y, z), thickness), boxMaterial));
            modelGroup.Children.Add(new GeometryModel3D(
                CreateLine(new Point3D(x, -y, z), new Point3D(-x, -y, z), thickness), boxMaterial));
            modelGroup.Children.Add(new GeometryModel3D(
                CreateLine(new Point3D(-x, -y, z), new Point3D(-x, -y, -z), thickness), boxMaterial));

            // Top edges
            modelGroup.Children.Add(new GeometryModel3D(
                CreateLine(new Point3D(-x, y, -z), new Point3D(x, y, -z), thickness), boxMaterial));
            modelGroup.Children.Add(new GeometryModel3D(
                CreateLine(new Point3D(x, y, -z), new Point3D(x, y, z), thickness), boxMaterial));
            modelGroup.Children.Add(new GeometryModel3D(
                CreateLine(new Point3D(x, y, z), new Point3D(-x, y, z), thickness), boxMaterial));
            modelGroup.Children.Add(new GeometryModel3D(
                CreateLine(new Point3D(-x, y, z), new Point3D(-x, y, -z), thickness), boxMaterial));

            // Vertical edges
            modelGroup.Children.Add(new GeometryModel3D(
                CreateLine(new Point3D(-x, -y, -z), new Point3D(-x, y, -z), thickness), boxMaterial));
            modelGroup.Children.Add(new GeometryModel3D(
                CreateLine(new Point3D(x, -y, -z), new Point3D(x, y, -z), thickness), boxMaterial));
            modelGroup.Children.Add(new GeometryModel3D(
                CreateLine(new Point3D(x, -y, z), new Point3D(x, y, z), thickness), boxMaterial));
            modelGroup.Children.Add(new GeometryModel3D(
                CreateLine(new Point3D(-x, -y, z), new Point3D(-x, y, z), thickness), boxMaterial));
        }

        // Add scaled axes
        private void AddScaledAxes(Model3DGroup modelGroup, double length)
        {
            // X-axis - Red
            var xLine = CreateLine(new Point3D(-length / 2, 0, 0), new Point3D(length / 2, 0, 0), 1);
            modelGroup.Children.Add(new GeometryModel3D(xLine,
                new DiffuseMaterial(new SolidColorBrush(Colors.Red))));

            // Y-axis - Green (stacking direction - make it prominent)
            var yLine = CreateLine(new Point3D(0, -length / 2, 0), new Point3D(0, length / 2, 0), 2);
            var yMaterial = new MaterialGroup();
            yMaterial.Children.Add(new DiffuseMaterial(new SolidColorBrush(Colors.Green)));
            yMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(0, 50, 0))));
            modelGroup.Children.Add(new GeometryModel3D(yLine, yMaterial));

            // Z-axis - Blue
            var zLine = CreateLine(new Point3D(0, 0, -length / 2), new Point3D(0, 0, length / 2), 1);
            modelGroup.Children.Add(new GeometryModel3D(zLine,
                new DiffuseMaterial(new SolidColorBrush(Colors.Blue))));
        }

        // Add bounding box to show the exaggerated scale
        private void AddBoundingBox(Model3DGroup modelGroup, double height)
        {
            // This shows the visual extent after scaling
            var boxMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(30, 200, 200, 200)));

            // Add vertical lines at corners to show height
            double x = 15, z = 15;  // Approximate structure radius

            var line1 = CreateLine(new Point3D(x, -height / 2, z), new Point3D(x, height / 2, z), 0.5);
            var line2 = CreateLine(new Point3D(-x, -height / 2, z), new Point3D(-x, height / 2, z), 0.5);
            var line3 = CreateLine(new Point3D(x, -height / 2, -z), new Point3D(x, height / 2, -z), 0.5);
            var line4 = CreateLine(new Point3D(-x, -height / 2, -z), new Point3D(-x, height / 2, -z), 0.5);

            modelGroup.Children.Add(new GeometryModel3D(line1, boxMaterial));
            modelGroup.Children.Add(new GeometryModel3D(line2, boxMaterial));
            modelGroup.Children.Add(new GeometryModel3D(line3, boxMaterial));
            modelGroup.Children.Add(new GeometryModel3D(line4, boxMaterial));
        }

        // Add a wireframe box to show data bounds
        private void AddBoundingWireframe(Model3DGroup modelGroup)
        {
            try
            {
                if (_dose3DGrid == null) return;

                // Calculate actual bounds of non-NaN data
                double minX = _dose3DGrid.Origin.x;
                double maxX = minX + _dose3DGrid.NX * _dose3DGrid.Spacing.x;
                double minY = _dose3DGrid.Origin.y;
                double maxY = minY + _dose3DGrid.NY * _dose3DGrid.Spacing.y;
                double minZ = _dose3DGrid.Origin.z;
                double maxZ = minZ + _dose3DGrid.NZ * _dose3DGrid.Spacing.z;

                // Create wireframe edges
                var edgeMaterial = new DiffuseMaterial(new SolidColorBrush(Colors.Gray));
                double thickness = 0.5;

                // Bottom square
                AddLine(modelGroup, minX, minY, minZ, maxX, minY, minZ, thickness, edgeMaterial);
                AddLine(modelGroup, maxX, minY, minZ, maxX, maxY, minZ, thickness, edgeMaterial);
                AddLine(modelGroup, maxX, maxY, minZ, minX, maxY, minZ, thickness, edgeMaterial);
                AddLine(modelGroup, minX, maxY, minZ, minX, minY, minZ, thickness, edgeMaterial);

                // Top square
                AddLine(modelGroup, minX, minY, maxZ, maxX, minY, maxZ, thickness, edgeMaterial);
                AddLine(modelGroup, maxX, minY, maxZ, maxX, maxY, maxZ, thickness, edgeMaterial);
                AddLine(modelGroup, maxX, maxY, maxZ, minX, maxY, maxZ, thickness, edgeMaterial);
                AddLine(modelGroup, minX, maxY, maxZ, minX, minY, maxZ, thickness, edgeMaterial);

                // Vertical edges
                AddLine(modelGroup, minX, minY, minZ, minX, minY, maxZ, thickness, edgeMaterial);
                AddLine(modelGroup, maxX, minY, minZ, maxX, minY, maxZ, thickness, edgeMaterial);
                AddLine(modelGroup, maxX, maxY, minZ, maxX, maxY, maxZ, thickness, edgeMaterial);
                AddLine(modelGroup, minX, maxY, minZ, minX, maxY, maxZ, thickness, edgeMaterial);

                OutputLog += $"Added bounding box: X[{minX:F0}-{maxX:F0}] Y[{minY:F0}-{maxY:F0}] Z[{minZ:F0}-{maxZ:F0}]\n";
            }
            catch (Exception ex)
            {
                OutputLog += $"Error adding bounding box: {ex.Message}\n";
            }
        }

        private void AddLine(Model3DGroup group, double x1, double y1, double z1,
                             double x2, double y2, double z2, double thickness, Material material)
        {
            var mesh = new MeshGeometry3D();

            // Create a thin box to represent the line
            var p1 = new Point3D(x1, y1, z1);
            var p2 = new Point3D(x2, y2, z2);

            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.Positions.Add(new Point3D(x1 + thickness, y1, z1));
            mesh.Positions.Add(new Point3D(x2 + thickness, y2, z2));

            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(2);

            group.Children.Add(new GeometryModel3D(mesh, material));
        }
        // Add coordinate axes for orientation
        private void AddCoordinateAxes(Model3DGroup modelGroup)
        {
            try
            {
                double length = 150;
                double radius = 1;

                // X-axis (red) - pointing right
                var xAxis = CreateCylinder(new Point3D(-length / 2, 0, 0), new Point3D(length / 2, 0, 0), radius);
                var xMaterial = new MaterialGroup();
                xMaterial.Children.Add(new DiffuseMaterial(new SolidColorBrush(Colors.Red)));
                xMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(50, 0, 0))));
                modelGroup.Children.Add(new GeometryModel3D(xAxis, xMaterial));

                // Y-axis (green) - pointing up
                var yAxis = CreateCylinder(new Point3D(0, -length / 2, 0), new Point3D(0, length / 2, 0), radius);
                var yMaterial = new MaterialGroup();
                yMaterial.Children.Add(new DiffuseMaterial(new SolidColorBrush(Colors.LightGreen)));
                yMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(0, 50, 0))));
                modelGroup.Children.Add(new GeometryModel3D(yAxis, yMaterial));

                // Z-axis (blue) - pointing forward
                var zAxis = CreateCylinder(new Point3D(0, 0, -length / 2), new Point3D(0, 0, length / 2), radius);
                var zMaterial = new MaterialGroup();
                zMaterial.Children.Add(new DiffuseMaterial(new SolidColorBrush(Colors.LightBlue)));
                zMaterial.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(0, 0, 50))));
                modelGroup.Children.Add(new GeometryModel3D(zAxis, zMaterial));

                // Add origin sphere
                var origin = CreateSimpleSphere(0, 0, 0, 3);
                var originMaterial = new DiffuseMaterial(new SolidColorBrush(Colors.White));
                modelGroup.Children.Add(new GeometryModel3D(origin, originMaterial));
            }
            catch { }
        }

        // Simple cylinder for axes
        private MeshGeometry3D CreateCylinder(Point3D start, Point3D end, double radius)
        {
            var mesh = new MeshGeometry3D();

            // Create a simple box to represent the cylinder
            Vector3D axis = end - start;
            Vector3D perp = new Vector3D(1, 0, 0);
            if (Math.Abs(axis.X) > 0.9) perp = new Vector3D(0, 1, 0);

            Vector3D side1 = Vector3D.CrossProduct(axis, perp);
            side1.Normalize();
            side1 *= radius;

            Vector3D side2 = Vector3D.CrossProduct(axis, side1);
            side2.Normalize();
            side2 *= radius;

            // Create 8 vertices for a box
            mesh.Positions.Add(start + side1);
            mesh.Positions.Add(start - side1);
            mesh.Positions.Add(start + side2);
            mesh.Positions.Add(start - side2);
            mesh.Positions.Add(end + side1);
            mesh.Positions.Add(end - side1);
            mesh.Positions.Add(end + side2);
            mesh.Positions.Add(end - side2);

            // Create faces
            // Side faces
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(6);

            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(5);
            mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(7); mesh.TriangleIndices.Add(5);

            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(4);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(4);

            mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(7);

            return mesh;
        }


        // Helper method to create a cube mesh
        private MeshGeometry3D CreateCube(VVector center, double size)
        {
            var mesh = new MeshGeometry3D();
            double h = size / 2;

            // Define the 8 vertices of the cube
            mesh.Positions.Add(new Point3D(center.x - h, center.y - h, center.z - h)); // 0
            mesh.Positions.Add(new Point3D(center.x + h, center.y - h, center.z - h)); // 1
            mesh.Positions.Add(new Point3D(center.x + h, center.y + h, center.z - h)); // 2
            mesh.Positions.Add(new Point3D(center.x - h, center.y + h, center.z - h)); // 3
            mesh.Positions.Add(new Point3D(center.x - h, center.y - h, center.z + h)); // 4
            mesh.Positions.Add(new Point3D(center.x + h, center.y - h, center.z + h)); // 5
            mesh.Positions.Add(new Point3D(center.x + h, center.y + h, center.z + h)); // 6
            mesh.Positions.Add(new Point3D(center.x - h, center.y + h, center.z + h)); // 7

            // Define the 12 triangles (2 per face, 6 faces)
            // Front face
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(3);

            // Back face
            mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(7);
            mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(7); mesh.TriangleIndices.Add(6);

            // Left face
            mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(7);

            // Right face
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(2);

            // Top face
            mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(7);

            // Bottom face
            mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(1);
            mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(0);

            return mesh;
        }

        // Optional: Add a legend showing dose colors
        private void AddDoseLegend(Model3DGroup modelGroup)
        {
            // This would add 3D text or colored boxes as a legend
            // For now, we'll skip this and just log the legend
            OutputLog += "Dose Color Legend:\n";
            OutputLog += "  Red: > 90% of max dose\n";
            OutputLog += "  Orange: 70-90%\n";
            OutputLog += "  Yellow: 50-70%\n";
            OutputLog += "  Green: 30-50%\n";
            OutputLog += "  Blue: 10-30%\n";
        }

        //private void AddBoundingBox(Model3DGroup modelGroup)
        //{
        //    try
        //    {
        //        if (_dose3DGrid == null) return;

        //        var origin = _dose3DGrid.Origin;
        //        var size = new VVector(
        //            _dose3DGrid.NX * _dose3DGrid.Spacing.x,
        //            _dose3DGrid.NY * _dose3DGrid.Spacing.y,
        //            _dose3DGrid.NZ * _dose3DGrid.Spacing.z
        //        );

        //        // Create wireframe box material
        //        var boxMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(50, 128, 128, 128)));

        //        // Create thin lines for the box edges
        //        double thickness = 0.5; // mm

        //        // Create 12 edges of the box
        //        // Bottom edges
        //        AddBoxEdge(modelGroup, origin, new VVector(origin.x + size.x, origin.y, origin.z), thickness, boxMaterial);
        //        AddBoxEdge(modelGroup, origin, new VVector(origin.x, origin.y + size.y, origin.z), thickness, boxMaterial);
        //        AddBoxEdge(modelGroup, new VVector(origin.x + size.x, origin.y, origin.z),
        //                   new VVector(origin.x + size.x, origin.y + size.y, origin.z), thickness, boxMaterial);
        //        AddBoxEdge(modelGroup, new VVector(origin.x, origin.y + size.y, origin.z),
        //                   new VVector(origin.x + size.x, origin.y + size.y, origin.z), thickness, boxMaterial);

        //        // Top edges
        //        var topZ = origin.z + size.z;
        //        AddBoxEdge(modelGroup, new VVector(origin.x, origin.y, topZ),
        //                   new VVector(origin.x + size.x, origin.y, topZ), thickness, boxMaterial);
        //        AddBoxEdge(modelGroup, new VVector(origin.x, origin.y, topZ),
        //                   new VVector(origin.x, origin.y + size.y, topZ), thickness, boxMaterial);
        //        AddBoxEdge(modelGroup, new VVector(origin.x + size.x, origin.y, topZ),
        //                   new VVector(origin.x + size.x, origin.y + size.y, topZ), thickness, boxMaterial);
        //        AddBoxEdge(modelGroup, new VVector(origin.x, origin.y + size.y, topZ),
        //                   new VVector(origin.x + size.x, origin.y + size.y, topZ), thickness, boxMaterial);

        //        // Vertical edges
        //        AddBoxEdge(modelGroup, origin, new VVector(origin.x, origin.y, topZ), thickness, boxMaterial);
        //        AddBoxEdge(modelGroup, new VVector(origin.x + size.x, origin.y, origin.z),
        //                   new VVector(origin.x + size.x, origin.y, topZ), thickness, boxMaterial);
        //        AddBoxEdge(modelGroup, new VVector(origin.x, origin.y + size.y, origin.z),
        //                   new VVector(origin.x, origin.y + size.y, topZ), thickness, boxMaterial);
        //        AddBoxEdge(modelGroup, new VVector(origin.x + size.x, origin.y + size.y, origin.z),
        //                   new VVector(origin.x + size.x, origin.y + size.y, topZ), thickness, boxMaterial);
        //    }
        //    catch (Exception ex)
        //    {
        //        OutputLog += $"Error adding bounding box: {ex.Message}\n";
        //    }
        //}

        private void AddBoxEdge(Model3DGroup group, VVector start, VVector end, double thickness, Material material)
        {
            // Create a thin cylinder to represent the edge
            var mesh = new MeshGeometry3D();

            // Simple box line (you could also use a cylinder)
            var dir = new VVector(end.x - start.x, end.y - start.y, end.z - start.z);
            var length = Math.Sqrt(dir.x * dir.x + dir.y * dir.y + dir.z * dir.z);

            // Create a thin box along the edge
            // For simplicity, just add two triangles
            // (In production, you'd want a proper cylinder or box mesh)
        }

        // Placeholder for structure outline
        private void AddStructureOutline(Model3DGroup modelGroup)
        {
            // Optional: Add structure mesh outline
            // This would require the structure mesh data
        }

        // Helper method to create a sphere mesh
        private MeshGeometry3D CreateSphere(VVector position, double radius)
        {
            var mesh = new MeshGeometry3D();
            int thetaDiv = 10;
            int phiDiv = 10;

            // Generate sphere vertices
            for (int theta = 0; theta <= thetaDiv; theta++)
            {
                double t = (double)theta / thetaDiv * Math.PI;

                for (int phi = 0; phi <= phiDiv; phi++)
                {
                    double p = (double)phi / phiDiv * 2 * Math.PI;

                    double x = position.x + radius * Math.Sin(t) * Math.Cos(p);
                    double y = position.y + radius * Math.Sin(t) * Math.Sin(p);
                    double z = position.z + radius * Math.Cos(t);

                    mesh.Positions.Add(new Point3D(x, y, z));
                }
            }

            // Generate triangles
            for (int theta = 0; theta < thetaDiv; theta++)
            {
                for (int phi = 0; phi < phiDiv; phi++)
                {
                    int i1 = theta * (phiDiv + 1) + phi;
                    int i2 = i1 + 1;
                    int i3 = i1 + phiDiv + 1;
                    int i4 = i3 + 1;

                    mesh.TriangleIndices.Add(i1);
                    mesh.TriangleIndices.Add(i3);
                    mesh.TriangleIndices.Add(i2);

                    mesh.TriangleIndices.Add(i2);
                    mesh.TriangleIndices.Add(i3);
                    mesh.TriangleIndices.Add(i4);
                }
            }

            return mesh;
        }

        // Simple colored axes
        private void AddSimpleAxes(Model3DGroup modelGroup)
        {
            // X-axis - Red line
            var xLine = CreateLine(new Point3D(-50, 0, 0), new Point3D(50, 0, 0), 2);
            modelGroup.Children.Add(new GeometryModel3D(xLine,
                new DiffuseMaterial(new SolidColorBrush(Colors.Red))));

            // Y-axis - Green line
            var yLine = CreateLine(new Point3D(0, -50, 0), new Point3D(0, 50, 0), 2);
            modelGroup.Children.Add(new GeometryModel3D(yLine,
                new DiffuseMaterial(new SolidColorBrush(Colors.Green))));

            // Z-axis - Blue line
            var zLine = CreateLine(new Point3D(0, 0, -50), new Point3D(0, 0, 50), 2);
            modelGroup.Children.Add(new GeometryModel3D(zLine,
                new DiffuseMaterial(new SolidColorBrush(Colors.Blue))));
        }

        private MeshGeometry3D CreateLine(Point3D start, Point3D end, double thickness)
        {
            var mesh = new MeshGeometry3D();

            // Simple line as two triangles
            mesh.Positions.Add(start);
            mesh.Positions.Add(end);
            mesh.Positions.Add(new Point3D(start.X + thickness, start.Y, start.Z));
            mesh.Positions.Add(new Point3D(end.X + thickness, end.Y, end.Z));

            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(1);
            mesh.TriangleIndices.Add(2);

            mesh.TriangleIndices.Add(1);
            mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(2);

            return mesh;
        }

        // Add this class near the top with your other helper classes
        public class MeshData
        {
            public List<Point3D> Positions { get; set; } = new List<Point3D>();
            public List<int> TriangleIndices { get; set; } = new List<int>();
        }

        // Simplified isodose surface creation (we'll improve this later)
        private MeshGeometry3D CreateIsodoseSurface(DoseGrid3D grid, double isovalue)
        {
            // For now, return null - we'll implement marching cubes later
            // This is complex, so we'll start with just peaks/valleys
            return null;
        }

        // Add this test method to verify Helix is working
        private void Test3DVisualization()
        {
            try
            {
                OutputLog += "Testing Helix Toolkit 3D visualization...\n";

                // This will test if we can access Helix components
                var testPoint = new System.Windows.Media.Media3D.Point3D(0, 0, 0);
                var testVector = new System.Windows.Media.Media3D.Vector3D(1, 0, 0);

                OutputLog += $"3D Point created: ({testPoint.X}, {testPoint.Y}, {testPoint.Z})\n";
                OutputLog += "Helix Toolkit is properly installed!\n";
            }
            catch (Exception ex)
            {
                OutputLog += $"Error testing Helix: {ex.Message}\n";
            }
        }

    }

}


