using MAAS_SFRThelper.Models;
using MAAS_SFRThelper.Services;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.Generic;
using System.Threading;
using VMS.TPS.Common.Model.API;

namespace MAAS_SFRThelper.ViewModels
{
    /// <summary>
    /// ViewModel for the Influence Matrix page of the Advanced Methods tab.
    /// Thin adapter only: binds UI state, delegates all work to services.
    /// Hosts P0 plumbing tests plus the plan-eligibility readout.
    /// </summary>
    public class InfluenceMatrixViewModel : BindableBase
    {
        private readonly EsapiWorker _esapi;

        public BindableRunProgress RunProgress { get; }

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
            private set { SetProperty(ref isEligible, value); }
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

        private bool isRunning;
        public bool IsRunning
        {
            get { return isRunning; }
            private set
            {
                if (SetProperty(ref isRunning, value))
                {
                    RunTestCommand.RaiseCanExecuteChanged();
                    TestHdfCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                    RefreshEligibilityCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public DelegateCommand RunTestCommand { get; }
        public DelegateCommand TestHdfCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DelegateCommand RefreshEligibilityCommand { get; }

        public InfluenceMatrixViewModel(EsapiWorker esapi)
        {
            _esapi = esapi;
            RunProgress = new BindableRunProgress();
            RunTestCommand = new DelegateCommand(RunFakeWorkload, () => !IsRunning);
            TestHdfCommand = new DelegateCommand(RunHdfSmokeTest, () => !IsRunning);
            CancelCommand = new DelegateCommand(
                () => RunProgress.RequestCancel(), () => IsRunning);
            RefreshEligibilityCommand = new DelegateCommand(
                RefreshEligibility, () => !IsRunning);

            RefreshEligibility();
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
            catch (System.Exception ex)
            {
                RunProgress.Message("=== HDF5 SMOKE TEST FAILED (unexpected) ===");
                RunProgress.Message(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private void RunFakeWorkload()
        {
            IsRunning = true;
            RunProgress.Reset();
            IRunProgress progress = RunProgress; // worker-facing face of the object
            try
            {
                progress.Message("Fake workload started (100 steps, ~10 s).");
                for (int i = 1; i <= 100; i++)
                {
                    if (progress.CancellationRequested)
                    {
                        progress.Message($"Cancelled at step {i} - cleaning up.");
                        return;
                    }

                    Thread.Sleep(100); // stand-in for a blocking ESAPI dose calc
                    progress.Progress(i);
                    if (i % 10 == 0)
                        progress.Message($"Completed step {i} of 100.");
                }
                progress.Message("Fake workload finished.");
            }
            finally
            {
                IsRunning = false;
            }
        }
    }
}