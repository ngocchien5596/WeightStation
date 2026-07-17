using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StationApp.Application.Interfaces;

namespace StationApp.Infrastructure.Services;

public sealed class TelegramNotificationService : ITelegramNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly bool _enabled;
    private readonly string _botToken;
    private readonly string _defaultChatId;
    private readonly IReadOnlyDictionary<string, string> _stationChatIds;

    public TelegramNotificationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TelegramNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var section = configuration.GetSection("Telegram");
        _enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled;
        _botToken = section["BotToken"] ?? string.Empty;
        _defaultChatId = section["DefaultChatId"] ?? section["ChatId"] ?? string.Empty;
        _stationChatIds = section.GetSection("StationChatIds")
            .GetChildren()
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(x => NormalizeStationCode(x.Key), x => x.Value!.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task SendNotificationAsync(string message, string? stationCode, CancellationToken ct)
    {
        var chatId = ResolveChatId(stationCode);
        if (!_enabled || string.IsNullOrWhiteSpace(_botToken) || string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogWarning("Skipped Telegram notification because configuration is incomplete. StationCode={StationCode}", stationCode);
            return;
        }

        try
        {
            await ExecuteSendAsync(_botToken, chatId, message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram notification. StationCode={StationCode}, ChatId={ChatId}", stationCode, MaskChatId(chatId));
        }
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(string token, string chatId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, "Bot Token không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(chatId))
        {
            return (false, "Chat ID không được để trống.");
        }

        try
        {
            var message = "<b>Kiểm tra kết nối Telegram</b>\n"
                + "Phần mềm: <i>Phần mềm cân trạm</i>\n"
                + $"Thời gian: <b>{DateTime.Now:dd/MM/yyyy HH:mm:ss}</b>\n"
                + "Trạng thái: <b>Kết nối thành công</b>";
            await ExecuteSendAsync(token, chatId, message, ct);
            return (true, "Đã gửi tin nhắn kiểm tra tới Telegram thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram test connection failed.");
            return (false, $"Lỗi kết nối Telegram: {ex.Message}");
        }
    }

    private string ResolveChatId(string? stationCode)
    {
        var normalizedStationCode = NormalizeStationCode(stationCode);
        if (!string.IsNullOrWhiteSpace(normalizedStationCode)
            && _stationChatIds.TryGetValue(normalizedStationCode, out var stationChatId))
        {
            return stationChatId;
        }

        return _defaultChatId;
    }

    private async Task ExecuteSendAsync(string token, string chatId, string message, CancellationToken ct)
    {
        var payload = new Dictionary<string, string>
        {
            ["chat_id"] = NormalizeChatId(chatId),
            ["text"] = message,
            ["parse_mode"] = "HTML",
            ["disable_web_page_preview"] = "true"
        };

        using var content = new FormUrlEncodedContent(payload);
        using var response = await _httpClient.PostAsync(BuildSendMessageUri(token), content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Telegram API trả về {(int)response.StatusCode} {response.StatusCode}: {BuildUserFriendlyDescription(body)}");
        }

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("ok", out var okElement) && okElement.ValueKind == JsonValueKind.True)
        {
            return;
        }

        throw new HttpRequestException($"Telegram API không xác nhận gửi thành công: {BuildUserFriendlyDescription(body)}");
    }

    private static Uri BuildSendMessageUri(string token)
        => new($"https://api.telegram.org/bot{token.Trim()}/sendMessage", UriKind.Absolute);

    private static string NormalizeChatId(string chatId)
        => chatId.Trim();

    private static string NormalizeStationCode(string? stationCode)
        => string.IsNullOrWhiteSpace(stationCode) ? string.Empty : stationCode.Trim().ToUpperInvariant();

    private static string ExtractTelegramDescription(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "Không có nội dung phản hồi.";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("description", out var description))
            {
                return description.GetString() ?? body;
            }
        }
        catch
        {
            // Fall through and return the raw response body.
        }

        return body;
    }

    private static string BuildUserFriendlyDescription(string body)
    {
        var description = ExtractTelegramDescription(body);
        var migrateToChatId = ExtractMigrateToChatId(body);
        if (migrateToChatId != null)
        {
            return $"Nhóm Telegram đã được nâng cấp thành supergroup. Vui lòng cập nhật ChatId mới: {migrateToChatId}.";
        }

        if (description.Contains("chat not found", StringComparison.OrdinalIgnoreCase))
        {
            return "Không tìm thấy nhóm/kênh Telegram. Vui lòng kiểm tra ChatId, thêm bot vào đúng nhóm/kênh và cho bot quyền gửi tin nhắn.";
        }

        return description;
    }

    private static string? ExtractMigrateToChatId(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("parameters", out var parameters)
                && parameters.TryGetProperty("migrate_to_chat_id", out var migrateToChatId))
            {
                return migrateToChatId.ValueKind == JsonValueKind.Number
                    ? migrateToChatId.GetInt64().ToString()
                    : migrateToChatId.GetString();
            }
        }
        catch
        {
            // Fall through and keep the original Telegram description.
        }

        return null;
    }

    private static string MaskChatId(string chatId)
    {
        var normalized = NormalizeChatId(chatId);
        return normalized.Length <= 6
            ? normalized
            : $"{normalized[..4]}...{normalized[^3..]}";
    }
}
