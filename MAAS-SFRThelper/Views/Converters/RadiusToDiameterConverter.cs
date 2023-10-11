using System;
using System.Globalization;
using System.Windows.Data;

namespace MAAS_SFRThelper.Views.Converters
{
    public class RadiusToDiameterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return 2.0 * (double)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return 0.5 * (double)value;
        }
    }
}
