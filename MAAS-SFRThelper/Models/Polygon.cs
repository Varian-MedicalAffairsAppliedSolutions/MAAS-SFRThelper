using System.Windows.Media;

namespace MAAS_SFRThelper.Models
{
    public class Polygon : BaseObject
    {
        PointCollection points;
        public PointCollection Points
        {
            get { return points; }
            set { points = value; NotifyPropertyChanged(); }
        }
    };
}