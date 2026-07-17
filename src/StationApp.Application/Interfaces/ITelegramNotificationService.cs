namespace StationApp.Application.Interfaces;

public interface ITelegramNotificationService
{
    Task SendNotificationAsync(string message, string? stationCode, CancellationToken ct);

    Task<(bool Success, string Message)> TestConnectionAsync(string token, string chatId, CancellationToken ct);
}
