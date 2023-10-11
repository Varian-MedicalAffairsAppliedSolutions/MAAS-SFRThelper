using System;
using System.Globalization;
using System.Windows.Data;

namespace MAAS_SFRThelper.Views.Converters
{
    public class BoolToStrikeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool selectionFlag = (bool)value;
            return selectionFlag ? string.Empty : "1";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool selectionFlag = (bool)value;
            return selectionFlag ? "1" : string.Empty;
        }
    }
}
