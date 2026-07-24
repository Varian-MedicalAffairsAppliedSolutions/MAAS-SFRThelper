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

        // Default 1e-6 Gy/MU (was 0.0): cutoff 0 keeps float-dust residuals
        // and produced 70%-dense gigabyte files; 1e-6 measured 4.5% density
        // at unchanged physics. Set 0 explicitly for identity-exact output.
        private double cutoffValue = 1.0e-6;
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

        // Default OFF (was ON): the dense export is a validation tool
        // (~1.5 GB per beam) and should be opted into, not stumbled into.
        private bool exportFullMatrix = false;
        public bool ExportFullMatrix
        {
            get { return exportFullMatrix; }
            set { SetProperty(ref exportFullMatrix, value); }
        }

        // When on, the run also saves the "answer key": Eclipse's one-shot
        // dose of each real field, used later to check how closely the
        // summed matrix reproduces the real thing. Off for everyday runs.
        private bool exportReferenceDose = false;
        public bool ExportReferenceDose
        {
            get { return exportReferenceDose; }
            set { SetProperty(ref exportReferenceDose, value); }
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
                    InspectOutputCommand.RaiseCanExecuteChanged();
                    ValidateReconstructionCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                    RefreshEligibilityCommand.RaiseCanExecuteChanged();
                }
            }
        }

        // Busy flag for Inspect Output: drives the progress bar's
        // indeterminate mode, since inspection has no meaningful percent.
        private bool isInspecting;
        public bool IsInspecting
        {
            get { return isInspecting; }
            private set { SetProperty(ref isInspecting, value); }
        }

        // Raised when a calculation run finishes (success, failure, or
        // cancellation). The view uses it to complete a deferred window close.
        public event EventHandler RunEnded;

        // Called by the view when the user tries to close the window during
        // an active run: request cancellation and log; the window closes via
        // RunEnded once the run stops at the next batch boundary.
        public void RequestCloseAfterRun()
        {
            RunProgress.RequestCancel();
            RunProgress.Message("Window close requested during an active run: cancellation requested; " +
                "the window will close automatically once the run stops at the next batch boundary. " +
                "(Forcing the window closed mid-run freezes the Eclipse script process.)");
        }

        public DelegateCommand RunExtractionCommand { get; }
        public DelegateCommand InspectOutputCommand { get; }
        public DelegateCommand ValidateReconstructionCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DelegateCommand RefreshEligibilityCommand { get; }

        public InfluenceMatrixViewModel(EsapiWorker esapi)
        {
            _esapi = esapi;
            RunProgress = new BindableRunProgress();

            RunExtractionCommand = new DelegateCommand(RunExtraction, () => IsEligible && !IsRunning);
            InspectOutputCommand = new DelegateCommand(InspectOutput, () => !IsRunning);
            ValidateReconstructionCommand = new DelegateCommand(ValidateReconstruction, () => !IsRunning);
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
            RunProgress.Message("=== Influence matrix calculation ===");
            RunProgress.Message($"Beamlet {BeamletSizeX} x {BeamletSizeY} mm; target: " +
                (targetId ?? "none (whole field - no envelope pruning)") +
                $"; margin {TargetMarginMM} mm");
            // Cutoff and scaling are echoed unconditionally so the log always
            // records the values the run ACTUALLY used - a binding hiccup in
            // the textbox (e.g. an unparseable entry silently leaving the old
            // value in place) becomes visible here instead of hiding.
            RunProgress.Message($"Batch {BatchSize}; retry {MaxRetry}; model {SelectedCalcModel}; grid {GridSizeCM} cm; " +
                $"cutoff {CutoffValue} Gy/MU; scaling {DoseScalingFactor}; " +
                $"full matrix: {(ExportFullMatrix ? "yes" : "no")}; reference dose: {(ExportReferenceDose ? "yes" : "no")}");
            if (CutoffDeviates)
                RunProgress.Message($"Note: cutoff = {CutoffValue} Gy/MU. Entries at or below this value are zeroed " +
                    "in the matrix before export - the sparse and full exports are the SAME thresholded matrix, " +
                    "and the discarded mass is audited per beamlet. Set cutoff 0 explicitly for the unthresholded, " +
                    "bit-exact matrix.");
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
                    _lastRunFolder = PhotonInfluenceMatrixCalc.CreateRunFolderPath(
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
                        string.IsNullOrWhiteSpace(OverrideEnergy) ? null : OverrideEnergy.Trim(),
                        _lastRunFolder,
                        ExportReferenceDose);
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
                    // Signals the view that a pending window-close (requested
                    // mid-run) may now proceed safely.
                    RunEnded?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        // Finds the folder to work on: the one this session's run wrote,
        // or - if no run happened in this window - the newest run folder
        // belonging to the open plan.
        private string ResolveRunFolder()
        {
            string runFolder = _lastRunFolder;
            if (runFolder != null)
            {
                RunProgress.Message("Using the folder written by this session's run.");
                return runFolder;
            }
            _esapi.RunWithWait(sc =>
            {
                if (sc.Patient != null && sc.ExternalPlanSetup != null)
                    runFolder = PhotonInfluenceMatrixCalc.GetPlanResultsPath(
                        OutputRoot, sc.Patient, sc.ExternalPlanSetup);
            });
            if (runFolder == null)
            {
                RunProgress.Message("No plan open - cannot find a run folder.");
                return null;
            }
            try
            {
                if (Directory.Exists(runFolder))
                {
                    string[] runDirs = Directory.GetDirectories(runFolder, "run_*");
                    if (runDirs.Length > 0)
                    {
                        Array.Sort(runDirs, StringComparer.OrdinalIgnoreCase);
                        runFolder = runDirs[runDirs.Length - 1];
                    }
                }
            }
            catch { /* fall back to the plan folder itself */ }
            RunProgress.Message("No run in this session - using the newest output folder for the open plan.");
            return runFolder;
        }

        private void InspectOutput()
        {
            IsRunning = true;
            IsInspecting = true;
            RunProgress.Reset();
            try
            {
                string runFolder = ResolveRunFolder();
                if (runFolder == null)
                    return;
                Inspection_Helpers.InspectRunFolder(runFolder, RunProgress.Message);
            }
            catch (Exception ex)
            {
                RunProgress.Message("Inspection failed: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                IsInspecting = false;
                IsRunning = false;
            }
        }

        // The "grader": adds up all the small pieces of the saved matrix and
        // compares the total against the answer key saved by the reference
        // checkbox, then reports how close they are and writes the CSV files
        // used for figures. Works purely on files; Eclipse is not involved.
        private void ValidateReconstruction()
        {
            IsRunning = true;
            IsInspecting = true;
            RunProgress.Reset();
            try
            {
                string runFolder = ResolveRunFolder();
                if (runFolder == null)
                    return;
                Reconstruction_Validator.ValidateRunFolder(runFolder, RunProgress.Message);
            }
            catch (Exception ex)
            {
                RunProgress.Message("Grading failed: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                IsInspecting = false;
                IsRunning = false;
            }
        }

        // Test HDF5 removed (handoff deferred item 3): native-DLL deployment
        // risk is retired; HdfSmokeTest.cs stays in Services should it ever
        // be needed for a new environment bring-up.
    }
}