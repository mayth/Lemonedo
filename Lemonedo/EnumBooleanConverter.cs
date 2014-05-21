using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows;

namespace Lemonedo
{
    [ValueConversion(typeof(Enum), typeof(bool))]
    class EnumBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var s = parameter as string;
            if (string.IsNullOrEmpty(s))
                return DependencyProperty.UnsetValue;
            
            if (!Enum.IsDefined(value.GetType(), value))
                return DependencyProperty.UnsetValue;

            var v = (Enum)Enum.Parse(value.GetType(), s);
            if (v.ToString() == Enum.GetName(value.GetType(), 0))
                return DependencyProperty.UnsetValue;
            return v.Equals(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var s = parameter as string;
            if (s == null)
                return DependencyProperty.UnsetValue;
            return Enum.Parse(targetType, s);
        }
    }
}
