using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ScrewingHub.Core.Models;

namespace ScrewingHub.App.Converters;

/// <summary>
/// Converts JudgmentStatus to corresponding color brush.
/// </summary>
public class JudgmentToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is JudgmentStatus status)
        {
            return status switch
            {
                JudgmentStatus.OK => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),   // Green
                JudgmentStatus.NG => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),   // Red
                JudgmentStatus.Error => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), // Amber
                _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))                     // Gray
            };
        }

        if (value is string statusStr)
        {
            return statusStr switch
            {
                "OK" => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                "NG" => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
            };
        }

        return new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts boolean to Visibility.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts boolean connection status to color (green=connected, red=disconnected).
/// </summary>
public class ConnectionStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool connected)
        {
            return connected
                ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))  // Green
                : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // Red
        }
        return new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts string to Visibility (Visible if not empty).
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
            return System.Windows.Visibility.Visible;
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
