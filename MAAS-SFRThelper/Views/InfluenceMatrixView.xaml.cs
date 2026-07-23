using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MAAS_SFRThelper.ViewModels;

namespace MAAS_SFRThelper.Views
{
    /// <summary>
    /// Interaction logic for InfluenceMatrixView.xaml.
    /// Code-behind stays free of logic: view mechanics only. The one piece
    /// of mechanics that MUST live here is the host-window close guard:
    /// closing the window mid-run kills the dispatcher pump the run depends
    /// on and freezes the Eclipse script process (observed 2026-07-23).
    /// While a run is active, close is refused, cancellation is requested,
    /// and the window closes itself when the run stops at a batch boundary.
    /// </summary>
    public partial class InfluenceMatrixView : UserControl
    {
        private Window _hostWindow;
        private bool _closePending;

        public InfluenceMatrixView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_hostWindow == null)
            {
                _hostWindow = Window.GetWindow(this);
                if (_hostWindow != null)
                    _hostWindow.Closing += OnHostWindowClosing;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_hostWindow != null)
            {
                _hostWindow.Closing -= OnHostWindowClosing;
                _hostWindow = null;
            }
        }

        private void OnHostWindowClosing(object sender, CancelEventArgs e)
        {
            InfluenceMatrixViewModel vm = DataContext as InfluenceMatrixViewModel;
            if (vm == null || !vm.IsRunning)
                return;                          // nothing running: close normally

            e.Cancel = true;                     // never allow close mid-run
            if (!_closePending)
            {
                _closePending = true;
                vm.RunEnded += OnRunEndedCloseWindow;
                vm.RequestCloseAfterRun();
            }
            MessageBox.Show(
                "A calculation is running. Cancellation has been requested; this window will close " +
                "automatically at the next batch boundary (this can take a few minutes on heavy runs).\n\n" +
                "Please do not force-close it - that freezes the Eclipse script process.",
                "Run in progress", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void OnRunEndedCloseWindow(object sender, EventArgs e)
        {
            InfluenceMatrixViewModel vm = DataContext as InfluenceMatrixViewModel;
            if (vm != null)
                vm.RunEnded -= OnRunEndedCloseWindow;
            Window w = _hostWindow;
            if (w != null)
                w.Dispatcher.BeginInvoke(new Action(() => { try { w.Close(); } catch { } }));
        }

        private void LogBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ((TextBox)sender).ScrollToEnd();
        }
    }
}
