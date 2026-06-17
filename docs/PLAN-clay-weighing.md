# Plan: Cân trạm mỏ sét và Báo cáo nhập mỏ sét

Kế hoạch chi tiết để clone giao diện, luồng cân và báo cáo từ chức năng Cân trạm đập sang chức năng Cân mỏ sét, phân vùng dữ liệu dựa trên `StationCode`.

## Overview
- **Mục tiêu**: Xây dựng chức năng "Cân mỏ sét" và "Báo cáo nhập (mỏ sét)" chạy song song trên cùng hệ thống nhưng phân vùng dữ liệu riêng biệt.
- **Yêu cầu cốt lõi**:
  - Giao diện và luồng cân (cân 1 lần bằng xe chuẩn, cân 2 lần) kế thừa hoàn toàn từ trạm đập.
  - **Đảm bảo đồng bộ 100% về UI & Logic**: Tất cả các trường thông tin hiển thị, nút chức năng, trạng thái khóa/mở (Read-Only), logic kết nối và cập nhật đầu cân, cũng như các hành vi điều khiển (như việc xóa sạch toàn bộ số cân lần 1/lần 2, driver name, standard tare,... khi nhấn nút **Làm mới**) phải giống hệt màn hình Cân trạm đập.
  - Phân vùng dữ liệu triệt để theo mã trạm `StationCode`.
  - Cấu hình hiển thị menu & KPI trên Dashboard thông qua Feature Flag riêng biệt.
  - Thiết lập thông tin mặc định cho trạm mỏ sét: mã vật tư `"Set"`, tên vật tư `"Sét"`. Mã khách hàng `"NCC2"`, tên khách hàng `"Nhà cung cấp 2"`.
  - Xây dựng chức năng báo cáo nhập riêng biệt cho mỏ sét.

## Resolved Decisions & Assumptions
- **Cơ chế Camera**: Hiện tại trạm đập hiển thị khung placeholder camera tĩnh, mỏ sét cũng sẽ clone tương tự và tính năng kết nối camera thực tế sẽ được cấu hình/triển khai sau.
- **In phiếu cân**: Cả trạm đập và trạm mỏ sét đều không tích hợp tính năng in phiếu cân trực tiếp tại màn hình cân nội bộ này, do đó không cần cài đặt logic in ấn cho trạm mỏ sét.

## Success Criteria
- [ ] Màn hình **Cân mỏ sét** được cấu hình hiển thị khi người dùng đăng nhập vào trạm mỏ sét.
- [ ] Giao diện màn hình cân có bố cục và các trường nhập liệu giống hệt trạm đập, hỗ trợ đầy đủ các phím tắt/chức năng tương ứng.
- [ ] Phiên cân được tạo và lưu trữ đúng theo `StationCode` của trạm mỏ sét.
- [ ] Chế độ cân 1 lần và cân 2 lần hoạt động chính xác với thông tin xe nội bộ và trọng lượng xe chuẩn.
- [ ] Logic nút **Làm mới** (Refresh) hoạt động hoàn hảo: khi click, xóa sạch sẽ toàn bộ trạng thái cân lần 1/lần 2, thông tin xe nội bộ và thông tin tài xế giống như trạm đập.
- [ ] Thông tin vật tư mặc định (Sét/Set) và Nhà cung cấp mặc định (Nhà cung cấp 2/NCC2) tự động hiển thị chính xác khi khởi tạo hoặc làm mới.
- [ ] Màn hình **Báo cáo nhập mỏ sét** hiển thị dữ liệu chính xác theo khoảng thời gian và xuất được báo cáo.
- [ ] Trang chủ (Dashboard) hiển thị đúng 3 thẻ KPI (Đang cân, Đã hoàn thành, Số tấn) khi chuyển sang trạm mỏ sét.

## File Structure

```plaintext
src/
├── StationApp.Domain/
│   └── Constants/
│       ├── StationFeatureKeys.cs (Thêm ShowMenuClayWeighing, ShowMenuClayInboundReport)
│       └── ClayWeighingConstants.cs (NEW - Định nghĩa các hằng số mỏ sét & Operation Setting Keys)
│
├── StationApp.Application/
│   ├── DTOs/
│   │   ├── StationDtos.cs (Cập nhật StationFeatureSetDto)
│   │   └── ClayInboundReportDtos.cs (NEW - DTO báo cáo mỏ sét)
│   ├── Interfaces/
│   │   ├── IClayInboundReportService.cs (NEW)
│   │   ├── IClayInboundReportExporter.cs (NEW)
│   │   └── IWeighingSessionRepository.cs (Thêm SearchClaySessionsAsync)
│   └── UseCases/
│       ├── ClayWeighingUseCases.cs (NEW - Logic tạo lượt cân & hoàn thành cân mỏ sét)
│       └── ClayInboundReportUseCases.cs (NEW)
│
├── StationApp.Infrastructure/
│   ├── Persistence/
│   │   └── SchemaCompatibilityBootstrapper.cs (Thêm seed dữ liệu menu và cấu hình vận hành mỏ sét)
│   ├── Repositories/
│   │   └── WeighingSessionRepository.cs (Cập nhật thực thi SearchClaySessionsAsync)
│   └── Services/
│       ├── StationScopeServices.cs (Cập nhật mapping feature flags mỏ sét)
│       └── ClayInboundReportServices.cs (NEW - Query database lấy báo cáo mỏ sét)
│
└── StationApp.UI/
    ├── ViewModels/
    │   ├── ClayWeighingViewModel.cs (NEW - Gồm logic Reset, Refresh, kết nối đầu cân tương tự trạm đập)
    │   ├── ClayInboundReportViewModel.cs (NEW)
    │   ├── DashboardViewModel.cs (Thêm logic KPI cho trạm mỏ sét)
    │   ├── Settings/
    │   │   └── StationMasterViewModel.cs (Thêm checkbox quản lý menu mỏ sét)
    │   └── MainViewModel.cs (Thêm menu Cân mỏ sét & Báo cáo mỏ sét)
    └── Views/
        ├── ClayWeighingView.xaml (.xaml.cs) (NEW)
        ├── ClayInboundReportView.xaml (.xaml.cs) (NEW)
        ├── Settings/
        │   └── StationMasterView.xaml (Thêm checkbox hiển thị menu mỏ sét)
        ├── MainWindow.xaml (Menu mỏ sét)
        └── DashboardView.xaml (Binding KPI mỏ sét)
```

## Proposed Changes

### 1. Database & Domain Config
- **SchemaCompatibilityBootstrapper.cs**:
  - Thêm seed dữ liệu feature flags: `show_menu_clay_weighing = 'false'`, `show_menu_clay_inbound_report = 'false'`.
  - Thêm seed dữ liệu vận hành cho mỏ sét: `clay_single_weigh_enabled = 'false'`, `clay_default_weigh_mode = 'TWO_WEIGH'`, `clay_require_standard_tare_for_single_weigh = 'true'`, `clay_standard_tare_tolerance_kg = '0'`, `clay_default_product_code = ''`.
- **StationFeatureKeys.cs**:
  - Thêm `ShowMenuClayWeighing = "show_menu_clay_weighing"`
  - Thêm `ShowMenuClayInboundReport = "show_menu_clay_inbound_report"`
- **ClayWeighingConstants.cs** [NEW]:
  - Định nghĩa các hằng số mặc định cho mỏ sét (Mã/Tên SP: Set/Sét, Mã/Tên KH: NCC2/Nhà cung cấp 2).
  - Định nghĩa các setting keys: `ClaySingleWeighEnabled`, `ClayDefaultWeighMode`, `ClayRequireStandardTareForSingleWeigh`, `ClayStandardTareToleranceKg`, `ClayDefaultProductCode`.

### 2. Application DTOs & Services
- **StationDtos.cs** (`StationFeatureSetDto`):
  - Thêm `bool ShowMenuClayWeighing` và `bool ShowMenuClayInboundReport`.
- **StationScopeServices.cs**:
  - Thêm mapping load/save feature flags cho mỏ sét.
- **IWeighingSessionRepository.cs & WeighingSessionRepository.cs**:
  - Thêm phương thức `SearchClaySessionsAsync` để truy vấn danh sách phiên cân mỏ sét dựa trên `StationCode` tương tự trạm đập.

### 3. Business Logic (Use Cases)
- **ClayWeighingUseCases.cs** [NEW]:
  - Kế thừa toàn bộ logic từ `CrusherWeighingUseCases.cs` nhưng sử dụng các hằng số mặc định của mỏ sét và cấu hình vận hành mỏ sét (`ClayWeighingConstants`).
- **ClayInboundReport Services & UseCases** [NEW]:
  - Kế thừa logic từ Crusher report để sinh báo cáo nhập mỏ sét.

### 4. Presentation & UI
- **StationMasterViewModel.cs & StationMasterView.xaml**:
  - Thêm các checkbox quản trị hiển thị menu Cân mỏ sét và Báo cáo nhập (mỏ sét).
- **DashboardViewModel.cs & DashboardView.xaml**:
  - Thêm các thuộc tính `ClayActiveCount`, `ClayCompletedCount`, `ClayCompletedTonnage`, `ShowClayKpi`.
  - Thêm thẻ KPI cho trạm mỏ sét hiển thị số xe đang cân, đã hoàn thành và số tấn.
- **MainViewModel.cs & MainWindow.xaml**:
  - Đăng ký navigation target cho `"ClayWeighing"` và `"Reports_ClayInbound"`.
  - Bổ sung menu điều hướng tương ứng tại Sidebar.
- **ClayWeighingViewModel.cs & ClayWeighingView.xaml** [NEW]:
  - Clone hoàn toàn từ Crusher Weighing.
  - Sửa logic nút **Làm mới** (Refresh): Khởi tạo lại toàn bộ state cân, reset số cân lần 1/lần 2 (`_pendingWeight1`, `_pendingWeight2`), xóa driver name, reset StandardTareText về null, và gán lại sản phẩm/khách hàng mặc định của mỏ sét (Sét/Set, Nhà cung cấp 2/NCC2).
- **ClayInboundReportViewModel.cs & ClayInboundReportView.xaml** [NEW]:
  - Giao diện báo cáo nhập cho mỏ sét.

---

## Task Breakdown

### Phase 1: Cấu hình và Domain Constants

#### Task 1.1: Tạo hằng số và cấu hình Feature Flag
- **Agent**: `database-architect`
- **Skill**: `database-design`
- **Dependencies**: Không
- **Input**:
  - `src/StationApp.Domain/Constants/StationFeatureKeys.cs`
- **Output**:
  - `src/StationApp.Domain/Constants/ClayWeighingConstants.cs`
  - Thêm vào `StationFeatureKeys.cs`
- **Verify**: Dự án build thành công.

#### Task 1.2: Seed cơ sở dữ liệu
- **Agent**: `database-architect`
- **Skill**: `database-design`
- **Input**: `SchemaCompatibilityBootstrapper.cs`
- **Output**: Thêm default settings và features cho mỏ sét.
- **Verify**: Chạy phần mềm local, kiểm tra DB bảng `station_feature_flags` và `station_operation_settings` tự động sinh các dòng cấu hình mỏ sét.

---

### Phase 2: Application & Infrastructure (Logic nghiệp vụ)

#### Task 2.1: Cập nhật Repository và DTO
- **Agent**: `backend-specialist`
- **Skill**: `api-patterns`
- **Input**: `StationDtos.cs`, `StationScopeServices.cs`, `IWeighingSessionRepository.cs`, `WeighingSessionRepository.cs`
- **Output**: Thêm thuộc tính feature flags mới và thực thi `SearchClaySessionsAsync`.
- **Verify**: Dự án compile thành công.

#### Task 2.2: Tạo ClayWeighingUseCases
- **Agent**: `backend-specialist`
- **Skill**: `api-patterns`
- **Dependencies**: Task 2.1
- **Input**: Clone logic từ `CrusherWeighingUseCases.cs`
- **Output**: `src/StationApp.Application/UseCases/ClayWeighingUseCases.cs`
- **Verify**: Lớp được khởi tạo thành công qua constructor DI.

#### Task 2.3: Thiết lập Báo cáo nhập mỏ sét (Clay Inbound Report)
- **Agent**: `backend-specialist`
- **Skill**: `api-patterns`
- **Dependencies**: Task 2.2
- **Output**:
  - `IClayInboundReportService.cs` & `IClayInboundReportExporter.cs`
  - `ClayInboundReportDtos.cs`
  - `ClayInboundReportServices.cs`
  - `ClayInboundReportUseCases.cs`
- **Verify**: Build thành công.

---

### Phase 3: UI & ViewModels (Giao diện & Bindings)

#### Task 3.1: Quản trị cấu hình trạm
- **Agent**: `frontend-specialist`
- **Skill**: `frontend-design`
- **Input**: `StationMasterView.xaml` & `StationMasterViewModel.cs`
- **Output**: Thêm checkbox quản lý hiển thị menu mỏ sét.
- **Verify**: Mở trang quản trị trạm thấy 2 checkbox mới, bật/tắt lưu cấu hình thành công.

#### Task 3.2: Màn hình vận hành Cân mỏ sét
- **Agent**: `frontend-specialist`
- **Skill**: `frontend-design`
- **Dependencies**: Task 2.2
- **Input**: `CrusherWeighingView.xaml` & `CrusherWeighingViewModel.cs`
- **Output**:
  - `ClayWeighingViewModel.cs` (Đảm bảo logic Refresh reset hoàn toàn số cân lần 2).
  - `ClayWeighingView.xaml` và code-behind.
- **Verify**: Chức năng làm mới hoạt động đúng.

#### Task 3.3: Màn hình Báo cáo nhập mỏ sét
- **Agent**: `frontend-specialist`
- **Skill**: `frontend-design`
- **Dependencies**: Task 2.3
- **Output**:
  - `ClayInboundReportViewModel.cs`
  - `ClayInboundReportView.xaml` và code-behind.
- **Verify**: Grid hiển thị đúng cột và nút Export Excel hoạt động tốt.

#### Task 3.4: Tích hợp menu và DI
- **Agent**: `frontend-specialist`
- **Skill**: `frontend-design`
- **Dependencies**: Task 3.2, Task 3.3
- **Input**: `App.xaml.cs`, `MainWindow.xaml`, `MainViewModel.cs`
- **Output**: Đăng ký DI và thêm menu sidebar cho mỏ sét.
- **Verify**: Menu hiển thị chính xác khi chuyển đổi trạm tương ứng.

#### Task 3.5: Tích hợp Dashboard KPI cho trạm mỏ sét
- **Agent**: `frontend-specialist`
- **Skill**: `frontend-design`
- **Dependencies**: Task 3.4
- **Input**: `DashboardViewModel.cs` & `DashboardView.xaml`
  - Sử dụng chung cấu trúc hiển thị hoặc mở rộng hiển thị dựa trên trạm đang chọn.
- **Verify**: KPI hiển thị đúng số liệu mỏ sét theo ngày được chọn.

---

## Phase X: Verification (Xác minh)

### Automated Tests
- Chạy biên dịch toàn bộ solution:
  ```powershell
  dotnet build
  ```

### Manual Verification
1. Khởi chạy phần mềm local.
2. Thiết lập cơ sở dữ liệu `station_feature_flags` bật `ShowMenuClayWeighing` và `ShowMenuClayReport` cho một mã trạm cụ thể (ví dụ: `CLAY_STATION`).
3. Đăng nhập và chuyển trạm về trạm mỏ sét.
4. Kiểm tra:
   - Menu xuất hiện "Cân trạm mỏ sét" và "Báo cáo nhập mỏ sét".
   - Màn hình cân mặc định mã sản phẩm là "Set", tên là "Sét" và NCC tương tự.
   - Thử thực hiện cân lần 1, cân lần 2, lưu phiếu và kiểm tra database lưu đúng `StationCode` của trạm mỏ sét.
   - Mở màn hình báo cáo, kiểm tra dữ liệu hiển thị đúng phiếu cân vừa lưu.
   - Kiểm tra Dashboard KPI cập nhật phản hồi chính xác.
