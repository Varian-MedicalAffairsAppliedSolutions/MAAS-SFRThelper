using MAAS_SFRThelper.Services;
using Prism.Commands;
using Prism.Mvvm;
using System.Threading;

namespace MAAS_SFRThelper.ViewModels
{
    /// <summary>
    /// ViewModel for the Influence Matrix page of the Advanced Methods tab.
    /// Thin adapter only: binds UI state, delegates all work to services.
    /// Currently hosts a fake workload proving the progress/cancellation
    /// execution model before any ESAPI work is ported.
    /// </summary>
    public class InfluenceMatrixViewModel : BindableBase
    {
        private readonly EsapiWorker _esapi;

        public BindableRunProgress RunProgress { get; }

        private bool isRunning;
        public bool IsRunning
        {
            get { return isRunning; }
            private set
            {
                if (SetProperty(ref isRunning, value))
                {
                    RunTestCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public DelegateCommand RunTestCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public InfluenceMatrixViewModel(EsapiWorker esapi)
        {
            _esapi = esapi;
            RunProgress = new BindableRunProgress();
            RunTestCommand = new DelegateCommand(RunFakeWorkload, () => !IsRunning);
            CancelCommand = new DelegateCommand(
                () => RunProgress.RequestCancel(), () => IsRunning);
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