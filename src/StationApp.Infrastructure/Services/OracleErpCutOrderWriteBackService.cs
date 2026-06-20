using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using StationApp.Application.Interfaces;

namespace StationApp.Infrastructure.Services;

public sealed class OracleErpCutOrderWriteBackService : IErpCutOrderWriteBackService
{
    private const string ConnectionStringPath = "ErpOracle:ConnectionString";

    private readonly IConfiguration _configuration;
    private readonly ILogger<OracleErpCutOrderWriteBackService> _logger;

    public OracleErpCutOrderWriteBackService(
        IConfiguration configuration,
        ILogger<OracleErpCutOrderWriteBackService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ErpCutOrderWriteBackResult> UpdateTransportInfoAsync(ErpCutOrderWriteBackRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ErpCutOrderId))
        {
            throw new InvalidOperationException("ErpCutOrderId is required for ERP write-back.");
        }

        var connectionString = _configuration[ConnectionStringPath];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Chưa cấu hình {ConnectionStringPath} trong appsettings.json.");
        }

        var normalizedErpCutOrderId = request.ErpCutOrderId.Trim();
        var normalizedVehiclePlate = request.VehiclePlate.Trim();
        var normalizedMoocNumber = NormalizeOptional(request.MoocNumber);

        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(ct);

        _logger.LogInformation(
            "Starting ERP transport write-back. ErpCutOrderId={ErpCutOrderId}, VehiclePlate={VehiclePlate}, MoocNumber={MoocNumber}",
            normalizedErpCutOrderId,
            normalizedVehiclePlate,
            normalizedMoocNumber);

        var previousValues = await ReadCurrentValuesAsync(connection, normalizedErpCutOrderId, ct);
        if (previousValues == null)
        {
            _logger.LogWarning(
                "ERP write-back skipped because document was not found. ErpCutOrderId={ErpCutOrderId}",
                normalizedErpCutOrderId);
            return new ErpCutOrderWriteBackResult(
                normalizedErpCutOrderId,
                0,
                null,
                null,
                null,
                null);
        }

        const string updateSql = """
UPDATE M_CommandLatching
SET
    transportNo = :transportNo,
    MoocNo = :moocNo
WHERE documentNo = :erpCutOrderId
""";

        await using var updateCommand = connection.CreateCommand();
        updateCommand.BindByName = true;
        updateCommand.CommandText = updateSql;
        updateCommand.Parameters.Add("transportNo", OracleDbType.Varchar2, normalizedVehiclePlate, System.Data.ParameterDirection.Input);
        updateCommand.Parameters.Add("moocNo", OracleDbType.Varchar2, (object?)normalizedMoocNumber ?? DBNull.Value, System.Data.ParameterDirection.Input);
        updateCommand.Parameters.Add("erpCutOrderId", OracleDbType.Varchar2, normalizedErpCutOrderId, System.Data.ParameterDirection.Input);

        var affectedRows = await updateCommand.ExecuteNonQueryAsync(ct);
        var currentValues = await ReadCurrentValuesAsync(connection, normalizedErpCutOrderId, ct);

        _logger.LogInformation(
            "Completed ERP transport write-back. ErpCutOrderId={ErpCutOrderId}, AffectedRows={AffectedRows}, PreviousVehiclePlate={PreviousVehiclePlate}, PreviousMoocNumber={PreviousMoocNumber}, CurrentVehiclePlate={CurrentVehiclePlate}, CurrentMoocNumber={CurrentMoocNumber}",
            normalizedErpCutOrderId,
            affectedRows,
            previousValues.TransportNo,
            previousValues.MoocNo,
            currentValues?.TransportNo,
            currentValues?.MoocNo);

        return new ErpCutOrderWriteBackResult(
            normalizedErpCutOrderId,
            affectedRows,
            previousValues.TransportNo,
            previousValues.MoocNo,
            currentValues?.TransportNo,
            currentValues?.MoocNo);
    }

    public async Task<ErpCutOrderSealWriteBackResult> UpdateSealNoAsync(ErpCutOrderSealWriteBackRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ErpCutOrderId))
        {
            throw new InvalidOperationException("ErpCutOrderId is required for ERP write-back.");
        }

        var connectionString = _configuration[ConnectionStringPath];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Chưa cấu hình {ConnectionStringPath} trong appsettings.json.");
        }

        var normalizedErpCutOrderId = request.ErpCutOrderId.Trim();
        var normalizedSealNo = NormalizeOptional(request.SealNo);

        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(ct);

        _logger.LogInformation(
            "Starting ERP seal write-back. ErpCutOrderId={ErpCutOrderId}, SealNo={SealNo}",
            normalizedErpCutOrderId,
            normalizedSealNo);

        var previousSealNo = await ReadCurrentSealNoAsync(connection, normalizedErpCutOrderId, ct);
        if (previousSealNo == null && !await ExistsByDocumentNoAsync(connection, normalizedErpCutOrderId, ct))
        {
            _logger.LogWarning(
                "ERP seal write-back skipped because document was not found. ErpCutOrderId={ErpCutOrderId}",
                normalizedErpCutOrderId);
            return new ErpCutOrderSealWriteBackResult(
                normalizedErpCutOrderId,
                0,
                null,
                null);
        }

        const string updateSql = """
UPDATE M_CommandLatching
SET
    SoNiemChi = :sealNo
WHERE documentNo = :erpCutOrderId
""";

        await using var updateCommand = connection.CreateCommand();
        updateCommand.BindByName = true;
        updateCommand.CommandText = updateSql;
        updateCommand.Parameters.Add("sealNo", OracleDbType.Varchar2, (object?)normalizedSealNo ?? DBNull.Value, System.Data.ParameterDirection.Input);
        updateCommand.Parameters.Add("erpCutOrderId", OracleDbType.Varchar2, normalizedErpCutOrderId, System.Data.ParameterDirection.Input);

        var affectedRows = await updateCommand.ExecuteNonQueryAsync(ct);
        var currentSealNo = await ReadCurrentSealNoAsync(connection, normalizedErpCutOrderId, ct);

        _logger.LogInformation(
            "Completed ERP seal write-back. ErpCutOrderId={ErpCutOrderId}, AffectedRows={AffectedRows}, PreviousSealNo={PreviousSealNo}, CurrentSealNo={CurrentSealNo}",
            normalizedErpCutOrderId,
            affectedRows,
            previousSealNo,
            currentSealNo);

        return new ErpCutOrderSealWriteBackResult(
            normalizedErpCutOrderId,
            affectedRows,
            previousSealNo,
            currentSealNo);
    }

    public async Task<ErpCutOrderNoteWriteBackResult> UpdateDescriptionAsync(ErpCutOrderNoteWriteBackRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ErpCutOrderId))
        {
            throw new InvalidOperationException("ErpCutOrderId is required for ERP write-back.");
        }

        var connectionString = _configuration[ConnectionStringPath];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Chưa cấu hình {ConnectionStringPath} trong appsettings.json.");
        }

        var normalizedErpCutOrderId = request.ErpCutOrderId.Trim();
        var normalizedDescription = NormalizeOptional(request.Description);

        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(ct);

        _logger.LogInformation(
            "Starting ERP description write-back. ErpCutOrderId={ErpCutOrderId}, Description={Description}",
            normalizedErpCutOrderId,
            normalizedDescription);

        var previousDescription = await ReadCurrentDescriptionAsync(connection, normalizedErpCutOrderId, ct);
        if (previousDescription == null && !await ExistsByDocumentNoAsync(connection, normalizedErpCutOrderId, ct))
        {
            _logger.LogWarning(
                "ERP description write-back skipped because document was not found. ErpCutOrderId={ErpCutOrderId}",
                normalizedErpCutOrderId);
            return new ErpCutOrderNoteWriteBackResult(
                normalizedErpCutOrderId,
                0,
                null,
                null);
        }

        const string updateSql = """
UPDATE M_CommandLatching
SET
    Description = :description
WHERE documentNo = :erpCutOrderId
""";

        await using var updateCommand = connection.CreateCommand();
        updateCommand.BindByName = true;
        updateCommand.CommandText = updateSql;
        updateCommand.Parameters.Add("description", OracleDbType.Varchar2, (object?)normalizedDescription ?? DBNull.Value, System.Data.ParameterDirection.Input);
        updateCommand.Parameters.Add("erpCutOrderId", OracleDbType.Varchar2, normalizedErpCutOrderId, System.Data.ParameterDirection.Input);

        var affectedRows = await updateCommand.ExecuteNonQueryAsync(ct);
        var currentDescription = await ReadCurrentDescriptionAsync(connection, normalizedErpCutOrderId, ct);

        _logger.LogInformation(
            "Completed ERP description write-back. ErpCutOrderId={ErpCutOrderId}, AffectedRows={AffectedRows}, PreviousDescription={PreviousDescription}, CurrentDescription={CurrentDescription}",
            normalizedErpCutOrderId,
            affectedRows,
            previousDescription,
            currentDescription);

        return new ErpCutOrderNoteWriteBackResult(
            normalizedErpCutOrderId,
            affectedRows,
            previousDescription,
            currentDescription);
    }

    public async Task<ErpCutOrderReceiverWriteBackResult> UpdateReceiverAsync(ErpCutOrderReceiverWriteBackRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ErpCutOrderId))
        {
            throw new InvalidOperationException("ErpCutOrderId is required for ERP write-back.");
        }

        var connectionString = _configuration[ConnectionStringPath];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Chưa cấu hình {ConnectionStringPath} trong appsettings.json.");
        }

        var normalizedErpCutOrderId = request.ErpCutOrderId.Trim();
        var normalizedReceiver = NormalizeOptional(request.Receiver);

        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(ct);

        _logger.LogInformation(
            "Starting ERP receiver write-back. ErpCutOrderId={ErpCutOrderId}, Receiver={Receiver}",
            normalizedErpCutOrderId,
            normalizedReceiver);

        var previousReceiver = await ReadCurrentReceiverAsync(connection, normalizedErpCutOrderId, ct);
        if (previousReceiver == null && !await ExistsByDocumentNoAsync(connection, normalizedErpCutOrderId, ct))
        {
            _logger.LogWarning(
                "ERP receiver write-back skipped because document was not found. ErpCutOrderId={ErpCutOrderId}",
                normalizedErpCutOrderId);
            return new ErpCutOrderReceiverWriteBackResult(
                normalizedErpCutOrderId,
                0,
                null,
                null);
        }

        const string updateSql = """
UPDATE M_CommandLatching
SET
    Receiver = :receiver
WHERE documentNo = :erpCutOrderId
""";

        await using var updateCommand = connection.CreateCommand();
        updateCommand.BindByName = true;
        updateCommand.CommandText = updateSql;
        updateCommand.Parameters.Add("receiver", OracleDbType.Varchar2, (object?)normalizedReceiver ?? DBNull.Value, System.Data.ParameterDirection.Input);
        updateCommand.Parameters.Add("erpCutOrderId", OracleDbType.Varchar2, normalizedErpCutOrderId, System.Data.ParameterDirection.Input);

        var affectedRows = await updateCommand.ExecuteNonQueryAsync(ct);
        var currentReceiver = await ReadCurrentReceiverAsync(connection, normalizedErpCutOrderId, ct);

        _logger.LogInformation(
            "Completed ERP receiver write-back. ErpCutOrderId={ErpCutOrderId}, AffectedRows={AffectedRows}, PreviousReceiver={PreviousReceiver}, CurrentReceiver={CurrentReceiver}",
            normalizedErpCutOrderId,
            affectedRows,
            previousReceiver,
            currentReceiver);

        return new ErpCutOrderReceiverWriteBackResult(
            normalizedErpCutOrderId,
            affectedRows,
            previousReceiver,
            currentReceiver);
    }

    private static async Task<ErpTransportInfo?> ReadCurrentValuesAsync(
        OracleConnection connection,
        string erpCutOrderId,
        CancellationToken ct)
    {
        const string selectSql = """
SELECT transportNo, MoocNo
FROM M_CommandLatching
WHERE documentNo = :erpCutOrderId
""";

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = selectSql;
        command.Parameters.Add("erpCutOrderId", OracleDbType.Varchar2, erpCutOrderId, System.Data.ParameterDirection.Input);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var transportNo = reader.IsDBNull(0) ? null : reader.GetString(0);
        var moocNo = reader.IsDBNull(1) ? null : reader.GetString(1);
        return new ErpTransportInfo(transportNo, moocNo);
    }

    private static async Task<string?> ReadCurrentSealNoAsync(
        OracleConnection connection,
        string erpCutOrderId,
        CancellationToken ct)
    {
        const string selectSql = """
SELECT SoNiemChi
FROM M_CommandLatching
WHERE documentNo = :erpCutOrderId
""";

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = selectSql;
        command.Parameters.Add("erpCutOrderId", OracleDbType.Varchar2, erpCutOrderId, System.Data.ParameterDirection.Input);

        var result = await command.ExecuteScalarAsync(ct);
        return result == null || result == DBNull.Value ? null : Convert.ToString(result);
    }

    private static async Task<string?> ReadCurrentDescriptionAsync(
        OracleConnection connection,
        string erpCutOrderId,
        CancellationToken ct)
    {
        const string selectSql = """
SELECT Description
FROM M_CommandLatching
WHERE documentNo = :erpCutOrderId
""";

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = selectSql;
        command.Parameters.Add("erpCutOrderId", OracleDbType.Varchar2, erpCutOrderId, System.Data.ParameterDirection.Input);

        var result = await command.ExecuteScalarAsync(ct);
        return result == null || result == DBNull.Value ? null : Convert.ToString(result);
    }

    private static async Task<string?> ReadCurrentReceiverAsync(
        OracleConnection connection,
        string erpCutOrderId,
        CancellationToken ct)
    {
        const string selectSql = """
SELECT Receiver
FROM M_CommandLatching
WHERE documentNo = :erpCutOrderId
""";

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = selectSql;
        command.Parameters.Add("erpCutOrderId", OracleDbType.Varchar2, erpCutOrderId, System.Data.ParameterDirection.Input);

        var result = await command.ExecuteScalarAsync(ct);
        return result == null || result == DBNull.Value ? null : Convert.ToString(result);
    }

    private static async Task<bool> ExistsByDocumentNoAsync(
        OracleConnection connection,
        string erpCutOrderId,
        CancellationToken ct)
    {
        const string selectSql = """
SELECT 1
FROM M_CommandLatching
WHERE documentNo = :erpCutOrderId
""";

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = selectSql;
        command.Parameters.Add("erpCutOrderId", OracleDbType.Varchar2, erpCutOrderId, System.Data.ParameterDirection.Input);

        var result = await command.ExecuteScalarAsync(ct);
        return result != null && result != DBNull.Value;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ErpTransportInfo(string? TransportNo, string? MoocNo);
}
