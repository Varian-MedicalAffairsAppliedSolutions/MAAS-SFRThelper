using MAAS_SFRThelper.Models;
using MAAS_SFRThelper.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using VMS.TPS.Common.Model.API;

namespace MAAS_SFRThelper.Views
{
    /// <summary>
    /// Interaction logic for GridDialog.xaml
    /// </summary>
    public partial class GridDialog : UserControl
    {
        public GridDialogViewModel vm;

        public GridDialog(EsapiWorker ew)
        {
            InitializeComponent();
            vm = new GridDialogViewModel(ew);
            this.DataContext = vm;
        }

        private void ToggleCircle(object sender, MouseButtonEventArgs e)
        {
            var selectedEllipse = (System.Windows.Shapes.Ellipse)sender;
            Circle selectedCircle = (Circle)selectedEllipse.DataContext;
            selectedCircle.Selected = !selectedCircle.Selected;
        }

        private void CreateGrid(object sender, RoutedEventArgs e)
        {
            vm.CreateGrid();
        }

        private void CreateGridAndInverse(object sender, RoutedEventArgs e)
        {
            vm.CreateGridAndInverse();
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
        }
    }
}
