using MAAS_SFRThelper.Models;
using MAAS_SFRThelper.Services;
using MAAS_SFRThelper.Utilities;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;

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
        /// <summary>
        /// Output log displayed to user
        /// </summary>
        public string Output
        {
            get { return _output; }
            set { SetProperty(ref _output, value); }
        }

        private double _progressValue;
        /// <summary>
        /// Progress bar value (0-100)
        /// </summary>
        public double ProgressValue
        {
            get { return _progressValue; }
            set { SetProperty(ref _progressValue, value); }
        }

        #endregion

        #region Validation Properties

        private string _validationText;
        /// <summary>
        /// Validation message to display to user
        /// </summary>
        public string ValidationText
        {
            get { return _validationText; }
            set { SetProperty(ref _validationText, value); }
        }

        private bool _validationVisible;
        /// <summary>
        /// Whether validation message should be visible
        /// </summary>
        public bool ValidationVisible
        {
            get { return _validationVisible; }
            set { SetProperty(ref _validationVisible, value); }
        }

        #endregion

        #region Structure Selection Properties

        private List<string> _availableLatticeStructures;
        /// <summary>
        /// Dropdown options for lattice (peak) structures
        /// </summary>
        public List<string> AvailableLatticeStructures
        {
            get { return _availableLatticeStructures; }
            set { SetProperty(ref _availableLatticeStructures, value); }
        }

        private string _selectedLatticeStructure;
        /// <summary>
        /// User-selected lattice structure
        /// </summary>
        public string SelectedLatticeStructure
        {
            get { return _selectedLatticeStructure; }
            set
            {
                SetProperty(ref _selectedLatticeStructure, value);
                // Update command availability when selection changes
                GenerateObjectivesCommand.RaiseCanExecuteChanged();
                CreateObjectivesCommand.RaiseCanExecuteChanged();
            }
        }

        private List<string> _availableValleyStructures;
        /// <summary>
        /// Dropdown options for valley (low-dose) structures
        /// </summary>
        public List<string> AvailableValleyStructures
        {
            get { return _availableValleyStructures; }
            set { SetProperty(ref _availableValleyStructures, value); }
        }

        private string _selectedValleyStructure;
        /// <summary>
        /// User-selected valley structure (or "[Auto-create Valley]")
        /// </summary>
        public string SelectedValleyStructure
        {
            get { return _selectedValleyStructure; }
            set
            {
                SetProperty(ref _selectedValleyStructure, value);

                // Show/hide PTV dropdown based on selection
                PTVSelectionVisible = (value == "[Auto-create Valley]");

                // Update command availability
                GenerateObjectivesCommand.RaiseCanExecuteChanged();
                CreateObjectivesCommand.RaiseCanExecuteChanged();
            }
        }

        private List<string> _availablePTVStructures;
        /// <summary>
        /// Dropdown options for PTV structures (used when creating valley)
        /// </summary>
        public List<string> AvailablePTVStructures
        {
            get { return _availablePTVStructures; }
            set { SetProperty(ref _availablePTVStructures, value); }
        }

        private string _selectedPTVStructure;
        /// <summary>
        /// User-selected PTV structure for valley creation
        /// </summary>
        public string SelectedPTVStructure
        {
            get { return _selectedPTVStructure; }
            set
            {
                SetProperty(ref _selectedPTVStructure, value);
                GenerateObjectivesCommand.RaiseCanExecuteChanged();
                CreateObjectivesCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _ptvSelectionVisible;
        /// <summary>
        /// Whether PTV dropdown should be visible (only when auto-creating valley)
        /// </summary>
        public bool PTVSelectionVisible
        {
            get { return _ptvSelectionVisible; }
            set { SetProperty(ref _ptvSelectionVisible, value); }
        }

        #endregion

        #region Optimization Parameter Properties

        // Peak (Lattice) objectives
        private double _peakLowerDose;
        public double PeakLowerDose
        {
            get { return _peakLowerDose; }
            set { SetProperty(ref _peakLowerDose, value); }
        }

        private double _peakLowerVolume;
        public double PeakLowerVolume
        {
            get { return _peakLowerVolume; }
            set { SetProperty(ref _peakLowerVolume, value); }
        }

        private int _peakLowerPriority;
        public int PeakLowerPriority
        {
            get { return _peakLowerPriority; }
            set { SetProperty(ref _peakLowerPriority, value); }
        }

        private double _peakMeanDose;
        public double PeakMeanDose
        {
            get { return _peakMeanDose; }
            set { SetProperty(ref _peakMeanDose, value); }
        }

        private int _peakMeanPriority;
        public int PeakMeanPriority
        {
            get { return _peakMeanPriority; }
            set { SetProperty(ref _peakMeanPriority, value); }
        }

        // Valley (Low dose) objectives
        private double _valleyUpperDose;
        public double ValleyUpperDose
        {
            get { return _valleyUpperDose; }
            set { SetProperty(ref _valleyUpperDose, value); }
        }

        private double _valleyUpperVolume;
        public double ValleyUpperVolume
        {
            get { return _valleyUpperVolume; }
            set { SetProperty(ref _valleyUpperVolume, value); }
        }

        private int _valleyUpperPriority;
        public int ValleyUpperPriority
        {
            get { return _valleyUpperPriority; }
            set { SetProperty(ref _valleyUpperPriority, value); }
        }

        private double _valleyMeanDose;
        public double ValleyMeanDose
        {
            get { return _valleyMeanDose; }
            set { SetProperty(ref _valleyMeanDose, value); }
        }

        private int _valleyMeanPriority;
        public int ValleyMeanPriority
        {
            get { return _valleyMeanPriority; }
            set { SetProperty(ref _valleyMeanPriority, value); }
        }

        // Add this property to OptimizationViewModel
        private string _oarSummary;
        public string OARSummary
        {
            get { return _oarSummary; }
            set { SetProperty(ref _oarSummary, value); }
        }
        #endregion

        #region Commands

        public DelegateCommand GenerateObjectivesCommand { get; set; }
        public DelegateCommand CreateObjectivesCommand { get; set; }

        #endregion

        #region Constructor

        public OptimizationViewModel(EsapiWorker esapi)
        {
            // Store EsapiWorker reference
            _esapiWorker = esapi;

            // Initialize plan and structure set (synchronous - must complete before continuing)
            _esapiWorker.RunWithWait(sc =>
            {
                _plan = sc.PlanSetup;
                _structureSet = sc.StructureSet;
            });

            // Initialize UI properties
            Output = "Optimization module ready. Select structures and click 'Generate Objectives' to begin.";
            ProgressValue = 0;
            ValidationVisible = false;
            PTVSelectionVisible = false;

            // Initialize dose/priority values to zero (will be populated by Generate Objectives)
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

            // Populate dropdown lists
            PopulateStructureLists();

            // Initialize commands
            GenerateObjectivesCommand = new DelegateCommand(OnGenerateObjectives, CanGenerateObjectives);
            CreateObjectivesCommand = new DelegateCommand(OnCreateObjectives, CanCreateObjectives);
        }

        #endregion

        #region Dropdown Population

        /// <summary>
        /// Populate all structure dropdown lists
        /// </summary>
        private void PopulateStructureLists()
        {
            _esapiWorker.Run(sc =>
            {
                // Get available structures using static methods from service class
                AvailableLatticeStructures = OptimizationObjectiveCreator.GetAvailableLatticeStructures(sc.StructureSet);
                AvailableValleyStructures = OptimizationObjectiveCreator.GetAvailableValleyStructures(sc.StructureSet);
                AvailablePTVStructures = OptimizationObjectiveCreator.GetAvailablePTVStructures(sc.StructureSet);

                // Auto-select if only one option available
                if (AvailableLatticeStructures != null && AvailableLatticeStructures.Count == 1)
                {
                    SelectedLatticeStructure = AvailableLatticeStructures[0];
                }

                // Auto-select valley based on priority: coreVoid > Voids > [Auto-create]
                if (AvailableValleyStructures != null && AvailableValleyStructures.Count > 0)
                {
                    if (AvailableValleyStructures.Contains("coreVoid"))
                    {
                        SelectedValleyStructure = "coreVoid";
                    }
                    else if (AvailableValleyStructures.Contains("Voids"))
                    {
                        SelectedValleyStructure = "Voids";
                    }
                    else
                    {
                        SelectedValleyStructure = "[Auto-create Valley]";
                    }
                }

                // Auto-select PTV if only one option
                if (AvailablePTVStructures != null && AvailablePTVStructures.Count == 1)
                {
                    SelectedPTVStructure = AvailablePTVStructures[0];
                }

                Output += $"\nFound {AvailableLatticeStructures?.Count ?? 0} lattice structure(s)";
                Output += $"\nFound {AvailableValleyStructures?.Count ?? 0} valley structure option(s)";
                Output += $"\nFound {AvailablePTVStructures?.Count ?? 0} PTV structure(s)";
            });
        }

        #endregion

        #region Generate Objectives Command

        /// <summary>
        /// Check if we can generate objectives (validation)
        /// </summary>
        private bool CanGenerateObjectives()
        {
            // Must have plan and structure set
            if (_plan == null || _structureSet == null)
            {
                ValidationText = "No plan or structure set loaded";
                ValidationVisible = true;
                return false;
            }

            // Must have lattice structure selected
            if (string.IsNullOrEmpty(SelectedLatticeStructure))
            {
                ValidationText = "Please select a lattice structure";
                ValidationVisible = true;
                return false;
            }

            // If auto-creating valley, must have PTV selected
            if (SelectedValleyStructure == "[Auto-create Valley]" && string.IsNullOrEmpty(SelectedPTVStructure))
            {
                ValidationText = "Please select a PTV structure for valley creation";
                ValidationVisible = true;
                return false;
            }

            // All good!
            ValidationVisible = false;
            return true;
        }

        /// <summary>
        /// Generate optimization parameters based on standard template
        /// Populates the text boxes with suggested values
        /// User can then review and edit before clicking Create Objectives
        /// </summary>
        private void OnGenerateObjectives()
        {
            Output += "\n\n=== Generating Optimization Parameters ===";
            ProgressValue = 0;

            _esapiWorker.Run(sc =>
            {
                try
                {
                    Output += "\nAnalyzing structures...";
                    ProgressValue = 20;

                    // Get standard template
                    var template = OptimizationTemplate.CreateStandardProstateTemplate();
                    Output += "\nUsing Standard Prostate SFRT template";
                    ProgressValue = 40;

                    // Populate UI properties with template values
                    PeakLowerDose = template.PeakLowerDose;
                    PeakLowerVolume = template.PeakLowerVolume;
                    PeakLowerPriority = template.PeakLowerPriority;
                    PeakMeanDose = template.PeakMeanDose;
                    PeakMeanPriority = template.PeakMeanPriority;

                    ValleyUpperDose = template.ValleyUpperDose;
                    ValleyUpperVolume = template.ValleyUpperVolume;
                    ValleyUpperPriority = template.ValleyUpperPriority;
                    ValleyMeanDose = template.ValleyMeanDose;
                    ValleyMeanPriority = template.ValleyMeanPriority;

                    Output += "\n\nGenerated Optimization Parameters:";
                    Output += $"\n  Lattice Structure: {SelectedLatticeStructure}";
                    Output += "\n  Peak Objectives:";
                    Output += $"\n    Lower: {PeakLowerDose} Gy @ {PeakLowerVolume}% (Priority {PeakLowerPriority})";
                    Output += $"\n    Mean:  {PeakMeanDose} Gy (Priority {PeakMeanPriority})";

                    Output += $"\n  Valley Structure: {SelectedValleyStructure}";
                    Output += "\n  Valley Objectives:";
                    Output += $"\n    Upper: {ValleyUpperDose} Gy @ {ValleyUpperVolume}% (Priority {ValleyUpperPriority})";
                    Output += $"\n    Mean:  {ValleyMeanDose} Gy (Priority {ValleyMeanPriority})";

                    // NEW: Show which OARs will get constraints
                    Output += "\n\n  OAR Constraints:";
                    var constraints = ProstateOARConstraints.GetConstraints();
                    int foundCount = 0;
                    foreach (var constraint in constraints)
                    {
                        var oarStructure = StructureMatchingHelper.FindStructureByNameVariations(
                            sc.StructureSet,
                            constraint.NameVariations,
                            requireNonEmpty: true);

                        if (oarStructure != null)
                        {
                            Output += $"\n    ✓ {oarStructure.Id}: Max {constraint.MaxDoseGy} Gy (Priority {constraint.Priority})";
                            foundCount++;
                        }
                        else
                        {
                            Output += $"\n    ✗ {constraint.StructureName}: Not found";
                        }
                    }
                    Output += $"\n  Found {foundCount} of {constraints.Count} OARs";

                    ProgressValue = 100;
                    Output += "\n\nParameter generation complete!";
                    Output += "\nReview the values above, edit if needed, then click 'Create Objectives' to apply.";
                }
                catch (Exception ex)
                {
                    Output += $"\n\nERROR generating parameters: {ex.Message}";
                    ProgressValue = 0;
                }
            });
        }

        #endregion

        #region Create Objectives Command

        /// <summary>
        /// Check if we can create objectives (same validation as generate)
        /// </summary>
        private bool CanCreateObjectives()
        {
            return CanGenerateObjectives();
        }

        /// <summary>
        /// Create optimization objectives in the plan
        /// Uses the values from the UI text boxes (whether generated or user-edited)
        /// </summary>
        private void OnCreateObjectives()
        {
            Output += "\n\n=== Creating Optimization Objectives ===";
            ProgressValue = 0;

            _esapiWorker.Run(sc =>
            {
                try
                {
                    sc.Patient.BeginModifications();

                    // Verify we have an external plan
                    var externalPlan = sc.PlanSetup as ExternalPlanSetup;
                    if (externalPlan == null)
                    {
                        Output += "\nERROR: Plan is not an external beam plan!";
                        return;
                    }

                    Output += "\nValidating selections...";
                    ProgressValue = 10;

                    // Build template from current UI values
                    var template = new OptimizationTemplate
                    {
                        Name = "Current Settings",
                        PeakLowerDose = PeakLowerDose,
                        PeakLowerVolume = PeakLowerVolume,
                        PeakLowerPriority = PeakLowerPriority,
                        PeakMeanDose = PeakMeanDose,
                        PeakMeanPriority = PeakMeanPriority,
                        ValleyUpperDose = ValleyUpperDose,
                        ValleyUpperVolume = ValleyUpperVolume,
                        ValleyUpperPriority = ValleyUpperPriority,
                        ValleyMeanDose = ValleyMeanDose,
                        ValleyMeanPriority = ValleyMeanPriority
                    };

                    Output += "\nUsing optimization parameters from UI";
                    ProgressValue = 20;

                    // Create objective creator service
                    var creator = new OptimizationObjectiveCreator(externalPlan, sc.StructureSet);
                    ProgressValue = 30;

                    // Create objectives
                    Output += "\nCreating objectives...";
                    string result = creator.CreateObjectives(
                        SelectedLatticeStructure,
                        SelectedValleyStructure,
                        SelectedPTVStructure,
                        template);

                    // Append result to output
                    Output += result;
                    ProgressValue = 100;

                    Output += "\n\n=== Objective Creation Complete ===";
                    Output += "\nYou can now run optimization from Eclipse.";
                }
                catch (Exception ex)
                {
                    Output += $"\n\nERROR creating objectives: {ex.Message}";
                    Output += $"\nStack trace: {ex.StackTrace}";
                    ProgressValue = 0;
                }
            });
        }

        #endregion
    }
}