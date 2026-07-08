# Kế hoạch chi tiết: Động hóa cấu hình Camera theo Trạm cân (C2 / C6)

Tài liệu này mô tả kế hoạch thiết kế và triển khai giải pháp kỹ thuật để tự động nhận diện và cấu hình Camera tương ứng với trạm in/cân cục bộ (`PrintingStationName` trong `appsettings.json` hoặc tên máy tính Windows), thay vì cấu hình cố định trạm cân C6 cho các màn hình xuất khẩu và lưu chụp ảnh.

---

## ⚠️ Bài học từ lỗi đã gặp (Lessons Learned)

> **Bug đã xảy ra với tính năng in phiếu cân (PrintingStationName):**
> - Logic thay thế tên trạm `C2 → C6` đã được implement trong `PrintDocumentExporter.cs` (đường export ra file Word/Excel).
> - Nhưng **bỏ sót** `PrintOverlayRenderer.cs` (đường render preview WPF trực tiếp trên màn hình).
> - Kết quả: File in đúng trạm, nhưng preview trên màn hình vẫn hiện sai.

**Nguyên tắc bắt buộc rút ra:**
1. 🔴 **Audit ALL call sites** – Trước khi implement, phải liệt kê đầy đủ TẤT CẢ nơi gọi hàm liên quan (dùng grep search toàn bộ codebase).
2. 🔴 **Parallel path check** – Bất kỳ tính năng nào có nhiều đường render/thực thi (preview vs. export, UI vs. background service) đều phải xử lý đồng bộ trên tất cả các đường đó.
3. 🔴 **Post-implementation grep** – Sau khi implement xong, chạy lại grep để tìm hardcoded string còn sót.

---

## 1. Phân tích yêu cầu & Thiết kế giải pháp

### Hiện trạng – Audit TẤT CẢ call sites `GetForStationAsync`

Đây là danh sách đầy đủ tất cả nơi gọi camera với mã trạm cố định (kết quả từ grep search):

| File | Dòng | Mã trạm hiện tại | Cần thay đổi? |
|------|------|-----------------|--------------|
| `WeighingViewModel.cs` | 564 | `"C2"` | ❌ **Giữ nguyên** – Cân nội địa luôn dùng C2 theo nghiệp vụ |
| `ExportWeighingViewModel.cs` | 1102 | `"C6"` | ✅ **Cần sửa** → dùng `null` (auto-resolve) |
| `CrusherWeighingViewModel.cs` | 384 | `"CRUSHER"` | ❌ Giữ nguyên – trạm nghiền chuyên biệt |
| `ClayWeighingViewModel.cs` | 386 | `"CLAY"` | ❌ Giữ nguyên – trạm đất sét chuyên biệt |
| `CaptureSessionWeight1UseCase.cs` | 220 | `isExport ? "C6" : "C2"` | ✅ **Cần sửa** → `isExport ? null : "C2"` |
| `CaptureSessionWeight2UseCase.cs` | 401 | `isExport ? "C6" : "C2"` | ✅ **Cần sửa** → `isExport ? null : "C2"` |
| `InfrastructureServices.cs` (GetAsync) | 286 | `"C2"` (fallback default) | ✅ **Cần sửa** → auto-resolve từ config |

### Giải pháp kỹ thuật đề xuất

1. **Cho phép tham số StationCode có giá trị Null/Trống trong Provider:**
   - Thay đổi chữ ký của `GetForStationAsync` trong interface `ICameraSettingsProvider` thành `GetForStationAsync(string? stationCode, CancellationToken ct)`.
   - Nếu truyền vào `null`, hệ thống tự phân giải trạm cân từ cấu hình.

2. **Triển khai phân giải động tại `CameraSettingsProvider`:**
   - Inject thêm `IConfiguration` vào constructor.
   - Tạo phương thức nội bộ `ResolveStationCode(string? overrideCode)`:
     - Nếu `overrideCode` có giá trị → dùng luôn (ví dụ: `"C2"`, `"CRUSHER"`, `"CLAY"`).
     - Nếu `null` → đọc `configuration["PrintingStationName"]`, fallback `Environment.MachineName`.
   - Cập nhật `GetAsync` để gọi `ResolveStationCode(null)`.

3. **Cập nhật call sites có hardcoded "C6":**
   - `ExportWeighingViewModel.cs`: `"C6"` → `null`
   - `CaptureSessionWeight1UseCase.cs`: `isExport ? "C6" : "C2"` → `isExport ? null : "C2"`
   - `CaptureSessionWeight2UseCase.cs`: `isExport ? "C6" : "C2"` → `isExport ? null : "C2"`

---

## 2. Chi tiết các tệp thay đổi

### 📂 StationApp.Application

#### [MODIFY] [ICameraSettingsProvider.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/Interfaces/ICameraSettingsProvider.cs)
```diff
- Task<CameraSystemSettings> GetForStationAsync(string stationCode, CancellationToken ct);
+ Task<CameraSystemSettings> GetForStationAsync(string? stationCode, CancellationToken ct);
```

#### [MODIFY] [CaptureSessionWeight1UseCase.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/UseCases/CaptureSessionWeight1UseCase.cs)
```diff
- var settings = await _cameraSettingsProvider.GetForStationAsync(isExport ? "C6" : "C2", ct);
+ var settings = await _cameraSettingsProvider.GetForStationAsync(isExport ? null : "C2", ct);
```

#### [MODIFY] [CaptureSessionWeight2UseCase.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/UseCases/CaptureSessionWeight2UseCase.cs)
```diff
- var settings = await _cameraSettingsProvider.GetForStationAsync(isExport ? "C6" : "C2", ct);
+ var settings = await _cameraSettingsProvider.GetForStationAsync(isExport ? null : "C2", ct);
```

### 📂 StationApp.Infrastructure

#### [MODIFY] [InfrastructureServices.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Infrastructure/Services/InfrastructureServices.cs)

```csharp
// Constructor: thêm IConfiguration
public CameraSettingsProvider(IAppConfigRepository configRepo, IConfiguration configuration)
{
    _configRepo = configRepo;
    var station = configuration["PrintingStationName"];
    if (string.IsNullOrWhiteSpace(station))
    {
        var machineName = Environment.MachineName.ToUpperInvariant();
        station = machineName.Contains("C6") ? "C6" : "C2";
    }
    _resolvedStationCode = station.Trim();
}

// GetAsync: sử dụng resolved code thay vì hardcode "C2"
public Task<CameraSystemSettings> GetAsync(CancellationToken ct)
    => GetForStationAsync(null, ct);

// GetForStationAsync: null → dùng _resolvedStationCode
public async Task<CameraSystemSettings> GetForStationAsync(string? stationCode, CancellationToken ct)
{
    var code = string.IsNullOrWhiteSpace(stationCode) ? _resolvedStationCode : stationCode;
    var profile = CameraStationProfile.Resolve(code);
    // ... phần còn lại không đổi
}
```

### 📂 StationApp.UI

#### [MODIFY] [ExportWeighingViewModel.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs)
```diff
- var settings = await provider.GetForStationAsync("C6", CancellationToken.None);
+ var settings = await provider.GetForStationAsync(null, CancellationToken.None);
```

---

## 3. Checklist Triển khai (Implementation Checklist)

> Phải hoàn thành **theo thứ tự**. Không bỏ qua bước nào.

- [ ] **Bước 1**: Sửa `ICameraSettingsProvider.cs` – đổi signature `string` → `string?`
- [ ] **Bước 2**: Sửa `InfrastructureServices.cs` – inject `IConfiguration`, thêm `_resolvedStationCode`, sửa `GetAsync` + `GetForStationAsync`
- [ ] **Bước 3**: Sửa `CaptureSessionWeight1UseCase.cs`
- [ ] **Bước 4**: Sửa `CaptureSessionWeight2UseCase.cs`
- [ ] **Bước 5**: Sửa `ExportWeighingViewModel.cs`
- [ ] **Bước 6 (Bắt buộc)**: Chạy grep kiểm tra không còn hardcoded `"C6"` trong các lệnh gọi `GetForStationAsync`:
  ```powershell
  grep -rn '"C6"' src --include="*.cs" | grep -v "CameraStationProfile\|CameraConfigViewModel\|AppConfigKeys\|AppConfigDefaults"
  ```
- [ ] **Bước 7**: `dotnet build` thành công, 0 errors
- [ ] **Bước 8**: Kiểm thử thực tế (xem mục 4)

---

## 4. Kế hoạch kiểm thử & Xác minh (Verification Plan)

### Bước 1: Build
```powershell
dotnet build src\StationApp.UI\StationApp.UI.csproj
```
Kết quả mong đợi: **0 errors, 0 warnings**.

### Bước 2: Post-implementation Grep (bắt buộc – rút kinh nghiệm từ bug cũ)

Kiểm tra không còn hardcoded `"C6"` sai chỗ:
```powershell
# Tìm tất cả chỗ dùng "C6" trong GetForStationAsync - phải trả về 0 kết quả
grep -rn 'GetForStationAsync.*"C6"' src --include="*.cs"
```

Kiểm tra không còn `isExport ? "C6"` pattern:
```powershell
grep -rn 'isExport.*"C6"' src --include="*.cs"
```

### Bước 3: Kiểm thử thực tế

| # | Thiết lập | Màn hình | Kết quả mong đợi |
|---|-----------|----------|-----------------|
| 1 | `PrintingStationName: "C6"` | Cân xuất khẩu (preview camera) | Camera **C6** |
| 2 | `PrintingStationName: "C6"` | Lưu cân xuất khẩu → ảnh chụp | Ảnh từ camera **C6** |
| 3 | `PrintingStationName: "C2"` | Cân xuất khẩu (preview camera) | Camera **C2** |
| 4 | `PrintingStationName: "C6"` | Cân nội địa (preview camera) | Camera **C2** (ép buộc) |
| 5 | `PrintingStationName: "C6"` | Lưu cân nội địa → ảnh chụp | Ảnh từ camera **C2** (ép buộc) |
| 6 | *(không set)* | Cân xuất khẩu | Fallback theo `MachineName` |
