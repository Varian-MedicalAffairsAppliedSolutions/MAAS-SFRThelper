namespace MAAS_SFRThelper.Models
{
    public class Circle : BaseObject
    {
        double r;
        public double R
        {
            get { return r; }
            set
            {
                r = value;
                NotifyPropertyChanged();
            }
        }

        bool selected;
        public bool Selected
        {
            get { return selected; }
            set { selected = value; NotifyPropertyChanged(); }
        }

        public int XGrid { get; set; }
        public int YGrid { get; set; }
    };
}