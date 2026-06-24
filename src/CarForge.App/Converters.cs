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

/// <summary>string vazia/null → Collapsed; com conteúdo → Visible.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Veredito ("ok"/"warn"/"none") → cor. parameter="bg" devolve um tom escuro de
/// preenchimento; sem parameter devolve a cor cheia (borda/ícone/texto).
/// </summary>
public sealed class VerdictBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var kind = value as string ?? "none";
        bool bg = (parameter as string) == "bg";
        return kind switch
        {
            "ok"   => bg ? Rgb(0x16, 0x2A, 0x20) : Rgb(0x3F, 0xB3, 0x7A),  // verde
            "warn" => bg ? Rgb(0x30, 0x29, 0x14) : Rgb(0xEF, 0x9F, 0x27),  // âmbar
            _      => bg ? Rgb(0x26, 0x26, 0x2B) : Rgb(0x9A, 0x9A, 0xA2),  // cinza
        };
    }

    private static SolidColorBrush Rgb(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Veredito → glifo do selo.</summary>
public sealed class VerdictGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value as string switch { "ok" => "✓", "warn" => "!", _ => "•" };

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Linha de diff diferente → fundo levemente âmbar; igual → transparente.</summary>
public sealed class DiffRowBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush Hit = new(Color.FromRgb(0x30, 0x29, 0x14));

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Hit : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Linha de diff diferente → mostra o marcador "≠"; igual → vazio.</summary>
public sealed class DiffMarkerConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "≠" : "";

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
