using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MAAS_SFRThelper.Views.Converters
{
    public class BoolToBlueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool selectionFlag = (bool)value;
            return selectionFlag ? "Blue" : "Gray";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Color color = (Color)value;
            return color == Colors.Blue;
        }
    }
}
