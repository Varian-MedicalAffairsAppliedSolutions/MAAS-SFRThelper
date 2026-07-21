using Prism.Mvvm;
using System;
using System.Text;
using System.Windows.Threading;

namespace MAAS_SFRThelper.Services
{
    /// <summary>
    /// WPF-aware IRunProgress. Exposes bindable log text and percent for
    /// a view, pumps the dispatcher on each report so the UI stays alive
    /// while work runs on the UI thread (required by ESAPI's threading
    /// model), and turns a Cancel action into the polled flag.
    /// Workers receive this as IRunProgress (read/report only);
    /// the owning ViewModel keeps the concrete type and drives
    /// RequestCancel/Reset. Construct on the UI thread.
    /// </summary>
    public class BindableRunProgress : BindableBase, IRunProgress
    {
        private readonly Dispatcher _dispatcher;
        private readonly StringBuilder _log = new StringBuilder();

        private string logText = string.Empty;
        public string LogText
        {
            get { return logText; }
            private set { SetProperty(ref logText, value); }
        }

        private double percent;
        public double Percent
        {
            get { return percent; }
            private set { SetProperty(ref percent, value); }
        }

        private bool cancellationRequested;
        public bool CancellationRequested
        {
            get { return cancellationRequested; }
        }

        public BindableRunProgress()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        public void Message(string text)
        {
            _log.AppendLine($"[{DateTime.Now:HH:mm:ss}] {text}");
            LogText = _log.ToString();
            Pump();
        }

        public void Progress(double value)
        {
            Percent = Math.Max(0.0, Math.Min(100.0, value));
            Pump();
        }

        /// <summary>Called by the UI (Cancel button). The worker observes
        /// it via CancellationRequested at its next safe boundary.</summary>
        public void RequestCancel()
        {
            cancellationRequested = true;
        }

        /// <summary>Clear log, percent, and cancel flag before a new run.</summary>
        public void Reset()
        {
            _log.Clear();
            LogText = string.Empty;
            Percent = 0.0;
            cancellationRequested = false;
        }

        /// <summary>
        /// Let WPF drain its queue (input, render) before the worker
        /// continues. This is what keeps the window responsive and what
        /// lets a Cancel click actually get processed mid-run.
        /// </summary>
        private void Pump()
        {
            _dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Background);
        }
    }
}