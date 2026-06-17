using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StationApp.Application.Interfaces;
using StationApp.Infrastructure.Persistence;

namespace StationApp.Infrastructure.Services;

public class DocumentCounterService : IDocumentCounterService
{
    private readonly StationDbContext _db;

    public DocumentCounterService(StationDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetNextSequenceAsync(string counterKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(counterKey))
        {
            throw new ArgumentException("Counter key cannot be null or empty.", nameof(counterKey));
        }

        const string sql = @"
            DECLARE @Result TABLE (LastValue INT);

            -- 1. Try to update the row if it exists
            UPDATE dbo.document_counters WITH (ROWLOCK)
            SET LastValue = LastValue + 1, UpdatedAt = SYSDATETIME()
            OUTPUT inserted.LastValue INTO @Result
            WHERE CounterKey = @CounterKey;

            -- 2. If it does not exist, insert the initial row
            IF NOT EXISTS (SELECT 1 FROM @Result)
            BEGIN
                BEGIN TRY
                    INSERT INTO dbo.document_counters (CounterKey, LastValue, UpdatedAt)
                    OUTPUT inserted.LastValue INTO @Result
                    VALUES (@CounterKey, 1, SYSDATETIME());
                END TRY
                BEGIN CATCH
                    -- Handle race condition where another transaction inserted it concurrently
                    UPDATE dbo.document_counters WITH (ROWLOCK)
                    SET LastValue = LastValue + 1, UpdatedAt = SYSDATETIME()
                    OUTPUT inserted.LastValue INTO @Result
                    WHERE CounterKey = @CounterKey;
                END CATCH
            END

            SELECT LastValue FROM @Result;
        ";

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@CounterKey";
            parameter.Value = counterKey;
            command.Parameters.Add(parameter);

            var currentTx = _db.Database.CurrentTransaction;
            if (currentTx != null)
            {
                command.Transaction = currentTx.GetDbTransaction();
            }

            var result = await command.ExecuteScalarAsync(ct);
            if (result == null || result == DBNull.Value)
            {
                throw new InvalidOperationException("Failed to generate sequence value.");
            }

            return Convert.ToInt32(result);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
