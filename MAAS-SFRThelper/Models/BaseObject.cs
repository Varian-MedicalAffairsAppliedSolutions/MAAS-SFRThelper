using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MAAS_SFRThelper.Models
{
    public class BaseObject : INotifyPropertyChanged
    {
        private double x;
        public double X
        {
            get { return x; }
            set
            {
                x = value;
                NotifyPropertyChanged();
            }
        }

        private double y;
        public double Y
        {
            get { return y; }
            set
            {
                y = value;
                NotifyPropertyChanged();
            }
        }

        public double XTilted
        {
            get { return x + Math.Tan(TiltX / 180.0 * Math.PI) * ZCoordStarX; }
            set
            {
                x = value - Math.Tan(TiltX / 180.0 * Math.PI) * ZCoordStarX;
                NotifyPropertyChanged();
            }
        }


        public double YTilted
        {
            get { return y + Math.Tan(TiltY / 180.0 * Math.PI) * ZCoordStarY; }
            set
            {
                y = value - Math.Tan(TiltY / 180.0 * Math.PI) * ZCoordStarY;
                NotifyPropertyChanged();
            }
        }

        public double TiltX;
        public double TiltY;
        public double ZCoordStarX;
        public double ZCoordStarY;

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged
    }

    public class CopyOfBaseObject : INotifyPropertyChanged
    {
        private double x;
        public double X
        {
            get { return x; }
            set
            {
                x = value;
                NotifyPropertyChanged();
            }
        }

        private double y;
        public double Y
        {
            get { return y; }
            set
            {
                y = value;
                NotifyPropertyChanged();
            }
        }

        public double XTilted
        {
            get { return x + Math.Tan(TiltX / 180.0 * Math.PI) * ZCoordStarX; }
            set
            {
                x = value - Math.Tan(TiltX / 180.0 * Math.PI) * ZCoordStarX;
                NotifyPropertyChanged();
            }
        }


        public double YTilted
        {
            get { return y + Math.Tan(TiltY / 180.0 * Math.PI) * ZCoordStarY; }
            set
            {
                y = value - Math.Tan(TiltY / 180.0 * Math.PI) * ZCoordStarY;
                NotifyPropertyChanged();
            }
        }

        public double TiltX;
        public double TiltY;
        public double ZCoordStarX;
        public double ZCoordStarY;

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged
    }
}
