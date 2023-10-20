using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Xml;
using System.Collections.ObjectModel;

using System.Threading;
using System.Net;
using System.IO;


namespace gridTest
{

    public class Notifier : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

    }

    public class GridDialogViewModel : Notifier
    {

        private double radiusInCM;
        public double RadiusInCM
        {
            get { return radiusInCM; }
            set
            {
                radiusInCM = value;
                OnPropertyChanged("RadiusInCM");
            }
        }

        private double xOffsetInCM;
        public double XOffsetInCM
        {
            get { return xOffsetInCM; }
            set
            {
                xOffsetInCM = value;
                OnPropertyChanged("XOffsetInCM");
            }
        }

        private double yOffsetInCM;
        public double YOffsetInCM
        {
            get { return yOffsetInCM; }
            set
            {
                yOffsetInCM = value;
                OnPropertyChanged("YOffsetInCM");
            }
        }

        private double xSpacingInCM;
        public double XSpacingInCM
        {
            get { return xSpacingInCM; }
            set
            {
                xSpacingInCM = value;
                OnPropertyChanged("XSpacing");
            }
        }

        private double ySpacingInCM;
        public double YSpacingInCM
        {
            get { return ySpacingInCM; }
            set
            {
                ySpacingInCM = value;
                OnPropertyChanged("YSpacing");
            }
        }

        private List<string> targetStructures;
        public List<string> TargetStructures
        {
            get { return targetStructures; }
            set
            {
                targetStructures = value;
                OnPropertyChanged("TargetStructures");
            }
        }


        private int targetSelected;
        public int TargetSelected
        {
            get { return targetSelected; }
            set
            {
                targetSelected = value;
                OnPropertyChanged("TargetSelected");
            }
        }

        private bool isOkPressed;

        public bool IsOkPressed()
        {
            return isOkPressed;
        }

        //ScriptContext context;

        public struct VVector
        {
            public double x;
            public  double y;
            public double z;

            public VVector(double mx, double my, double mz)
            {
                x = mx;
                y = my;
                z = mz;
            }
        }
            
         VVector isocenter;

        public GridDialogViewModel(/*ScriptContext inContext*/)
        {
            //context = inContext;
            isOkPressed = false;

            targetStructures = new List<string>();
            targetStructures.Add("select");
            //foreach (Structure myStruct in context.StructureSet.Structures)
            //{
            //    if (myStruct.DicomType != "PTV") continue;
            //    targetStructures.Add(myStruct.Id);
            //}
            targetStructures.Add("TEST");
            targetSelected = 0;

            //var firstBeam = context.PlanSetup.Beams.First();
            //isocenter = firstBeam.IsocenterPosition;
            isocenter = new VVector(1.0, 2.0, 3.0);


            //defaults
            radiusInCM = 1.0;
            xOffsetInCM = 0.0;
            yOffsetInCM = 0.0;
            xSpacingInCM = 3.0;
            ySpacingInCM = 3.0;


        }

    }
}
