# Plan: Quản lý xe nhập mồi cho luồng tạo xe nhập hàng

## 1. Mục tiêu

Thêm chức năng cấu hình "xe nhập mồi" để người dùng quản lý các mẫu tạo xe nhập hàng thường dùng. Các bản ghi xe nhập mồi luôn hiển thị ở đầu grid màn Danh sách xe vào, text màu xám, không phải cắt lệnh/lượt xe thật. Khi chọn một xe nhập mồi, hệ thống chỉ nạp sẵn thông tin đã cấu hình vào form tạo xe nhập hàng; sau khi tạo xe nhập hàng xong, bản ghi mồi vẫn được giữ nguyên để dùng tiếp.

## 2. Ý hiểu nghiệp vụ

- Xe nhập mồi là mẫu cấu hình, không phải dữ liệu vận hành.
- Chỉ áp dụng cho trạm QN01 (NMC).
- Một xe nhập mồi hiện gồm:
  - Loại: cố định là Hàng nhập.
  - Sản phẩm: chọn từ Danh mục sản phẩm.
  - Khách hàng: chọn từ Danh mục khách hàng.
- Khi hiển thị ở Danh sách xe vào:
  - Luôn nằm trên các xe/cắt lệnh thật.
  - Dòng hiển thị màu xám để phân biệt.
  - Không được tính là xe đã vào bãi, không được thống kê, không được sync như cut order.
- Khi người dùng chọn dòng mồi:
  - Form chuyển sang chế độ tạo xe nhập.
  - Tự điền Loại = Nhập hàng, Sản phẩm, Khách hàng.
  - Người dùng nhập thêm biển số, mooc, tài xế, SL đặt, số bao, TTCP, đăng kiểm, ghi chú... như luồng tạo xe nhập hiện tại.
- Khi bấm Tạo xe nhập:
  - Tạo một `CutOrder` thật mới như hiện tại.
  - Bản ghi xe nhập mồi không bị xóa/ẩn/cập nhật trạng thái.

## 3. Quyết định thiết kế

- Tạo bảng riêng, dự kiến `incoming_seed_vehicles`, thay vì lưu JSON trong `app_config`, vì cần thêm/sửa/xóa, audit, liên kết sản phẩm/khách hàng rõ ràng.
- Không dùng bảng `cut_orders` để lưu mẫu, tránh mẫu bị lẫn vào báo cáo, sync, cân, không lấy hàng, danh sách xe ra.
- Tạo màn cấu hình riêng trong nhóm Cấu hình hệ thống, tên gợi ý: `Xe nhập mồi`.
- Tất cả role đều được thêm/sửa/xóa xe nhập mồi.
- `StationCode` của xe nhập mồi mặc định và cố định là `QN01`.

## 4. Thiết kế dữ liệu

### Bảng `incoming_seed_vehicles`

Các cột đề xuất:

- `Id uniqueidentifier`
- `StationCode nvarchar(50)` mặc định `QN01`.
- `TransactionType nvarchar(30)` mặc định `INBOUND`.
- `ProductCode nvarchar(50)`
- `ProductName nvarchar(255)`
- `CustomerCode nvarchar(50)`
- `CustomerName nvarchar(255)`
- `SortOrder int` để sắp thứ tự mẫu.
- `IsActive bit` để xóa mềm.
- `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `DeletedAt`, `DeletedBy`.

Index:

- `IX_incoming_seed_vehicles_station_active_sort` trên `StationCode, IsActive, SortOrder`.
- Unique mềm tùy chọn trên `StationCode, ProductCode, CustomerCode, IsActive` để tránh trùng mẫu cùng sản phẩm/khách hàng.

## 5. DTO và interface

Thêm DTO:

- `IncomingSeedVehicleDto`
- `CreateIncomingSeedVehicleRequest`
- `UpdateIncomingSeedVehicleRequest`
- `IncomingSeedVehicleListItem`

Thêm repository/interface:

- `IIncomingSeedVehicleRepository`
- `GetActiveForQn01Async(ct)` hoặc `GetActiveAsync(ct)` với `StationCode = QN01` cố định trong implementation.
  - `GetByIdAsync(id, ct)`
  - `AddAsync(entity, ct)`
  - `UpdateAsync(entity, ct)`
  - `SoftDeleteAsync(entity, ct)`

Thêm use case:

- `GetIncomingSeedVehiclesUseCase`
- `CreateIncomingSeedVehicleUseCase`
- `UpdateIncomingSeedVehicleUseCase`
- `DeleteIncomingSeedVehicleUseCase`

## 6. Màn cấu hình xe nhập mồi

Tạo màn mới trong Cấu hình hệ thống.

Trường nhập:

- Loại: hiển thị `Hàng nhập`, readonly.
- Sản phẩm: dropdown/autocomplete từ danh mục sản phẩm.
- Khách hàng: dropdown/autocomplete từ danh mục khách hàng.
- Thứ tự hiển thị: số nguyên, mặc định tự tăng.

Grid cấu hình:

- STT
- Loại
- Khách hàng
- Sản phẩm
- Thứ tự
- Trạng thái
- Người cập nhật
- Thời gian cập nhật

Nút:

- Thêm mới
- Lưu thay đổi
- Xóa/ngừng sử dụng
- Làm mới

Audit log:

- `CREATE_INCOMING_SEED_VEHICLE`
- `UPDATE_INCOMING_SEED_VEHICLE`
- `DELETE_INCOMING_SEED_VEHICLE`

## 7. Tích hợp vào màn Danh sách xe vào

### ViewModel

Hiện `IncomingVehicleListViewModel.ReloadVehiclesAsync` lấy danh sách xe/cắt lệnh thật từ `ICutOrderRepository.GetIncomingListAsync`.

Cần đổi thành:

1. Load danh sách xe nhập mồi active theo trạm.
2. Load danh sách xe vào thật như hiện tại.
3. Merge thành `Vehicles`:
   - Các mẫu mồi đứng đầu.
   - Sau đó là xe thật.

Mở rộng row model `IncomingVehicleSelectionItem`:

- `bool IsSeedVehicle`
- `Guid? SeedVehicleId`
- `bool CanSelectForWeighing`
- `bool IsGreyRow`

### Hành vi chọn dòng mồi

Khi `SelectedVehicle.IsSeedVehicle = true`:

- Gọi hàm `ApplySeedVehicleToCreateForm`.
- Chuyển `IsCreateMode = true`.
- Set:
  - `FormTransactionType = INBOUND`
  - `FormCustomerCode/Name`
  - `FormProductCode/Name`
  - `FormProductType` theo product master
  - `FormTransportMethod = ROAD` như mặc định hiện tại, nếu không có yêu cầu khác.
- Clear các trường vận hành:
  - Biển số
  - Mooc
  - Tài xế
  - SL đặt
  - Số bao
  - TTCP/đăng kiểm
  - Ghi chú
  - Lượt cân gắn kèm

### Hành vi với nút chức năng

Với dòng mồi:

- Không cho tích checkbox để cân hàng loạt.
- Không cho bấm Cân nội địa/Cân xuất khẩu trực tiếp.
- Không cho bấm Không lấy hàng.
- Chỉ cho chọn dòng để nạp form tạo xe nhập.

Sau khi tạo xe nhập thành công:

- Reload grid.
- Dòng mồi vẫn còn trên đầu.
- Dòng xe nhập thật vừa tạo xuất hiện bên dưới theo danh sách thật.

### Hiển thị màu xám

Thêm style row trong `IncomingVehicleListView.xaml`:

- Nếu `IsSeedVehicle = true`, set `Foreground = #7F8C8D` hoặc màu xám tương tự.
- Set thêm `FontStyle = Italic` cho dòng xe nhập mồi.

## 8. Validation

Ở màn cấu hình:

- Bắt buộc chọn sản phẩm.
- Bắt buộc chọn khách hàng.
- Loại luôn là Hàng nhập, không cho sửa sang Xuất hàng.
- Không cho tạo trùng cùng `StationCode + ProductCode + CustomerCode` nếu bản ghi active đã tồn tại.
- `StationCode` luôn là `QN01`, không hiển thị lựa chọn trạm.

Ở màn Danh sách xe vào:

- Chọn mẫu không tự tạo `CutOrder`.
- Chỉ tạo `CutOrder` khi người dùng nhập đủ dữ liệu bắt buộc và bấm Tạo xe nhập.
- Biển số/mooc vẫn uppercase theo logic mới đã thêm.

## 9. Tác động tới báo cáo, sync, cân

- Xe nhập mồi chỉ áp dụng cho QN01 và không nằm trong `cut_orders`, nên không ảnh hưởng:
  - Báo cáo nhập/xuất.
  - Danh sách xe ra.
  - Cân nội địa.
  - Cân xuất khẩu.
  - Không lấy hàng.
  - Sync cut order với central/ERP.
- Khi từ mẫu tạo ra xe nhập thật, bản ghi thật đi theo luồng `CreateInboundRegistrationUseCase` hiện tại, nên vẫn sync/audit/master data như bình thường.

## 10. Task triển khai

### Task 1: Thêm schema và entity xe nhập mồi

Acceptance criteria:

- Có entity `IncomingSeedVehicle`.
- Có `DbSet` và EF configuration.
- Có schema bootstrap/migration tạo bảng khi app chạy.
- Build pass.

Files dự kiến:

- `src/StationApp.Domain/Entities/IncomingSeedVehicle.cs`
- `src/StationApp.Infrastructure/Persistence/StationDbContext.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/IncomingSeedVehicleEntityConfiguration.cs`
- `src/StationApp.Infrastructure/Persistence/SchemaCompatibilityBootstrapper.cs`

### Task 2: Thêm repository và use case CRUD

Acceptance criteria:

- Lấy được danh sách mẫu active theo trạm.
- Thêm/sửa/xóa mềm được mẫu.
- Có audit log cho thêm/sửa/xóa.
- Validate trùng sản phẩm/khách hàng trong cùng trạm.

Files dự kiến:

- `src/StationApp.Application/Interfaces/IIncomingSeedVehicleRepository.cs`
- `src/StationApp.Infrastructure/Repositories/IncomingSeedVehicleRepository.cs`
- `src/StationApp.Application/UseCases/IncomingSeedVehicleUseCases.cs`
- `src/StationApp.Application/DTOs/Dtos.cs` hoặc DTO file riêng.

### Task 3: Tạo màn Cấu hình xe nhập mồi

Acceptance criteria:

- Có màn quản lý trong nhóm Cấu hình hệ thống.
- Chọn sản phẩm/khách hàng từ danh mục hiện có.
- Thêm/sửa/xóa mềm mẫu được.
- Role không có quyền quản lý thì không thao tác được.
- Tất cả role đều thêm/sửa/xóa được.

Files dự kiến:

- `src/StationApp.UI/Views/Settings/IncomingSeedVehicleConfigView.xaml`
- `src/StationApp.UI/ViewModels/Settings/IncomingSeedVehicleConfigViewModel.cs`
- `src/StationApp.UI/ViewModels/MainViewModel.cs`
- `src/StationApp.UI/Views/MainWindow.xaml` hoặc resource/menu tương ứng.

### Task 4: Merge mẫu vào grid Danh sách xe vào

Acceptance criteria:

- Mẫu hiển thị đầu grid khi trạm hiện tại là QN01.
- Nếu không phải QN01 thì không load/không hiển thị xe nhập mồi.
- Mẫu có text màu xám.
- Mẫu có font italic.
- Mẫu không làm mất các dòng xe thật.
- Reload/tìm kiếm vẫn hoạt động ổn.

Files dự kiến:

- `src/StationApp.UI/ViewModels/IncomingVehicleListViewModel.cs`
- `src/StationApp.UI/Views/IncomingVehicleListView.xaml`
- DTO/list item liên quan trong Application.

### Task 5: Chọn mẫu để tạo xe nhập thật

Acceptance criteria:

- Chọn dòng mẫu sẽ nạp sẵn Loại, Khách hàng, Sản phẩm vào form.
- Các trường vận hành được để trống để người dùng nhập tiếp.
- Bấm Tạo xe nhập tạo ra `CutOrder` thật.
- Sau khi tạo, mẫu vẫn còn trên đầu danh sách.

Files dự kiến:

- `src/StationApp.UI/ViewModels/IncomingVehicleListViewModel.cs`
- Có thể cần cập nhật `IncomingVehicleSelectionItem`.

### Task 6: Khóa các action không hợp lệ với dòng mồi

Acceptance criteria:

- Không chọn checkbox cho dòng mồi.
- Không Cân nội địa/Cân xuất khẩu/Không lấy hàng trực tiếp từ dòng mồi.
- Không làm bẩn `SelectedItem` hoặc trạng thái form khi chuyển giữa dòng mồi và dòng thật.

Files dự kiến:

- `src/StationApp.UI/ViewModels/IncomingVehicleListViewModel.cs`
- `src/StationApp.UI/Views/IncomingVehicleListView.xaml`

### Task 7: Test và rà soát

Acceptance criteria:

- Unit test cho use case CRUD/validation.
- Test ViewModel cho chọn mẫu tạo form.
- Build pass.
- Kiểm thử tay:
  - Thêm mẫu.
  - Chọn mẫu.
  - Tạo xe nhập thật.
  - Mẫu vẫn còn.
  - Xe thật xuất hiện và cân được.

Command kiểm tra:

```powershell
dotnet build src\StationApp.UI\StationApp.UI.csproj -p:NodeReuse=false
dotnet test tests\StationApp.Application.Tests\StationApp.Application.Tests.csproj --no-build
```

## 11. Rủi ro và cách xử lý

| Rủi ro | Ảnh hưởng | Cách xử lý |
| --- | --- | --- |
| Lẫn mẫu mồi với cắt lệnh thật | Báo cáo/sync/cân sai | Dùng bảng riêng, không insert vào `cut_orders` cho tới khi người dùng bấm Tạo xe nhập |
| Người dùng tưởng dòng mồi là xe thật | Thao tác nhầm | Text màu xám, khóa checkbox/action cân, chọn chỉ nạp form |
| Trùng mẫu quá nhiều | Grid đầu danh sách rối | Validate trùng theo sản phẩm/khách hàng/trạm, có `SortOrder` |
| Mẫu lẫn sang trạm khác | Sai luồng QN02/QN03 | Cố định `StationCode = QN01`, chỉ load mẫu khi current station là QN01 |
| Product/customer bị xóa/ngừng dùng | Mẫu không tạo được xe | Ẩn luôn mẫu có product/customer không còn active |

## 12. Quyết định đã chốt

1. Không cần trường `Tên mẫu`/`DisplayName`.
2. Nếu sản phẩm/khách hàng trong mẫu bị ngừng sử dụng thì ẩn mẫu luôn.
3. Không cấu hình sẵn `SL đặt`, `Số bao`, `Ghi chú`; chỉ cấu hình Loại, Sản phẩm, Khách hàng.
4. Chỉ tạo/hiển thị xe nhập mồi cho trạm QN01 (NMC).
5. Tất cả role đều được thêm/sửa/xóa xe nhập mồi.
