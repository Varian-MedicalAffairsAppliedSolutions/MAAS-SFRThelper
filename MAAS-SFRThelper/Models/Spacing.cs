using Prism.Mvvm;
using System;

namespace MAAS_SFRThelper.Models
{
    public class Spacing : BindableBase
    {
        // Helper class for representing Rectagonal vs Hexagonal Spacing
        private double value_;
        public double Value
        {
            get { return value_; }
            set { SetProperty(ref value_, value); }
        }

        //private double hex_spacing;
        //public double Hex_Spacing
        //{
        //    get { return hex_spacing; }
        //    set { SetProperty(ref hex_spacing, value); }
        //}

        private string stringRep;
        public string StringRep
        {
            get { return stringRep; }
            set { SetProperty(ref stringRep, value); }
        }

        public Spacing(double spacing)
        {
            value_ = spacing;
            // Hex_Spacing = rect_spacing * Math.Sqrt(3);
            StringRep = ToString();
        }

        public override string ToString()
        {
            string v = $"{Math.Round(value_, 1)}"; // | {Math.Round(hex_spacing, 1)} (Hex)"; // change from hex/rec to axial/lat - JP
            // string v = $"{Math.Round(value_, 1)} (Lateral) | {Math.Round(hex_spacing, 1)} (Axial)";
            return v;
        }
    }
}