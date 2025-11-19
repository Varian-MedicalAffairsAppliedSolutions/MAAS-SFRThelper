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

            Output = "Optimization module ready.\n1. Select Peak and Valley structures\n2. If needed, click 'Create Valley' button\n3. Click 'Populate Objectives' to fill table\n4. Edit objectives as needed\n5. Click 'Create Objectives' to apply to Eclipse";
            ProgressValue = 0;
            ValidationVisible = false;
            PTVSelectionVisible = false;
            CreateValleyButtonVisible = false;

            Objectives = new ObservableCollection<ObjectiveDefinition>();

            PopulateObjectivesCommand = new DelegateCommand(OnPopulateObjectives, CanPopulateObjectives);
            CreateObjectivesCommand = new DelegateCommand(OnCreateObjectives, CanCreateObjectives);
            RemoveObjectiveCommand = new DelegateCommand(OnRemoveObjective, CanRemoveObjective);
            AddObjectiveCommand = new DelegateCommand(OnAddObjective);
            ResetToDefaultsCommand = new DelegateCommand(OnResetToDefaults);
            CreateValleyStructureCommand = new DelegateCommand(OnCreateValleyStructure, CanCreateValleyStructure);

            PopulateStructureLists();
        }

        #endregion

        #region Dropdown Population

        private void PopulateStructureLists()
        {
            _esapiWorker.Run(sc =>
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

                Output += $"\nFound {AvailableLatticeStructures?.Count ?? 0} lattice structure(s)";
                Output += $"\nFound {AvailableValleyStructures?.Count ?? 0} valley structure option(s)";
                Output += $"\nFound {AvailablePTVStructures?.Count ?? 0} PTV structure(s)";
            });
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

            // Build objectives list INSIDE worker using RunWithWait
            var objectivesList = new List<ObjectiveDefinition>();

            _esapiWorker.RunWithWait(sc =>
            {
                try
                {
                    // Get template
                    var template = OptimizationTemplate.CreateStandardProstateTemplate();
                    var defaultConstraints = ProstateOARConstraints.GetConstraints();

                    // Add Peak objectives
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

                    // Add Valley objectives
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

                    // Add OAR objectives
                    var structures = sc.StructureSet.Structures
                        .Where(s => !s.IsEmpty)
                        .OrderBy(s => s.Id)
                        .ToList();

                    int oarCount = 0;
                    foreach (var structure in structures)
                    {
                        // Skip lattice/valley type structures
                        var skipPatterns = new[] { "Lattice", "CVT", "Sphere", "Valley", "Void" };
                        if (skipPatterns.Any(p => structure.Id.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0))
                            continue;

                        // Try to find matching default
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

            // NOW update ObservableCollection - we're back on UI thread after RunWithWait
            Objectives.Clear();
            foreach (var obj in objectivesList)
            {
                Objectives.Add(obj);
            }

            Output += "\n✓ Objectives table populated";
            Output += "\n\nReview and edit objectives as needed, then click 'Create Objectives' to apply to Eclipse.";

            // Tell the command to re-evaluate - this will enable the button
            CreateObjectivesCommand?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Create Objectives Command

        private bool CanCreateObjectives()
        {
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

            // Get included objectives BEFORE entering worker - on UI thread
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

                    // Add NTO
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
                    Output += "\nYou can now run optimization from Eclipse.";
                }
                catch (Exception ex)
                {
                    Output += $"\n\nERROR: {ex.Message}";
                    Output += $"\nStack trace: {ex.StackTrace}";
                }
            });
        }

        #endregion

        #region Objective Management

        private void OnAddObjective()
        {
            ObjectiveDefinition newObjective;

            if (SelectedObjective != null)
            {
                // Duplicate the selected objective
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

                // Insert below selected row
                int selectedIndex = Objectives.IndexOf(SelectedObjective);
                Objectives.Insert(selectedIndex + 1, newObjective);

                Output += $"\n✓ Added duplicate objective for {newObjective.StructureName}";
            }
            else
            {
                // No selection - add generic objective at end
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
            Output += "\n✓ Objectives table cleared";
            Output += "\nClick 'Populate Objectives' to reload";
        }

        #endregion
    }
}