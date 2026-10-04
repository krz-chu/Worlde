using System;
using System.Windows.Data;

namespace Worlde
{
    public class IntEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int iv && parameter != null)
            {
                if (int.TryParse(parameter.ToString(), out int pv)) return iv == pv;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
            {
                if (int.TryParse(parameter.ToString(), out int pv)) return pv;
            }
            return Binding.DoNothing;
        }
    }
}
