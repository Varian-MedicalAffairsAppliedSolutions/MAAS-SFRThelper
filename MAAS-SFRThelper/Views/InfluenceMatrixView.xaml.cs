using System.Windows.Controls;

namespace MAAS_SFRThelper.Views
{
    /// <summary>
    /// Interaction logic for InfluenceMatrixView.xaml.
    /// Code-behind stays free of logic: view mechanics only.
    /// </summary>
    public partial class InfluenceMatrixView : UserControl
    {
        public InfluenceMatrixView()
        {
            InitializeComponent();
        }

        private void LogBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ((TextBox)sender).ScrollToEnd();
        }
    }
}