using MAAS_SFRThelper.Models;
using MAAS_SFRThelper.Services;
using MAAS_SFRThelper.Utilities;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows;
using System.Text;
using System.ComponentModel;

namespace MAAS_SFRThelper.ViewModels
{
    public class OptimizationViewModel : BindableBase
    {
        #region Private Fields
        private EsapiWorker _esapiWorker;
        private PlanSetup _plan;
        private StructureSet _structureSet;
        #endregion

        #region Output and Progress Properties

        private string _output;
        public string Output
        {
            get { return _output; }
            set { SetProperty(ref _output, value); }
        }

        private double _progressValue;
        public double ProgressValue
        {
            get { return _progressValue; }
            set { SetProperty(ref _progressValue, value); }
        }

        #endregion

        #region Validation Properties

        private string _validationText;
        public string ValidationText
        {
            get { return _validationText; }
            set { SetProperty(ref _validationText, value); }
        }

        private bool _validationVisible;
        public bool ValidationVisible
        {
            get { return _validationVisible; }
            set { SetProperty(ref _validationVisible, value); }
        }

        #endregion

        #region Geometric Analysis Properties

        /// <summary>
        /// List of available OARs for geometric analysis (with checkbox selection)
        /// </summary>
        private ObservableCollection<OARInfo> _availableOARs;
        public ObservableCollection<OARInfo> AvailableOARs
        {
            get => _availableOARs;
            set => SetProperty(ref _availableOARs, value);
        }

        /// <summary>
        /// Results from geometric surrogate calculation
        /// </summary>
        private GeometricResults _geometricResults;
        public GeometricResults GeometricResults
        {
            get => _geometricResults;
            set => SetProperty(ref _geometricResults, value);
        }

        /// <summary>
        /// Flag indicating if geometric calculation is in progress
        /// </summary>
        private bool _isCalculatingMetrics;
        public bool IsCalculatingMetrics
        {
            get => _isCalculatingMetrics;
            set
            {
                SetProperty(ref _isCalculatingMetrics, value);
                CalculateGeometricMetricsCommand?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Summary text for geometric results display
        /// </summary>
        private string _geometricResultsSummary;
        public string GeometricResultsSummary
        {
            get => _geometricResultsSummary;
            set => SetProperty(ref _geometricResultsSummary, value);
        }

        #endregion

        #region Grid Search Properties

        /// <summary>
        /// Search range as percentage of target radius (default 20%)
        /// </summary>
        private double _gridSearchRangePercent = 20.0;
        public double GridSearchRangePercent
        {
            get => _gridSearchRangePercent;
            set => SetProperty(ref _gridSearchRangePercent, value);
        }

        /// <summary>
        /// Display of search range in mm (calculated from percentage)
        /// </summary>
        private string _gridSearchRangeDisplay = "";
        public string GridSearchRangeDisplay
        {
            get => _gridSearchRangeDisplay;
            set => SetProperty(ref _gridSearchRangeDisplay, value);
        }

        /// <summary>
        /// Number of steps per axis (3, 5, 7, 9)
        /// </summary>
        private int _gridSearchSteps = 5;
        public int GridSearchSteps
        {
            get => _gridSearchSteps;
            set => SetProperty(ref _gridSearchSteps, value);
        }

        /// <summary>
        /// Available step options
        /// </summary>
        public List<int> AvailableGridSearchSteps { get; } = new List<int> { 3, 5, 7, 9 };

        /// <summary>
        /// Grid search results
        /// </summary>
        private GridSearchResult _gridSearchResult;
        public GridSearchResult GridSearchResult
        {
            get => _gridSearchResult;
            set
            {
                SetProperty(ref _gridSearchResult, value);
                RaisePropertyChanged(nameof(HasGridSearchResults));
                RaisePropertyChanged(nameof(GridSearchResultsSummary));
                RaisePropertyChanged(nameof(ShowBestFullOption));
                ApplyBestOverallCommand?.RaiseCanExecuteChanged();
                ApplyBestFullCountCommand?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Flag indicating grid search has results
        /// </summary>
        public bool HasGridSearchResults => GridSearchResult != null && GridSearchResult.Success;

        /// <summary>
        /// Flag indicating whether to show the "Best Full Count" option
        /// </summary>
        public bool ShowBestFullOption => HasGridSearchResults && GridSearchResult.HasDifferentBestOptions;

        /// <summary>
        /// Summary text for grid search results
        /// </summary>
        public string GridSearchResultsSummary
        {
            get
            {
                if (GridSearchResult == null)
                    return "Click 'Run Grid Search' to find optimal position...";
                return GridSearchResult.GetSummary();
            }
        }

        /// <summary>
        /// Flag indicating grid search is running
        /// </summary>
        private bool _isRunningGridSearch;
        public bool IsRunningGridSearch
        {
            get => _isRunningGridSearch;
            set
            {
                SetProperty(ref _isRunningGridSearch, value);
                RunGridSearchCommand?.RaiseCanExecuteChanged();
                CalculateGeometricMetricsCommand?.RaiseCanExecuteChanged();
            }
        }

        // Simple double to store target radius for display (no complex object)
        private double _targetRadius = 0;

        #endregion

        #region Structure Selection Properties

        private List<string> _availableLatticeStructures;
        public List<string> AvailableLatticeStructures
        {
            get { return _availableLatticeStructures; }
            set { SetProperty(ref _availableLatticeStructures, value); }
        }

        private string _selectedLatticeStructure;
        public string SelectedLatticeStructure
        {
            get { return _selectedLatticeStructure; }
            set
            {
                SetProperty(ref _selectedLatticeStructure, value);
                OnLatticeStructureChanged();
                PopulateObjectivesCommand?.RaiseCanExecuteChanged();
                CreateObjectivesCommand?.RaiseCanExecuteChanged();
                CreateValleyStructureCommand?.RaiseCanExecuteChanged();
                TestSphereExtractionCommand?.RaiseCanExecuteChanged();
                CalculateGeometricMetricsCommand?.RaiseCanExecuteChanged();
            }
        }

        private List<string> _availableValleyStructures;
        public List<string> AvailableValleyStructures
        {
            get { return _availableValleyStructures; }
            set { SetProperty(ref _availableValleyStructures, value); }
        }

        private string _selectedValleyStructure;
        public string SelectedValleyStructure
        {
            get { return _selectedValleyStructure; }
            set
            {
                SetProperty(ref _selectedValleyStructure, value);
                PTVSelectionVisible = (value == "[Auto-create Valley]");
                CreateValleyButtonVisible = (value == "[Auto-create Valley]");
                OnValleyStructureChanged();
                PopulateObjectivesCommand?.RaiseCanExecuteChanged();
                CreateObjectivesCommand?.RaiseCanExecuteChanged();
                CreateValleyStructureCommand?.RaiseCanExecuteChanged();
            }
        }

        private List<string> _availablePTVStructures;
        public List<string> AvailablePTVStructures
        {
            get { return _availablePTVStructures; }
            set { SetProperty(ref _availablePTVStructures, value); }
        }

        private string _selectedPTVStructure;
        public string SelectedPTVStructure
        {
            get { return _selectedPTVStructure; }
            set
            {
                SetProperty(ref _selectedPTVStructure, value);
                PopulateObjectivesCommand?.RaiseCanExecuteChanged();
                CreateObjectivesCommand?.RaiseCanExecuteChanged();
                CreateValleyStructureCommand?.RaiseCanExecuteChanged();
                CalculateGeometricMetricsCommand?.RaiseCanExecuteChanged();
                RunGridSearchCommand?.RaiseCanExecuteChanged();
            }
        }

        private bool _ptvSelectionVisible;
        public bool PTVSelectionVisible
        {
            get { return _ptvSelectionVisible; }
            set { SetProperty(ref _ptvSelectionVisible, value); }
        }

        private bool _createValleyButtonVisible;
        public bool CreateValleyButtonVisible
        {
            get { return _createValleyButtonVisible; }
            set { SetProperty(ref _createValleyButtonVisible, value); }
        }

        #endregion

        #region Optimization Settings Properties

        private List<string> _availableMLCs;
        public List<string> AvailableMLCs
        {
            get { return _availableMLCs; }
            set { SetProperty(ref _availableMLCs, value); }
        }

        private string _selectedMLC;
        public string SelectedMLC
        {
            get { return _selectedMLC; }
            set
            {
                SetProperty(ref _selectedMLC, value);
                RunOptimizationCommand?.RaiseCanExecuteChanged();
            }
        }

        private bool _useIntermediateDose = true;
        public bool UseIntermediateDose
        {
            get { return _useIntermediateDose; }
            set { SetProperty(ref _useIntermediateDose, value); }
        }

        private bool _isOptimizing;
        public bool IsOptimizing
        {
            get { return _isOptimizing; }
            set
            {
                SetProperty(ref _isOptimizing, value);
                RunOptimizationCommand?.RaiseCanExecuteChanged();
                CreateObjectivesCommand?.RaiseCanExecuteChanged();
                PopulateObjectivesCommand?.RaiseCanExecuteChanged();
                CalculateDoseCommand?.RaiseCanExecuteChanged();
            }
        }

        private bool _optimizationCompleted;
        public bool OptimizationCompleted
        {
            get { return _optimizationCompleted; }
            set
            {
                SetProperty(ref _optimizationCompleted, value);
                CalculateDoseCommand?.RaiseCanExecuteChanged();
            }
        }

        private ObservableCollection<BeamSelectionItem> _availableBeams;
        public ObservableCollection<BeamSelectionItem> AvailableBeams
        {
            get { return _availableBeams; }
            set { SetProperty(ref _availableBeams, value); }
        }

        private bool _hasVMATArcs;
        public bool HasVMATArcs
        {
            get { return _hasVMATArcs; }
            set
            {
                SetProperty(ref _hasVMATArcs, value);
                RunOptimizationCommand?.RaiseCanExecuteChanged();
            }
        }

        private string _beamWarningText;
        public string BeamWarningText
        {
            get { return _beamWarningText; }
            set { SetProperty(ref _beamWarningText, value); }
        }

        #endregion

        #region Optimization Objectives Properties

        private ObservableCollection<ObjectiveDefinition> _objectives;
        public ObservableCollection<ObjectiveDefinition> Objectives
        {
            get { return _objectives; }
            set { SetProperty(ref _objectives, value); }
        }

        private ObjectiveDefinition _selectedObjective;
        public ObjectiveDefinition SelectedObjective
        {
            get { return _selectedObjective; }
            set
            {
                SetProperty(ref _selectedObjective, value);
                RemoveObjectiveCommand?.RaiseCanExecuteChanged();
            }
        }

        public List<OptimizationObjectiveOperator> AvailableOperators
        {
            get
            {
                return new List<OptimizationObjectiveOperator>
                {
                    OptimizationObjectiveOperator.Upper,
                    OptimizationObjectiveOperator.Lower
                };
            }
        }

        public List<string> AvailableObjectiveTypes
        {
            get { return new List<string> { "Point", "Mean" }; }
        }

        #endregion

        #region Commands

        public DelegateCommand PopulateObjectivesCommand { get; set; }
        public DelegateCommand CreateObjectivesCommand { get; set; }
        public DelegateCommand RemoveObjectiveCommand { get; set; }
        public DelegateCommand AddObjectiveCommand { get; set; }
        public DelegateCommand ResetToDefaultsCommand { get; set; }
        public DelegateCommand CreateValleyStructureCommand { get; set; }
        public DelegateCommand RunOptimizationCommand { get; set; }
        public DelegateCommand CalculateDoseCommand { get; set; }
        public DelegateCommand TestSphereExtractionCommand { get; set; }
        public DelegateCommand CalculateGeometricMetricsCommand { get; set; }
        public DelegateCommand RunGridSearchCommand { get; set; }
        public DelegateCommand ApplyBestOverallCommand { get; set; }
        public DelegateCommand ApplyBestFullCountCommand { get; set; }

        #endregion

        #region Constructor

        public OptimizationViewModel(EsapiWorker esapi)
        {
            _esapiWorker = esapi;

            _esapiWorker.RunWithWait(sc =>
            {
                _plan = sc.PlanSetup;
                _structureSet = sc.StructureSet;
            });

            Output = "Optimization module ready.\n1. Select Peak and Valley structures\n2. If needed, click 'Create Valley' button\n3. Click 'Populate Objectives' to fill table\n4. Edit objectives as needed\n5. Click 'Create Objectives' to apply to Eclipse\n6. Select beams and click 'Run Optimization'\n7. Click 'Calculate Dose' when ready (can take 1+ hour)";
            ProgressValue = 0;
            ValidationVisible = false;
            PTVSelectionVisible = false;
            CreateValleyButtonVisible = false;
            OptimizationCompleted = false;

            Objectives = new ObservableCollection<ObjectiveDefinition>();
            AvailableBeams = new ObservableCollection<BeamSelectionItem>();
            AvailableOARs = new ObservableCollection<OARInfo>();

            PopulateObjectivesCommand = new DelegateCommand(OnPopulateObjectives, CanPopulateObjectives);
            CreateObjectivesCommand = new DelegateCommand(OnCreateObjectives, CanCreateObjectives);
            RemoveObjectiveCommand = new DelegateCommand(OnRemoveObjective, CanRemoveObjective);
            AddObjectiveCommand = new DelegateCommand(OnAddObjective);
            ResetToDefaultsCommand = new DelegateCommand(OnResetToDefaults);
            CreateValleyStructureCommand = new DelegateCommand(OnCreateValleyStructure, CanCreateValleyStructure);
            RunOptimizationCommand = new DelegateCommand(OnRunOptimization, CanRunOptimization);
            CalculateDoseCommand = new DelegateCommand(OnCalculateDose, CanCalculateDose);
            TestSphereExtractionCommand = new DelegateCommand(OnTestSphereExtraction, CanTestSphereExtraction);
            CalculateGeometricMetricsCommand = new DelegateCommand(OnCalculateGeometricMetrics, CanCalculateGeometricMetrics);
            RunGridSearchCommand = new DelegateCommand(OnRunGridSearch, CanRunGridSearch);
            ApplyBestOverallCommand = new DelegateCommand(OnApplyBestOverall, CanApplyBestOverall);
            ApplyBestFullCountCommand = new DelegateCommand(OnApplyBestFullCount, CanApplyBestFullCount);

            PopulateStructureLists();
        }

        #endregion

        #region Dropdown Population

        private void PopulateStructureLists()
        {
            // Capture dispatcher BEFORE entering worker thread
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

            // Use async Run() to avoid blocking UI when Eclipse shows dialogs
            _esapiWorker.Run(sc =>
            {
                // Collect all data first - don't update UI properties directly from worker thread!
                var latticeList = OptimizationObjectiveCreator.GetAvailableLatticeStructures(sc.StructureSet);
                var valleyList = OptimizationObjectiveCreator.GetAvailableValleyStructures(sc.StructureSet);
                var ptvList = OptimizationObjectiveCreator.GetAvailablePTVStructures(sc.StructureSet);

                // Determine auto-selections (but don't set properties yet)
                string autoSelectLattice = (latticeList != null && latticeList.Count == 1) ? latticeList[0] : null;

                string autoSelectValley = null;
                if (valleyList != null && valleyList.Count > 0)
                {
                    var valleyOpt = valleyList.FirstOrDefault(v => v.StartsWith("Valley_", StringComparison.OrdinalIgnoreCase));
                    if (valleyOpt != null)
                        autoSelectValley = valleyOpt;
                    else if (valleyList.Contains("coreVoid"))
                        autoSelectValley = "coreVoid";
                    else if (valleyList.Contains("Voids"))
                        autoSelectValley = "Voids";
                    else if (valleyList.Contains("Valley"))
                        autoSelectValley = "Valley";
                    else
                        autoSelectValley = "[Auto-create Valley]";
                }

                string autoSelectPTV = (ptvList != null && ptvList.Count == 1) ? ptvList[0] : null;

                // Populate MLCs from plan beams
                var mlcs = sc.PlanSetup.Beams
                    .Where(b => !b.IsSetupField && b.MLC != null)
                    .Select(b => b.MLC.Id)
                    .Distinct()
                    .ToList();

                string autoSelectMLC = mlcs.Count >= 1 ? mlcs[0] : null;

                // Populate beam list
                var beamItems = new List<BeamSelectionItem>();
                bool hasVMAT = false;

                foreach (var beam in sc.PlanSetup.Beams.Where(b => !b.IsSetupField))
                {
                    bool isVMAT = beam.MLCPlanType == MLCPlanType.VMAT;
                    if (isVMAT) hasVMAT = true;

                    string description;
                    try
                    {
                        description = isVMAT
                            ? $"{beam.GantryDirection} {beam.ControlPoints.First().GantryAngle:F0}° - {beam.ControlPoints.Last().GantryAngle:F0}°"
                            : $"Gantry {beam.ControlPoints.First().GantryAngle:F0}°";
                    }
                    catch
                    {
                        description = "Unknown";
                    }

                    beamItems.Add(new BeamSelectionItem
                    {
                        BeamId = beam.Id,
                        Description = description,
                        Technique = beam.Technique?.Id ?? "Unknown",
                        IsVMAT = isVMAT,
                        IsSelected = isVMAT
                    });
                }

                string beamWarning = !hasVMAT ? "⚠ No VMAT arcs found. Please create VMAT arcs in Eclipse before optimizing." : "";

                // Populate OAR list
                var oarStructures = sc.StructureSet.Structures
                    .Where(s => !s.IsEmpty &&
                                !s.Id.ToUpper().Contains("PTV") &&
                                !s.Id.ToUpper().Contains("GTV") &&
                                !s.Id.ToUpper().Contains("CTV") &&
                                !s.Id.ToUpper().Contains("LATTICE") &&
                                !s.Id.ToUpper().Contains("CVT") &&
                                !s.Id.ToUpper().Contains("SPHERE") &&
                                !s.Id.ToUpper().Contains("VALLEY") &&
                                !s.Id.ToUpper().Contains("BODY") &&
                                !s.Id.ToUpper().Contains("EXTERNAL") &&
                                !s.Id.ToUpper().Contains("COUCH") &&
                                !s.Id.ToUpper().Contains("SUPPORT") &&
                                s.DicomType != "EXTERNAL" &&
                                s.DicomType != "SUPPORT")
                    .OrderBy(s => s.Id)
                    .ToList();

                var oarList = new List<OARInfo>();
                foreach (var structure in oarStructures)
                {
                    var oarInfo = OARInfo.FromStructure(structure);
                    if (oarInfo != null)
                    {
                        string upperName = oarInfo.Name.ToUpper();
                        oarInfo.IsSelected = upperName.Contains("CORD") ||
                                             upperName.Contains("BRAIN") ||
                                             upperName.Contains("STEM") ||
                                             upperName.Contains("CHIASM") ||
                                             upperName.Contains("NERVE") ||
                                             upperName.Contains("EYE") ||
                                             upperName.Contains("PAROTID") ||
                                             upperName.Contains("ESOPH") ||
                                             upperName.Contains("HEART") ||
                                             upperName.Contains("KIDNEY") ||
                                             upperName.Contains("LIVER") ||
                                             upperName.Contains("BOWEL") ||
                                             upperName.Contains("RECTUM") ||
                                             upperName.Contains("BLADDER") ||
                                             upperName.Contains("FEMUR") ||
                                             upperName.Contains("OAR");
                        oarList.Add(oarInfo);
                    }
                }

                // Log output (Output property handles threading internally)
                if (!hasVMAT)
                    Output += "\n⚠ WARNING: No VMAT arcs found in plan";

                Output += $"\nFound {latticeList?.Count ?? 0} lattice structure(s)";
                Output += $"\nFound {valleyList?.Count ?? 0} valley structure option(s)";
                Output += $"\nFound {ptvList?.Count ?? 0} PTV structure(s)";
                Output += $"\nFound {mlcs.Count} MLC(s)";
                Output += $"\nFound {beamItems.Count} beam(s) ({beamItems.Count(b => b.IsVMAT)} VMAT)";
                Output += $"Found {oarList.Count} potential OAR(s) for geometric analysis\n";

                // Update ALL UI properties via dispatcher (CRITICAL - avoid threading errors)
                dispatcher.BeginInvoke(new Action(() =>
                {
                    // Update list properties
                    AvailableLatticeStructures = latticeList;
                    AvailableValleyStructures = valleyList;
                    AvailablePTVStructures = ptvList;
                    AvailableMLCs = mlcs;
                    HasVMATArcs = hasVMAT;
                    BeamWarningText = beamWarning;

                    // Update selected items (triggers PropertyChanged safely on UI thread)
                    if (autoSelectLattice != null)
                        SelectedLatticeStructure = autoSelectLattice;
                    if (autoSelectValley != null)
                        SelectedValleyStructure = autoSelectValley;
                    if (autoSelectPTV != null)
                        SelectedPTVStructure = autoSelectPTV;
                    if (autoSelectMLC != null)
                        SelectedMLC = autoSelectMLC;

                    // Update ObservableCollections
                    AvailableBeams.Clear();
                    foreach (var beam in beamItems)
                    {
                        AvailableBeams.Add(beam);
                    }

                    AvailableOARs.Clear();
                    foreach (var oar in oarList)
                    {
                        AvailableOARs.Add(oar);
                    }
                }));
            });
        }

        #endregion

        #region Structure Selection Handlers

        private void OnLatticeStructureChanged()
        {
            if (string.IsNullOrEmpty(SelectedLatticeStructure))
                return;

            Output += $"\n✓ Selected lattice structure: {SelectedLatticeStructure}";

            // Clear grid search results when lattice changes
            GridSearchResult = null;
        }

        private void OnValleyStructureChanged()
        {
            if (string.IsNullOrEmpty(SelectedValleyStructure) || SelectedValleyStructure == "[Auto-create Valley]")
                return;

            Output += $"\n✓ Selected valley structure: {SelectedValleyStructure}";
        }

        #endregion

        #region Create Valley Structure

        private bool CanCreateValleyStructure()
        {
            return SelectedValleyStructure == "[Auto-create Valley]"
                   && !string.IsNullOrEmpty(SelectedLatticeStructure)
                   && !string.IsNullOrEmpty(SelectedPTVStructure);
        }

        private void OnCreateValleyStructure()
        {
            Output += "\n\n=== Creating Valley Structure ===";

            string latticeId = SelectedLatticeStructure;
            string ptvId = SelectedPTVStructure;

            _esapiWorker.Run(sc =>
            {
                try
                {
                    sc.Patient.BeginModifications();

                    var externalPlan = sc.PlanSetup as ExternalPlanSetup;
                    if (externalPlan == null)
                    {
                        Output += "\nERROR: Plan is not an external beam plan!";
                        return;
                    }

                    Output += $"\nCreating Valley = {ptvId} - {latticeId}";

                    var creator = new OptimizationObjectiveCreator(externalPlan, sc.StructureSet);
                    var valleyResult = creator.CreateValleyStructure(latticeId, ptvId);

                    Output += valleyResult.message;

                    if (valleyResult.structure != null)
                    {
                        Output += $"\n✓ Valley structure created: {valleyResult.structure.Volume:F2} cc";
                        Output += "\n✓ Valley is now available in structure set";
                        Output += "\n\nNext: Click 'Populate Objectives' to fill the table";
                    }
                    else
                    {
                        Output += "\n✗ Valley structure creation failed";
                    }
                }
                catch (Exception ex)
                {
                    Output += $"\n\nERROR: {ex.Message}";
                }
            });
        }

        #endregion

        #region Populate Objectives Command

        private bool CanPopulateObjectives()
        {
            if (IsOptimizing)
                return false;

            if (_plan == null || _structureSet == null)
            {
                ValidationText = "No plan or structure set loaded";
                ValidationVisible = true;
                return false;
            }

            if (string.IsNullOrEmpty(SelectedLatticeStructure))
            {
                ValidationText = "Please select a lattice structure";
                ValidationVisible = true;
                return false;
            }

            ValidationVisible = false;
            return true;
        }

        private void OnPopulateObjectives()
        {
            Output += "\n\n=== Populating Objectives Table ===";

            // Capture selected structures and dispatcher BEFORE entering worker
            string selectedLattice = SelectedLatticeStructure;
            string selectedValley = SelectedValleyStructure;
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

            // Use async Run() to avoid blocking UI
            _esapiWorker.Run(sc =>
            {
                try
                {
                    // Collect all data in worker thread - don't update UI properties directly!
                    var latticeList = OptimizationObjectiveCreator.GetAvailableLatticeStructures(sc.StructureSet);
                    var valleyList = OptimizationObjectiveCreator.GetAvailableValleyStructures(sc.StructureSet);

                    Output += $"\nFound {latticeList?.Count ?? 0} lattice structure(s)";
                    Output += $"\nFound {valleyList?.Count ?? 0} valley structure(s)";

                    // Determine which lattice to use - prefer _Opt version
                    string latticeToUse = selectedLattice;
                    string newSelectedLattice = null;  // Will be set via dispatcher

                    if (!string.IsNullOrEmpty(selectedLattice))
                    {
                        string optVersion = latticeList?.FirstOrDefault(l =>
                            l.StartsWith(selectedLattice.Substring(0, Math.Min(10, selectedLattice.Length))) &&
                            l.Contains("_Opt"));
                        if (optVersion != null)
                        {
                            latticeToUse = optVersion;
                            newSelectedLattice = optVersion;
                            Output += $"\n  → Using optimized lattice: {optVersion}";
                        }
                    }

                    var template = OptimizationTemplate.CreateStandardProstateTemplate();
                    var defaultConstraints = ProstateOARConstraints.GetConstraints();
                    var objectivesList = new List<ObjectiveDefinition>();

                    // Add Peak objectives for selected lattice
                    if (!string.IsNullOrEmpty(latticeToUse))
                    {
                        objectivesList.Add(new ObjectiveDefinition
                        {
                            StructureName = latticeToUse,
                            ObjectiveType = "Point",
                            Operator = OptimizationObjectiveOperator.Lower,
                            Dose = template.PeakLowerDose,
                            Volume = template.PeakLowerVolume,
                            Priority = template.PeakLowerPriority,
                            Role = "Peak",
                            IsIncluded = true
                        });

                        objectivesList.Add(new ObjectiveDefinition
                        {
                            StructureName = latticeToUse,
                            ObjectiveType = "Mean",
                            Operator = OptimizationObjectiveOperator.Lower,
                            Dose = template.PeakMeanDose,
                            Volume = 0,
                            Priority = template.PeakMeanPriority,
                            Role = "Peak",
                            IsIncluded = true
                        });

                        Output += $"\n  ✓ Added Peak objectives for {latticeToUse}";
                    }

                    // Find valley structure - prefer Valley_Opt if it exists
                    string valleyToUse = null;
                    string newSelectedValley = null;  // Will be set via dispatcher

                    var valleyStructure = sc.StructureSet.Structures.FirstOrDefault(s =>
                        s.Id.StartsWith("Valley_Opt", StringComparison.OrdinalIgnoreCase));

                    if (valleyStructure == null && !string.IsNullOrEmpty(selectedValley) &&
                        selectedValley != "[Auto-create Valley]")
                    {
                        valleyStructure = sc.StructureSet.Structures.FirstOrDefault(s =>
                            s.Id.Equals(selectedValley, StringComparison.OrdinalIgnoreCase));
                    }

                    if (valleyStructure == null)
                    {
                        valleyStructure = sc.StructureSet.Structures.FirstOrDefault(s =>
                            s.Id.StartsWith("Valley", StringComparison.OrdinalIgnoreCase) ||
                            s.Id.Equals("Voids", StringComparison.OrdinalIgnoreCase) ||
                            s.Id.Equals("coreVoid", StringComparison.OrdinalIgnoreCase));
                    }

                    if (valleyStructure != null)
                    {
                        valleyToUse = valleyStructure.Id;
                        newSelectedValley = valleyStructure.Id;

                        objectivesList.Add(new ObjectiveDefinition
                        {
                            StructureName = valleyStructure.Id,
                            ObjectiveType = "Point",
                            Operator = OptimizationObjectiveOperator.Upper,
                            Dose = template.ValleyUpperDose,
                            Volume = template.ValleyUpperVolume,
                            Priority = template.ValleyUpperPriority,
                            Role = "Valley",
                            IsIncluded = true
                        });

                        objectivesList.Add(new ObjectiveDefinition
                        {
                            StructureName = valleyStructure.Id,
                            ObjectiveType = "Mean",
                            Operator = OptimizationObjectiveOperator.Upper,
                            Dose = template.ValleyMeanDose,
                            Volume = 0,
                            Priority = template.ValleyMeanPriority,
                            Role = "Valley",
                            IsIncluded = true
                        });

                        Output += $"\n  ✓ Added Valley objectives for {valleyStructure.Id}";
                    }
                    else
                    {
                        Output += "\n  ⚠ WARNING: Valley structure not found, skipping valley objectives";
                    }

                    // Add OAR objectives
                    var structures = sc.StructureSet.Structures
                        .Where(s => !s.IsEmpty)
                        .OrderBy(s => s.Id)
                        .ToList();

                    int oarCount = 0;
                    foreach (var structure in structures)
                    {
                        var skipPatterns = new[] { "Lattice", "CVT", "Sphere", "Valley", "Void" };
                        if (skipPatterns.Any(p => structure.Id.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0))
                            continue;

                        var matchedDefault = defaultConstraints.FirstOrDefault(dc =>
                            dc.NameVariations.Any(nv =>
                                structure.Id.Equals(nv, StringComparison.OrdinalIgnoreCase) ||
                                structure.Id.IndexOf(nv, StringComparison.OrdinalIgnoreCase) >= 0));

                        if (matchedDefault != null)
                        {
                            objectivesList.Add(new ObjectiveDefinition
                            {
                                StructureName = structure.Id,
                                ObjectiveType = "Point",
                                Operator = matchedDefault.Operator,
                                Dose = matchedDefault.MaxDoseGy,
                                Volume = matchedDefault.VolumePercent,
                                Priority = matchedDefault.Priority,
                                Role = "OAR",
                                IsIncluded = true
                            });
                            oarCount++;
                        }
                        else
                        {
                            objectivesList.Add(new ObjectiveDefinition
                            {
                                StructureName = structure.Id,
                                ObjectiveType = "Point",
                                Operator = OptimizationObjectiveOperator.Upper,
                                Dose = 15.0,
                                Volume = 0,
                                Priority = 100,
                                Role = "OAR",
                                IsIncluded = false
                            });
                        }
                    }

                    Output += $"\n  ✓ Added {oarCount} OAR objectives";
                    Output += $"\n\n✓ Total: {objectivesList.Count} objectives ready";

                    // Update ALL UI properties via dispatcher (CRITICAL - avoid threading errors)
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        // Update dropdown lists
                        AvailableLatticeStructures = latticeList;
                        AvailableValleyStructures = valleyList;

                        // Update selected items (this triggers property changed events safely on UI thread)
                        if (newSelectedLattice != null)
                            SelectedLatticeStructure = newSelectedLattice;
                        if (newSelectedValley != null)
                            SelectedValleyStructure = newSelectedValley;

                        // Update objectives collection
                        Objectives.Clear();
                        foreach (var obj in objectivesList)
                        {
                            Objectives.Add(obj);
                        }

                        Output += "\n✓ Objectives table populated";
                        Output += "\n\nReview and edit objectives as needed, then click 'Create Objectives' to apply to Eclipse.";

                        CreateObjectivesCommand?.RaiseCanExecuteChanged();
                    }));
                }
                catch (Exception ex)
                {
                    Output += $"\n\nERROR: {ex.Message}";
                    Output += $"\nStack trace: {ex.StackTrace}";
                }
            });
        }

        #endregion

        #region Create Objectives Command

        private bool CanCreateObjectives()
        {
            if (IsOptimizing)
                return false;

            if (_plan == null || _structureSet == null)
            {
                ValidationText = "No plan or structure set loaded";
                ValidationVisible = true;
                return false;
            }

            if (Objectives == null || Objectives.Count == 0)
            {
                ValidationText = "No objectives in table. Click 'Populate Objectives' first.";
                ValidationVisible = true;
                return false;
            }

            ValidationVisible = false;
            return true;
        }

        private void OnCreateObjectives()
        {
            Output += "\n\n=== Creating Optimization Objectives in Eclipse ===";

            var includedObjectives = Objectives.Where(o => o.IsIncluded).ToList();

            _esapiWorker.Run(sc =>
            {
                try
                {
                    sc.Patient.BeginModifications();

                    var externalPlan = sc.PlanSetup as ExternalPlanSetup;
                    if (externalPlan == null)
                    {
                        Output += "\nERROR: Plan is not an external beam plan!";
                        return;
                    }

                    Output += "\nClearing existing objectives...";
                    var existingObjectives = externalPlan.OptimizationSetup.Objectives.ToList();
                    foreach (var obj in existingObjectives)
                        externalPlan.OptimizationSetup.RemoveObjective(obj);
                    Output += $"\nCleared {existingObjectives.Count} existing objectives";

                    Output += $"\n\nCreating {includedObjectives.Count} objectives:";

                    int successCount = 0;
                    foreach (var objective in includedObjectives)
                    {
                        var structure = sc.StructureSet.Structures.FirstOrDefault(s =>
                            s.Id.Equals(objective.StructureName, StringComparison.OrdinalIgnoreCase));

                        if (structure == null)
                        {
                            Output += $"\n  ✗ {objective.StructureName}: Structure not found";
                            continue;
                        }

                        try
                        {
                            if (objective.ObjectiveType == "Point")
                            {
                                externalPlan.OptimizationSetup.AddPointObjective(
                                    structure,
                                    objective.Operator,
                                    new DoseValue(objective.Dose, DoseValue.DoseUnit.Gy),
                                    objective.Volume,
                                    objective.Priority);

                                Output += $"\n  ✓ {structure.Id}: {objective.Operator} {objective.Dose} Gy @ {objective.Volume}%";
                            }
                            else if (objective.ObjectiveType == "Mean")
                            {
                                externalPlan.OptimizationSetup.AddMeanDoseObjective(
                                    structure,
                                    new DoseValue(objective.Dose, DoseValue.DoseUnit.Gy),
                                    objective.Priority);

                                Output += $"\n  ✓ {structure.Id}: Mean {objective.Dose} Gy";
                            }

                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            Output += $"\n  ✗ {structure.Id}: {ex.Message}";
                        }
                    }

                    try
                    {
                        externalPlan.OptimizationSetup.AddAutomaticNormalTissueObjective(100);
                        Output += "\n  ✓ Automatic NTO added";
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Output += $"\n  ✗ NTO: {ex.Message}";
                    }

                    Output += $"\n\n✓ Created {successCount} objectives successfully";
                    Output += "\n\n=== Objective Creation Complete ===";
                    Output += "\nYou can now run optimization.";
                }
                catch (Exception ex)
                {
                    Output += $"\n\nERROR: {ex.Message}";
                    Output += $"\nStack trace: {ex.StackTrace}";
                }
            });

            RunOptimizationCommand?.RaiseCanExecuteChanged();
        }

        #endregion

        #region Run Optimization

        private bool CanRunOptimization()
        {
            if (IsOptimizing)
                return false;

            if (_plan == null)
                return false;

            if (string.IsNullOrEmpty(SelectedMLC))
                return false;

            if (!HasVMATArcs)
                return false;

            return true;
        }

        private void OnRunOptimization()
        {
            var selectedVMATBeams = AvailableBeams.Where(b => b.IsSelected && b.IsVMAT).ToList();

            if (selectedVMATBeams.Count == 0)
            {
                Output += "\n\nERROR: No VMAT beams selected for optimization.";
                Output += "\nPlease select at least one VMAT arc.";
                return;
            }

            IsOptimizing = true;
            _optimizationCompleted = false;  // Reset using backing field
            var startTime = DateTime.Now;
            Output += $"\n\n=== Running VMAT Optimization ===";
            Output += $"\nStarted at: {startTime:HH:mm:ss}";

            string mlcId = SelectedMLC;
            bool useIntermediate = UseIntermediateDose;

            var beamIdsToOptimize = selectedVMATBeams.Select(b => b.BeamId).ToList();

            Output += $"\nBeams to optimize: {string.Join(", ", beamIdsToOptimize)}";

            // CRITICAL: Capture the UI dispatcher BEFORE entering the worker
            var uiDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

            _esapiWorker.Run(sc =>
            {
                bool success = false;
                DateTime endTime = DateTime.Now;

                try
                {
                    sc.Patient.BeginModifications();

                    var externalPlan = sc.PlanSetup as ExternalPlanSetup;
                    if (externalPlan == null)
                    {
                        Output += "\nERROR: Not an external beam plan!";
                        return;
                    }

                    int objectiveCount = 0;
                    try
                    {
                        objectiveCount = externalPlan.OptimizationSetup.Objectives.Count();
                    }
                    catch
                    {
                        Output += "\nWARNING: Could not count objectives";
                    }

                    if (objectiveCount == 0)
                    {
                        Output += "\nERROR: No optimization objectives found in plan!";
                        Output += "\nPlease click 'Create Objectives' first.";
                        return;
                    }

                    Output += $"\nPlan: {externalPlan.Id}";
                    Output += $"\nMLC: {mlcId}";
                    Output += $"\nIntermediate Dose: {(useIntermediate ? "Yes" : "No")}";
                    Output += $"\nObjectives in plan: {objectiveCount}";
                    Output += $"\n\n[{DateTime.Now:HH:mm:ss}] Starting optimization...";

                    success = RunVMATOptimization(externalPlan, mlcId, useIntermediate);

                    endTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    try
                    {
                        Output += $"\n\n[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}";
                    }
                    catch { }
                }

                // Final status
                try
                {
                    var duration = endTime - startTime;

                    if (success)
                    {
                        Output += $"\n\n=== OPTIMIZATION COMPLETE ===";
                        Output += $"\nFinished at: {endTime:HH:mm:ss}";
                        Output += $"\nTotal time: {duration.TotalMinutes:F1} minutes";
                        Output += "\n\n→ Click 'Calculate Dose' when ready (can take 1+ hour)";
                        Output += "\n→ Or calculate dose manually in Eclipse";
                    }
                    else
                    {
                        Output += $"\n\nOptimization encountered issues.";
                        Output += $"\nElapsed time: {duration.TotalMinutes:F1} minutes";
                        Output += "\nCheck Eclipse for plan status.";
                    }
                }
                catch { }

                // Update UI on the UI thread using captured dispatcher
                try
                {
                    uiDispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            _isOptimizing = false;
                            RaisePropertyChanged(nameof(IsOptimizing));

                            if (_optimizationCompleted)
                            {
                                RaisePropertyChanged(nameof(OptimizationCompleted));
                            }

                            // Force command re-evaluation
                            RunOptimizationCommand?.RaiseCanExecuteChanged();
                            CalculateDoseCommand?.RaiseCanExecuteChanged();
                            CreateObjectivesCommand?.RaiseCanExecuteChanged();
                            PopulateObjectivesCommand?.RaiseCanExecuteChanged();
                        }
                        catch { }
                    }));
                }
                catch { }
            });
        }

        //private bool RunVMATOptimization(ExternalPlanSetup plan, string mlcId, bool useIntermediateDose)
        //{
        //    try
        //    {
        //        Output += $"\n  [{DateTime.Now:HH:mm:ss}] Calling OptimizeVMAT...";

        //        var intermediateOption = useIntermediateDose
        //            ? OptimizationIntermediateDoseOption.UseIntermediateDose
        //            : OptimizationIntermediateDoseOption.NoIntermediateDose;

        //        var options = new OptimizationOptionsVMAT(intermediateOption, mlcId);

        //        var result = plan.OptimizeVMAT(options);


        //        Output += $"\n  [{DateTime.Now:HH:mm:ss}] Optimization result: {result}";

        //        // Use backing field directly to avoid threading issues with RaiseCanExecuteChanged
        //        _optimizationCompleted = true;

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        try
        //        {
        //            Output += $"\n  [{DateTime.Now:HH:mm:ss}] Optimization failed: {ex.Message}";
        //        }
        //        catch { }
        //        return false;
        //    }
        //}

        private bool RunVMATOptimization(ExternalPlanSetup plan, string mlcId, bool useIntermediateDose)
        {
            try
            {
                // Run multiple optimization cycles
                int numberOfCycles = 5;

                for (int cycle = 1; cycle <= numberOfCycles; cycle++)
                {
                    Output += $"\n  [{DateTime.Now:HH:mm:ss}] Running optimization cycle {cycle}/{numberOfCycles}...";

                    try
                    {
                        // Try parameterless first
                        var result = plan.OptimizeVMAT();
                        Output += $" Done.";
                    }
                    catch
                    {
                        // Fallback to MLC-specified version
                        var result = plan.OptimizeVMAT(mlcId);
                        Output += $" Done (with MLC).";
                    }
                }

                _optimizationCompleted = true;
                return true;
            }
            catch (Exception ex)
            {
                Output += $"\n  [{DateTime.Now:HH:mm:ss}] Optimization failed: {ex.Message}";
                return false;
            }
        }

        #endregion

        #region Calculate Dose

        private bool CanCalculateDose()
        {
            if (IsOptimizing)
                return false;

            if (_plan == null)
                return false;

            if (!OptimizationCompleted)
                return false;

            return true;
        }

        private void OnCalculateDose()
        {
            IsOptimizing = true;
            var startTime = DateTime.Now;
            Output += $"\n\n=== Calculating Dose ===";
            Output += $"\nStarted at: {startTime:HH:mm:ss}";
            Output += "\n\n⚠ This may take 1+ hour for SFRT plans. Please wait...";

            string selectedLattice = SelectedLatticeStructure;

            _esapiWorker.Run(sc =>
            {
                bool success = false;
                DateTime endTime = DateTime.Now;

                try
                {
                    sc.Patient.BeginModifications();

                    var externalPlan = sc.PlanSetup as ExternalPlanSetup;
                    if (externalPlan == null)
                    {
                        Output += "\nERROR: Not an external beam plan!";
                        return;
                    }

                    Output += $"\n  [{DateTime.Now:HH:mm:ss}] Calculating dose...";

                    externalPlan.CalculateDose();

                    Output += $"\n  [{DateTime.Now:HH:mm:ss}] ✓ Dose calculation complete";
                    success = true;
                    endTime = DateTime.Now;

                    // Display DVH summary
                    try
                    {
                        DisplayDVHSummary(externalPlan, sc.StructureSet, selectedLattice);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            Output += $"\n  [{DateTime.Now:HH:mm:ss}] DVH summary failed: {ex.Message}";
                            Output += "\n  (You can view DVH in Eclipse)";
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        Output += $"\n\n[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}";
                        endTime = DateTime.Now;
                    }
                    catch { }
                }

                try
                {
                    var duration = endTime - startTime;

                    if (success)
                    {
                        Output += $"\n\n=== DOSE CALCULATION COMPLETE ===";
                        Output += $"\nFinished at: {endTime:HH:mm:ss}";
                        Output += $"\nTotal time: {duration.TotalMinutes:F1} minutes";
                        Output += "\n\nYou can now review the plan in Eclipse.";
                    }
                    else
                    {
                        Output += $"\n\nDose calculation encountered issues.";
                        Output += $"\nElapsed time: {duration.TotalMinutes:F1} minutes";
                        Output += "\nCheck Eclipse for plan status.";
                    }
                }
                catch { }
                finally
                {
                    try
                    {
                        IsOptimizing = false;
                    }
                    catch { }
                }
            });
        }

        private void DisplayDVHSummary(ExternalPlanSetup plan, StructureSet structureSet, string selectedLattice)
        {
            Output += "\n\n=== DVH Summary ===";

            try
            {
                var structuresToCheck = new List<string>();

                if (!string.IsNullOrEmpty(selectedLattice))
                    structuresToCheck.Add(selectedLattice);

                var valleyNames = new[] { "Valley", "Voids", "coreVoid" };
                foreach (var name in valleyNames)
                {
                    var valley = structureSet.Structures.FirstOrDefault(s =>
                        s.Id.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (valley != null && !valley.IsEmpty)
                    {
                        structuresToCheck.Add(valley.Id);
                        break;
                    }
                }

                var oarNames = new[] { "Rectum", "Bladder", "Bowel", "Femur_L", "Femur_R", "PTV" };
                foreach (var name in oarNames)
                {
                    var oar = structureSet.Structures.FirstOrDefault(s =>
                        s.Id.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 && !s.IsEmpty);
                    if (oar != null && !structuresToCheck.Contains(oar.Id))
                        structuresToCheck.Add(oar.Id);
                }

                foreach (var structureId in structuresToCheck)
                {
                    try
                    {
                        var structure = structureSet.Structures.FirstOrDefault(s =>
                            s.Id.Equals(structureId, StringComparison.OrdinalIgnoreCase));

                        if (structure == null || structure.IsEmpty)
                            continue;

                        var dvh = plan.GetDVHCumulativeData(structure,
                            DoseValuePresentation.Absolute,
                            VolumePresentation.Relative,
                            0.1);

                        if (dvh == null)
                            continue;

                        double dMax = dvh.MaxDose.Dose;
                        double dMean = dvh.MeanDose.Dose;
                        double dMin = dvh.MinDose.Dose;
                        string unit = dvh.MaxDose.UnitAsString;

                        Output += $"\n\n  {structure.Id}:";
                        Output += $"\n    Dmax:  {dMax:F2} {unit}";
                        Output += $"\n    Dmean: {dMean:F2} {unit}";
                        Output += $"\n    Dmin:  {dMin:F2} {unit}";

                        try
                        {
                            var d95 = plan.GetDoseAtVolume(structure, 95, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                            var d5 = plan.GetDoseAtVolume(structure, 5, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                            Output += $"\n    D95:   {d95.Dose:F2} {unit}";
                            Output += $"\n    D5:    {d5.Dose:F2} {unit}";
                        }
                        catch { }
                    }
                    catch { }
                }

                Output += "\n\n=== End DVH Summary ===";
            }
            catch (Exception ex)
            {
                Output += $"\n  Error generating DVH summary: {ex.Message}";
            }
        }

        #endregion

        #region Objective Management

        private void OnAddObjective()
        {
            ObjectiveDefinition newObjective;

            if (SelectedObjective != null)
            {
                newObjective = new ObjectiveDefinition
                {
                    StructureName = SelectedObjective.StructureName,
                    ObjectiveType = SelectedObjective.ObjectiveType,
                    Operator = SelectedObjective.Operator,
                    Dose = SelectedObjective.Dose,
                    Volume = SelectedObjective.Volume,
                    Priority = SelectedObjective.Priority,
                    Role = SelectedObjective.Role,
                    IsIncluded = true
                };

                int selectedIndex = Objectives.IndexOf(SelectedObjective);
                Objectives.Insert(selectedIndex + 1, newObjective);

                Output += $"\n✓ Added duplicate objective for {newObjective.StructureName}";
            }
            else
            {
                newObjective = new ObjectiveDefinition
                {
                    StructureName = "Select Structure",
                    ObjectiveType = "Point",
                    Operator = OptimizationObjectiveOperator.Upper,
                    Dose = 15.0,
                    Volume = 0,
                    Priority = 100,
                    Role = "OAR",
                    IsIncluded = true
                };

                Objectives.Add(newObjective);
                Output += "\n✓ Added new blank objective";
            }
        }

        private bool CanRemoveObjective()
        {
            return SelectedObjective != null;
        }

        private void OnRemoveObjective()
        {
            if (SelectedObjective == null)
                return;

            string name = SelectedObjective.StructureName;
            Objectives.Remove(SelectedObjective);
            Output += $"\n✓ Removed objective for '{name}'";
            SelectedObjective = null;
        }

        private void OnResetToDefaults()
        {
            Output += "\n\n=== Resetting ===";
            Objectives.Clear();
            OptimizationCompleted = false;
            GridSearchResult = null;
            Output += "\n✓ Objectives table cleared";
            Output += "\nClick 'Populate Objectives' to reload";
        }

        #endregion

        #region Sphere Extraction Test

        private bool CanTestSphereExtraction()
        {
            return !IsOptimizing && !string.IsNullOrEmpty(SelectedLatticeStructure);
        }

        private void OnTestSphereExtraction()
        {
            Output += "\n\n========================================";
            Output += "\n=== TESTING SPHERE EXTRACTION ===";
            Output += "\n========================================\n";

            string latticeId = SelectedLatticeStructure;
            Output += $"Selected structure: {latticeId}\n";

            // Use async Run() to avoid blocking UI
            _esapiWorker.Run(sc =>
            {
                try
                {
                    var latticeStructure = sc.StructureSet.Structures
                        .FirstOrDefault(s => s.Id.Equals(latticeId, StringComparison.OrdinalIgnoreCase));

                    if (latticeStructure == null)
                    {
                        Output += $"ERROR: Structure '{latticeId}' not found!\n";
                        return;
                    }

                    if (latticeStructure.IsEmpty)
                    {
                        Output += $"ERROR: Structure '{latticeId}' is empty!\n";
                        return;
                    }

                    Output += $"Structure found: {latticeStructure.Id}\n";
                    Output += $"  Volume: {latticeStructure.Volume:F2} cc\n";

                    var log = new StringBuilder();

                    Output += "\nRunning SphereExtractor...\n";
                    var extractor = new SphereExtractor();
                    var result = extractor.ExtractSpheres(latticeStructure, sc.Image, log);

                    Output += log.ToString();

                    Output += "\n--- RESULTS ---\n";
                    Output += $"Success: {result.Success}\n";
                    Output += $"Message: {result.Message}\n";
                    Output += $"Sphere Count (from ESAPI): {result.SphereCount}\n";
                    Output += $"Spheres Extracted: {result.Spheres.Count}\n";
                    Output += $"Mean Radius: {result.MeanRadius:F1} mm\n";
                    Output += $"Total Volume: {result.TotalVolume:F2} cc\n";

                    if (result.Success && result.Spheres.Count > 0)
                    {
                        Output += "\n✓ Sphere extraction successful!\n";
                    }
                    else
                    {
                        Output += "\n✗ Sphere extraction failed.\n";
                    }

                    Output += "\n========================================\n";
                }
                catch (Exception ex)
                {
                    Output += $"\nEXCEPTION: {ex.Message}\n";
                    Output += "\n========================================\n";
                }
            });
        }

        #endregion

        #region Geometric Analysis

        /// <summary>
        /// Populate the OAR list from structure set
        /// Call this after structure set is loaded
        /// </summary>
        private void PopulateOARList()
        {
            // OAR list is now populated inside PopulateStructureLists to avoid threading issues
            // This method is kept for backward compatibility but does nothing
        }

        /// <summary>
        /// Check if geometric metrics calculation can run
        /// </summary>
        private bool CanCalculateGeometricMetrics()
        {
            return !IsCalculatingMetrics &&
                   !IsOptimizing &&
                   !IsRunningGridSearch &&
                   !string.IsNullOrEmpty(SelectedLatticeStructure) &&
                   !string.IsNullOrEmpty(SelectedPTVStructure);
        }

        /// <summary>
        /// Calculate geometric surrogate metrics - FIXED THREADING
        /// </summary>
        private void OnCalculateGeometricMetrics()
        {
            IsCalculatingMetrics = true;
            GeometricResultsSummary = "Calculating geometric metrics...";
            Output += "\n========================================\n";
            Output += "=== GEOMETRIC SURROGATE ANALYSIS ===\n";
            Output += "========================================\n";

            string latticeId = SelectedLatticeStructure;
            string targetId = SelectedPTVStructure;

            var selectedOARNames = AvailableOARs
                .Where(o => o.IsSelected)
                .Select(o => o.Name)
                .ToList();

            Output += $"Lattice: {latticeId}\n";
            Output += $"Target: {targetId}\n";
            Output += $"OARs selected: {selectedOARNames.Count}\n";

            // CRITICAL: Capture the UI dispatcher BEFORE entering the worker
            var uiDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

            _esapiWorker.Run(sc =>
            {
                var log = new StringBuilder();
                GeometricResults results = null;
                double targetRadius = 0;

                try
                {
                    if (sc?.StructureSet == null)
                    {
                        log.AppendLine("ERROR: No structure set available");
                        return;
                    }

                    var image = sc.StructureSet.Image;

                    var latticeStructure = sc.StructureSet.Structures
                        .FirstOrDefault(s => s.Id.Equals(latticeId, StringComparison.OrdinalIgnoreCase));

                    if (latticeStructure == null || latticeStructure.IsEmpty)
                    {
                        log.AppendLine($"ERROR: Lattice structure '{latticeId}' not found or empty");
                        return;
                    }

                    var targetStructure = sc.StructureSet.Structures
                        .FirstOrDefault(s => s.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase));

                    if (targetStructure == null || targetStructure.IsEmpty)
                    {
                        log.AppendLine($"ERROR: Target structure '{targetId}' not found or empty");
                        return;
                    }

                    // Step 1: Extract spheres
                    log.AppendLine("\n--- Step 1: Extracting Spheres ---");
                    var sphereExtractor = new SphereExtractor();
                    var extractionLog = new StringBuilder();
                    var extractionResult = sphereExtractor.ExtractSpheres(latticeStructure, image, extractionLog);
                    log.Append(extractionLog.ToString());

                    if (!extractionResult.Success)
                    {
                        log.AppendLine($"ERROR: Sphere extraction failed - {extractionResult.Message}");
                        return;
                    }

                    log.AppendLine($"Extracted {extractionResult.Spheres.Count} spheres, radius = {extractionResult.MeanRadius:F1} mm");

                    // Step 2: Create target info
                    log.AppendLine("\n--- Step 2: Analyzing Target ---");
                    var targetInfo = TargetInfo.FromStructure(targetStructure);
                    log.AppendLine($"Target: {targetInfo}");

                    // Capture target radius for grid search display
                    targetRadius = targetInfo.Radius;

                    // Step 3: Create OAR info list
                    log.AppendLine("\n--- Step 3: Analyzing OARs ---");
                    var oarInfoList = new List<OARInfo>();

                    foreach (var oarName in selectedOARNames)
                    {
                        var oarStructure = sc.StructureSet.Structures
                            .FirstOrDefault(s => s.Id.Equals(oarName, StringComparison.OrdinalIgnoreCase));

                        if (oarStructure != null && !oarStructure.IsEmpty)
                        {
                            var oarInfo = OARInfo.FromStructure(oarStructure);
                            oarInfo.IsSelected = true;
                            oarInfoList.Add(oarInfo);
                            log.AppendLine($"  OAR: {oarInfo}");
                        }
                    }

                    if (oarInfoList.Count == 0)
                    {
                        log.AppendLine("  No OARs selected (OSI will default to 1.0)");
                    }

                    // Step 4: Calculate geometric metrics
                    log.AppendLine("\n--- Step 4: Calculating Metrics ---");
                    log.AppendLine("  Gantry angles: 0 to 355 in 5 deg steps (72 angles)");

                    var calculator = new GeometricSurrogateCalculator(
                        gantryAngleStep: 5.0,
                        gantryStart: 0.0,
                        gantryEnd: 360.0);

                    results = calculator.Calculate(
                        extractionResult.Spheres,
                        targetInfo,
                        oarInfoList);

                    // Step 5: Display results
                    if (results.Success)
                    {
                        log.AppendLine("\n" + results.GetSummary());
                    }
                    else
                    {
                        log.AppendLine($"ERROR: Calculation failed - {results.Message}");
                    }
                }
                catch (Exception ex)
                {
                    log.AppendLine($"ERROR: {ex.Message}");
                }
                finally
                {
                    // Capture final values for UI update
                    string logOutput = log.ToString();
                    var finalResults = results;
                    var finalRadius = targetRadius;

                    // FIX: Use captured dispatcher instead of Application.Current.Dispatcher
                    try
                    {
                        uiDispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                Output += logOutput;

                                if (finalResults != null && finalResults.Success)
                                {
                                    GeometricResults = finalResults;
                                    GeometricResultsSummary = FormatResultsSummary(finalResults);
                                    _targetRadius = finalRadius;

                                    // Update search range display
                                    if (_targetRadius > 0)
                                    {
                                        double rangeMm = _targetRadius * GridSearchRangePercent / 100.0;
                                        GridSearchRangeDisplay = $"(±{rangeMm:F1}mm)";
                                    }
                                }

                                IsCalculatingMetrics = false;

                                // Force all commands to re-evaluate
                                RunGridSearchCommand?.RaiseCanExecuteChanged();
                                ApplyBestOverallCommand?.RaiseCanExecuteChanged();
                                ApplyBestFullCountCommand?.RaiseCanExecuteChanged();
                            }
                            catch { }
                        }));
                    }
                    catch { }
                }
            });
        }

        /// <summary>
        /// Format results for compact UI display
        /// </summary>
        private string FormatResultsSummary(GeometricResults results)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"SII: {results.SII_Mean:F3}  (range: {results.SII_Min:F3} - {results.SII_Max:F3})");
            sb.AppendLine($"VSI: {results.VSI_Mean:F3}  (range: {results.VSI_Min:F3} - {results.VSI_Max:F3})");
            sb.AppendLine($"SSI: {results.SSI:F3}");

            if (results.OAR_Results.Count > 0)
            {
                sb.AppendLine($"OSI: {results.OSI_Combined:F3}  (worst: {results.OSI_WorstOARName} @ {results.OSI_Worst:F3})");
            }

            sb.AppendLine($"─────────────────────");
            sb.AppendLine($"Combined Score: {results.CombinedScore:F3}");

            return sb.ToString();
        }

        #endregion

        #region Grid Search

        /// <summary>
        /// Check if grid search can run
        /// </summary>
        private bool CanRunGridSearch()
        {
            return !IsRunningGridSearch &&
                   !IsCalculatingMetrics &&
                   !IsOptimizing &&
                   !string.IsNullOrEmpty(SelectedLatticeStructure) &&
                   !string.IsNullOrEmpty(SelectedPTVStructure) &&
                   GeometricResults != null;  // Must have baseline first
        }

        /// <summary>
        /// Run grid search to find optimal X/Y offset - FIXED THREADING
        /// </summary>
        private void OnRunGridSearch()
        {
            IsRunningGridSearch = true;
            Output += "\n========================================\n";
            Output += "=== GRID SEARCH OPTIMIZATION ===\n";
            Output += "========================================\n";

            string latticeId = SelectedLatticeStructure;
            string targetId = SelectedPTVStructure;
            double rangePercent = GridSearchRangePercent;
            int steps = GridSearchSteps;

            var selectedOARNames = AvailableOARs
                .Where(o => o.IsSelected)
                .Select(o => o.Name)
                .ToList();

            // CRITICAL: Capture the UI dispatcher BEFORE entering the worker
            var uiDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

            _esapiWorker.Run(sc =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var result = new GridSearchResult();
                var log = new StringBuilder();

                try
                {
                    // Get structures
                    var latticeStructure = sc.StructureSet.Structures
                        .FirstOrDefault(s => s.Id.Equals(latticeId, StringComparison.OrdinalIgnoreCase));
                    var targetStructure = sc.StructureSet.Structures
                        .FirstOrDefault(s => s.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase));

                    if (latticeStructure == null || targetStructure == null)
                    {
                        result.Success = false;
                        result.Message = "Could not find lattice or target structure";
                        return;
                    }

                    // Extract spheres
                    var extractor = new SphereExtractor();
                    var extractionLog = new System.Text.StringBuilder();
                    var extraction = extractor.ExtractSpheres(latticeStructure, sc.Image, extractionLog);

                    if (!extraction.Success || extraction.Spheres.Count == 0)
                    {
                        result.Success = false;
                        result.Message = "Failed to extract spheres from lattice";
                        return;
                    }

                    var baseSpheres = extraction.Spheres;
                    result.OriginalSphereCount = baseSpheres.Count;
                    result.SphereRadius = extraction.MeanRadius;

                    // Get target info
                    var targetInfo = TargetInfo.FromStructure(targetStructure);
                    result.SearchRangePercent = rangePercent;
                    result.SearchRangeMm = targetInfo.Radius * rangePercent / 100.0;
                    result.StepsPerAxis = steps;

                    // Get OAR info
                    var oarInfoList = new List<OARInfo>();
                    foreach (var oarName in selectedOARNames)
                    {
                        var oarStructure = sc.StructureSet.Structures
                            .FirstOrDefault(s => s.Id.Equals(oarName, StringComparison.OrdinalIgnoreCase));
                        if (oarStructure != null && !oarStructure.IsEmpty)
                        {
                            var oarInfo = OARInfo.FromStructure(oarStructure);
                            oarInfo.IsSelected = true;
                            oarInfoList.Add(oarInfo);
                        }
                    }

                    // Create calculator
                    var calculator = new GeometricSurrogateCalculator(gantryAngleStep: 5.0);

                    // Calculate grid offsets
                    double maxOffset = result.SearchRangeMm;
                    double stepSize = steps > 1 ? (2.0 * maxOffset) / (steps - 1) : 0;

                    log.AppendLine($"Search range: ±{maxOffset:F1}mm, Step size: {stepSize:F1}mm");
                    log.AppendLine($"Testing {steps}x{steps} = {steps * steps} positions...");

                    // Run grid search
                    for (int xi = 0; xi < steps; xi++)
                    {
                        for (int yi = 0; yi < steps; yi++)
                        {
                            double offsetX = -maxOffset + xi * stepSize;
                            double offsetY = -maxOffset + yi * stepSize;

                            // Shift sphere centers
                            var shiftedSpheres = ShiftSpheres(baseSpheres, offsetX, offsetY);

                            // Filter invalid spheres
                            var validSpheres = FilterValidSpheres(shiftedSpheres, targetInfo, oarInfoList, extraction.MeanRadius);

                            if (validSpheres.Count == 0)
                                continue;

                            // Calculate metrics
                            var metrics = calculator.Calculate(validSpheres, targetInfo, oarInfoList);

                            if (!metrics.Success)
                                continue;

                            var posResult = new GridPositionResult
                            {
                                OffsetX = offsetX,
                                OffsetY = offsetY,
                                ValidSphereCount = validSpheres.Count,
                                OriginalSphereCount = baseSpheres.Count,
                                SII = metrics.SII_Mean,
                                VSI = metrics.VSI_Mean,
                                SSI = metrics.SSI,
                                OSI = metrics.OSI_Combined,
                                CombinedScore = metrics.CombinedScore,
                                FullResults = metrics
                            };

                            result.AllResults.Add(posResult);

                            // Track baseline (0,0)
                            if (Math.Abs(offsetX) < 0.01 && Math.Abs(offsetY) < 0.01)
                            {
                                result.Baseline = posResult;
                            }
                        }
                    }

                    result.TotalPositionsTested = result.AllResults.Count;

                    if (result.AllResults.Count == 0)
                    {
                        result.Success = false;
                        result.Message = "No valid positions found in search";
                        return;
                    }

                    // Find best overall
                    result.BestOverall = result.AllResults
                        .OrderByDescending(r => r.CombinedScore)
                        .First();

                    // Find best with full sphere count
                    var fullCountResults = result.AllResults
                        .Where(r => r.ValidSphereCount == result.OriginalSphereCount)
                        .ToList();

                    if (fullCountResults.Count > 0)
                    {
                        result.BestFullCount = fullCountResults
                            .OrderByDescending(r => r.CombinedScore)
                            .First();
                    }
                    else
                    {
                        // No position keeps all spheres - use best overall
                        result.BestFullCount = result.BestOverall;
                    }

                    // If baseline wasn't found (rare), use (0,0) result or closest
                    if (result.Baseline == null)
                    {
                        result.Baseline = result.AllResults
                            .OrderBy(r => Math.Abs(r.OffsetX) + Math.Abs(r.OffsetY))
                            .First();
                    }

                    stopwatch.Stop();
                    result.ComputationTimeMs = stopwatch.ElapsedMilliseconds;
                    result.Success = true;
                    result.Message = "Grid search completed successfully";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"Error: {ex.Message}";
                }
                finally
                {
                    // Capture final values for UI update
                    string logOutput = log.ToString();
                    var finalResult = result;

                    // FIX: Use captured dispatcher instead of Application.Current.Dispatcher
                    try
                    {
                        uiDispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                Output += logOutput;
                                Output += "\n" + finalResult.GetSummary();
                                GridSearchResult = finalResult;
                                IsRunningGridSearch = false;
                            }
                            catch { }
                        }));
                    }
                    catch { }
                }
            });
        }

        /// <summary>
        /// Shift sphere centers by given offset
        /// </summary>
        private List<ExtractedSphere> ShiftSpheres(List<ExtractedSphere> spheres, double offsetX, double offsetY)
        {
            return spheres.Select(s => new ExtractedSphere
            {
                CenterX = s.CenterX + offsetX,
                CenterY = s.CenterY + offsetY,
                CenterZ = s.CenterZ,
                Radius = s.Radius
            }).ToList();
        }

        /// <summary>
        /// Filter out spheres that are outside target or too close to OARs
        /// </summary>
        private List<ExtractedSphere> FilterValidSpheres(
            List<ExtractedSphere> spheres,
            TargetInfo target,
            List<OARInfo> oars,
            double sphereRadius)
        {
            var validSpheres = new List<ExtractedSphere>();

            foreach (var sphere in spheres)
            {
                // Check if sphere center is inside target (with margin for sphere radius)
                double distToTargetCenter = Math.Sqrt(
                    Math.Pow(sphere.CenterX - target.CenterX, 2) +
                    Math.Pow(sphere.CenterY - target.CenterY, 2) +
                    Math.Pow(sphere.CenterZ - target.CenterZ, 2));

                // Sphere must fit entirely within target (spherical approximation)
                if (distToTargetCenter + sphereRadius > target.Radius)
                    continue;

                // Check distance to each OAR
                bool tooCloseToOAR = false;
                foreach (var oar in oars)
                {
                    double distToOAR = Math.Sqrt(
                        Math.Pow(sphere.CenterX - oar.CenterX, 2) +
                        Math.Pow(sphere.CenterY - oar.CenterY, 2) +
                        Math.Pow(sphere.CenterZ - oar.CenterZ, 2));

                    // Sphere must not overlap OAR (using OAR's XY radius as approximation)
                    if (distToOAR < sphereRadius + oar.RadiusXY)
                    {
                        tooCloseToOAR = true;
                        break;
                    }
                }

                if (!tooCloseToOAR)
                {
                    validSpheres.Add(sphere);
                }
            }

            return validSpheres;
        }

        /// <summary>
        /// Check if apply best overall is available
        /// </summary>
        private bool CanApplyBestOverall()
        {
            return HasGridSearchResults && GridSearchResult.BestOverall != null;
        }

        /// <summary>
        /// Check if apply best full count is available
        /// </summary>
        private bool CanApplyBestFullCount()
        {
            return HasGridSearchResults && GridSearchResult.BestFullCount != null;
        }

        /// <summary>
        /// Apply the best overall position
        /// </summary>
        private void OnApplyBestOverall()
        {
            if (GridSearchResult?.BestOverall == null) return;
            ApplyOptimalOffset(GridSearchResult.BestOverall.OffsetX, GridSearchResult.BestOverall.OffsetY, "Opt");
        }

        /// <summary>
        /// Apply the best full-count position
        /// </summary>
        private void OnApplyBestFullCount()
        {
            if (GridSearchResult?.BestFullCount == null) return;
            ApplyOptimalOffset(GridSearchResult.BestFullCount.OffsetX, GridSearchResult.BestFullCount.OffsetY, "OptFull");
        }

        /// <summary>
        /// Create new lattice structure at optimal offset and auto-create valley - FIXED THREADING
        /// </summary>
        /// <summary>
        /// Create new lattice structure at optimal offset - uses async Run() to avoid freezing
        /// </summary>
        private void ApplyOptimalOffset(double offsetX, double offsetY, string suffix)
        {
            Output += $"\n\n=== Applying Optimal Offset ===\n";
            Output += $"Offset: X={offsetX:+0.0;-0.0}mm, Y={offsetY:+0.0;-0.0}mm\n";

            string latticeId = SelectedLatticeStructure;
            string targetId = SelectedPTVStructure;
            var selectedOARNames = AvailableOARs.Where(o => o.IsSelected).Select(o => o.Name).ToList();

            // Use async Run() to avoid blocking UI (RunWithWait causes freezes with dialogs)
            _esapiWorker.Run(sc =>
            {
                try
                {
                    sc.Patient.BeginModifications();

                    var latticeStructure = sc.StructureSet.Structures
                        .FirstOrDefault(s => s.Id.Equals(latticeId, StringComparison.OrdinalIgnoreCase));
                    var targetStructure = sc.StructureSet.Structures
                        .FirstOrDefault(s => s.Id.Equals(targetId, StringComparison.OrdinalIgnoreCase));

                    if (latticeStructure == null || targetStructure == null)
                    {
                        Output += "ERROR: Could not find structures\n";
                        return;
                    }

                    // Extract spheres
                    var extractor = new SphereExtractor();
                    var extractionLog = new System.Text.StringBuilder();
                    var extraction = extractor.ExtractSpheres(latticeStructure, sc.Image, extractionLog);

                    if (!extraction.Success)
                    {
                        Output += "ERROR: Could not extract spheres\n";
                        return;
                    }

                    Output += $"Extracted {extraction.Spheres.Count} spheres, radius = {extraction.MeanRadius:F1} mm\n";

                    // Create target info for filtering
                    var targetInfo = TargetInfo.FromStructure(targetStructure);

                    // Get selected OARs
                    var oarInfoList = new List<OARInfo>();
                    foreach (var oarName in selectedOARNames)
                    {
                        var oarStructure = sc.StructureSet.Structures
                            .FirstOrDefault(s => s.Id.Equals(oarName, StringComparison.OrdinalIgnoreCase));
                        if (oarStructure != null && !oarStructure.IsEmpty)
                        {
                            oarInfoList.Add(OARInfo.FromStructure(oarStructure));
                        }
                    }

                    // Shift and filter spheres
                    var shiftedSpheres = ShiftSpheres(extraction.Spheres, offsetX, offsetY);
                    var validSpheres = FilterValidSpheres(shiftedSpheres, targetInfo, oarInfoList, extraction.MeanRadius);

                    Output += $"After filtering: {validSpheres.Count} valid spheres\n";

                    if (validSpheres.Count == 0)
                    {
                        Output += "ERROR: No valid spheres remain after filtering!\n";
                        return;
                    }

                    // Generate new structure name
                    string baseName = latticeId.Length > 10 ? latticeId.Substring(0, 10) : latticeId;
                    string newLatticeId = $"{baseName}_{suffix}";
                    if (newLatticeId.Length > 16)
                        newLatticeId = newLatticeId.Substring(0, 16);

                    // Remove existing if present
                    var existingLattice = sc.StructureSet.Structures
                        .FirstOrDefault(s => s.Id.Equals(newLatticeId, StringComparison.OrdinalIgnoreCase));
                    if (existingLattice != null)
                    {
                        sc.StructureSet.RemoveStructure(existingLattice);
                        Output += $"Removed existing structure: {newLatticeId}\n";
                    }

                    // Create new structure
                    Output += $"Creating new lattice structure: {newLatticeId}\n";
                    var newLattice = sc.StructureSet.AddStructure("PTV", newLatticeId);
                    newLattice.ConvertToHighResolution();

                    // Build spheres
                    Output += "Building spheres...\n";
                    foreach (var sphere in validSpheres)
                    {
                        var center = new VMS.TPS.Common.Model.Types.VVector(
                            sphere.CenterX, sphere.CenterY, sphere.CenterZ);
                        BuildSphereOnStructure(newLattice, center, (float)sphere.Radius, sc.Image);
                    }

                    Output += $"Built spheres, volume before crop: {newLattice.Volume:F2} cc\n";

                    // Handle resolution mismatch for boolean operations
                    Structure targetForBoolean = targetStructure;
                    Structure tempTarget = null;

                    if (!targetStructure.IsHighResolution)
                    {
                        Output += $"Target '{targetStructure.Id}' is low-resolution, creating high-res copy for boolean...\n";

                        tempTarget = sc.StructureSet.AddStructure("CONTROL", "TempTarget_HR");

                        for (int z = 0; z < sc.StructureSet.Image.ZSize; z++)
                        {
                            var contours = targetStructure.GetContoursOnImagePlane(z);
                            foreach (var contour in contours)
                            {
                                if (contour.Length > 0)
                                {
                                    tempTarget.AddContourOnImagePlane(contour, z);
                                }
                            }
                        }

                        tempTarget.ConvertToHighResolution();
                        targetForBoolean = tempTarget;
                        Output += $"Created high-res target copy\n";
                    }

                    // Crop to target
                    newLattice.SegmentVolume = newLattice.SegmentVolume.And(targetForBoolean);
                    Output += $"✓ Created: {newLatticeId} ({newLattice.Volume:F2} cc)\n";

                    // Auto-create valley structure
                    string valleyId = $"Valley_{suffix}";
                    if (valleyId.Length > 16)
                        valleyId = valleyId.Substring(0, 16);

                    var existingValley = sc.StructureSet.Structures
                        .FirstOrDefault(s => s.Id.Equals(valleyId, StringComparison.OrdinalIgnoreCase));
                    if (existingValley != null)
                    {
                        sc.StructureSet.RemoveStructure(existingValley);
                    }

                    var valleyStructure = sc.StructureSet.AddStructure("CONTROL", valleyId);
                    valleyStructure.ConvertToHighResolution();
                    valleyStructure.SegmentVolume = targetForBoolean.Sub(newLattice);

                    // Clean up temporary structure
                    if (tempTarget != null)
                    {
                        sc.StructureSet.RemoveStructure(tempTarget);
                        Output += "Cleaned up temporary target structure\n";
                    }

                    Output += $"✓ Created: {valleyId} ({valleyStructure.Volume:F2} cc)\n";
                    Output += $"\n=== Apply Complete ===\n";
                    Output += $"Created: {newLatticeId} and {valleyId}\n";
                    Output += $"\n*** IMPORTANT: Click 'Populate Objectives' to refresh dropdowns and use the new structures! ***\n";
                }
                catch (Exception ex)
                {
                    Output += $"ERROR: {ex.Message}\n";
                    Output += $"Stack: {ex.StackTrace}\n";
                }
            });

            // Don't try to refresh dropdowns here - Run() is async so this executes immediately
            // User must click "Populate Objectives" which will refresh the lists
        }

        /// <summary>
        /// Build a sphere on a structure (same logic as SphereDialogViewModel.BuildSphere)
        /// </summary>
        private void BuildSphereOnStructure(
            VMS.TPS.Common.Model.API.Structure parentStruct,
            VMS.TPS.Common.Model.Types.VVector center,
            float radius,
            VMS.TPS.Common.Model.API.Image image)
        {
            // Calculate slice sign for Z direction
            double sliceSign = Math.Sign(
                image.XDirection.x * image.YDirection.y -
                image.XDirection.y * image.YDirection.x);

            for (int z = 0; z < image.ZSize; ++z)
            {
                double zCoord = sliceSign * z * image.ZRes + image.Origin.z;

                var zDiff = Math.Abs(zCoord - center.z);
                if (zDiff > radius)
                    continue;

                var rZ = Math.Sqrt(Math.Pow(radius, 2.0) - Math.Pow(zDiff, 2.0));
                var contour = CreateCircleContour(center, rZ, 100);
                parentStruct.AddContourOnImagePlane(contour, z);
            }
        }

        /// <summary>
        /// Create a circular contour
        /// </summary>
        private VMS.TPS.Common.Model.Types.VVector[] CreateCircleContour(
            VMS.TPS.Common.Model.Types.VVector center,
            double radius,
            int nPoints)
        {
            var contour = new VMS.TPS.Common.Model.Types.VVector[nPoints + 1];
            double angleIncrement = Math.PI * 2.0 / nPoints;

            for (int i = 0; i < nPoints; i++)
            {
                double angle = i * angleIncrement;
                double xDelta = radius * Math.Cos(angle);
                double yDelta = radius * Math.Sin(angle);
                contour[i] = new VMS.TPS.Common.Model.Types.VVector(
                    center.x + xDelta, center.y + yDelta, center.z);
            }
            contour[nPoints] = contour[0];

            return contour;
        }

        #endregion
    }
}