using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Frontend.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invertir { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool val = value is bool b && b;
            if (Invertir) val = !val;
            return val ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EqualityToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;
            return value.ToString() == parameter.ToString() ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Stepper del asistente. values[0] = PasoActual (base 0), values[1] = número
    /// del círculo (base 1, normalmente en Tag). Devuelve "Completado", "Actual" o "Pendiente".
    /// </summary>
    public class PasoEstadoConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 ||
                !int.TryParse(values[0]?.ToString(), out var actual) ||
                !int.TryParse(values[1]?.ToString(), out var numero))
                return "Pendiente";

            var indice = numero - 1;
            if (indice < actual) return "Completado";
            if (indice == actual) return "Actual";
            return "Pendiente";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Devuelve true cuando value == parameter. Al marcarse (ConvertBack con true)
    /// regresa el parámetro convertido al tipo destino; ideal para RadioButtons
    /// enlazados TwoWay a un índice entero.
    /// </summary>
    public class EqualityToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
            {
                var tipo = Nullable.GetUnderlyingType(targetType) ?? targetType;
                return System.Convert.ChangeType(parameter.ToString(), tipo, CultureInfo.InvariantCulture);
            }
            return Binding.DoNothing;
        }
    }

    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        public bool Invertir { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool esVacio = value == null || string.IsNullOrWhiteSpace(value.ToString());
            if (Invertir) esVacio = !esVacio;
            return esVacio ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

