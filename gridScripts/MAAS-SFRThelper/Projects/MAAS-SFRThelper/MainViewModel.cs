using NLog.Layouts;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace GridBlockCreator
{

    public class MainViewModel: BindableBase
    {

        public DelegateCommand HyperlinkCmd { get; private set; }
        
        private string postText;

        public string PostText
        {
            get { return postText; }
            set { SetProperty(ref postText, value); }
        }


        public MainViewModel()
        {
            var isDebug = MAAS_SFRThelper.Properties.Settings.Default.Debug;
            //MessageBox.Show($"Display Terms {isDebug}");
            PostText = "";
            if ( isDebug ) { PostText += " *** Not Validated For Clinical Use ***"; }

            HyperlinkCmd = new DelegateCommand(OnHyperlink, CanHyperlink);


        }

        private void OnHyperlink()
        {
            var url = "http://medicalaffairs.varian.com/download/VarianLUSLA.pdf";
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url)
                );
        }

        private bool CanHyperlink()
        {
            return true;
        }

    }
}
