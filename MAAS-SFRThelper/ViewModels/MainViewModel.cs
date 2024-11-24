using MAAS_SFRThelper.Services;
using MAAS_SFRThelper.Views;
using Prism.Mvvm;

namespace MAAS_SFRThelper.ViewModels
{
    public class MainViewModel : BindableBase
    {
        private string postText;
        public string PostText
        {
            get { return postText; }
            set { SetProperty(ref postText, value); }
        }

        private string windowTitle;
        public string WindowTitle
        {
            get { return windowTitle; }
            set { SetProperty(ref windowTitle, value); }
        }

        public OptimizationViewModel OptimizationViewModel { get; }
        internal EvaluationViewModel EvaluationViewModel { get; }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
             );
            e.Handled = true;
        }

        public MainViewModel(EsapiWorker esapi)
        {
            OptimizationViewModel = new OptimizationViewModel(esapi);
            EvaluationViewModel = new EvaluationViewModel(esapi);
            WindowTitle = AppConfig.GetValueByKey("ValidForClinicalUse") == "true" ? "MAAS-SFRTHelper" : "MAAS-SFRTHelper  \t NOT VALIDATED FOR CLINICAL USE";

            //var isDebug = MAAS_SFRThelper.Properties.Settings.Default.Debug;
            ////MessageBox.Show($"Display Terms {isDebug}");
            //PostText = "";
            //if (isDebug) { PostText += " *** Not Validated For Clinical Use ***"; }
        }
    }
}
