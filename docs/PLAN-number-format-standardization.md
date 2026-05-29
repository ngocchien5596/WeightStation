# Kế hoạch triển khai: Chuẩn hóa Định dạng Số (Number Formatting & Parsing) trên Toàn hệ thống

Tài liệu này phân tích chi tiết nguyên nhân gây không đồng nhất định dạng số (lúc dùng dấu chấm `.`, lúc dùng dấu phẩy `,` phân tách hàng nghìn) trên các màn hình, xác định rủi ro gây lỗi tính toán và đề xuất kế hoạch chuẩn hóa toàn diện trên toàn dự án.

---

## 1. Phân tích Hiện trạng & Rủi ro Lỗi tính toán (Culture Mismatch)

### 1.1 Nguyên nhân gây không đồng nhất hiển thị
Qua nghiên cứu mã nguồn, sự không đồng nhất này xuất phát từ cách cấu hình `Binding` trong XAML:
1. **Sử dụng `ConverterCulture='vi-VN'`:** Một số trường (như `TtcpWeight` ở màn danh sách xe vào, `EditTtcpWeight` ở màn cấu hình xe) chỉ định rõ văn hóa Việt Nam.
   * Phân tách hàng nghìn: Dấu chấm `.` (Ví dụ: `1.234`)
   * Phân tách thập phân: Dấu phẩy `,` (Ví dụ: `1,50`)
2. **Không cấu hình `ConverterCulture`:** Hầu hết các trường hiển thị cân (như `Weight1`, `Weight2`, `NetWeight` ở màn lập phiếu cân và danh sách xe ra) sử dụng định dạng mặc định của WPF (tiêu chuẩn XML là `en-US` hoặc `InvariantCulture`).
   * Phân tách hàng nghìn: Dấu phẩy `,` (Ví dụ: `1,234`)
   * Phân tách thập phân: Dấu chấm `.` (Ví dụ: `1.50`)

### 1.2 Rủi ro cực kỳ nguy hiểm (Lỗi tính toán/Ép kiểu nhầm)
Khi người vận hành nhập dữ liệu vào ô TextBox (ví dụ nhập trọng lượng đăng kiểm `TtcpWeight` hoặc các trường cân tay):
* Nếu hệ thống chạy `decimal.TryParse(value, out parsedWeight)` mà không chỉ định rõ `CultureInfo.InvariantCulture`, C# sẽ tự động sử dụng **Văn hóa Hệ điều hành (OS Culture)** của máy tính chạy ứng dụng.
* **Kịch bản lỗi:**
  * Máy tính của trạm cân cấu hình vùng là Việt Nam (`vi-VN`), người dùng nhập `1234` không dấu phân cách hoặc `1.234` (1 nghìn 234 kg). C# sẽ hiểu đúng là `1234`.
  * Tuy nhiên, nếu binding sử dụng định dạng US (hiển thị `1,234` có dấu phẩy), C# parse `1,234` dưới văn hóa `vi-VN` (coi `,` là dấu thập phân) sẽ ép kiểu ra giá trị **`1.234`** (Một phẩy hai trăm ba mươi tư kg). **Trọng lượng bị giảm đi 1000 lần!** Đây chính là rủi ro nghiêm trọng gây lỗi sai lệch khối lượng cực lớn.

---

## 2. Giải pháp Chuẩn hóa An toàn & Nhất quang

Để loại bỏ hoàn toàn nguy cơ tính toán sai lệch và đảm bảo hiển thị 100% đồng nhất, chúng ta áp dụng **Giải pháp Định dạng Chuẩn Quốc tế (US/InvariantCulture)**:
* **Hiển thị hàng nghìn:** Luôn sử dụng dấu phẩy `,` (Ví dụ: `1,234.50 kg` hoặc `1,234 kg`).
* **Hiển thị số thập phân:** Luôn sử dụng dấu chấm `.` (Ví dụ: `1.5` hoặc `15.75`).
* **Tại sao giải pháp này an toàn nhất?**
  1. Tất cả hệ quản trị cơ sở dữ liệu (SQL Server) và các API RESTful / dịch vụ Outbox đồng bộ dữ liệu đều sử dụng dấu chấm `.` cho số thập phân.
  2. Việc ép kiểu trong code sử dụng `CultureInfo.InvariantCulture` sẽ hoạt động 100% nhất quán ở mọi máy trạm bất kể OS của máy trạm đó được cài đặt là tiếng Anh hay tiếng Việt.

---

## 3. Kiến trúc Triển khai (Technical Design)

### 3.1 Cấu hình Thread Culture Toàn cục (Global App Startup)
Cấu hình ngôn ngữ mặc định tại điểm khởi chạy ứng dụng `App.xaml.cs` để ép buộc toàn bộ thread và WPF binding sử dụng định dạng Invariant/en-US:
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    // Cấu hình Culture mặc định cho toàn bộ các Thread con sinh ra sau này
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

    // Ép buộc WPF binding sử dụng InvariantCulture (phân tách hàng nghìn bằng dấu phẩy)
    FrameworkElement.LanguageProperty.OverrideMetadata(
        typeof(FrameworkElement),
        new FrameworkPropertyMetadata(
            XmlLanguage.GetLanguage(CultureInfo.InvariantCulture.IetfLanguageTag)));

    base.OnStartup(e);
    ...
}
```

### 3.2 Loại bỏ `ConverterCulture='vi-VN'` trong XAML
Rà soát và xóa bỏ toàn bộ thuộc tính `ConverterCulture='vi-VN'` trong các file `.xaml`:
* [IncomingVehicleListView.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/IncomingVehicleListView.xaml)
* [VehicleMasterView.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/Settings/VehicleMasterView.xaml)
* [ExportWeighingView.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/ExportWeighingView.xaml)

### 3.3 Chuẩn hóa các hàm Ép kiểu Số trong C#
Cập nhật toàn bộ các hàm `decimal.TryParse` hoặc `double.TryParse` trong phần mềm để luôn truyền vào `CultureInfo.InvariantCulture` hoặc `NumberStyles.Any`:
* Trong `WeighingConverters.cs` (các Converter của WPF)
* Trong các ViewModel liên quan đến nhập liệu của người dùng.

---

## 4. Kế hoạch Phân công Công việc (Task Breakdown)

### 4.1 Giai đoạn 1: Thiết lập Toàn cục (Core Layer)
* **Task 1.1:** Cập nhật `App.xaml.cs` để cấu hình Culture và Language mặc định toàn cục là InvariantCulture.
  * *Agent:* `backend-specialist`, *Skill:* `clean-code`
  * *INPUT:* `App.xaml.cs`.
  * *OUTPUT:* Ứng dụng khởi chạy ép buộc định dạng US/InvariantCulture.
  * *VERIFY:* Chạy thử ứng dụng, tất cả các grid mặc định hiển thị phân tách hàng nghìn bằng dấu `,`.
* **Task 1.2:** Cập nhật các hàm Converter trong `WeighingConverters.cs` để parse số an toàn bằng `InvariantCulture`.
  * *Agent:* `backend-specialist`, *Skill:* `clean-code`
  * *INPUT:* `WeighingConverters.cs`.
  * *OUTPUT:* Các converter không còn phụ thuộc vào văn hóa hệ điều hành khi chuyển đổi chuỗi -> số.
  * *VERIFY:* Unit test Converter chạy thành công độc lập với OS Culture.

### 4.2 Giai đoạn 2: Chuẩn hóa XAML & ViewModel (UI & ViewModel Layer)
* **Task 2.1:** Rà soát và loại bỏ thuộc tính `ConverterCulture='vi-VN'` trong các file XAML.
  * *Agent:* `frontend-specialist`, *Skill:* `frontend-design`
  * *INPUT:* Các file XAML (`IncomingVehicleListView.xaml`, `VehicleMasterView.xaml`, `ExportWeighingView.xaml`).
  * *OUTPUT:* Các trường nhập liệu và hiển thị định dạng số đồng bộ.
  * *VERIFY:* Kiểm tra trực quan tất cả các trường hiển thị và nhập liệu số đều dùng dấu `,` phân tách hàng nghìn.
* **Task 2.2:** Rà soát các ViewModel (`WeighingViewModel.cs`, `IncomingVehicleListViewModel.cs`) để đảm bảo các hàm ép kiểu số từ TextBox đầu vào luôn sử dụng `InvariantCulture`.
  * *Agent:* `backend-specialist`, *Skill:* `clean-code`
  * *INPUT:* C# ViewModels.
  * *OUTPUT:* Quá trình gán trọng lượng hoặc planned weight từ giao diện vào Model tuyệt đối an toàn.
  * *VERIFY:* Kiểm tra nhập liệu `1,234` và `1234` đều được lưu đúng là `1234` kg.

---

## 5. Kế hoạch Xác minh & Kiểm thử (Verification Plan)

### 5.1 Kiểm thử tự động (Automated Testing)
* Viết unit test cho các bộ chuyển đổi số (`WeightToTonConverter`, `DecimalMultiplierConverter`) kiểm tra các trường hợp:
  * Chuỗi đầu vào có dấu phẩy hàng nghìn `1,234` -> Đưa ra đúng `1234`.
  * Chuỗi đầu vào không có phân tách `1234` -> Đưa ra đúng `1234`.
  * Chuỗi thập phân `12.5` -> Đưa ra đúng `12.5`.

### 5.2 Kiểm thử thủ công (Manual Verification)
1. **Thiết lập OS Culture thành Việt Nam:** Thay đổi vùng của hệ điều hành máy tính thử nghiệm sang Việt Nam (`vi-VN`).
2. **Kiểm tra hiển thị:** Mở tất cả các màn hình (Lập phiếu cân, Danh sách xe vào, Danh sách xe ra, Danh mục xe, Cấu hình). Xác nhận toàn bộ định dạng hiển thị hàng nghìn đều là dấu phẩy `,` (ví dụ: `15,000 kg` thay vì `15.000 kg`).
3. **Kiểm tra nhập liệu an toàn:**
   * Nhập trọng lượng xe con hoặc planned weight là `12,500`. Xác nhận hệ thống lưu đúng `12500` kg.
   * Nhập `12500`. Xác nhận hệ thống lưu đúng `12500` kg.
   * Đảm bảo tuyệt đối không xảy ra trường hợp trọng lượng bị chia 1000 lần (thành `12.5` kg).
4. **Kiểm tra In phiếu:** Xác nhận phiếu in ra giấy hoặc hóa đơn đồng bộ với định dạng hiển thị.
