using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ScrewingHub.App.Converters;

/// <summary>
/// Converts a percentage (0-100) to a width for gauge bars.
/// Used with the parent container's ActualWidth.
/// </summary>
public class PercentToWidthConverter : IValueConverter
{
    public static readonly PercentToWidthConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            // Return as a GridLength star value or just clamp
            return Math.Max(0, Math.Min(100, percent)) * 5; // Scale: 500px max
        }
        return 250.0; // Default 50%
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
