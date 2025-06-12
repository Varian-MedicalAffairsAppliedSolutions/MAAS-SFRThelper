using MAAS_SFRThelper.ViewModels;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Controls;
using MAAS_SFRThelper.ViewModels;

namespace MAAS_SFRThelper.Views
{
    public partial class EvaluationView : UserControl
    {
        public EvaluationView()
        {
            InitializeComponent();
            // Pass the Canvas reference to the ViewModel after loading
            this.Loaded += (s, e) =>
            {
                if (DataContext is EvaluationViewModel vm)
                {
                    vm.PlotCanvas = PlotCanvas;
                }
            };
        }
    }
   
}

