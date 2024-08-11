using MAAS_SFRThelper.Models;
using MAAS_SFRThelper.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VMS.TPS.Common.Model.API;

namespace MAAS_SFRThelper.Views
{
    /// <summary>
    /// Interaction logic for GridDialog.xaml
    /// </summary>
    public partial class SphereDialog : UserControl
    {
        private readonly SphereDialogViewModel vm;
        public TextBoxOutputter outputter;

        public SphereDialog(ScriptContext context)
        {
            InitializeComponent();
            vm = new SphereDialogViewModel(context);
            DataContext = vm;
        }

        void TimerTick(object state)
        {
            var who = state as string;
            Console.WriteLine(who);
        }

        private void ToggleCircle(object sender, MouseButtonEventArgs e)
        {
            var selectedEllipse = (System.Windows.Shapes.Ellipse)sender;
            Circle selectedCircle = (Circle)selectedEllipse.DataContext;
            selectedCircle.Selected = !selectedCircle.Selected;
        }


      //  private void CreateLattice(object sender, RoutedEventArgs e)
      //  {
      //      vm.CreateLattice();
      //  }


        private void Cancel(object sender, RoutedEventArgs e)
        {
            //this.Close();

        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

    }
}

