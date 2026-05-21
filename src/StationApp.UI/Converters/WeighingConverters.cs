using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StationApp.Domain.Enums;

namespace StationApp.UI.Converters;

public class WeightToTonConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal weight)
        {
            return weight / 1000m;
        }

        return 0m;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (decimal.TryParse(value?.ToString(), out var result))
        {
            return result * 1000m;
        }

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
        {
            return new SolidColorBrush(Colors.Red);
        }

        return new SolidColorBrush(Colors.Transparent);
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
        try
        {
            if (value == null)
            {
                return Brushes.Black;
            }

            dynamic item = value;
            try
            {
                if (item.HasOverweightCase)
                {
                    return Brushes.Red;
                }
            }
            catch
            {
            }

            try
            {
                if (item.TransactionType == TransactionType.OUTBOUND)
                {
                    return Brushes.Green;
                }
            }
            catch
            {
            }
        }
        catch
        {
        }

        return Brushes.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public static class CutOrderStatusMapper
{
    public static string ToDisplayString(CutOrderStatus status)
    {
        return status switch
        {
            CutOrderStatus.REGISTERED => "Đã đăng ký",
            CutOrderStatus.IN_SESSION => "Đang trong lượt cân",
            CutOrderStatus.LOADING_IN_PROGRESS => "Đang lấy hàng",
            CutOrderStatus.COMPLETED => "Đã hoàn tất",
            CutOrderStatus.CANCELLED => "Đã hủy",
            _ => status.ToString()
        };
    }

    public static string ToDisplayString(string? statusStr)
    {
        if (string.IsNullOrWhiteSpace(statusStr))
        {
            return string.Empty;
        }

        if (Enum.TryParse<CutOrderStatus>(statusStr, true, out var status))
        {
            return ToDisplayString(status);
        }

        return statusStr;
    }
}

public static class SessionStatusMapper
{
    public static string ToDisplayString(WeighingSessionStatus status)
    {
        return status switch
        {
            WeighingSessionStatus.PENDING_WEIGHT1 => "Chờ cân lần 1",
            WeighingSessionStatus.PENDING_WEIGHT2 => "Chờ cân lần 2",
            WeighingSessionStatus.ALLOCATION_PENDING => "Chờ phân bổ",
            WeighingSessionStatus.READY_TO_COMPLETE => "Sẵn sàng hoàn tất",
            WeighingSessionStatus.COMPLETED => "Đã hoàn tất",
            WeighingSessionStatus.CANCELLED => "Đã hủy",
            _ => status.ToString()
        };
    }
}

public static class SessionLineStatusMapper
{
    public static string ToDisplayString(WeighingSessionLineStatus status)
    {
        return status switch
        {
            WeighingSessionLineStatus.PENDING => "Chưa phân bổ",
            WeighingSessionLineStatus.ALLOCATED => "Đã phân bổ",
            WeighingSessionLineStatus.CANCELLED => "Đã hủy",
            _ => status.ToString()
        };
    }
}

public static class OverweightResolutionStatusMapper
{
    public static string ToDisplayString(OverweightResolutionStatus status)
    {
        return status switch
        {
            OverweightResolutionStatus.NOT_APPLICABLE => "Không cần tách tải",
            OverweightResolutionStatus.PENDING => "Chờ tách tải",
            OverweightResolutionStatus.SPLIT_CONFIRMED => "Đã xác nhận tách tải",
            OverweightResolutionStatus.NO_SPLIT_CONFIRMED => "Đã xác nhận không tách tải",
            _ => status.ToString()
        };
    }
}

public static class RecordRoleMapper
{
    public static string ToDisplayString(string? role)
    {
        return role switch
        {
            "MASTER_SESSION" => "Phiếu cân tổng",
            "NORMAL" => "Phiếu giao nhận thường",
            "SPLIT_DERIVED" => "Chứng từ tách tải",
            _ => role ?? string.Empty
        };
    }
}

public class CutOrderStatusDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CutOrderStatus status)
        {
            return CutOrderStatusMapper.ToDisplayString(status);
        }

        if (value is string statusStr)
        {
            return CutOrderStatusMapper.ToDisplayString(statusStr);
        }

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SessionStatusDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is WeighingSessionStatus status
            ? SessionStatusMapper.ToDisplayString(status)
            : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SessionLineStatusDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is WeighingSessionLineStatus status
            ? SessionLineStatusMapper.ToDisplayString(status)
            : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class OverweightResolutionStatusDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is OverweightResolutionStatus status
            ? OverweightResolutionStatusMapper.ToDisplayString(status)
            : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class RecordRoleDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return RecordRoleMapper.ToDisplayString(value?.ToString());
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

public class OverweightFlagDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool flag && flag ? "Tách tải" : "Bình thường";
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

