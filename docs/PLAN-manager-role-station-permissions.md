# Plan: Bổ sung role Manager và phân quyền quản lý theo trạm

## 1. Mục tiêu

Bổ sung role `MANAGER` (Quản lý) bên cạnh `ADMIN` và `OPERATOR`, để người dùng quản lý trạm được truy cập và kiểm soát các chức năng nghiệp vụ thuộc các trạm được phân quyền.

Role Manager cần:

- Có thể được gán nhiều trạm.
- Làm việc trong phạm vi trạm hiện tại đã được gán ở `user_station_assignments`.
- Được dùng Cân tay.
- Được quản lý đầy đủ các danh mục nghiệp vụ.
- Được xem báo cáo và toàn bộ audit log của trạm đang chọn.
- Được thao tác các chức năng ảnh hưởng dữ liệu nghiệp vụ với audit log đầy đủ.
- Không có quyền quản trị hệ thống như Admin.

## 2. Kết luận chốt nghiệp vụ

Các điểm đã chốt:

1. Manager có thể được gán nhiều trạm.
2. Manager được quản lý đầy đủ danh mục nghiệp vụ.
3. Operator:
   - Với trạm `QN01`: được phép truy cập master data.
   - Với trạm `QN02` và `QN03`: không được phép truy cập master data; chỉ Manager/Admin được phép.
4. Cấu hình in/layout: Admin-only.
5. Cập nhật ứng dụng: tất cả role đều được cập nhật.
6. Manager được xem toàn bộ audit log của trạm đang chọn.

## 3. Hiện trạng đã rà soát

### 3.1 Tài liệu hiện có

- `docs/PLAN-authorization-rbac.md`: tài liệu RBAC cũ đang chốt cứng 2 role `ADMIN`, `OPERATOR`; file đang bị lỗi encoding mojibake, cần cập nhật hoặc thay thế bằng bản UTF-8 mới.
- `docs/PLAN-user-station-scope.md`: đã có thiết kế user-station assignment, chọn/đổi trạm, `CurrentStationContext`, `IStationScope`.
- `docs/PLAN-account-management.md`: có thiết kế quản lý tài khoản, nhưng role dropdown vẫn theo hướng 2 role cũ.
- `SRSdocs/StationApp_System_SRS.md`: phần phân quyền/user characteristics hiện mô tả 2 role.

### 3.2 Code hiện tại

- Role/capability tập trung ở `src/StationApp.Application/Security/StationAuthorization.cs`.
- `StationRoles.SupportedRoles` hiện chỉ có `ADMIN`, `OPERATOR`.
- `CanUseManualWeighing` hiện chỉ cho Admin.
- `CanManageAccounts`, `CanManageSystemSettings`, `CanManageDeviceConfiguration`, `CanManagePrintLayout`, `CanViewSettingsAdministration` hiện chủ yếu là Admin-only.
- Menu visibility nằm ở `src/StationApp.UI/ViewModels/MainViewModel.cs`.
- Sub-tab cấu hình nằm ở `src/StationApp.UI/ViewModels/SettingsViewModel.cs`.
- Account management hard-code role options trong `src/StationApp.UI/ViewModels/Settings/AccountManagementViewModel.cs`.
- User management use cases đang gọi `StationAuthorization.EnsureAdmin(...)`, đúng với yêu cầu giữ quản lý tài khoản Admin-only.
- Login đã có `CurrentUserContext.RoleCode` và `CurrentUserContext.StationCode`.
- Gán trạm cho user đã có trong màn quản lý tài khoản và service quản trị trạm.

## 4. Định nghĩa role

| Role | Tên hiển thị | Phạm vi |
|---|---|---|
| `ADMIN` | Quản trị hệ thống | Toàn quyền hệ thống, quản trị tài khoản, cấu hình kỹ thuật, cấu hình trạm |
| `MANAGER` | Quản lý | Quản lý nghiệp vụ trong các trạm được phân quyền |
| `OPERATOR` | Vận hành | Vận hành cân thông thường theo trạm |

## 5. Nguyên tắc phân quyền

1. Không dùng `IsAdmin` để đại diện cho Manager. Manager phải có capability riêng.
2. Quyền menu chỉ là lớp UI. Use case/service vẫn phải guard quyền.
3. Mọi quyền dữ liệu của Manager phải đi qua station scope.
4. Admin có thể nhìn/quản trị toàn hệ thống, nhưng khi vận hành theo trạm vẫn phải có station context rõ ràng.
5. Manager không được tự mở rộng phạm vi trạm hoặc quyền của mình.
6. Operator ở `QN01` giữ quyền master data theo vận hành hiện tại.
7. Operator ở `QN02`, `QN03` không được truy cập master data.
8. Các chức năng kỹ thuật, cấu hình hệ thống, cấu hình trạm, cấu hình in/layout giữ Admin-only.

## 6. Ma trận quyền cấp menu

| Menu / màn hình | Admin | Manager | Operator QN01 | Operator QN02/QN03 | Ghi chú |
|---|---:|---:|---:|---:|---|
| Trang chủ | Có | Có | Có | Có | Theo feature trạm |
| Danh sách xe vào | Có | Có | Có | Có | Theo feature trạm |
| Cân nội địa | Có | Có | Có | Có | Theo feature trạm |
| Cân mỏ đá | Có | Có | Có | Có | Theo feature trạm |
| Cân mỏ sét | Có | Có | Có | Có | Theo feature trạm |
| Cân xuất khẩu | Có | Có | Có | Có | Theo feature trạm |
| Danh sách xe ra | Có | Có | Có | Có | Theo feature trạm |
| Báo cáo xuất - NĐ | Có | Có | Có | Có | Theo feature trạm |
| Báo cáo xuất - XK | Có | Có | Có | Có | Theo feature trạm |
| Báo cáo nhập hàng/cân hàng | Có | Có | Có | Có | Theo feature trạm |
| Báo cáo cân hàng mỏ đá | Có | Có | Có | Có | Theo feature trạm |
| Báo cáo cân hàng mỏ sét | Có | Có | Có | Có | Theo feature trạm |
| Lịch sử chỉnh sửa/audit log | Có | Có | Không | Không | Manager xem toàn bộ audit log của trạm |
| Cập nhật ứng dụng | Có | Có | Có | Có | All role |
| Tham số hệ thống | Có | Không | Không | Không | Admin-only |
| Cấu hình camera | Có | Không | Không | Không | Admin-only |
| Thiết bị cân | Có | Không | Không | Không | Admin-only |
| Cấu hình in/layout | Có | Không | Không | Không | Admin-only |
| Danh mục xe | Có | Có | Có | Không | Operator chỉ QN01 |
| Khách hàng | Có | Có | Có | Không | Operator chỉ QN01 |
| Sản phẩm | Có | Có | Có | Không | Operator chỉ QN01 |
| Đồng bộ | Có | Không | Không | Không | Admin-only |
| Lịch sử cân PM cũ / External Datacan | Có | Không | Không | Không | Admin-only |
| Danh mục trạm | Có | Không | Không | Không | Admin-only |
| Quản lý tài khoản | Có | Không | Không | Không | Admin-only |
| Diagnostics | Có | Không | Không | Không | Hiện đang ẩn; nếu bật thì Admin-only |

## 7. Ma trận quyền hành vi chi tiết

### 7.1 Đăng nhập và chọn trạm

| Hành vi | Admin | Manager | Operator |
|---|---:|---:|---:|
| Đăng nhập | Có | Có | Có |
| Tự vào trạm mặc định nếu chỉ có 1 trạm | Có | Có | Có |
| Chọn trạm nếu được gán nhiều trạm | Có | Có | Có |
| Đổi trạm sau đăng nhập | Có | Có | Có nếu được gán nhiều trạm |
| Xem dữ liệu trạm chưa được gán | Không | Không | Không |
| Tự gán thêm trạm cho tài khoản | Không | Không | Không |
| Gán trạm cho tài khoản khác | Có | Không | Không |

### 7.2 Cân nội địa / Lập phiếu cân

| Hành vi | Admin | Manager | Operator |
|---|---:|---:|---:|
| Xem danh sách lượt cân | Có | Có | Có |
| Cân tự động lần 1/lần 2 | Có | Có | Có |
| Cân tay lần 1/lần 2 | Có | Có | Không |
| Nhập/sửa thông tin lượt cân theo luồng hiện có | Có | Có | Có |
| Phân bổ dòng hàng | Có | Có | Có |
| Tách tải | Có | Có | Có |
| Không tách tải | Có | Có | Có |
| Hủy session / hủy phiếu theo luồng hiện có | Có | Có | Có |
| Xem ảnh cân | Có | Có | Có |
| In phiếu / tải phiếu | Có | Có | Có |
| Đổi số xe nếu chức năng đang bật | Có | Có | Theo luồng hiện có |
| Xem audit log liên quan | Có | Có | Không |

### 7.3 Cân xuất khẩu

| Hành vi | Admin | Manager | Operator |
|---|---:|---:|---:|
| Xem danh sách cắt lệnh | Có | Có | Có |
| Tạo cắt lệnh tạm | Có | Có | Có |
| Sửa cắt lệnh tạm chưa chốt | Có | Có | Có nếu đang cho theo luồng hiện tại |
| Tạo chuyến xe | Có | Có | Có |
| Chuyển chuyến xe | Có | Có | Có |
| Xóa chuyến xe chưa cân lần 2 | Có | Có | Có |
| Cân tự động lần 1/lần 2 | Có | Có | Có |
| Cân tay lần 1/lần 2 | Có | Có | Không |
| Đánh dấu Hoàn | Có | Có | Có |
| Chốt tổng cắt lệnh | Có | Có | Có nếu đang cho theo luồng hiện tại |
| Nhập SL khác khi chốt tổng | Có | Có | Có nếu được chốt tổng |
| Gửi số liệu thực xuất ERP | Có | Có | Có nếu được chốt tổng |
| Xem ảnh | Có | Có | Có |
| In/tải phiếu nếu có | Có | Có | Có |
| Xem audit log cắt lệnh/chuyến xe | Có | Có | Không |

### 7.4 Cân mỏ đá

| Hành vi | Admin | Manager | Operator |
|---|---:|---:|---:|
| Xem danh sách chuyến xe | Có | Có | Có |
| Chọn xe nội bộ trong danh mục | Có | Có | Có |
| Tạo/lưu chuyến cân | Có | Có | Có |
| Cân tự động lần 1/lần 2 | Có | Có | Có |
| Cân tay lần 1/lần 2 | Có | Có | Không |
| Đánh dấu Hoàn/Không hoàn | Có | Có | Có |
| Áp logic giới hạn TL hoàn | Có | Có | Có |
| Xem ảnh | Có | Có | Có |
| Xem lịch sử chỉnh sửa/audit | Có | Có | Không |

### 7.5 Cân mỏ sét

| Hành vi | Admin | Manager | Operator |
|---|---:|---:|---:|
| Xem danh sách tàu/chuyến xe | Có | Có | Có |
| Tạo tàu | Có | Có | Có |
| Sửa tàu | Có | Có | Có |
| Tạo chuyến xe | Có | Có | Có |
| Chuyển chuyến xe | Có | Có | Có |
| Xóa chuyến xe chưa cân lần 2 | Có | Có | Có |
| Cân tự động lần 1/lần 2 | Có | Có | Có |
| Cân tay lần 1/lần 2 | Có | Có | Không |
| Đánh dấu Hoàn/Không hoàn | Có | Có | Có |
| Áp logic giới hạn TL hoàn trong cùng tàu | Có | Có | Có |
| Chốt tổng tàu | Có | Có | Có |
| Xem ảnh | Có | Có | Có |
| Xem lịch sử chỉnh sửa/audit | Có | Có | Không |

### 7.6 Danh sách xe vào

| Hành vi | Admin | Manager | Operator |
|---|---:|---:|---:|
| Xem danh sách xe vào | Có | Có | Có |
| Cập nhật thông tin cắt lệnh | Có | Có | Có |
| Xác nhận vào cân | Có | Có | Có |
| Không lấy hàng | Có | Có | Có |
| Tạo/cập nhật master data phát sinh theo luồng | Có | Có | Có với QN01, hạn chế với QN02/QN03 nếu đi qua màn danh mục |
| Xem audit liên quan | Có | Có | Không |

### 7.7 Danh sách xe ra

| Hành vi | Admin | Manager | Operator |
|---|---:|---:|---:|
| Xem danh sách xe ra | Có | Có | Có |
| In lại phiếu cân / phiếu giao nhận | Có | Có | Có |
| Xem ảnh cân | Có | Có | Có |
| Xem chi tiết lượt cân | Có | Có | Có |
| Xem audit liên quan | Có | Có | Không |

### 7.8 Báo cáo

| Hành vi | Admin | Manager | Operator |
|---|---:|---:|---:|
| Xem preview báo cáo | Có | Có | Có |
| In báo cáo | Có | Có | Có |
| Tải/xuất báo cáo | Có | Có | Có |
| Lọc báo cáo theo trạm hiện tại | Bắt buộc | Bắt buộc | Bắt buộc |
| Xem báo cáo trạm khác khi chưa chọn/gán trạm | Không | Không | Không |
| Xem audit log của trạm | Có | Có | Không |

### 7.9 Master data

| Hành vi | Admin | Manager | Operator QN01 | Operator QN02/QN03 |
|---|---:|---:|---:|---:|
| Xem danh mục xe | Có | Có | Có | Không |
| Thêm xe | Có | Có | Có | Không |
| Sửa xe | Có | Có | Có | Không |
| Xem khách hàng | Có | Có | Có | Không |
| Thêm khách hàng | Có | Có | Có | Không |
| Sửa khách hàng | Có | Có | Có | Không |
| Xem sản phẩm | Có | Có | Có | Không |
| Thêm sản phẩm | Có | Có | Có | Không |
| Sửa sản phẩm | Có | Có | Có | Không |
| Ghi audit khi thêm/sửa | Có | Có | Có | Không áp dụng |

### 7.10 Cấu hình hệ thống

| Hành vi | Admin | Manager | Operator |
|---|---:|---:|---:|
| Tham số hệ thống | Có | Không | Không |
| Camera | Có | Không | Không |
| Thiết bị cân | Có | Không | Không |
| Cấu hình in/layout | Có | Không | Không |
| Đồng bộ | Có | Không | Không |
| External Datacan / lịch sử cân PM cũ | Có | Không | Không |
| Danh mục trạm | Có | Không | Không |
| Quản lý tài khoản | Có | Không | Không |
| Cập nhật ứng dụng | Có | Có | Có |

### 7.11 Quản lý tài khoản

| Hành vi | Admin | Manager | Operator |
|---|---:|---:|---:|
| Xem danh sách tài khoản | Có | Không | Không |
| Tạo tài khoản Admin/Manager/Operator | Có | Không | Không |
| Sửa role | Có | Không | Không |
| Gán trạm cho tài khoản | Có | Không | Không |
| Khóa/mở tài khoản | Có | Không | Không |
| Reset mật khẩu | Có | Không | Không |
| Tự đổi role hoặc tự gán trạm | Không | Không | Không |

## 8. Quy tắc station scope cho Manager

1. Manager chỉ được thấy dữ liệu của trạm hiện tại đã được phân quyền.
2. Manager có thể được gán nhiều trạm, dùng luồng chọn/đổi trạm hiện có.
3. Khi đổi trạm, menu, dashboard, báo cáo, danh sách cân và selection phải reload.
4. Mọi truy vấn nghiệp vụ, báo cáo, audit log phải lọc theo `CurrentStationContext` hoặc `IStationScope`.
5. Mọi dữ liệu tạo/sửa bởi Manager phải ghi đúng `StationCode` của phiên hiện tại.
6. Manager không được tự gán thêm trạm cho mình hoặc người khác.
7. Background sync không được phụ thuộc vào role Manager hay session user hiện tại.

## 9. Capability cần bổ sung/điều chỉnh

Đề xuất mở rộng `StationAuthorization` theo capability sau:

| Capability | Admin | Manager | Operator | Ghi chú |
|---|---:|---:|---:|---|
| `IsManager` | Không | Có | Không | Helper mới |
| `CanUseManualWeighing` | Có | Có | Không | Thay đổi chính |
| `CanViewOperationalScreens` | Có | Có | Có | Như hiện tại + Manager |
| `CanViewReports` | Có | Có | Có | Capability mới, tách khỏi operational |
| `CanViewEditHistory` | Có | Có | Không | Audit log theo trạm |
| `CanViewMasterData(stationCode)` | Có | Có | Có nếu QN01 | QN02/QN03 Operator = Không |
| `CanManageMasterData(stationCode)` | Có | Có | Có nếu QN01 | QN02/QN03 Operator = Không |
| `CanManageAccounts` | Có | Không | Không | Giữ Admin-only |
| `CanManageSystemSettings` | Có | Không | Không | Giữ Admin-only |
| `CanManageDeviceConfiguration` | Có | Không | Không | Giữ Admin-only |
| `CanManagePrintLayout` | Có | Không | Không | Đã chốt Admin-only |
| `CanViewSettingsAdministration` | Có | Không | Không | Admin-only |
| `CanManageStations` | Có | Không | Không | Helper mới nên có |
| `CanUpdateApplication` | Có | Có | Có | All role |
| `CanViewDiagnostics` | Có | Không | Không | Nếu bật menu diagnostics |

## 10. Quy tắc audit log

Manager được xem toàn bộ audit log của trạm đang chọn.

Các thao tác cần audit rõ khi Admin/Manager/Operator thực hiện:

- Cân tay: số cân nhập tay, lượt cân, xe, trạm, thời gian, user, role.
- Sửa cắt lệnh tạm.
- Tạo/sửa/chốt tàu mỏ sét.
- Tạo/chuyển/xóa chuyến xe xuất khẩu hoặc mỏ sét.
- Đánh dấu Hoàn/Không hoàn.
- Chốt tổng, nhập SL khác, gửi số liệu thực xuất ERP.
- Sửa thông tin cắt lệnh ở Danh sách xe vào.
- Không lấy hàng.
- Đổi số xe nếu màn nào còn dùng chức năng này.
- Thêm/sửa danh mục xe, khách hàng, sản phẩm.
- Cập nhật app nên ghi log mức vận hành nếu hiện đã có cơ chế log phù hợp.

Audit log tối thiểu cần có: `Actor`, `RoleCode`, `StationCode`, `Action`, `EntityName`, `EntityId`, dữ liệu trước/sau hoặc payload đủ để hậu kiểm.

## 11. Task triển khai

### Task 1: Chuẩn hóa role constants

**Mô tả:** Thêm role `MANAGER` vào hệ thống role.

**Acceptance criteria:**
- `StationRoles.Manager = "MANAGER"`.
- `StationRoles.SupportedRoles` có `MANAGER`.
- Có `StationAuthorization.IsManager`.
- Validation tạo/sửa tài khoản chấp nhận `MANAGER`.

**Files likely touched:**
- `src/StationApp.Application/Security/StationAuthorization.cs`
- `src/StationApp.Application/UseCases/UserManagementUseCases.cs`

### Task 2: Thiết kế lại capability trong `StationAuthorization`

**Mô tả:** Tách capability theo bảng ở mục 9.

**Acceptance criteria:**
- Không dùng `IsAdmin || IsManager` tràn lan ngoài helper.
- `CanUseManualWeighing` cho Admin/Manager.
- `CanManagePrintLayout` vẫn chỉ Admin.
- `CanUpdateApplication` cho Admin/Manager/Operator.
- Có capability master data xét theo `stationCode`.
- Có capability audit/edit history cho Admin/Manager.

**Files likely touched:**
- `src/StationApp.Application/Security/StationAuthorization.cs`

### Task 3: Cập nhật Account Management

**Mô tả:** Cho Admin tạo/sửa/lọc role Manager.

**Acceptance criteria:**
- Dropdown Role có `ADMIN`, `MANAGER`, `OPERATOR`.
- Search role có `Tất cả`, `ADMIN`, `MANAGER`, `OPERATOR`.
- Manager vẫn không thấy và không gọi được màn/use case quản lý tài khoản.
- Gán nhiều trạm cho Manager hoạt động như user khác.
- Rule còn ít nhất 1 Admin active giữ nguyên.

**Files likely touched:**
- `src/StationApp.UI/ViewModels/Settings/AccountManagementViewModel.cs`
- `src/StationApp.UI/Views/Settings/AccountManagementView.xaml`
- `src/StationApp.Application/UseCases/UserManagementUseCases.cs`

### Task 4: Cập nhật menu/navigation

**Mô tả:** Áp quyền Manager và rule master data theo trạm vào menu.

**Acceptance criteria:**
- Manager thấy các màn vận hành, báo cáo, master data, lịch sử chỉnh sửa, cập nhật app.
- Manager không thấy tham số hệ thống, camera, thiết bị cân, cấu hình in, đồng bộ, External Datacan, danh mục trạm, quản lý tài khoản.
- Operator QN01 thấy master data.
- Operator QN02/QN03 không thấy master data.
- Operator không thấy lịch sử chỉnh sửa.
- Điều hướng trực tiếp bằng destination key vẫn bị chặn đúng quyền.

**Files likely touched:**
- `src/StationApp.UI/ViewModels/MainViewModel.cs`
- `src/StationApp.UI/ViewModels/SettingsViewModel.cs`
- `src/StationApp.UI/Views/MainWindow.xaml`

### Task 5: Mở Cân tay cho Manager

**Mô tả:** Manager được dùng manual weighing ở các màn cân.

**Acceptance criteria:**
- UI Cân tay hiển thị với Admin/Manager.
- UI Cân tay ẩn với Operator.
- Use case lưu cân tay cho Manager không bị chặn.
- Nếu Operator cố gọi use case manual bằng đường khác thì vẫn bị từ chối.
- Audit log ghi nhận user/role/mode manual.

**Files likely touched:**
- `src/StationApp.Application/UseCases/CaptureSessionWeight1UseCase.cs`
- `src/StationApp.Application/UseCases/CaptureSessionWeight2UseCase.cs`
- `src/StationApp.UI/ViewModels/WeighingViewModel.cs`
- `src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs`
- `src/StationApp.UI/ViewModels/CrusherWeighingViewModel.cs`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

### Task 6: Áp rule master data theo trạm

**Mô tả:** Master data không chỉ check role mà còn check station.

**Acceptance criteria:**
- Admin/Manager quản lý đầy đủ master data ở mọi trạm được chọn.
- Operator QN01 được truy cập và quản lý master data.
- Operator QN02/QN03 không truy cập được master data qua menu, tab, command hoặc điều hướng trực tiếp.
- Nếu sau này có trạm khác, cần default rõ: Operator không được master data trừ khi được whitelist.

**Files likely touched:**
- `src/StationApp.Application/Security/StationAuthorization.cs`
- `src/StationApp.UI/ViewModels/MainViewModel.cs`
- `src/StationApp.UI/ViewModels/SettingsViewModel.cs`
- ViewModel danh mục nếu cần command-level guard.

### Task 7: Guard các chức năng Admin-only

**Mô tả:** Đảm bảo Manager không lọt vào cấu hình kỹ thuật.

**Acceptance criteria:**
- System settings, camera, scale device, print config, sync, External Datacan, station master, account management đều Admin-only ở UI và command/use case.
- Manager/Operator gọi trực tiếp command vẫn không lưu được nếu thiếu quyền.

**Files likely touched:**
- `src/StationApp.UI/ViewModels/Settings/SystemSettingsViewModel.cs`
- `src/StationApp.UI/ViewModels/Settings/CameraConfigViewModel.cs`
- `src/StationApp.UI/ViewModels/Settings/ScaleDeviceConfigViewModel.cs`
- `src/StationApp.UI/ViewModels/Settings/PrintConfigViewModel.cs`
- `src/StationApp.UI/ViewModels/Settings/StationMasterViewModel.cs`
- `src/StationApp.Application/UseCases/UserManagementUseCases.cs`

### Task 8: Rà audit log và quyền xem lịch sử chỉnh sửa

**Mô tả:** Manager xem toàn bộ audit log của trạm, Operator không xem.

**Acceptance criteria:**
- Menu Lịch sử chỉnh sửa hiển thị cho Admin/Manager.
- Query audit log lọc theo station hiện tại với Manager.
- Admin có thể xem theo station context hiện tại; nếu cần xem toàn hệ thống thì làm chức năng riêng sau.
- Các thao tác nghiệp vụ chính ghi audit đầy đủ actor/role/station.

**Files likely touched:**
- ViewModel/service lịch sử chỉnh sửa.
- `IAuditService` và các call site.
- Repository audit log nếu thiếu filter station.

### Task 9: Cập nhật tài liệu RBAC/SRS

**Mô tả:** Cập nhật tài liệu cũ để không còn mâu thuẫn 2 role.

**Acceptance criteria:**
- `docs/PLAN-authorization-rbac.md` được sửa thành UTF-8 đọc được hoặc thay bằng tài liệu mới.
- SRS mô tả 3 role.
- Tài liệu nêu rõ rule Operator QN01 được master data, QN02/QN03 không được.
- Tài liệu nêu rõ cấu hình in Admin-only và cập nhật app all role.

**Files likely touched:**
- `docs/PLAN-authorization-rbac.md`
- `SRSdocs/StationApp_System_SRS.md`

### Task 10: Test phân quyền theo 3 role

**Mô tả:** Test đủ role, station scope, menu và guard.

**Acceptance criteria:**
- Admin: toàn quyền như hiện tại.
- Manager nhiều trạm: chọn/đổi trạm, thấy dữ liệu đúng trạm.
- Manager: dùng được Cân tay, master data, báo cáo, audit log.
- Manager: không vào được cấu hình kỹ thuật/Admin-only.
- Operator QN01: vào được master data, không dùng Cân tay, không xem audit log.
- Operator QN02/QN03: không vào được master data, không dùng Cân tay, không xem audit log.
- Tất cả role cập nhật app được.
- Build pass.

**Verification:**
- `dotnet build`
- Test thủ công ít nhất 4 tài khoản: Admin, Manager nhiều trạm, Operator QN01, Operator QN02 hoặc QN03.

## 12. Rủi ro và cách xử lý

| Rủi ro | Mức độ | Cách xử lý |
|---|---|---|
| Mở quyền Manager quá rộng do dùng lại `IsAdmin` | Cao | Tách capability rõ, giữ Admin-only cho cấu hình kỹ thuật |
| Chỉ ẩn menu nhưng use case vẫn gọi được | Cao | Guard ở command/use case với action nhạy cảm |
| Manager xem dữ liệu trạm khác | Cao | Bắt buộc lọc theo `IStationScope`/`CurrentStationContext` |
| Operator QN02/QN03 lọt vào master data qua tab trực tiếp | Cao | Check cả menu, `CanAccessTab`, command và navigation destination |
| Audit log thiếu station/role | Trung bình | Chuẩn hóa payload audit cho action nghiệp vụ |
| Tài liệu cũ bị mojibake gây hiểu sai | Trung bình | Cập nhật tài liệu RBAC/SRS bằng UTF-8 |
| Cập nhật app all role nhưng UI đang nằm trong Settings | Trung bình | Đảm bảo Settings menu vẫn hiện nếu user chỉ có quyền AppUpdate |

## 13. Thứ tự triển khai đề xuất

1. Cập nhật role/capability trong `StationAuthorization`.
2. Cập nhật Account Management để tạo/sửa/lọc Manager.
3. Cập nhật menu/navigation và Settings tab theo role + station.
4. Mở Cân tay cho Manager ở UI và use case.
5. Áp rule master data theo trạm.
6. Rà guard Admin-only.
7. Rà audit log và lịch sử chỉnh sửa theo trạm.
8. Cập nhật tài liệu RBAC/SRS.
9. Build và test theo 3 role + các trạm QN01/QN02/QN03.

## 14. Definition of Done

- Có thể tạo tài khoản `MANAGER`.
- Manager có thể được gán nhiều trạm và đổi trạm đúng.
- Manager dùng được Cân tay.
- Manager quản lý đầy đủ master data trong trạm được phân quyền.
- Manager xem được báo cáo và toàn bộ audit log của trạm.
- Manager không vào được cấu hình in/layout, tham số hệ thống, camera, thiết bị cân, đồng bộ, danh mục trạm, quản lý tài khoản.
- Operator QN01 vẫn truy cập master data được.
- Operator QN02/QN03 không truy cập master data được.
- Operator không dùng được Cân tay và không xem audit log.
- Tất cả role đều cập nhật app được.
- Các thao tác ảnh hưởng dữ liệu nghiệp vụ có audit log.
- Tài liệu phân quyền không còn chốt 2 role.
- Build pass.
