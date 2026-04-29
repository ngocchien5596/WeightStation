using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

using StationApp.Application.DTOs;
using StationApp.Domain.Enums;

namespace StationApp.UI.Converters;

public class WeightToTonConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal weight)
            return weight / 1000m;
        return 0m;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (decimal.TryParse(value?.ToString(), out var result))
            return result * 1000m;
        return 0m;
    }
}

public class ExpiryToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        TimeZoneInfo vnTimeZone;
        try
        {
            vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone).Date;

        if (value is DateTime expiry && expiry < today)
            return new SolidColorBrush(Colors.Red);
        return new SolidColorBrush(Colors.Transparent); // Or default foreground
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class OverweightToRowBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Design Update: Overloaded records no longer have a background color highlight.
        // Return Transparent to let default row background/hover/selection take over.
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class OverweightToForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Try to handle generically using dynamic to support multiple DTO types
        try
        {
            if (value == null) return Brushes.Black;
            
            dynamic item = value;
            
            // Check for overweight first (only for WeightViewListItem)
            try {
                if (item.HasOverweightCase) return Brushes.Red;
            } catch { /* Property doesn't exist */ }

            // Then check TransactionType
            try {
                if (item.TransactionType == TransactionType.OUTBOUND)
                    return Brushes.Green;
            } catch { /* Property doesn't exist */ }
        }
        catch
        {
            // Fallback for safety
        }
        
        return Brushes.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public static class RegistrationStatusMapper
{
    public static string ToDisplayString(RegistrationStatus status)
    {
        return status switch
        {
            RegistrationStatus.REGISTERED => "Chờ cân",
            RegistrationStatus.LOADING_IN_PROGRESS => "Đang lấy hàng",
            RegistrationStatus.COMPLETED => "Hoàn thành",
            RegistrationStatus.CANCELLED => "Đã hủy",
            _ => status.ToString()
        };
    }

    public static string ToDisplayString(string? statusStr)
    {
        if (string.IsNullOrWhiteSpace(statusStr))
            return string.Empty;

        if (Enum.TryParse<RegistrationStatus>(statusStr, true, out var status))
        {
            return ToDisplayString(status);
        }
        return statusStr;
    }
}

public class RegistrationStatusDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RegistrationStatus status)
            return RegistrationStatusMapper.ToDisplayString(status);
            
        if (value is string statusStr)
            return RegistrationStatusMapper.ToDisplayString(statusStr);
            
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BooleanToStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isActive && isActive
            ? "Đang hoạt động"
            : "Ngừng hoạt động";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TransactionTypeDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TransactionType type)
        {
            return type switch
            {
                TransactionType.INBOUND => "Nhập hàng",
                TransactionType.OUTBOUND => "Xuất hàng",
                _ => type.ToString()
            };
        }

        if (value is string typeStr && Enum.TryParse<TransactionType>(typeStr, true, out var parsedType))
        {
            return parsedType switch
            {
                TransactionType.INBOUND => "Nhập hàng",
                TransactionType.OUTBOUND => "Xuất hàng",
                _ => typeStr
            };
        }

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TransportMethodDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TransportMethod method)
        {
            return method switch
            {
                TransportMethod.ROAD => "Đường bộ",
                TransportMethod.WATERWAY => "Đường thủy",
                _ => method.ToString()
            };
        }

        if (value is string methodStr && Enum.TryParse<TransportMethod>(methodStr, true, out var parsedMethod))
        {
            return parsedMethod switch
            {
                TransportMethod.ROAD => "Đường bộ",
                TransportMethod.WATERWAY => "Đường thủy",
                _ => methodStr
            };
        }

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
