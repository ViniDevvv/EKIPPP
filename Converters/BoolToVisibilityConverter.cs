using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EKIPPP.Converters;

public class TabVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int tab && parameter is string p && int.TryParse(p, out int target))
            return tab == target ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}


public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

public class LogTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.LogType type)
        {
            var hex = type switch
            {
                Models.LogType.Success => "#10B981",
                Models.LogType.Error   => "#EF4444",
                Models.LogType.Warning => "#F59E0B",
                _                      => "#A78BFA"
            };
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A78BFA"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
