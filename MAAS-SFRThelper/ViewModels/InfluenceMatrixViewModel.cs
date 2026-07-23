using CalculateInfluenceMatrix;
using MAAS_SFRThelper.Models;
using MAAS_SFRThelper.Services;
using PhotonCalculateInfluenceMatrix;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace MAAS_SFRThelper.ViewModels
{
    /// <summary>
    /// ViewModel for the Influence Matrix page of the Advanced Methods tab.
    /// Thin adapter only: binds parameters and state, delegates all work to
    /// the DoseInfluenceMatrix library and services.
    /// </summary>
    public class InfluenceMatrixViewModel : BindableBase
    {
        public const string TargetNone = "(none - whole field)";

        private readonly EsapiWorker _esapi;

        // Handoff v2 deferred item 2: the folder the last run in this session
        // actually wrote to, captured at launch while the source plan is
        // still the live context. Inspect Output prefers this over deriving
        // the folder from the live plan context, which after a run can be
        // the scratch plan (zD_...) and points at the wrong directory.
        private string _lastRunFolder = null;

        public BindableRunProgress RunProgress { get; }

        // ---------------- eligibility ----------------
        private List<EligibilityCheck> eligibilityChecks;
        public List<EligibilityCheck> EligibilityChecks
        {
            get { return eligibilityChecks; }
            private set { SetProperty(ref eligibilityChecks, value); }
        }

        private bool isEligible;
        public bool IsEligible
        {
            get { return isEligible; }
            private set
            {
                if (SetProperty(ref isEligible, value))
                    RunExtractionCommand.RaiseCanExecuteChanged();
            }
        }

        private string bannerStatus;
        public string BannerStatus
        {
            get { return bannerStatus; }
            private set { SetProperty(ref bannerStatus, value); }
        }

        private string eligibilityBanner;
        public string EligibilityBanner
        {
            get { return eligibilityBanner; }
            private set { SetProperty(ref eligibilityBanner, value); }
        }

        // ---------------- parameters ----------------
        private float beamletSizeX = 2.5f;
        public float BeamletSizeX
        {
            get { return beamletSizeX; }
            set { SetProperty(ref beamletSizeX, value); }
        }

        private float beamletSizeY = 5.0f;
        public float BeamletSizeY
        {
            get { return beamletSizeY; }
            set { SetProperty(ref beamletSizeY, value); }
        }

        public List<string> TargetStructures { get; } = new List<string>();
        private string selectedTarget = TargetNone;
        public string SelectedTarget
        {
            get { return selectedTarget; }
            set { SetProperty(ref selectedTarget, value); }
        }

        private float targetMarginMM = 10.0f;
        public float TargetMarginMM
        {
            get { return targetMarginMM; }
            set { SetProperty(ref targetMarginMM, value); }
        }

        private double cutoffValue = 0.0;
        public double CutoffValue
        {
            get { return cutoffValue; }
            set
            {
                if (SetProperty(ref cutoffValue, value))
                    RaisePropertyChanged(nameof(CutoffDeviates));
            }
        }
        public bool CutoffDeviates => CutoffValue != 0.0;

        private float doseScalingFactor = 1.0f;
        public float DoseScalingFactor
        {
            get { return doseScalingFactor; }
            set
            {
                if (SetProperty(ref doseScalingFactor, value))
                    RaisePropertyChanged(nameof(ScalingDeviates));
            }
        }
        public bool ScalingDeviates => Math.Abs(DoseScalingFactor - 1.0f) > 1e-6f;

        private int batchSize = 5;      // upstream's shipped default
        public int BatchSize
        {
            get { return batchSize; }
            set { SetProperty(ref batchSize, value); }
        }

        private int maxRetry = 4;       // upstream's shipped default
        public int MaxRetry
        {
            get { return maxRetry; }
            set { SetProperty(ref maxRetry, value); }
        }

        public List<string> CalcModels { get; } = new List<string>();
        private string selectedCalcModel;
        public string SelectedCalcModel
        {
            get { return selectedCalcModel; }
            set { SetProperty(ref selectedCalcModel, value); }
        }

        private string gridSizeCM = "0.25";
        public string GridSizeCM
        {
            get { return gridSizeCM; }
            set { SetProperty(ref gridSizeCM, value); }
        }

        private bool exportFullMatrix = true;
        public bool ExportFullMatrix
        {
            get { return exportFullMatrix; }
            set { SetProperty(ref exportFullMatrix, value); }
        }

        private string outputRoot;
        public string OutputRoot
        {
            get { return outputRoot; }
            set { SetProperty(ref outputRoot, value); }
        }

        // Suggested machine names for the editable override combo. Free text
        // is still allowed; this list just spares users guessing the string.
        public List<string> MachineSuggestions { get; } = new List<string>
        {
            "", "Edge", "TrueBeam", "TrueBeamSTx", "Halcyon", "VitalBeam", "Clinac iX"
        };

        private string overrideMachine = "";
        public string OverrideMachine
        {
            get { return overrideMachine; }
            set
            {
                if (SetProperty(ref overrideMachine, value))
                    RaisePropertyChanged(nameof(OverrideActive));
            }
        }

        private string overrideEnergy = "";
        public string OverrideEnergy
        {
            get { return overrideEnergy; }
            set
            {
                if (SetProperty(ref overrideEnergy, value))
                    RaisePropertyChanged(nameof(OverrideActive));
            }
        }

        public bool OverrideActive =>
            !string.IsNullOrWhiteSpace(OverrideMachine) || !string.IsNullOrWhiteSpace(OverrideEnergy);

        // ---------------- run state ----------------
        private bool isRunning;
        public bool IsRunning
        {
            get { return isRunning; }
            private set
            {
                if (SetProperty(ref isRunning, value))
                {
                    RunExtractionCommand.RaiseCanExecuteChanged();
                    TestHdfCommand.RaiseCanExecuteChanged();
                    InspectOutputCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                    RefreshEligibilityCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public DelegateCommand RunExtractionCommand { get; }
        public DelegateCommand TestHdfCommand { get; }
        public DelegateCommand InspectOutputCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DelegateCommand RefreshEligibilityCommand { get; }

        public InfluenceMatrixViewModel(EsapiWorker esapi)
        {
            _esapi = esapi;
            RunProgress = new BindableRunProgress();

            RunExtractionCommand = new DelegateCommand(RunExtraction, () => IsEligible && !IsRunning);
            TestHdfCommand = new DelegateCommand(RunHdfSmokeTest, () => !IsRunning);
            InspectOutputCommand = new DelegateCommand(InspectOutput, () => !IsRunning);
            CancelCommand = new DelegateCommand(() => RunProgress.RequestCancel(), () => IsRunning);
            RefreshEligibilityCommand = new DelegateCommand(RefreshEligibility, () => !IsRunning);

            OutputRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SFRThelper");

            LoadContextLists();
            RefreshEligibility();
        }

        private void LoadContextLists()
        {
            TargetStructures.Add(TargetNone);
            _esapi.RunWithWait(sc =>
            {
                ExternalPlanSetup plan = sc.ExternalPlanSetup;
                if (plan == null)
                    return;

                if (plan.StructureSet != null)
                {
                    foreach (Structure s in plan.StructureSet.Structures)
                    {
                        if (!string.IsNullOrEmpty(s.Id))
                            TargetStructures.Add(s.Id);
                    }
                }

                foreach (string model in plan.GetModelsForCalculationType(CalculationType.PhotonVolumeDose))
                    CalcModels.Add(model);

                string current = plan.GetCalculationModel(CalculationType.PhotonVolumeDose);
                SelectedCalcModel = !string.IsNullOrEmpty(current) && CalcModels.Contains(current)
                    ? current
                    : CalcModels.FirstOrDefault();
            });

            string ptv = TargetStructures.FirstOrDefault(
                s => s != TargetNone && s.IndexOf("PTV", StringComparison.OrdinalIgnoreCase) >= 0);
            SelectedTarget = ptv ?? TargetNone;
        }

        private void RefreshEligibility()
        {
            List<EligibilityCheck> checks = null;
            _esapi.RunWithWait(sc =>
            {
                checks = PlanEligibilityService.CheckPlan(sc.ExternalPlanSetup);
            });

            EligibilityChecks = checks;
            IsEligible = PlanEligibilityService.IsEligible(checks);
            bool warn = PlanEligibilityService.HasWarnings(checks);

            BannerStatus = !IsEligible ? "Fail" : (warn ? "Warn" : "Pass");
            EligibilityBanner = !IsEligible
                ? "Plan is NOT eligible - resolve the blocking items below."
                : warn
                    ? "Plan is eligible with notes - review the warnings below."
                    : "Plan is eligible for influence matrix extraction.";
        }

        private void RunExtraction()
        {
            RunProgress.Reset();

            if (string.IsNullOrWhiteSpace(OutputRoot))
            {
                RunProgress.Message("Output root folder is empty - set it before running.");
                return;
            }
            if (BeamletSizeX <= 0 || BeamletSizeY <= 0 || BatchSize <= 0 || MaxRetry <= 0)
            {
                RunProgress.Message("Beamlet sizes, batch size, and retry count must be positive.");
                return;
            }
            if (string.IsNullOrEmpty(SelectedCalcModel))
            {
                RunProgress.Message("No volume dose calculation model selected.");
                return;
            }

            string targetId = (SelectedTarget == TargetNone) ? null : SelectedTarget;

            IsRunning = true;
            RunProgress.Message("=== Influence matrix extraction ===");
            RunProgress.Message($"Beamlet {BeamletSizeX} x {BeamletSizeY} mm; target: " +
                (targetId ?? "none (whole field - no envelope pruning)") +
                $"; margin {TargetMarginMM} mm");
            RunProgress.Message($"Batch {BatchSize}; retry {MaxRetry}; model {SelectedCalcModel}; grid {GridSizeCM} cm; " +
                $"full matrix: {(ExportFullMatrix ? "yes" : "no")}");
            if (CutoffDeviates)
                RunProgress.Message($"WARNING: cutoff = {CutoffValue} (nonzero). The sparse matrix will drop " +
                    "entries at or below this value; the exact reconstruction identity will NOT hold for this file.");
            if (ScalingDeviates)
                RunProgress.Message($"WARNING: DoseScalingFactor = {DoseScalingFactor} (not 1). Stored values " +
                    "are scaled; downstream consumers must honor the dose_units metadata.");

            RunProgressDisplayAdapter adapter = new RunProgressDisplayAdapter(RunProgress);

            // Fire-and-forget through the dispatcher, NOT RunWithWait:
            // EsapiWorker's "worker" is the UI thread itself, and RunWithWait
            // is BeginInvoke(...).Wait() - blocking the UI thread on work that
            // pumps the same UI thread is a wait-inside-a-wait deadlock (the
            // freeze observed on the first run). Run() queues the work onto
            // the dispatcher's normal loop: the click handler returns, the
            // pump keeps the window alive between computations, and Cancel
            // is processed at pumps - the exact context the fake workload
            // already proved. Completion and errors are handled inside the
            // queued action, so the truthful-exit discipline is unchanged.
            _esapi.Run(sc =>
            {
                try
                {
                    // Capture the run folder NOW, while the source plan is the
                    // live context, using the library's own path convention -
                    // writer and inspector share one function and cannot drift.
                    _lastRunFolder = PhotonInfluenceMatrixCalc.GetPlanResultsPath(
                        OutputRoot, sc.Patient, sc.ExternalPlanSetup);

                    PhotonInfluenceMatrixCalc.Calculate(
                        sc.Patient, sc.Course, sc.ExternalPlanSetup,
                        CutoffValue, ExportFullMatrix, MaxRetry,
                        BeamletSizeX, BeamletSizeY,
                        targetId, TargetMarginMM,
                        BatchSize, SelectedCalcModel, GridSizeCM,
                        DoseScalingFactor, OutputRoot,
                        adapter, () => RunProgress.CancellationRequested,
                        string.IsNullOrWhiteSpace(OverrideMachine) ? null : OverrideMachine.Trim(),
                        string.IsNullOrWhiteSpace(OverrideEnergy) ? null : OverrideEnergy.Trim());
                }
                catch (Exception ex)
                {
                    RunProgress.Message("=== EXTRACTION FAILED ===");
                    for (Exception e = ex; e != null; e = e.InnerException)
                        RunProgress.Message(e.GetType().Name + ": " + e.Message);
                }
                finally
                {
                    IsRunning = false;
                }
            });
        }

        private void InspectOutput()
        {
            IsRunning = true;
            RunProgress.Reset();
            try
            {
                string runFolder = _lastRunFolder;
                if (runFolder != null)
                {
                    RunProgress.Message("Inspecting the folder written by this session's run.");
                }
                else
                {
                    // No run this session: fall back to deriving the folder
                    // from the live plan context via the library's shared
                    // path helper (single source of truth for the convention).
                    _esapi.RunWithWait(sc =>
                    {
                        if (sc.Patient != null && sc.ExternalPlanSetup != null)
                            runFolder = PhotonInfluenceMatrixCalc.GetPlanResultsPath(
                                OutputRoot, sc.Patient, sc.ExternalPlanSetup);
                    });
                    if (runFolder == null)
                    {
                        RunProgress.Message("No plan in context - cannot locate a run folder.");
                        return;
                    }
                    RunProgress.Message("No run in this session - deriving the folder from the live plan " +
                        "context. If the active plan is a scratch plan (zD_...), open the source plan " +
                        "and inspect again.");
                }
                Inspection_Helpers.InspectRunFolder(runFolder, RunProgress.Message);
            }
            catch (Exception ex)
            {
                RunProgress.Message("Inspection failed: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private void RunHdfSmokeTest()
        {
            IsRunning = true;
            RunProgress.Reset();
            try
            {
                bool ok = HdfSmokeTest.Run(RunProgress);
                RunProgress.Message(ok
                    ? "=== HDF5 SMOKE TEST PASSED ==="
                    : "=== HDF5 SMOKE TEST FAILED ===");
            }
            catch (Exception ex)
            {
                RunProgress.Message("=== HDF5 SMOKE TEST FAILED (unexpected) ===");
                RunProgress.Message(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                IsRunning = false;
            }
        }
    }
}