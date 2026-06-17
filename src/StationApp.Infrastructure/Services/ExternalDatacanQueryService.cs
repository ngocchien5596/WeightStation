using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;

namespace StationApp.Infrastructure.Services;

public sealed class ExternalDatacanQueryService : IExternalDatacanQueryService
{
    private const int CommandTimeoutSeconds = 30;
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
        var connectionStringName = source == "Trạm đập" ? "ExternalCrusherConnection" : "ExternalDatacanConnection";
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

        command.Parameters.AddWithValue("@VehiclePlateKeyword", NormalizeKeyword(vehiclePlateKeyword));
        command.Parameters.AddWithValue("@ProductKeyword", NormalizeKeyword(productKeyword));
        command.Parameters.AddWithValue("@CustomerKeyword", NormalizeKeyword(customerKeyword));
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

    private static object NormalizeKeyword(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? DBNull.Value : trimmed;
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

        var value = reader.GetValue(ordinal);
        return value switch
        {
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
}
