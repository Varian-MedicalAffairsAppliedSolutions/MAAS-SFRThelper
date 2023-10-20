using MAAS_SFRThelper.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using VMS.TPS.Common.Model.API;

namespace MAAS_SFRThelper.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(ScriptContext context)
        {
            InitializeComponent();

            SphereLatticeTab.Content = new SphereDialog(context);
            RapidRodsTab.Content = new GridDialog(context);
            DataContext = new MainViewModel();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // for .NET Core you need to add UseShellExecute = true
            // see https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
