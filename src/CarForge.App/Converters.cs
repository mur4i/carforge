using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CarForge.App;

/// <summary>true (diferente) → cor de alerta; false → cor normal.</summary>
public sealed class DiffBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? new SolidColorBrush(Color.FromRgb(0xBA, 0x75, 0x17))   // amber
                      : new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x41));   // gray

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
