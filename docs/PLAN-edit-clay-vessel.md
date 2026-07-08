# Kế hoạch: Sửa tàu ở màn Cân mỏ sét

## Mục tiêu

Thêm chức năng **Sửa tàu** cho màn **Cân mỏ sét**, tương tự chức năng **Sửa cắt lệnh tạm** ở Cân xuất khẩu:

- Chỉ hiện nút khi người dùng đã chọn một tàu.
- Chỉ cho sửa khi tàu chưa chốt tổng, chưa bị hủy/xóa.
- Cho phép sửa thông tin tàu dù đã có chuyến xe phát sinh.
- Khi sửa thông tin đơn vị vận chuyển/hàng hóa/ghi chú, dữ liệu snapshot trên các chuyến xe đã gắn với tàu phải được cập nhật tương ứng để grid, báo cáo và chốt tổng không lệch.
- Ghi audit log để xem lại ở màn **Lịch sử chỉnh sửa**.

## Hiện trạng code

### UI và ViewModel

File chính:

- `src/StationApp.UI/Views/ClayWeighingView.xaml`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`
- `src/StationApp.UI/ViewModels/Dialogs/CreateClayVesselDialogViewModel.cs`

Hiện đã có:

- Nút `TẠO TÀU`.
- Grid `Vessels`, selected item là `SelectedVessel`.
- Dialog `CreateClayVesselDialogViewModel` để nhập:
  - Tàu/Sà lan.
  - Mã đơn vị vận chuyển.
  - Đơn vị vận chuyển.
  - Mã hàng.
  - Hàng hóa.
  - Ghi chú.
- Command `CreateClayVesselCommand`.
- Các command liên quan chuyến xe:
  - `CreateTripCommand`.
  - `TransferTripCommand`.
  - `DeleteTripCommand`.
  - `ViewImageHistoryCommand`.
  - `FinalizeClayVesselCommand`.
  - `ToggleReturnedBrokenTripCommand`.

### Application layer

File chính:

- `src/StationApp.Application/DTOs/Dtos.cs`
- `src/StationApp.Application/UseCases/ClayVesselFlowUseCases.cs`
- `src/StationApp.Application/Interfaces/ICutOrderRepository.cs`

Hiện đã có:

- `CreateClayVesselRequest`.
- `CreateClayTemporaryCutOrderUseCase`.
- `ClayVesselListItem`.
- `ClayVehicleTripListItem`.
- `GetClayVesselsAsync`.
- `GetClayVehicleTripsAsync`.

Tàu mỏ sét đang lưu bằng `CutOrder` với đặc điểm:

- `CutOrderSource = MANUAL`.
- `TransactionType = INBOUND`.
- `TransportMethod = WATERWAY`.
- `IsExportScale = false`.
- `VehiclePlate = tên tàu/sà lan`.
- `CustomerCode/CustomerName = đơn vị vận chuyển`.
- `ProductCode/ProductName = hàng hóa`.

### Điểm cần lưu ý

- `CreateClayVesselDialogViewModel` hiện đang có một số chuỗi tiếng Việt bị mojibake. Khi đụng vào dialog này nên sửa luôn các label/validation liên quan sang UTF-8 đúng.
- Khi tạo chuyến xe, `WeighingSession` và `WeighingSessionLine` đang snapshot `CustomerCode`, `CustomerName`, `ProductCode`, `ProductName` từ tàu.
- Khi sửa tàu sau khi đã có chuyến, cần cập nhật lại snapshot ở:
  - `WeighingSession`.
  - `WeighingSessionLine`.
- Mỏ sét hiện không có phiếu cân/phiếu giao nhận như xuất khẩu, nên không cần cập nhật `WeighTicket`/`DeliveryTicket` trừ khi rà code thấy mỏ sét có phát sinh ticket.

## Quyết định thiết kế

- Tái dùng dialog tạo tàu cho edit mode, giống cách làm với cắt lệnh tạm xuất khẩu.
- Thêm request/use case riêng: `UpdateClayVesselRequest` và `UpdateClayVesselUseCase`.
- Không cho sửa tàu đã chốt tổng hoặc đã xóa/hủy.
- Cho sửa tàu đã có chuyến xe, nhưng phải cập nhật snapshot các chuyến đang thuộc tàu đó.
- Không đổi `CutOrderId`; chỉ cập nhật dữ liệu trên cùng bản ghi `CutOrder`.
- Sau khi sửa xong, reload lại danh sách tàu và chuyến, giữ selected tàu hiện tại.
- Ghi audit log action `UPDATE_CLAY_VESSEL`.

## Phạm vi chỉnh sửa

Trong scope:

- Thêm nút **Sửa tàu** ở màn Cân mỏ sét.
- Tái dùng modal tạo tàu ở chế độ sửa.
- Cập nhật thông tin tàu và snapshot chuyến xe.
- Ghi audit log và hiển thị được ở Lịch sử chỉnh sửa.
- Build/test liên quan.

Ngoài scope:

- Không thay đổi nghiệp vụ chốt tổng.
- Không thay đổi logic tạo/chuyển/xóa chuyến xe.
- Không thêm sửa báo cáo mỏ sét trong lượt này, trừ khi cần vì snapshot đổi tên hàng/đơn vị vận chuyển.
- Không cho sửa tàu đã chốt tổng.

## Task List

### Task 1: Bổ sung contract update tàu

**Mô tả:** Thêm DTO request phục vụ update tàu mỏ sét.

**Acceptance criteria:**

- [ ] Có `UpdateClayVesselRequest`.
- [ ] Request chứa `CutOrderId`, `VesselName`, `CustomerCode`, `CustomerName`, `ProductCode`, `ProductName`, `Notes`.
- [ ] Không ảnh hưởng contract tạo tàu hiện tại.

**Files likely touched:**

- `src/StationApp.Application/DTOs/Dtos.cs`

**Verification:**

- [ ] Build `StationApp.Application` pass.

### Task 2: Thêm use case cập nhật tàu

**Mô tả:** Tạo use case cập nhật `CutOrder` tàu mỏ sét, validate trạng thái và cập nhật master data nếu cần.

**Acceptance criteria:**

- [ ] Không tìm thấy tàu thì báo lỗi rõ.
- [ ] Chỉ cho sửa `CutOrder` thuộc luồng mỏ sét:
  - `TransactionType = INBOUND`.
  - `TransportMethod = WATERWAY`.
  - `IsExportScale = false`.
- [ ] Không cho sửa nếu tàu đã chốt tổng, bị xóa hoặc bị hủy.
- [ ] Cập nhật:
  - `VehiclePlate`.
  - `CustomerCode`.
  - `CustomerName`.
  - `ProductCode`.
  - `ProductName`.
  - `Notes`.
  - `UpdatedAt`, `UpdatedBy`, `SyncStatus`.
- [ ] Gọi `EnsureInboundMasterDataUseCase` để đảm bảo master customer/product như luồng tạo tàu.

**Files likely touched:**

- `src/StationApp.Application/UseCases/ClayVesselFlowUseCases.cs`

**Verification:**

- [ ] Unit/build compile pass.

### Task 3: Cập nhật snapshot các chuyến xe thuộc tàu

**Mô tả:** Khi sửa tàu đã có chuyến xe, cập nhật snapshot thông tin vận chuyển/hàng hóa xuống session và line để grid/report/chốt tổng dùng dữ liệu mới.

**Acceptance criteria:**

- [ ] Lấy các chuyến xe thuộc tàu qua `GetClayVehicleTripsAsync` hoặc repository/session line hiện có.
- [ ] Cập nhật `WeighingSession`:
  - `ProductCode`.
  - `ProductName`.
  - `CustomerCode`.
  - `CustomerName`.
  - `SyncStatus`, `LastSyncAttemptAt`, `LastSyncError`, `UpdatedAt`, `UpdatedBy`.
- [ ] Cập nhật `WeighingSessionLine`:
  - `CustomerCode`.
  - `CustomerName`.
  - `DistributorCode`.
  - `DistributorName`.
  - `ProductCode`.
  - `ProductName`.
  - `SyncStatus`, `LastSyncAttemptAt`, `LastSyncError`, `UpdatedAt`, `UpdatedBy`.
- [ ] Không thay đổi cân lần 1, cân lần 2, TL hàng, trạng thái chuyến.
- [ ] Không thay đổi `CutOrderId` của line.

**Files likely touched:**

- `src/StationApp.Application/UseCases/ClayVesselFlowUseCases.cs`
- Có thể cần dùng `IWeighingSessionRepository`.

**Verification:**

- [ ] Sửa tàu có chuyến hoàn thành, grid chuyến hiển thị hàng hóa/đơn vị mới.
- [ ] Lũy kế tàu không đổi sai số lượng.

### Task 4: Ghi audit log

**Mô tả:** Ghi lại thao tác sửa tàu, gồm dữ liệu trước/sau và số lượng snapshot đã cập nhật.

**Acceptance criteria:**

- [ ] Ghi action `UPDATE_CLAY_VESSEL`.
- [ ] `EntityType` nên là `CutOrder`.
- [ ] `EntityId` là `CutOrderId` của tàu.
- [ ] Detail JSON có:
  - `VesselName`.
  - `Old`.
  - `New`.
  - `UpdatedSessionCount`.
  - `UpdatedLineCount`.
- [ ] Log được lưu cùng station hiện tại.

**Files likely touched:**

- `src/StationApp.Application/UseCases/ClayVesselFlowUseCases.cs`

**Verification:**

- [ ] Sau khi sửa tàu, có bản ghi audit log trong DB.

### Task 5: Tái dùng dialog tạo tàu cho edit mode

**Mô tả:** Mở `CreateClayVesselDialogViewModel` ở chế độ sửa, nạp sẵn thông tin tàu đang chọn.

**Acceptance criteria:**

- [ ] Constructor edit nhận `ClayVesselListItem`.
- [ ] Title đổi thành `Sửa tàu`.
- [ ] Nạp sẵn:
  - `VesselName`.
  - `CustomerCode`.
  - `CustomerName`.
  - `ProductCode`.
  - `ProductName`.
  - `Notes`.
- [ ] Validation dùng text tiếng Việt đúng encoding.
- [ ] Không phá luồng tạo tàu hiện có.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/Dialogs/CreateClayVesselDialogViewModel.cs`
- Có thể chạm `src/StationApp.UI/Views/Dialogs/CreateClayVesselDialogWindow.xaml` nếu cần chỉnh title/button.

**Verification:**

- [ ] Mở sửa tàu thấy dữ liệu cũ.
- [ ] Bấm hủy không thay đổi dữ liệu.

### Task 6: Thêm command và trạng thái nút Sửa tàu

**Mô tả:** Thêm command trong `ClayWeighingViewModel` để gọi dialog edit và use case update.

**Acceptance criteria:**

- [ ] Có `CanEditClayVessel`.
- [ ] `CanEditClayVessel = true` khi:
  - có `SelectedVessel`;
  - tàu chưa chốt tổng;
  - màn không loading.
- [ ] Có `EditClayVesselCommand`.
- [ ] Sau khi lưu:
  - reload lại `Vessels`;
  - giữ selected tàu vừa sửa;
  - reload lại `Trips` của tàu đó;
  - refresh command states.
- [ ] Toast thành công/lỗi hiển thị tiếng Việt đúng encoding.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

**Verification:**

- [ ] Chọn tàu chưa chốt thì nút enable.
- [ ] Chọn tàu đã chốt thì nút disable.
- [ ] Sửa xong grid tàu và grid chuyến cập nhật đúng.

### Task 7: Thêm nút Sửa tàu vào UI

**Mô tả:** Thêm nút **SỬA TÀU** cạnh nút **TẠO TÀU** trong toolbar Cân mỏ sét.

**Acceptance criteria:**

- [ ] Nút nằm sát `TẠO TÀU`.
- [ ] Chỉ hiển thị hoặc enable theo `CanEditClayVessel`. Đề xuất: luôn hiển thị nhưng disable khi chưa đủ điều kiện để người dùng thấy có chức năng sửa.
- [ ] Style thống nhất với các nút utility hiện tại.
- [ ] Không làm lệch cụm nút đang căn giữa.

**Files likely touched:**

- `src/StationApp.UI/Views/ClayWeighingView.xaml`

**Verification:**

- [ ] Mở màn Cân mỏ sét, toolbar không bị vỡ layout.

### Task 8: Hiển thị audit ở Lịch sử chỉnh sửa

**Mô tả:** Bổ sung action `UPDATE_CLAY_VESSEL` vào query/parse của màn lịch sử chỉnh sửa nếu màn này đang lọc whitelist action.

**Acceptance criteria:**

- [ ] Repository audit search lấy action `UPDATE_CLAY_VESSEL` cho station mỏ sét.
- [ ] ViewModel lịch sử parse được log này.
- [ ] Dòng lịch sử thể hiện được:
  - Tên tàu.
  - Đơn vị vận chuyển cũ/mới hoặc hàng hóa cũ/mới.
  - Ghi chú số snapshot đã cập nhật.
- [ ] Không làm hỏng các action lịch sử hiện có.

**Files likely touched:**

- `src/StationApp.Infrastructure/Repositories/OtherRepositories.cs`
- `src/StationApp.UI/ViewModels/WeighingSessionEditHistoryViewModel.cs`

**Verification:**

- [ ] Sửa tàu xong vào Lịch sử chỉnh sửa thấy log.

### Task 9: Test và build

**Mô tả:** Bổ sung test nếu phù hợp và chạy build để đảm bảo feature không làm vỡ luồng cân mỏ sét.

**Acceptance criteria:**

- [ ] Có test cho update tàu chưa chốt.
- [ ] Có test không cho update tàu đã chốt.
- [ ] Có test update snapshot line/session khi tàu đã có chuyến.
- [ ] Build UI thành công.

**Files likely touched:**

- `tests/StationApp.Application.Tests/CrusherClayWeighingUseCasesTests.cs` hoặc test file clay tương ứng.

**Verification commands:**

- [ ] `dotnet test tests\StationApp.Application.Tests\StationApp.Application.Tests.csproj --filter Clay`
- [ ] `dotnet build src\StationApp.UI\StationApp.UI.csproj`

## Thứ tự triển khai đề xuất

1. Task 1-3: thêm contract/use case và cập nhật snapshot.
2. Task 4: ghi audit trong cùng lát use case.
3. Task 5-7: nối dialog, command và nút UI.
4. Task 8: đưa log lên Lịch sử chỉnh sửa.
5. Task 9: test/build và rà UI thủ công.

## Rủi ro và cách xử lý

| Rủi ro | Ảnh hưởng | Cách xử lý |
| --- | --- | --- |
| Sửa hàng hóa/đơn vị nhưng session/line cũ vẫn giữ snapshot cũ | Grid chuyến, báo cáo, lịch sử dữ liệu bị lệch | Use case update phải cập nhật cả `WeighingSession` và `WeighingSessionLine` |
| Cho sửa tàu đã chốt | Sai dữ liệu báo cáo/chốt tổng đã khóa | Chặn ở cả `CanEditClayVessel` và use case |
| Dialog tạo tàu đang bị lỗi encoding | UI/validation khó đọc | Khi thêm edit mode, sửa luôn text trong file dialog đang chạm |
| Audit log ghi nhưng không hiện lịch sử | Người dùng không kiểm tra lại được thao tác | Thêm action vào whitelist `SearchEditLogsAsync` và parse trong view model lịch sử |
| Sửa tên tàu trùng tên tàu khác | Người dùng khó phân biệt chuyến tàu | Plan hiện chưa chặn trùng; khi code nên rà có pattern validate trùng không. Nếu chưa có yêu cầu, chỉ cảnh báo trong audit/UX, chưa tự đặt rule mới |

## Câu hỏi mở

- Có cần chặn **tên tàu/sà lan trùng** với một tàu mỏ sét đang mở trong cùng ngày/khoảng thời gian không? Nếu không chốt thêm, mặc định chưa chặn để không làm thay đổi nghiệp vụ hiện tại.
- Khi tàu đã có chuyến xe, có cho sửa **tên tàu/sà lan** không? Plan đang giả định là **có**, vì đây là metadata của `CutOrder` và không ảnh hưởng số cân.

