# Kế hoạch triển khai kết nối Telegram Bot gửi thông báo giám sát

Tài liệu này mô tả chi tiết phương án tích hợp Telegram Bot (qua BotFather) vào phần mềm cân C# để tự động bắn tin nhắn cảnh báo tới quản lý khi nhân viên thực hiện các hành động nhạy cảm (Chuyển chuyến xe, Đổi số xe).

---

## 📋 Yêu cầu Nghiệp vụ

1. **Trạm NMC**: 
   - Gửi tin nhắn khi xác nhận **Chuyển chuyến** (`TRANSFER_EXPORT_TRIP`).
2. **Trạm Mỏ đá và Mỏ sét**:
   - Gửi tin nhắn khi xác nhận **Đổi số xe** (`EDIT_WEIGHING_SESSION` / `UpdateSessionVehicleAsync`).
   - Gửi tin nhắn khi xác nhận **Chuyển chuyến** (`TRANSFER_CLAY_TRIP` / `TransferClayVehicleTripUseCase`).
3. **Nội dung tin nhắn**:
   - Thời gian thao tác.
   - Tài khoản nhân viên thao tác.
   - Mã trạm thực hiện.
   - Chi tiết lượt cân (Số phiếu, Biển số xe cũ/mới, thông tin hàng hóa, khối lượng cân lần 1/lần 2/Net, lý do sửa đổi).
4. **Cấu hình**:
   - Lưu trữ an toàn trong file [appsettings.json](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/appsettings.json).
5. **Kiểm tra kết nối**:
   - Cung cấp giao diện kiểm tra kết nối trực tiếp trong màn hình Tham số hệ thống.

---

## 🛠️ Đề xuất Thay đổi

### 1. Cấu hình hệ thống

#### [MODIFY] [appsettings.json](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/appsettings.json)
Thêm cấu hình kết nối Telegram dưới định dạng JSON (Lưu ý: Group Chat ID của Telegram thường bắt đầu bằng dấu trừ, ví dụ `-5155246228` hoặc `-1005155246228` nếu là Supergroup):
```json
  "Telegram": {
    "BotToken": "7899891008:AAH6ETLVAcoU6qBoeFldXYAySl79ZrFsssc",
    "ChatId": "-1005155246228",
    "Enabled": true
  }
```

---

### 2. Định nghĩa Dịch vụ Telegram (Application Layer)

#### [NEW] [ITelegramNotificationService.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/Interfaces/ITelegramNotificationService.cs)
```csharp
using System.Threading;
using System.Threading.Tasks;

namespace StationApp.Application.Interfaces;

public interface ITelegramNotificationService
{
    Task SendNotificationAsync(string message, CancellationToken ct);
    Task<(bool Success, string Message)> TestConnectionAsync(string token, string chatId, CancellationToken ct);
}
```

---

### 3. Cài đặt Dịch vụ Telegram (Infrastructure Layer)

#### [NEW] [TelegramNotificationService.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Infrastructure/Services/TelegramNotificationService.cs)
Sử dụng `HttpClient` để thực hiện HTTP POST request lên API của Telegram (`https://api.telegram.org/bot<token>/sendMessage`). Nội dung tin nhắn sẽ định dạng theo chuẩn HTML hoặc Markdown để hiển thị trực quan và chuyên nghiệp.

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly string _chatId;

    public TelegramNotificationService(HttpClient httpClient, IConfiguration configuration, ILogger<TelegramNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var section = configuration.GetSection("Telegram");
        _enabled = section.GetValue<bool>("Enabled", false);
        _botToken = section.GetValue<string>("BotToken") ?? string.Empty;
        _chatId = section.GetValue<string>("ChatId") ?? string.Empty;
    }

    public async Task SendNotificationAsync(string message, CancellationToken ct)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(_botToken) || string.IsNullOrWhiteSpace(_chatId))
        {
            return;
        }

        try
        {
            await ExecuteSendAsync(_botToken, _chatId, message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram notification");
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
            string testMessage = "🔔 <b>Kiểm tra kết nối Telegram</b>\n" +
                                 "Từ phần mềm: <i>Phần mềm cân trạm</i>\n" +
                                 "Trạng thái: <b>KẾT NỐI THÀNH CÔNG!</b>";
            await ExecuteSendAsync(token, chatId, testMessage, ct);
            return (true, "Đã gửi tin nhắn kiểm tra tới Telegram thành công!");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi kết nối Telegram: {ex.Message}");
        }
    }

    private async Task ExecuteSendAsync(string token, string chatId, string message, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{token}/sendMessage";
        var payload = new Dictionary<string, string>
        {
            { "chat_id", chatId },
            { "text", message },
            { "parse_mode", "HTML" }
        };

        using var content = new FormUrlEncodedContent(payload);
        using var response = await _httpClient.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Telegram API returned status {response.StatusCode}: {errorBody}");
        }
    }
}
```

---

### 4. Đăng ký dịch vụ trong DI Container

#### [MODIFY] [App.xaml.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/App.xaml.cs)
Đăng ký `TelegramNotificationService` kèm `HttpClient`:
```csharp
services.AddHttpClient<ITelegramNotificationService, TelegramNotificationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
```

---

### 5. Tích hợp gửi thông báo vào các UseCase Nghiệp vụ

Chúng ta sẽ bổ sung injection `ITelegramNotificationService` vào các UseCase và thực hiện bắn tin nhắn sau khi nghiệp vụ hoàn thành thành công:

#### [MODIFY] [TransferExportVehicleTripUseCase.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/UseCases/TransferExportVehicleTripUseCase.cs)
- Tiêm `ITelegramNotificationService` qua constructor.
- Gọi gửi thông báo khi hoàn tất chuyển chuyến xuất khẩu (trạm NMC):
```csharp
string message = $"⚠️ <b>CẢNH BÁO: CHUYỂN CHUYẾN XE (NMC)</b>\n" +
                 $"• <b>Số phiếu:</b> {session.SessionNo}\n" +
                 $"• <b>Xe cân:</b> {session.VehiclePlate}\n" +
                 $"• <b>Người thực hiện:</b> {_userContext.DisplayName} ({_userContext.Username})\n" +
                 $"• <b>Trạm:</b> {session.StationCode}\n" +
                 $"• <b>Từ cắt lệnh:</b> {sourceDisplayCode} ➡️ <b>Sang:</b> {targetDisplayCode}\n" +
                 $"• <b>Hàng hóa:</b> {targetCutOrder.ProductName}\n" +
                 $"• <b>Khách hàng:</b> {targetCutOrder.CustomerName}\n" +
                 $"• <b>Thời gian:</b> {now:dd/MM/yyyy HH:mm:ss}";
await _telegramService.SendNotificationAsync(message, ct);
```

#### [MODIFY] [ClayVesselFlowUseCases.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/UseCases/ClayVesselFlowUseCases.cs) (cho lớp `TransferClayVehicleTripUseCase`)
- Tiêm `ITelegramNotificationService`.
- Gửi thông báo khi chuyển chuyến mỏ sét:
```csharp
string message = $"⚠️ <b>CẢNH BÁO: CHUYỂN CHUYẾN XE (MỎ SÉT)</b>\n" +
                 $"• <b>Số phiếu:</b> {session.SessionNo}\n" +
                 $"• <b>Xe cân:</b> {session.VehiclePlate}\n" +
                 $"• <b>Người thực hiện:</b> {_userContext.DisplayName} ({_userContext.Username})\n" +
                 $"• <b>Trạm:</b> {session.StationCode}\n" +
                 $"• <b>Từ tàu:</b> {source.VehiclePlate} ➡️ <b>Sang:</b> {target.VehiclePlate}\n" +
                 $"• <b>Thời gian:</b> {now:dd/MM/yyyy HH:mm:ss}";
await _telegramService.SendNotificationAsync(message, ct);
```

#### [MODIFY] [ClayWeighingUseCases.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/UseCases/ClayWeighingUseCases.cs)
- Tiêm `ITelegramNotificationService`.
- Gửi thông báo khi cập nhật biển số xe mới (Đổi số xe mỏ sét):
```csharp
string message = $"⚠️ <b>CẢNH BÁO: ĐỔI SỐ XE (MỎ SÉT)</b>\n" +
                 $"• <b>Số phiếu:</b> {session.SessionNo}\n" +
                 $"• <b>Xe cũ:</b> {oldVehiclePlate} ➡️ <b>Xe mới:</b> {session.VehiclePlate}\n" +
                 $"• <b>Người thực hiện:</b> {_currentUser.DisplayName} ({CurrentUsername()})\n" +
                 $"• <b>Lý do:</b> {reason}\n" +
                 $"• <b>KL cân 1:</b> {session.Weight1:N0} kg\n" +
                 $"• <b>KL cân 2:</b> {session.Weight2:N0} kg\n" +
                 $"• <b>KL tịnh:</b> {session.NetWeight:N0} kg\n" +
                 $"• <b>Thời gian:</b> {now:dd/MM/yyyy HH:mm:ss}";
await _telegramService.SendNotificationAsync(message, ct);
```

#### [MODIFY] [CrusherWeighingUseCases.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/UseCases/CrusherWeighingUseCases.cs)
- Tiêm `ITelegramNotificationService`.
- Gửi thông báo khi cập nhật biển số xe mới (Đổi số xe mỏ đá):
```csharp
string message = $"⚠️ <b>CẢNH BÁO: ĐỔI SỐ XE (MỎ ĐÁ)</b>\n" +
                 $"• <b>Số phiếu:</b> {session.SessionNo}\n" +
                 $"• <b>Xe cũ:</b> {oldVehiclePlate} ➡️ <b>Xe mới:</b> {session.VehiclePlate}\n" +
                 $"• <b>Người thực hiện:</b> {_currentUser.DisplayName} ({CurrentUsername()})\n" +
                 $"• <b>Lý do:</b> {reason}\n" +
                 $"• <b>KL cân 1:</b> {session.Weight1:N0} kg\n" +
                 $"• <b>KL cân 2:</b> {session.Weight2:N0} kg\n" +
                 $"• <b>KL tịnh:</b> {session.NetWeight:N0} kg\n" +
                 $"• <b>Thời gian:</b> {now:dd/MM/yyyy HH:mm:ss}";
await _telegramService.SendNotificationAsync(message, ct);
```

---

### 6. Giao diện kiểm tra kết nối trong màn hình Cấu hình

Chúng ta sẽ bổ sung hiển thị trạng thái cấu hình Telegram từ file `appsettings.json` và cung cấp nút bấm "KIỂM TRA KẾT NỐI TELEGRAM" vào mục **THAM SỐ HỆ THỐNG**.

#### [MODIFY] [SystemSettingsViewModel.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/ViewModels/Settings/SystemSettingsViewModel.cs)
- Nạp cấu hình Telegram từ `IConfiguration` để hiển thị trực quan thông tin cấu hình (chỉ đọc):
  - `TelegramStatusMessage`: Hiển thị ví dụ "Đang hoạt động (BotToken: ***... | ChatId: -100...)".
- Thêm `TestTelegramConnectionCommand` để thực hiện kiểm tra gửi tin nhắn test bằng Token/ChatId hiện hành trong cấu hình.

#### [MODIFY] [SystemSettingsView.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/Settings/SystemSettingsView.xaml)
Bổ sung một mục cấu hình Telegram (chỉ đọc thông tin cấu hình từ `appsettings.json`) kèm nút bấm **KIỂM TRA KẾT NỐI TELEGRAM**.

---

## 🧪 Kế hoạch Xác minh

### Hướng dẫn kiểm tra nhanh kết nối bên ngoài
Quản lý hoặc kỹ thuật viên có thể kiểm tra trực tiếp xem Bot Token và Chat ID có hợp lệ hay không bằng cách gọi trực tiếp qua trình duyệt hoặc công cụ curl:
```bash
curl -X POST "https://api.telegram.org/bot<YOUR_BOT_TOKEN>/sendMessage" \
     -d "chat_id=<YOUR_CHAT_ID>" \
     -d "text=Hello from weighing system test"
```
*(Nếu nhận được phản hồi JSON có `"ok":true` và có tin nhắn trong nhóm là thành công!)*

### Xác minh tự động
1. **Kiểm tra biên dịch**: Chạy `dotnet build` để kiểm tra lỗi cú pháp sau chỉnh sửa.
2. **Kiểm tra chức năng trong phần mềm**:
   - Mở màn hình **Tham số hệ thống**, xem thông tin Telegram cấu hình từ file `appsettings.json`.
   - Ấn **KIỂM TRA KẾT NỐI TELEGRAM** -> Xem thông báo kết quả trả về và kiểm tra điện thoại xem nhóm Telegram có nhận được thông báo test hay không.
   - Chạy ứng dụng, giả lập các thao tác **Chuyển chuyến** tại trạm NMC, mỏ sét và **Đổi số xe** tại mỏ đá, mỏ sét để đảm bảo có tin nhắn gửi tới nhóm kịp thời.
