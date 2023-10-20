using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace GridBlockJoiner
{
    /// <summary>
    /// Interaction logic for TargetSelector.xaml
    /// </summary>
    public partial class TargetSelector : Window
    {

        public TargetSelectorVM vm;


        public TargetSelector(ScriptContext context)
        {
            InitializeComponent();
            vm = new TargetSelectorVM(context);
            this.DataContext = vm;

            //if (vm.CloseAction == null)
            //    vm.CloseAction = new Action(this.Close);
        }


        private void CreateGrid(object sender, RoutedEventArgs e)
        {
            vm.CreateGrid();
            this.Close();
        }

        private void CancelTask(object sender, RoutedEventArgs e)
        {
            this.Close();

        }

    }
    
}
