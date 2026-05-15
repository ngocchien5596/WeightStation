using System.Globalization;
using System.Windows.Data;

namespace StationApp.UI.Converters;

public static class SplitModeDisplayMapper
{
    public static string ToDisplayString(bool isManualOverride)
        => isManualOverride ? "Tùy chỉnh tay" : "Đề xuất hệ thống";
}

public class SplitModeDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isManualOverride
            ? SplitModeDisplayMapper.ToDisplayString(isManualOverride)
            : SplitModeDisplayMapper.ToDisplayString(false);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
