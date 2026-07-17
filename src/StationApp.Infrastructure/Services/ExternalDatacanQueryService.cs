using System.Data.OleDb;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;

namespace StationApp.Infrastructure.Services;

[SupportedOSPlatform("windows")]
public sealed class ExternalDatacanQueryService : IExternalDatacanQueryService
{
    private const int CommandTimeoutSeconds = 30;
    private const string CrusherSource = "Mỏ đá";
    private const string ClaySource = "Mỏ sét";
    private const int AccessReadWarningThreshold = 10000;

    private static readonly string[] AccessProviders =
    {
        "Microsoft.ACE.OLEDB.16.0",
        "Microsoft.ACE.OLEDB.12.0"
    };

    private readonly IConfiguration _configuration;

    public ExternalDatacanQueryService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<ExternalDatacanQueryResult> GetLatestAsync(
        string source,
        string? vehiclePlateKeyword,
        string? productKeyword,
        string? customerKeyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (string.Equals(source, ClaySource, StringComparison.OrdinalIgnoreCase))
        {
            return await Task.Run(
                () => GetClayAccessLatest(
                    vehiclePlateKeyword,
                    productKeyword,
                    customerKeyword,
                    pageIndex,
                    pageSize,
                    cancellationToken),
                cancellationToken);
        }

        return await GetSqlLatestAsync(
            source,
            vehiclePlateKeyword,
            productKeyword,
            customerKeyword,
            pageIndex,
            pageSize,
            cancellationToken);
    }

    private async Task<ExternalDatacanQueryResult> GetSqlLatestAsync(
        string source,
        string? vehiclePlateKeyword,
        string? productKeyword,
        string? customerKeyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var connectionStringName = source == CrusherSource ? "ExternalCrusherConnection" : "ExternalDatacanConnection";
        var connectionString = _configuration.GetConnectionString(connectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Chưa cấu hình ConnectionStrings:{connectionStringName} để đọc dữ liệu Lịch sử cân (PM cũ).");
        }

        pageIndex = Math.Max(0, pageIndex);
        pageSize = Math.Clamp(pageSize, 20, 500);
        var fetchSize = pageSize + 1;
        var offset = pageIndex * pageSize;

        const string sql = """
SELECT
    Sophieu AS TicketNo,
    Soxe AS VehiclePlate,
    Nhomhang AS GroupName,
    Khachhang AS CustomerName,
    Hanghoa AS ProductName,
    Ngayvao AS Weight1Time,
    Ngayra AS Weight2Time,
    KLxe AS Weight1,
    KLTong AS Weight2,
    KLhang AS NetWeight,
    Nvc AS OperatorName
FROM dbo.Datacan
WHERE (@VehiclePlateKeyword IS NULL OR Soxe LIKE N'%' + @VehiclePlateKeyword + N'%')
  AND (@ProductKeyword IS NULL OR Hanghoa LIKE N'%' + @ProductKeyword + N'%')
  AND (@CustomerKeyword IS NULL OR Khachhang LIKE N'%' + @CustomerKeyword + N'%')
ORDER BY CASE WHEN Ngayra IS NULL THEN 1 ELSE 0 END, Ngayra DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
""";

        var records = new List<ExternalDatacanRecordDto>(fetchSize);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText = sql;

        command.Parameters.AddWithValue("@VehiclePlateKeyword", NormalizeKeywordDbValue(vehiclePlateKeyword));
        command.Parameters.AddWithValue("@ProductKeyword", NormalizeKeywordDbValue(productKeyword));
        command.Parameters.AddWithValue("@CustomerKeyword", NormalizeKeywordDbValue(customerKeyword));
        command.Parameters.AddWithValue("@Offset", offset);
        command.Parameters.AddWithValue("@PageSize", fetchSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new ExternalDatacanRecordDto(
                TicketNo: GetString(reader, "TicketNo"),
                VehiclePlate: GetString(reader, "VehiclePlate"),
                GroupName: GetString(reader, "GroupName"),
                CustomerName: GetString(reader, "CustomerName"),
                ProductName: GetString(reader, "ProductName"),
                Weight1Time: GetDateTime(reader, "Weight1Time"),
                Weight2Time: GetDateTime(reader, "Weight2Time"),
                Weight1: GetDecimal(reader, "Weight1"),
                Weight2: GetDecimal(reader, "Weight2"),
                NetWeight: GetDecimal(reader, "NetWeight"),
                OperatorName: GetString(reader, "OperatorName")));
        }

        var hasNextPage = records.Count > pageSize;
        if (hasNextPage)
        {
            records.RemoveAt(records.Count - 1);
        }

        return new ExternalDatacanQueryResult(records, hasNextPage);
    }

    private ExternalDatacanQueryResult GetClayAccessLatest(
        string? vehiclePlateKeyword,
        string? productKeyword,
        string? customerKeyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        pageIndex = Math.Max(0, pageIndex);
        pageSize = Math.Clamp(pageSize, 20, 500);

        var options = GetClayAccessOptions();
        var sourceFilePath = options.FilePath;
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Không tìm thấy file dữ liệu Mỏ sét: {sourceFilePath}");
        }

        string? tempFilePath = null;
        try
        {
            var fileToOpen = sourceFilePath;
            if (options.CopyToTempBeforeRead)
            {
                tempFilePath = CopyAccessFileToTemp(sourceFilePath);
                fileToOpen = tempFilePath;
            }

            var connectionString = BuildWorkingAccessConnectionString(fileToOpen, options.Password);
            using var connection = new OleDbConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText = """
SELECT
    ID,
    SO_PHIEU,
    BIEN_SO,
    LOAI_HANG,
    BEN_BAN,
    KL_TONG,
    KL_BI,
    KL_HANG,
    NGAY_VAO,
    NGAY_RA,
    PHAN_LOAI,
    NGUOI_CAN
FROM tbl_weigh
""";

            var allRecords = new List<ExternalDatacanAccessRow>();
            using var reader = command.ExecuteReader();
            while (reader is not null && reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                allRecords.Add(new ExternalDatacanAccessRow(
                    Id: GetInt(reader, "ID") ?? 0,
                    TicketNo: GetString(reader, "SO_PHIEU"),
                    VehiclePlate: GetString(reader, "BIEN_SO"),
                    GroupName: GetString(reader, "PHAN_LOAI"),
                    CustomerName: GetString(reader, "BEN_BAN"),
                    ProductName: GetString(reader, "LOAI_HANG"),
                    Weight1Time: ParseAccessDate(GetString(reader, "NGAY_VAO")),
                    Weight2Time: ParseAccessDate(GetString(reader, "NGAY_RA")),
                    Weight1: GetDecimal(reader, "KL_TONG"),
                    Weight2: GetDecimal(reader, "KL_BI"),
                    NetWeight: GetDecimal(reader, "KL_HANG"),
                    OperatorName: GetString(reader, "NGUOI_CAN")));
            }

            var filtered = allRecords
                .Where(x => ContainsKeyword(x.VehiclePlate, vehiclePlateKeyword))
                .Where(x => ContainsKeyword(x.ProductName, productKeyword))
                .Where(x => ContainsKeyword(x.CustomerName, customerKeyword))
                .OrderBy(x => x.Weight2Time is null)
                .ThenByDescending(x => x.Weight2Time ?? x.Weight1Time ?? DateTime.MinValue)
                .ThenByDescending(x => x.Id)
                .Skip(pageIndex * pageSize)
                .Take(pageSize + 1)
                .Select(x => x.ToDto())
                .ToList();

            var hasNextPage = filtered.Count > pageSize;
            if (hasNextPage)
            {
                filtered.RemoveAt(filtered.Count - 1);
            }

            if (allRecords.Count > AccessReadWarningThreshold)
            {
                // Keep the first implementation simple, but make future performance risk visible in diagnostics.
                System.Diagnostics.Trace.TraceWarning(
                    "External clay MDB query read {0} rows. Consider keyset paging if the file grows significantly.",
                    allRecords.Count);
            }

            return new ExternalDatacanQueryResult(filtered, hasNextPage);
        }
        catch (OleDbException ex) when (LooksLikeMissingProvider(ex))
        {
            throw new InvalidOperationException(BuildMissingAceMessage(), ex);
        }
        catch (OleDbException ex) when (LooksLikeInvalidPassword(ex))
        {
            throw new InvalidOperationException("Không mở được file Access Mỏ sét. Vui lòng kiểm tra lại mật khẩu MDB trong appsettings.json.", ex);
        }
        finally
        {
            TryDeleteTempFile(tempFilePath);
        }
    }

    private ClayAccessOptions GetClayAccessOptions()
    {
        var filePath = _configuration["ExternalClayAccess:FilePath"];
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Chưa cấu hình ExternalClayAccess:FilePath để đọc dữ liệu Mỏ sét từ file MDB.");
        }

        var password = _configuration["ExternalClayAccess:Password"];
        var copyToTemp = !bool.TryParse(_configuration["ExternalClayAccess:CopyToTempBeforeRead"], out var parsed) || parsed;
        return new ClayAccessOptions(filePath, password, copyToTemp);
    }

    private static string CopyAccessFileToTemp(string sourceFilePath)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "StationApp", "ExternalClayAccess");
        Directory.CreateDirectory(tempDirectory);
        var tempFilePath = Path.Combine(
            tempDirectory,
            $"{Path.GetFileNameWithoutExtension(sourceFilePath)}_{Guid.NewGuid():N}{Path.GetExtension(sourceFilePath)}");

        File.Copy(sourceFilePath, tempFilePath, overwrite: false);
        return tempFilePath;
    }

    private static string BuildWorkingAccessConnectionString(string filePath, string? password)
    {
        OleDbException? lastProviderException = null;

        foreach (var provider in AccessProviders)
        {
            var connectionString = BuildAccessConnectionString(provider, filePath, password);
            try
            {
                using var connection = new OleDbConnection(connectionString);
                connection.Open();
                return connectionString;
            }
            catch (OleDbException ex) when (LooksLikeMissingProvider(ex))
            {
                lastProviderException = ex;
            }
        }

        throw new InvalidOperationException(BuildMissingAceMessage(), lastProviderException);
    }

    private static string BuildAccessConnectionString(string provider, string filePath, string? password)
    {
        var builder = new OleDbConnectionStringBuilder
        {
            Provider = provider,
            DataSource = filePath
        };

        builder["Persist Security Info"] = "False";
        if (!string.IsNullOrWhiteSpace(password))
        {
            builder["Jet OLEDB:Database Password"] = password;
        }

        return builder.ConnectionString;
    }

    private static string BuildMissingAceMessage()
    {
        return "Máy này chưa cài Microsoft Access Database Engine/ACE OLEDB 64-bit nên chưa đọc được dữ liệu Mỏ sét từ file MDB. "
            + "Vui lòng chạy bộ cài trong thư mục prerequisites của bản phát hành, sau đó mở lại ứng dụng.";
    }

    private static bool ContainsKeyword(string? source, string? keyword)
    {
        var normalized = NormalizeKeywordString(keyword);
        return normalized is null || (source?.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
    }

    private static object NormalizeKeywordDbValue(string? value)
    {
        var trimmed = NormalizeKeywordString(value);
        return trimmed is null ? DBNull.Value : trimmed;
    }

    private static string? NormalizeKeywordString(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static DateTime? ParseAccessDate(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (DateTime.TryParseExact(
                trimmed,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return parsed;
        }

        return DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed)
            ? parsed
            : null;
    }

    private static string? GetString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
    }

    private static DateTime? GetDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.LocalDateTime,
            _ => DateTime.TryParse(Convert.ToString(value), out var parsed) ? parsed : null
        };
    }

    private static decimal? GetDecimal(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return ToDecimal(reader.GetValue(ordinal));
    }

    private static string? GetString(OleDbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
    }

    private static int? GetInt(OleDbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            int i => i,
            long l => Convert.ToInt32(l),
            short s => s,
            byte b => b,
            _ => int.TryParse(Convert.ToString(value), out var parsed) ? parsed : null
        };
    }

    private static decimal? GetDecimal(OleDbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return ToDecimal(reader.GetValue(ordinal));
    }

    private static decimal? ToDecimal(object? value)
    {
        return value switch
        {
            null => null,
            decimal d => d,
            double d => Convert.ToDecimal(d),
            float f => Convert.ToDecimal(f),
            int i => i,
            long l => l,
            short s => s,
            byte b => b,
            _ => decimal.TryParse(Convert.ToString(value), out var parsed) ? parsed : null
        };
    }

    private static bool LooksLikeMissingProvider(Exception ex)
    {
        var message = ex.Message;
        return message.Contains("provider is not registered", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not registered", StringComparison.OrdinalIgnoreCase)
            || message.Contains("class not registered", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeInvalidPassword(Exception ex)
    {
        return ex.Message.Contains("Not a valid password", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Cannot start your application", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteTempFile(string? tempFilePath)
    {
        if (string.IsNullOrWhiteSpace(tempFilePath))
        {
            return;
        }

        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch
        {
            // Temp cleanup is best effort only; a later OS temp cleanup can remove the file.
        }
    }

    private sealed record ClayAccessOptions(string FilePath, string? Password, bool CopyToTempBeforeRead);

    private sealed record ExternalDatacanAccessRow(
        int Id,
        string? TicketNo,
        string? VehiclePlate,
        string? GroupName,
        string? CustomerName,
        string? ProductName,
        DateTime? Weight1Time,
        DateTime? Weight2Time,
        decimal? Weight1,
        decimal? Weight2,
        decimal? NetWeight,
        string? OperatorName)
    {
        public ExternalDatacanRecordDto ToDto()
        {
            return new ExternalDatacanRecordDto(
                TicketNo,
                VehiclePlate,
                GroupName,
                CustomerName,
                ProductName,
                Weight1Time,
                Weight2Time,
                Weight1,
                Weight2,
                NetWeight,
                OperatorName);
        }
    }
}
