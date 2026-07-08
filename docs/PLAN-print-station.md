# Kế hoạch chi tiết: Nhận diện và in thông tin trạm cân động (C2 / C6)

Tài liệu này mô tả kế hoạch thiết kế và triển khai giải pháp kỹ thuật để tự động nhận diện và hiển thị thông tin trạm cân C2 hoặc C6 trên chân trang phiếu in cân, trong khi hệ thống cơ sở dữ liệu vẫn dùng chung mã trạm chính là QN01 (NMC).

---

## 1. Phân tích yêu cầu & Thiết kế giải pháp

### Hiện trạng
* Hệ thống cấu hình dùng chung thuộc trạm chính NMC có mã là `QN01`.
* Trên phiếu in cân, trường in `StaticFooterLeft` đang được định nghĩa tĩnh trong bộ khung mẫu là `"XMCP cân 120 tấn - C2"`.
* Cả hai trạm cân C2 và C6 chạy cùng một phần mềm trên 2 máy tính vật lý khác nhau, trong đó cơ sở dữ liệu SQL Server Local được cài đặt dùng chung trên 1 trong 2 máy tính này.
* **Lưu ý đặc biệt:** Vì sử dụng chung một cơ sở dữ liệu, việc ghi cấu hình trạm in vào bảng cấu hình của database (`AppConfigs`) sẽ gây đồng bộ đè cấu hình của nhau. Việc phân biệt trạm in bắt buộc phải dựa vào tài nguyên cục bộ trên mỗi máy tính vật lý độc lập (file cấu hình local hoặc tên máy tính Windows).

### Giải pháp kỹ thuật đề xuất
1. **Lưu cấu hình cục bộ:** Khai báo cấu hình tên trạm in cụ thể trong file `appsettings.json` nội bộ đặt trên từng máy tính (do file này nằm riêng biệt trên ổ đĩa của mỗi máy tính và không bị đồng bộ qua DB).
2. **Tự động nhận diện (Dự phòng):** Nếu file cấu hình không khai báo, hệ thống sử dụng tên máy tính Windows (`Environment.MachineName` - đại diện cho máy tính thực hiện thao tác bấm in) để nhận diện (nếu chứa từ khóa `"C6"` sẽ tự động chuyển sang `"C6"`, chứa `"C2"` sẽ chuyển sang `"C2"`).
3. **Thay thế động khi in:** Nạp cấu hình máy trạm vào lớp xử lý in `PrintDocumentExporter` và viết lại phương thức biên dịch giá trị in `ResolveFieldValue` để tự động thay thế hậu tố `- C2` thành trạm in tương ứng trước khi kết xuất ra file Excel/Word tạm để in.

---

## 2. Chi tiết các tệp thay đổi

### 📂 StationApp.UI

#### [MODIFY] [appsettings.json](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/appsettings.json)
* Bổ sung khóa `"PrintingStationName": "C2"` (hoặc `"C6"` tùy thuộc trạm cài đặt).
* Ví dụ cấu hình:
  ```json
  {
    "ConnectionStrings": { ... },
    "ErpOracle": { ... },
    "DiagnosticMode": false,
    "PrintingStationName": "C2"
  }
  ```

### 📂 StationApp.Infrastructure

#### [MODIFY] [PrintDocumentExportService.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Infrastructure/Services/PrintDocumentExportService.cs)
* **Hàm khởi tạo (Constructor):**
  * Thêm tham số `IConfiguration configuration` để hệ thống tự động inject cấu hình.
  * Phân tích và lưu trữ giá trị trạm in vào biến readonly `_printingStationName`.
* **Loại bỏ từ khóa `static` tại các phương thức xử lý cấu trúc để truy cập biến đối tượng:**
  * `ResolveFieldValue`
  * `BuildExcelPage`
  * `BuildWeighTicketExcelPage`
  * `BuildWordPageTable`
  * `BuildWordPlacements`
* **Cập nhật phương thức `ResolveFieldValue`:**
  * Thêm logic: Nếu trường in là `StaticFooterLeft` và có giá trị tĩnh chứa `- C2`, tự động thay thế `"C2"` thành tên trạm in đã nhận diện.

---

## 3. Phân công Agent thực hiện (Agent Assignments)

* **Explorer Agent:** Thực hiện rà soát các khai báo dịch vụ và kiểm tra các phụ thuộc DI (Dependency Injection) của dự án.
* **Backend Specialist:** Chỉnh sửa code logic khởi tạo lớp `PrintDocumentExporter` và thay đổi phương thức `ResolveFieldValue`.
* **Test Engineer:** Kiểm tra biên dịch dự án và kiểm thử hiển thị phiếu in.

---

## 4. Kế hoạch kiểm thử & Xác minh (Verification Plan)

### Bước 1: Build xác nhận cú pháp
Chạy lệnh kiểm tra biên dịch toàn bộ solution:
```powershell
dotnet build src\StationApp.UI\StationApp.UI.csproj
```

### Bước 2: Kiểm thử thủ công trên máy ảo/trạm chạy thử
1. Thêm cấu hình `"PrintingStationName": "C6"` vào file `appsettings.json`.
2. Mở ứng dụng và thực hiện in thử/xem trước phiếu cân.
3. Xác nhận chân trang bên trái in ra dòng chữ: **"XMCP cân 120 tấn - C6"**.
4. Sửa cấu hình thành `"PrintingStationName": "C2"`.
5. Thực hiện in lại và xác nhận chân trang hiển thị đúng: **"XMCP cân 120 tấn - C2"**.
6. Xóa cấu hình `"PrintingStationName"` khỏi `appsettings.json`, đổi tên máy tính Windows thử nghiệm chứa ký tự `C6`, chạy lại app và kiểm tra tính năng tự động nhận diện thiết bị dự phòng.
