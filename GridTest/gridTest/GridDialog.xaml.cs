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



namespace GridBlockCreator
{
    /// <summary>
    /// Interaction logic for GridDialog.xaml
    /// </summary>
    public partial class GridDialog : Window
    {

        public GridDialogViewModel vm;


        public GridDialog(ScriptContext context)
        {
            InitializeComponent();
            vm = new GridDialogViewModel(context);
            this.DataContext = vm;

            //if (vm.CloseAction == null)
            //    vm.CloseAction = new Action(this.Close);
        }
    }
}
