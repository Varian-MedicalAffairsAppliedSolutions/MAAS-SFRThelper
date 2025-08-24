//using MAAS_SFRThelper.ViewModels;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Data;
//using System.Windows.Documents;
//using System.Windows.Input;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Navigation;
//using System.Windows.Shapes;
//using System.Windows.Controls;
//using MAAS_SFRThelper.ViewModels;

//namespace MAAS_SFRThelper.Views
//{
//    public partial class EvaluationView : UserControl
//    {
//        public EvaluationView()
//        {
//            InitializeComponent();
//            // Pass the Canvas reference to the ViewModel after loading
//            this.Loaded += (s, e) =>
//            {
//                if (DataContext is EvaluationViewModel vm)
//                {
//                    vm.PlotCanvas = PlotCanvas;
//                }
//            };
//        }
//    }

//}

using MAAS_SFRThelper.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

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

                // Set up initial camera position for HelixViewport3D
                SetupDefaultView();
            };
        }

        private void SetupDefaultView()
        {
            // HelixViewport3D automatically handles camera controls
            // but we can set a good initial position
            if (Viewport3D != null)
            {
                // Set a nice 3/4 view
                Viewport3D.Camera.Position = new Point3D(150, 100, 150);
                Viewport3D.Camera.LookDirection = new Vector3D(-1, -0.7, -1);
                Viewport3D.Camera.UpDirection = new Vector3D(0, 1, 0);

                // Optional: Configure viewport settings
                Viewport3D.ShowFrameRate = false; // Turn off for production
                Viewport3D.ShowCoordinateSystem = true;
                Viewport3D.ShowCameraInfo = false;
                Viewport3D.IsHeadLightEnabled = true;

                // Set zoom/pan/rotate speeds if needed
                Viewport3D.ZoomSensitivity = 1.0;
                Viewport3D.RotationSensitivity = 1.0;
                //Viewport3D.PanSensitivity = 1.0;
            }
        }

        // Add keyboard shortcuts for preset views
        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Viewport3D != null && Viewport3D.Visibility == Visibility.Visible)
            {
                switch (e.Key)
                {
                    case Key.Home:
                        // Reset to default view
                        ResetView_Click(null, null);
                        e.Handled = true;
                        break;

                    case Key.F:
                        // Fit view to content
                        Viewport3D.ZoomExtents(500);
                        e.Handled = true;
                        break;

                    case Key.NumPad1: // Front view
                        Viewport3D.Camera.Position = new Point3D(0, 0, 200);
                        Viewport3D.Camera.LookDirection = new Vector3D(0, 0, -1);
                        Viewport3D.Camera.UpDirection = new Vector3D(0, 1, 0);
                        e.Handled = true;
                        break;

                    case Key.NumPad3: // Side view
                        Viewport3D.Camera.Position = new Point3D(200, 0, 0);
                        Viewport3D.Camera.LookDirection = new Vector3D(-1, 0, 0);
                        Viewport3D.Camera.UpDirection = new Vector3D(0, 1, 0);
                        e.Handled = true;
                        break;

                    case Key.NumPad7: // Top view
                        Viewport3D.Camera.Position = new Point3D(0, 200, 0);
                        Viewport3D.Camera.LookDirection = new Vector3D(0, -1, 0);
                        Viewport3D.Camera.UpDirection = new Vector3D(0, 0, -1);
                        e.Handled = true;
                        break;
                }
            }
        }

        // Reset view button handler
        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            if (Viewport3D != null)
            {
                Viewport3D.Camera.Position = new Point3D(150, 100, 150);
                Viewport3D.Camera.LookDirection = new Vector3D(-1, -0.7, -1);
                Viewport3D.Camera.UpDirection = new Vector3D(0, 1, 0);

                // Alternatively, use HelixToolkit's reset method:
                // Viewport3D.ResetCamera();
            }
        }

        // Add this method to your EvaluationView.xaml.cs file
        private void FitView_Click(object sender, RoutedEventArgs e)
        {
            if (Viewport3D != null)
            {
                Viewport3D.ZoomExtents(500);
            }
        }

        // Optional: Export 3D view as image
        private void Export3D_Click(object sender, RoutedEventArgs e)
        {
            if (Viewport3D != null)
            {
                // Export viewport to image
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG Image|*.png|JPEG Image|*.jpg",
                    DefaultExt = ".png",
                    FileName = "3D_Dose_View"
                };

                if (dialog.ShowDialog() == true)
                {
                    Viewport3D.Export(dialog.FileName);
                }
            }
        }
    }
}