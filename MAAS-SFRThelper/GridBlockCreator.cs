//using MAAS_SFRThelper.Services;
//using MAAS_SFRThelper.ViewModels;
//using MAAS_SFRThelper.Views;
//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using System.Runtime.CompilerServices;
//using System.Threading;
//using System.Windows;
//using System.Windows.Threading;
//using VMS.TPS.Common.Model.API;

//using System.Windows.Media.Imaging;
//using MAAS.Common.EulaVerification;

//namespace VMS.TPS
//{
//    public class Script
//    {
//        // Define the project information for EULA verification
//        private const string PROJECT_NAME = "SFRThelper";
//        private const string PROJECT_VERSION = "1.0.0";
//        private const string LICENSE_URL = "https://varian-medicalaffairsappliedsolutions.github.io/MAAS-SFRThelper/";
//        private const string GITHUB_URL = "https://github.com/Varian-MedicalAffairsAppliedSolutions/MAAS-SFRThelper";

//        public Script()
//        {
//        }

//        [MethodImpl(MethodImplOptions.NoInlining)]
//        public void Execute(ScriptContext context)
//        {
//            try
//            {
//                // First, initialize the AppConfig - THIS IS CRUCIAL
//                // based on the AppConfig implementation you shared
//                string scriptPath = Assembly.GetExecutingAssembly().Location;
//                try
//                {
//                    // Initialize the AppConfig with the executing assembly path
//                    AppConfig.GetAppConfig(scriptPath);
//                    //MessageBox.Show("AppConfig initialized successfully", "Configuration", MessageBoxButton.OK, MessageBoxImage.Information);
//                }
//                catch (Exception configEx)
//                {
//                    MessageBox.Show($"Failed to initialize AppConfig: {configEx.Message}\n\nPath: {scriptPath}\n\nContinuing without configuration...",
//                                   "Configuration Warning",
//                                   MessageBoxButton.OK,
//                                   MessageBoxImage.Warning);

//                    // Create an empty dictionary as fallback if the config file can't be loaded
//                    AppConfig.m_appSettings = new Dictionary<string, string>();
//                }

//                // Set up the EulaConfig directory
//                string scriptDirectory = Path.GetDirectoryName(scriptPath);
//                EulaConfig.ConfigDirectory = scriptDirectory;

//                // EULA verification
//                var eulaVerifier = new EulaVerifier(PROJECT_NAME, PROJECT_VERSION, LICENSE_URL);
//                var eulaConfig = EulaConfig.Load(PROJECT_NAME);
//                if (eulaConfig.Settings == null)
//                {
//                    eulaConfig.Settings = new ApplicationSettings();
//                }

//                if (!eulaVerifier.IsEulaAccepted())
//                {
//                    MessageBox.Show(
//                        $"This version of {PROJECT_NAME} (v{PROJECT_VERSION}) requires license acceptance before first use.\n\n" +
//                        "You will be prompted to provide an access code. Please follow the instructions to obtain your code.",
//                        "License Acceptance Required",
//                        MessageBoxButton.OK,
//                        MessageBoxImage.Information);

//                    BitmapImage qrCode = null;
//                    try
//                    {
//                        string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
//                        qrCode = new BitmapImage(new Uri($"pack://application:,,,/{assemblyName};component/Resources/qrcode.bmp"));
//                    }
//                    catch (Exception ex)
//                    {
//                        System.Diagnostics.Debug.WriteLine($"Error loading QR code: {ex.Message}");
//                    }

//                    if (!eulaVerifier.ShowEulaDialog(qrCode))
//                    {
//                        MessageBox.Show(
//                            "License acceptance is required to use this application.\n\n" +
//                            "The application will now close.",
//                            "License Not Accepted",
//                            MessageBoxButton.OK,
//                            MessageBoxImage.Warning);
//                        return;
//                    }
//                }

//                // Check if patient/plan is selected
//                if (context.Patient == null || context.PlanSetup == null)
//                {
//                    MessageBox.Show("No active patient/plan selected - exiting",
//                                    "MAAS-SFRTHelper",
//                                    MessageBoxButton.OK,
//                                    MessageBoxImage.Exclamation);
//                    return;
//                }

//                // Continue with original expiration check
//                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
//                var noexp_path = Path.Combine(path, "NOEXPIRE");
//                bool foundNoExpire = File.Exists(noexp_path);

//                var provider = new CultureInfo("en-US");
//                var asmCa = typeof(Script).Assembly.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(AssemblyExpirationDate));

//                // Check if we have a valid expiration date and if the app is expired
//                if (asmCa != null && asmCa.ConstructorArguments.Count > 0)
//                {
//                    DateTime endDate;
//                    if (DateTime.TryParse(asmCa.ConstructorArguments.FirstOrDefault().Value as string, provider, DateTimeStyles.None, out endDate)
//                        && (DateTime.Now <= endDate || foundNoExpire))
//                    {
//                        // Display opening msg based on validation status
//                        string msg;

//                        if (!eulaConfig.Settings.Validated)
//                        {
//                            // First-time message
//                            msg = $"The current MAAS-SFRThelper application is provided AS IS as a non-clinical, research only tool in evaluation only. The current " +
//                            $"application will only be available until {endDate.Date} after which the application will be unavailable. " +
//                            $"By Clicking 'Yes' you agree that this application will be evaluated and not utilized in providing planning decision support\n\n" +
//                            $"Newer builds with future expiration dates can be found here: {GITHUB_URL}\n\n" +
//                            "See the FAQ for more information on how to remove this pop-up and expiration";
//                        }
//                        else
//                        {
//                            // Returning user message
//                            msg = $"Application will only be available until {endDate.Date} after which the application will be unavailable. " +
//                            "By Clicking 'Yes' you agree that this application will be evaluated and not utilized in providing planning decision support\n\n" +
//                            $"Newer builds with future expiration dates can be found here: {GITHUB_URL} \n\n" +
//                            "See the FAQ for more information on how to remove this pop-up and expiration";
//                        }

//                        if (!foundNoExpire)
//                        {
//                            bool userAgree = MessageBox.Show(msg,
//                                                            "MAAS-SFRTHelper",
//                                                            MessageBoxButton.YesNo,
//                                                            MessageBoxImage.Question) == MessageBoxResult.Yes;
//                            if (!userAgree)
//                            {
//                                return;
//                            }
//                        }

//                        try
//                        {
//                            // Create the ESAPI worker in the main thread with careful exception handling
//                            EsapiWorker esapiWorker = null;
//                            try
//                            {
//                                esapiWorker = new EsapiWorker(context);
//                            }
//                            catch (Exception workerEx)
//                            {
//                                MessageBox.Show($"Error creating EsapiWorker: {workerEx.Message}\n\n{workerEx.StackTrace}",
//                                               "Worker Initialization Error",
//                                               MessageBoxButton.OK,
//                                               MessageBoxImage.Error);
//                                return;
//                            }

//                            // This will prevent the script from exiting until the window is closed
//                            DispatcherFrame frame = new DispatcherFrame();

//                            // Set up the thread that will run the UI
//                            Thread thread = new Thread(() =>
//                            {
//                                try
//                                {
//                                    // Make sure the AppConfig is available in this thread too
//                                    if (AppConfig.m_appSettings == null)
//                                    {
//                                        AppConfig.GetAppConfig(scriptPath);
//                                    }

//                                    // Create and show the window
//                                    var mainWindow = new MainWindow(esapiWorker);
//                                    mainWindow.ShowDialog();
//                                }
//                                catch (Exception ex)
//                                {
//                                    MessageBox.Show($"UI Thread Error: {ex.Message}\n\n{ex.StackTrace}",
//                                                   "Error in UI Thread",
//                                                   MessageBoxButton.OK,
//                                                   MessageBoxImage.Error);
//                                }
//                                finally
//                                {
//                                    // Always ensure the frame is released
//                                    frame.Continue = false;
//                                }
//                            });

//                            // Configure the thread properly
//                            thread.SetApartmentState(ApartmentState.STA);
//                            thread.IsBackground = true;
//                            thread.Start();

//                            // Wait until the window is closed
//                            Dispatcher.PushFrame(frame);
//                        }
//                        catch (Exception ex)
//                        {
//                            MessageBox.Show($"Thread Creation Error: {ex.Message}\n\n{ex.StackTrace}",
//                                           "Error Starting Application",
//                                           MessageBoxButton.OK,
//                                           MessageBoxImage.Error);
//                        }
//                    }
//                    else
//                    {
//                        MessageBox.Show($"Application has expired. Newer builds with future expiration dates can be found here: {GITHUB_URL}",
//                                        "MAAS-SFRTHelper",
//                                        MessageBoxButton.OK,
//                                        MessageBoxImage.Information);
//                    }
//                }
//                else
//                {
//                    // If there's no expiration date attribute, warn and continue
//                    MessageBox.Show("Could not verify application expiration date. The application will continue, but may have limited functionality.",
//                                    "MAAS-SFRTHelper",
//                                    MessageBoxButton.OK,
//                                    MessageBoxImage.Warning);

//                    // Try to launch anyway 
//                    try
//                    {
//                        EsapiWorker esapiWorker = new EsapiWorker(context);
//                        DispatcherFrame frame = new DispatcherFrame();

//                        // Launch UI thread
//                        Thread thread = new Thread(() =>
//                        {
//                            try
//                            {
//                                // Ensure AppConfig is initialized in this thread too
//                                if (AppConfig.m_appSettings == null)
//                                {
//                                    AppConfig.GetAppConfig(scriptPath);
//                                }

//                                var mainWindow = new MainWindow(esapiWorker);
//                                mainWindow.ShowDialog();
//                            }
//                            catch (Exception ex)
//                            {
//                                MessageBox.Show($"Error in UI thread: {ex.Message}\n\n{ex.StackTrace}",
//                                               "Error",
//                                               MessageBoxButton.OK,
//                                               MessageBoxImage.Error);
//                            }
//                            finally
//                            {
//                                frame.Continue = false;
//                            }
//                        });

//                        thread.SetApartmentState(ApartmentState.STA);
//                        thread.IsBackground = true;
//                        thread.Start();

//                        Dispatcher.PushFrame(frame);
//                    }
//                    catch (Exception ex)
//                    {
//                        MessageBox.Show($"Error launching application: {ex.Message}\n\n{ex.StackTrace}",
//                                       "MAAS-SFRTHelper",
//                                       MessageBoxButton.OK,
//                                       MessageBoxImage.Error);
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                // More detailed error reporting
//                MessageBox.Show($"Main Thread Error: {ex.Message}\n\n{ex.StackTrace}",
//                               "MAAS-SFRTHelper Error",
//                               MessageBoxButton.OK,
//                               MessageBoxImage.Error);
//            }
//        }
//    }
//}

using MAAS_SFRThelper.Services;
using MAAS_SFRThelper.ViewModels;
using MAAS_SFRThelper.Views;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using VMS.TPS.Common.Model.API;

using System.Windows.Media.Imaging;
using MAAS.Common.EulaVerification;

namespace VMS.TPS
{
    public class Script
    {
        // Define the project information for EULA verification
        private const string PROJECT_NAME = "SFRThelper";
        private const string PROJECT_VERSION = "1.0.0";
        private const string LICENSE_URL = "https://varian-medicalaffairsappliedsolutions.github.io/MAAS-SFRThelper/";
        private const string GITHUB_URL = "https://github.com/Varian-MedicalAffairsAppliedSolutions/MAAS-SFRThelper";

        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context)
        {
            try
            {
                // First, initialize the AppConfig - THIS IS CRUCIAL
                string scriptPath = Assembly.GetExecutingAssembly().Location;
                try
                {
                    AppConfig.GetAppConfig(scriptPath);
                }
                catch (Exception configEx)
                {
                    MessageBox.Show($"Failed to initialize AppConfig: {configEx.Message}\n\nPath: {scriptPath}\n\nContinuing without configuration...",
                                   "Configuration Warning",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Warning);

                    AppConfig.m_appSettings = new Dictionary<string, string>();
                }

                // Set up the EulaConfig directory
                string scriptDirectory = Path.GetDirectoryName(scriptPath);
                EulaConfig.ConfigDirectory = scriptDirectory;

                // EULA verification
                var eulaVerifier = new EulaVerifier(PROJECT_NAME, PROJECT_VERSION, LICENSE_URL);
                var eulaConfig = EulaConfig.Load(PROJECT_NAME);
                if (eulaConfig.Settings == null)
                {
                    eulaConfig.Settings = new ApplicationSettings();
                }

                if (!eulaVerifier.IsEulaAccepted())
                {
                    MessageBox.Show(
                        $"This version of {PROJECT_NAME} (v{PROJECT_VERSION}) requires license acceptance before first use.\n\n" +
                        "You will be prompted to provide an access code. Please follow the instructions to obtain your code.",
                        "License Acceptance Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    BitmapImage qrCode = null;
                    try
                    {
                        string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                        qrCode = new BitmapImage(new Uri($"pack://application:,,,/{assemblyName};component/Resources/qrcode.bmp"));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading QR code: {ex.Message}");
                    }

                    if (!eulaVerifier.ShowEulaDialog(qrCode))
                    {
                        MessageBox.Show(
                            "License acceptance is required to use this application.\n\n" +
                            "The application will now close.",
                            "License Not Accepted",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }

                // Check if patient/plan is selected
                if (context.Patient == null || context.PlanSetup == null)
                {
                    MessageBox.Show("No active patient/plan selected - exiting",
                                    "MAAS-SFRTHelper",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Exclamation);
                    return;
                }

                // Continue with original expiration check
                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var noexp_path = Path.Combine(path, "NOEXPIRE");
                bool foundNoExpire = File.Exists(noexp_path);

                var provider = new CultureInfo("en-US");
                var asmCa = typeof(Script).Assembly.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(AssemblyExpirationDate));

                // Check if we have a valid expiration date and if the app is expired
                if (asmCa != null && asmCa.ConstructorArguments.Count > 0)
                {
                    DateTime endDate;
                    if (DateTime.TryParse(asmCa.ConstructorArguments.FirstOrDefault().Value as string, provider, DateTimeStyles.None, out endDate)
                        && (DateTime.Now <= endDate || foundNoExpire))
                    {
                        // Display opening msg based on validation status
                        string msg;

                        if (!eulaConfig.Settings.Validated)
                        {
                            msg = $"The current MAAS-SFRThelper application is provided AS IS as a non-clinical, research only tool in evaluation only. The current " +
                            $"application will only be available until {endDate.Date} after which the application will be unavailable. " +
                            $"By Clicking 'Yes' you agree that this application will be evaluated and not utilized in providing planning decision support\n\n" +
                            $"Newer builds with future expiration dates can be found here: {GITHUB_URL}\n\n" +
                            "See the FAQ for more information on how to remove this pop-up and expiration";
                        }
                        else
                        {
                            msg = $"Application will only be available until {endDate.Date} after which the application will be unavailable. " +
                            "By Clicking 'Yes' you agree that this application will be evaluated and not utilized in providing planning decision support\n\n" +
                            $"Newer builds with future expiration dates can be found here: {GITHUB_URL} \n\n" +
                            "See the FAQ for more information on how to remove this pop-up and expiration";
                        }

                        if (!foundNoExpire)
                        {
                            bool userAgree = MessageBox.Show(msg,
                                                            "MAAS-SFRTHelper",
                                                            MessageBoxButton.YesNo,
                                                            MessageBoxImage.Question) == MessageBoxResult.Yes;
                            if (!userAgree)
                            {
                                return;
                            }
                        }

                        try
                        {
                            EsapiWorker esapiWorker = null;
                            try
                            {
                                esapiWorker = new EsapiWorker(context);
                            }
                            catch (Exception workerEx)
                            {
                                MessageBox.Show($"Error creating EsapiWorker: {workerEx.Message}\n\n{workerEx.StackTrace}",
                                               "Worker Initialization Error",
                                               MessageBoxButton.OK,
                                               MessageBoxImage.Error);
                                return;
                            }

                            DispatcherFrame frame = new DispatcherFrame();

                            Thread thread = new Thread(() =>
                            {
                                try
                                {
                                    if (AppConfig.m_appSettings == null)
                                    {
                                        AppConfig.GetAppConfig(scriptPath);
                                    }

                                    var mainWindow = new MainWindow(esapiWorker);
                                    mainWindow.ShowDialog();
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"UI Thread Error: {ex.Message}\n\n{ex.StackTrace}",
                                                   "Error in UI Thread",
                                                   MessageBoxButton.OK,
                                                   MessageBoxImage.Error);
                                }
                                finally
                                {
                                    frame.Continue = false;
                                }
                            });

                            thread.SetApartmentState(ApartmentState.STA);
                            thread.IsBackground = true;
                            thread.Start();

                            // FIXED: Wrap in try-catch to handle long-running operation issues
                            try
                            {
                                Dispatcher.PushFrame(frame);
                            }
                            catch (Exception frameEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Frame exit exception (non-fatal): {frameEx.Message}");
                                // Non-fatal - window has already closed
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Thread Creation Error: {ex.Message}\n\n{ex.StackTrace}",
                                           "Error Starting Application",
                                           MessageBoxButton.OK,
                                           MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Application has expired. Newer builds with future expiration dates can be found here: {GITHUB_URL}",
                                        "MAAS-SFRTHelper",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Could not verify application expiration date. The application will continue, but may have limited functionality.",
                                    "MAAS-SFRTHelper",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);

                    try
                    {
                        EsapiWorker esapiWorker = new EsapiWorker(context);
                        DispatcherFrame frame = new DispatcherFrame();

                        Thread thread = new Thread(() =>
                        {
                            try
                            {
                                if (AppConfig.m_appSettings == null)
                                {
                                    AppConfig.GetAppConfig(scriptPath);
                                }

                                var mainWindow = new MainWindow(esapiWorker);
                                mainWindow.ShowDialog();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error in UI thread: {ex.Message}\n\n{ex.StackTrace}",
                                               "Error",
                                               MessageBoxButton.OK,
                                               MessageBoxImage.Error);
                            }
                            finally
                            {
                                frame.Continue = false;
                            }
                        });

                        thread.SetApartmentState(ApartmentState.STA);
                        thread.IsBackground = true;
                        thread.Start();

                        // FIXED: Wrap in try-catch to handle long-running operation issues
                        try
                        {
                            Dispatcher.PushFrame(frame);
                        }
                        catch (Exception frameEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Frame exit exception (non-fatal): {frameEx.Message}");
                            // Non-fatal - window has already closed
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error launching application: {ex.Message}\n\n{ex.StackTrace}",
                                       "MAAS-SFRTHelper",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Main Thread Error: {ex.Message}\n\n{ex.StackTrace}",
                               "MAAS-SFRTHelper Error",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }
    }
}