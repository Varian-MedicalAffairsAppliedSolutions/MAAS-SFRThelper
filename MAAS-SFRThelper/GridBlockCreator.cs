using MAAS_SFRThelper.Services;
using MAAS_SFRThelper.ViewModels;
using MAAS_SFRThelper.Views;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Contexts;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using VMS.TPS.Common.Model.API;

namespace VMS.TPS
{
    public class Script
    {

        public Script()
        {

        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context)
        {
            try
            {
                AppConfig.GetAppConfig(Assembly.GetExecutingAssembly().Location);

                // Consider removing requirement of a plan - just need contours not a plan for spheres - Matt
                if (context.Patient == null || context.PlanSetup == null)
                // if (context.Patient == null) // 8/4 trying to comment out context.PlanSetup
                {
                    MessageBox.Show("No active patient/plan selected - exiting",
                                    "MAAS-SFRTHelper",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Exclamation);
                    return;
                }

                // Check exp date
                var provider = new CultureInfo("en-US");
                var asmCa = typeof(Script).Assembly.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(AssemblyExpirationDate));
                if (DateTime.TryParse(asmCa.ConstructorArguments.FirstOrDefault().Value as string, provider, DateTimeStyles.None, out var endDate)
                    && DateTime.Now <= endDate)
                {
                    // Display opening msg
                    string msg = $"The current MAAS-SFRThelper application is provided AS IS as a non-clinical, research only tool in evaluation only. The current " +
                    $"application will only be available until {endDate.Date} after which the application will be unavailable. " +
                    $"By Clicking 'Yes' you agree that this application will be evaluated and not utilized in providing planning decision support\n\n" +
                    "Newer builds with future expiration dates can be found here: https://github.com/Varian-MedicalAffairsAppliedSolutions/MAAS-SFRThelper\n\n" +
                    "See the FAQ for more information on how to remove this pop-up and expiration";

                    bool userAgree = MessageBox.Show(msg,
                                                     "MAAS-SFRTHelper",
                                                     MessageBoxButton.YesNo,
                                                     MessageBoxImage.Question) == MessageBoxResult.Yes;
                                                     // MessageBox.Show(ConfigurationManager.AppSettings["ValidForClinicalUse"]);
                    if (userAgree)
                    {
                        // The ESAPI worker needs to be created in the main thread
                        var esapiWorker = new EsapiWorker(context);

                    // This new queue of tasks will prevent the script
                    // for exiting until the new window is closed
                    DispatcherFrame frame = new DispatcherFrame();

                    RunOnNewStaThread(() =>
                    {
                        // This method won't return until the window is closed
                        InitializeAndStartMainWindow(esapiWorker);

                        // End the queue so that the script can exit
                        frame.Continue = false;
                    });

                    // Start the new queue, waiting until the window is closed
                    Dispatcher.PushFrame(frame);
                   
                        //var mainWindow = new MainWindow(context);
                        //mainWindow.ShowDialog();
                    }
                }
                else
                {
                    MessageBox.Show("Application has expired. Newer builds with future expiration dates can be found here: https://github.com/Varian-MedicalAffairsAppliedSolutions/MAAS-SFRThelper",
                                    "MAAS-SFRTHelper",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "MAAS-SFRTHelper", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeAndStartMainWindow(EsapiWorker esapiWorker)
        {
            //var viewModel = new MainViewModel(esapiWorker);
            var mainWindow = new MainWindow(esapiWorker);
            mainWindow.ShowDialog();
        }

        private void RunOnNewStaThread(Action a)
        {
            Thread thread = new Thread(() => a());
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }
    }


}

