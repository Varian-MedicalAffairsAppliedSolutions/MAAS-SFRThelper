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

            PopulateObjectivesCommand = new DelegateCommand(OnPopulateObjectives, CanPopulateObjectives);
            CreateObjectivesCommand = new DelegateCommand(OnCreateObjectives, CanCreateObjectives);
            RemoveObjectiveCommand = new DelegateCommand(OnRemoveObjective, CanRemoveObjective);
            AddObjectiveCommand = new DelegateCommand(OnAddObjective);
            ResetToDefaultsCommand = new DelegateCommand(OnResetToDefaults);
            CreateValleyStructureCommand = new DelegateCommand(OnCreateValleyStructure, CanCreateValleyStructure);
            RunOptimizationCommand = new DelegateCommand(OnRunOptimization, CanRunOptimization);
            CalculateDoseCommand = new DelegateCommand(OnCalculateDose, CanCalculateDose);

            PopulateStructureLists();
        }

        #endregion

        #region Dropdown Population

        private void PopulateStructureLists()
        {
            var beamItems = new List<BeamSelectionItem>();

            _esapiWorker.RunWithWait(sc =>
            {
                AvailableLatticeStructures = OptimizationObjectiveCreator.GetAvailableLatticeStructures(sc.StructureSet);
                AvailableValleyStructures = OptimizationObjectiveCreator.GetAvailableValleyStructures(sc.StructureSet);
                AvailablePTVStructures = OptimizationObjectiveCreator.GetAvailablePTVStructures(sc.StructureSet);

                if (AvailableLatticeStructures != null && AvailableLatticeStructures.Count == 1)
                {
                    SelectedLatticeStructure = AvailableLatticeStructures[0];
                }

                if (AvailableValleyStructures != null && AvailableValleyStructures.Count > 0)
                {
                    if (AvailableValleyStructures.Contains("coreVoid"))
                        SelectedValleyStructure = "coreVoid";
                    else if (AvailableValleyStructures.Contains("Voids"))
                        SelectedValleyStructure = "Voids";
                    else if (AvailableValleyStructures.Contains("Valley"))
                        SelectedValleyStructure = "Valley";
                    else
                        SelectedValleyStructure = "[Auto-create Valley]";
                }

                if (AvailablePTVStructures != null && AvailablePTVStructures.Count == 1)
                {
                    SelectedPTVStructure = AvailablePTVStructures[0];
                }

                // Populate MLCs from plan beams
                var mlcs = sc.PlanSetup.Beams
                    .Where(b => !b.IsSetupField && b.MLC != null)
                    .Select(b => b.MLC.Id)
                    .Distinct()
                    .ToList();

                AvailableMLCs = mlcs;
                if (mlcs.Count >= 1)
                {
                    SelectedMLC = mlcs[0];
                }

                // Populate beam list
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

                HasVMATArcs = hasVMAT;

                if (!hasVMAT)
                {
                    BeamWarningText = "⚠ No VMAT arcs found. Please create VMAT arcs in Eclipse before optimizing.";
                    Output += "\n⚠ WARNING: No VMAT arcs found in plan";
                }
                else
                {
                    BeamWarningText = "";
                }

                Output += $"\nFound {AvailableLatticeStructures?.Count ?? 0} lattice structure(s)";
                Output += $"\nFound {AvailableValleyStructures?.Count ?? 0} valley structure option(s)";
                Output += $"\nFound {AvailablePTVStructures?.Count ?? 0} PTV structure(s)";
                Output += $"\nFound {mlcs.Count} MLC(s)";
                Output += $"\nFound {beamItems.Count} beam(s) ({beamItems.Count(b => b.IsVMAT)} VMAT)";
            });

            // Update ObservableCollection on UI thread after RunWithWait
            AvailableBeams.Clear();
            foreach (var beam in beamItems)
            {
                AvailableBeams.Add(beam);
            }
        }

        #endregion

        #region Structure Selection Handlers

        private void OnLatticeStructureChanged()
        {
            if (string.IsNullOrEmpty(SelectedLatticeStructure))
                return;

            Output += $"\n✓ Selected lattice structure: {SelectedLatticeStructure}";
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

            var objectivesList = new List<ObjectiveDefinition>();

            _esapiWorker.RunWithWait(sc =>
            {
                try
                {
                    var template = OptimizationTemplate.CreateStandardProstateTemplate();
                    var defaultConstraints = ProstateOARConstraints.GetConstraints();

                    if (!string.IsNullOrEmpty(SelectedLatticeStructure))
                    {
                        objectivesList.Add(new ObjectiveDefinition
                        {
                            StructureName = SelectedLatticeStructure,
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
                            StructureName = SelectedLatticeStructure,
                            ObjectiveType = "Mean",
                            Operator = OptimizationObjectiveOperator.Lower,
                            Dose = template.PeakMeanDose,
                            Volume = 0,
                            Priority = template.PeakMeanPriority,
                            Role = "Peak",
                            IsIncluded = true
                        });

                        Output += $"\n  ✓ Added Peak objectives for {SelectedLatticeStructure}";
                    }

                    var valleyStructure = sc.StructureSet.Structures.FirstOrDefault(s =>
                        s.Id.Equals("Valley", StringComparison.OrdinalIgnoreCase) ||
                        s.Id.Equals("Voids", StringComparison.OrdinalIgnoreCase) ||
                        s.Id.Equals("coreVoid", StringComparison.OrdinalIgnoreCase));

                    if (valleyStructure != null)
                    {
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
                }
                catch (Exception ex)
                {
                    Output += $"\n\nERROR: {ex.Message}";
                    Output += $"\nStack trace: {ex.StackTrace}";
                }
            });

            Objectives.Clear();
            foreach (var obj in objectivesList)
            {
                Objectives.Add(obj);
            }

            Output += "\n✓ Objectives table populated";
            Output += "\n\nReview and edit objectives as needed, then click 'Create Objectives' to apply to Eclipse.";

            CreateObjectivesCommand?.RaiseCanExecuteChanged();
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

        private bool RunVMATOptimization(ExternalPlanSetup plan, string mlcId, bool useIntermediateDose)
        {
            try
            {
                Output += $"\n  [{DateTime.Now:HH:mm:ss}] Calling OptimizeVMAT...";

                var intermediateOption = useIntermediateDose
                    ? OptimizationIntermediateDoseOption.UseIntermediateDose
                    : OptimizationIntermediateDoseOption.NoIntermediateDose;

                var options = new OptimizationOptionsVMAT(intermediateOption, mlcId);

                var result = plan.OptimizeVMAT(options);

                Output += $"\n  [{DateTime.Now:HH:mm:ss}] Optimization result: {result}";

                // Use backing field directly to avoid threading issues with RaiseCanExecuteChanged
                _optimizationCompleted = true;

                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    Output += $"\n  [{DateTime.Now:HH:mm:ss}] Optimization failed: {ex.Message}";
                }
                catch { }
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
            Output += "\n✓ Objectives table cleared";
            Output += "\nClick 'Populate Objectives' to reload";
        }

        #endregion
    }
}