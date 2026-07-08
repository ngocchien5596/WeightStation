# PLAN: Đồng bộ giao diện và thao tác Cân mỏ sét theo Cân xuất khẩu

## Mục tiêu

Nâng cấp màn **Cân mỏ sét** để cách thao tác giống màn **Cân xuất khẩu**:

- Người dùng phải tạo/chọn **Thông tin tàu** trước.
- Sau đó bấm **Tạo chuyến xe** để tạo lượt cân cho xe chở hàng thuộc tàu đó.
- Danh sách tàu nằm ở grid trên, danh sách chuyến xe của tàu nằm ở grid dưới.
- Các nút nghiệp vụ chính giống Cân xuất khẩu: tạo tàu/cắt lệnh, tạo chuyến xe, chuyển chuyến, xóa chuyến xe, xem ảnh, chốt tổng.
- Không cần in **Phiếu cân** và **Phiếu giao nhận** ở Cân mỏ sét.
- Có nghiệp vụ **Hoàn** cho chuyến xe mỏ sét.
- Có chụp ảnh và xem ảnh theo chuyến xe.
- Khác biệt nghiệp vụ: xe của chuyến mỏ sét là **xe nội bộ**, áp dụng logic cân như màn **Cân mỏ đá**:
  - xe nội bộ có TL bì hiệu lực trong ngày thì cân 1 lần;
  - xe nội bộ chưa có TL bì hiệu lực trong ngày thì cân 2 lần;
  - không tạo xe master mới từ màn Cân mỏ sét.

## Rà soát hiện trạng

### Cân xuất khẩu đang có

File chính:

- `src/StationApp.UI/Views/ExportWeighingView.xaml`
- `src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs`

Luồng thao tác hiện có:

- Toolbar:
  - `TẠO CẮT LỆNH TẠM`
  - `TẠO CHUYẾN XE`
  - `CHUYỂN CHUYẾN`
  - `XÓA CHUYẾN XE`
  - `IN PC`
  - `IN PGN`
  - `XEM ẢNH`
  - `CHỐT TỔNG`
- Grid 1: danh sách cắt lệnh.
- Grid 2: danh sách chuyến xe theo cắt lệnh đang chọn.
- ViewModel có selection riêng:
  - `SelectedCutOrder`
  - `SelectedTrip`
  - `Trips`
  - `CutOrders`
- Các command theo selected trip:
  - `CreateTripCommand`
  - `TransferTripCommand`
  - `DeleteTripCommand`
  - `FinalizeCommand`
  - `ViewImageHistoryCommand`
  - `CaptureWeight1Command`
  - `CaptureWeight2Command`
  - `SaveCapturedWeightCommand`
- Có cơ chế tránh lỗi selection grid:
  - `SelectedTripIndex`
  - handler code-behind cho trips grid
  - `ClearSelectedTrip`
  - `ApplySelectedTripWeights`
  - reload/reselect sau thao tác.

### Cân mỏ sét hiện tại sau lát code vừa làm

File chính:

- `src/StationApp.UI/Views/ClayWeighingView.xaml`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`
- `src/StationApp.Application/UseCases/ClayVesselFlowUseCases.cs`

Đã có:

- DTO/repository để list tàu và list chuyến theo tàu.
- Tạo tàu mỏ sét.
- Lưu lượt cân gắn vào tàu đang chọn.
- Chốt tổng tàu sơ bộ.
- Grid tàu + grid chuyến sơ bộ.
- Chặn xe chưa có trong danh mục xe nội bộ khi cân.

Chưa giống Cân xuất khẩu:

- Chưa có nút **Tạo chuyến xe** riêng; hiện vẫn nhập xe rồi bấm cân/lưu trực tiếp.
- Chưa có **Chuyển chuyến** giữa các tàu.
- Chưa có **Xóa chuyến xe**.
- Chưa có **Xem ảnh** theo chuyến.
- Vẫn còn nút **IN PC** và code in phiếu cân, trong khi yêu cầu mới là không cần in.
- Grid chuyến vẫn dùng `CrusherWeighingSessionListItem`, chưa có model UI riêng như `ExportVehicleTripListItem`.
- Chưa có `SelectedTrip` riêng cho mỏ sét; đang dùng `SelectedSession`.
- Chưa có cơ chế selection/reselect giống Cân xuất khẩu.
- Form thông tin tàu chưa giống panel thông tin cắt lệnh của Cân xuất khẩu.
- Toolbar chưa đồng bộ vị trí, thứ tự và trạng thái enable/disable theo Cân xuất khẩu.

## Quyết định thiết kế

- Màn Cân mỏ sét sẽ dùng thuật ngữ UI:
  - `Thông tin tàu` thay cho `Cắt lệnh`.
  - `Tạo tàu` thay cho `Tạo cắt lệnh tạm`.
  - `Tạo chuyến xe`, `Chuyển chuyến`, `Xóa chuyến xe`, `Xem ảnh`, `Chốt tổng` giữ cách thao tác như Cân xuất khẩu.
- Không thêm bảng mới. Tàu vẫn lưu bằng `CutOrder`:
  - `TransactionType = INBOUND`
  - `TransportMethod = WATERWAY`
  - `CutOrderSource = MANUAL`
  - `IsExportScale = false`
  - `VehiclePlate = tên tàu/sà lan`
- Chuyến xe vẫn dùng `WeighingSession` + `WeighingSessionLine`.
- Không đưa nút in `IN PC`, `IN PGN` vào Cân mỏ sét.
- Có checkbox/cột `Hoàn` cho chuyến mỏ sét, xử lý giống nghiệp vụ Hoàn đã làm cho mỏ đá/export nhưng scope là chuyến xe thuộc tàu mỏ sét.
- Nên tạo DTO riêng cho trip mỏ sét và ViewModel state riêng, tránh ép dùng `CrusherWeighingSessionListItem`.

## Task List

### Phase 1: Chuẩn hóa contract và use case cho thao tác giống export

#### Task 1: Bổ sung DTO trip/tàu phục vụ UI parity

**Mô tả:** Chuẩn hóa DTO mỏ sét để đủ thông tin cho grid tàu và grid chuyến giống export, không phải dùng tạm DTO crusher.

**Acceptance criteria:**

- [ ] Có DTO `ClayVesselListItem` đủ các cột tàu: tên tàu, đơn vị vận chuyển, hàng hóa, lũy kế, số chuyến, chuyến cuối, trạng thái, ghi chú.
- [ ] Có DTO `ClayVehicleTripListItem` đủ các cột chuyến: số lượt cân, xe, tài xế, cân lần 1, cân lần 2, TL hàng, TL bì, trạng thái, ghi chú.
- [ ] DTO có property hỗ trợ enable/disable xóa/chuyển/xem ảnh/Hoàn nếu cần.

**Files likely touched:**

- `src/StationApp.Application/DTOs/Dtos.cs`
- `src/StationApp.Infrastructure/Repositories/CutOrderRepository.cs`
- `src/StationApp.Application/Interfaces/ICutOrderRepository.cs`

**Verification:**

- [ ] Build Application pass.

#### Task 2: Thêm use case tạo chuyến xe riêng cho mỏ sét

**Mô tả:** Đổi thao tác từ “nhập xe rồi cân ngay” sang “chọn tàu -> tạo chuyến xe -> cân chuyến xe”, giống Cân xuất khẩu.

**Acceptance criteria:**

- [ ] Bấm `Tạo chuyến xe` chỉ được khi đã chọn tàu chưa chốt.
- [ ] Dialog/form tạo chuyến chỉ cho chọn xe nội bộ đang active.
- [ ] Nếu xe có TL bì hiệu lực trong ngày thì chuyến được tạo ở chế độ cân 1 lần.
- [ ] Nếu xe chưa có TL bì hiệu lực trong ngày thì chuyến được tạo ở chế độ cân 2 lần.
- [ ] Không tạo/cập nhật master xe từ màn cân.

**Files likely touched:**

- `src/StationApp.Application/UseCases/ClayVesselFlowUseCases.cs`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`
- dialog tạo/chọn chuyến mới nếu cần.

**Verification:**

- [ ] Test tạo chuyến với xe có TL bì.
- [ ] Test tạo chuyến với xe chưa có TL bì.
- [ ] Test xe không có trong danh mục bị chặn.

#### Task 3: Hoàn tất line sau khi lưu cân chuyến

**Mô tả:** Khi cân lần 1/lần 2 xong, cập nhật `WeighingSessionLine.ActualAllocatedWeight` và `LineStatus` để tàu tính đúng lũy kế.

**Acceptance criteria:**

- [ ] Cân 1 lần: sau lưu, line được `ALLOCATED`, trọng lượng bằng `NetWeight`.
- [ ] Cân 2 lần: sau lưu cân lần 1, chuyến vẫn pending; sau lưu cân lần 2, line được `ALLOCATED`.
- [ ] Lũy kế tàu refresh đúng sau mỗi lần lưu.

**Files likely touched:**

- `src/StationApp.Application/UseCases/ClayVesselFlowUseCases.cs`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

**Verification:**

- [ ] Test repository/list tàu tính `AccumulatedWeight` đúng.
- [ ] Test use case complete line.

### Phase 2: Đồng bộ ViewModel thao tác theo ExportWeighingViewModel

#### Task 4: Tách state `SelectedTrip` cho mỏ sét

**Mô tả:** Không dùng `SelectedSession` làm selected row chính nữa; tạo state tương đương export để điều khiển trip grid.

**Acceptance criteria:**

- [ ] Có `Vessels`, `SelectedVessel`, `Trips`, `SelectedTrip`, `SelectedTripIndex`.
- [ ] Chọn tàu tự load trips và clear selected trip cũ.
- [ ] Chọn trip tự đổ weight/form trạng thái như Cân xuất khẩu.
- [ ] Reload sau thao tác giữ lại đúng tàu/trip vừa thao tác.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`
- `src/StationApp.UI/Views/ClayWeighingView.xaml.cs` nếu cần handler grid như export.

**Verification:**

- [ ] Manual: chọn nhanh nhiều trip không bị lệch selected item.
- [ ] Build UI pass.

#### Task 5: Thêm command chức năng giống export, bỏ print

**Mô tả:** Toolbar Cân mỏ sét cần đủ nút thao tác như Cân xuất khẩu, trừ in phiếu.

**Acceptance criteria:**

- [ ] Có `CreateClayVesselCommand`.
- [ ] Có `CreateTripCommand`.
- [ ] Có `TransferTripCommand`.
- [ ] Có `DeleteTripCommand`.
- [ ] Có `ViewImageHistoryCommand`.
- [ ] Có command chụp/lưu ảnh theo chuyến như luồng cân hiện có.
- [ ] Có `ToggleReturnedBrokenTripCommand` hoặc command tương đương cho checkbox `Hoàn`.
- [ ] Có `FinalizeCommand`.
- [ ] Không hiển thị `IN PC`, `IN PGN`.
- [ ] Enable/disable giống export:
  - chưa chọn tàu thì không tạo chuyến;
  - tàu đã chốt thì không tạo/chuyển/xóa chuyến;
  - chỉ xóa chuyến chưa có cân lần 2;
  - cho chuyển chuyến đã hoàn tất nếu tàu nguồn và tàu đích đều chưa chốt;
  - chỉ chốt khi không còn chuyến dở dang và đã có chuyến hoàn thành.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`
- `src/StationApp.UI/Views/ClayWeighingView.xaml`

**Verification:**

- [ ] Manual: trạng thái nút thay đổi đúng khi chọn tàu/trip.
- [ ] Build UI pass.

#### Task 6: Implement chuyển chuyến xe giữa các tàu

**Mô tả:** Cho phép chuyển một chuyến xe từ tàu hiện tại sang tàu khác chưa chốt, tương tự `TransferExportVehicleTripUseCase`.

**Acceptance criteria:**

- [ ] Dialog hiển thị chuyến cần chuyển, tàu nguồn, tàu đích.
- [ ] Danh sách tàu đích chỉ gồm tàu mỏ sét chưa chốt, không bao gồm tàu nguồn.
- [ ] Cho chuyển cả chuyến đã hoàn tất nếu tàu nguồn và tàu đích đều chưa chốt.
- [ ] Sau chuyển, line đổi `CutOrderId`, thông tin line customer/product cập nhật theo tàu đích.
- [ ] Lũy kế tàu nguồn và tàu đích refresh đúng.
- [ ] Ghi audit log cho thao tác chuyển chuyến.

**Files likely touched:**

- `src/StationApp.Application/UseCases/TransferClayVehicleTripUseCase.cs`
- `src/StationApp.UI/ViewModels/Dialogs/...`
- `src/StationApp.UI/Views/Dialogs/...`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

**Verification:**

- [ ] Unit test chuyển chuyến.
- [ ] Manual: chuyển xong selected tàu đích và selected trip đúng.

#### Task 7: Implement xóa chuyến xe

**Mô tả:** Cho phép xóa mềm chuyến xe chưa hoàn tất, giống export.

**Acceptance criteria:**

- [ ] Chỉ xóa chuyến chưa cân lần 2.
- [ ] Xóa mềm session/line hoặc line theo đúng pattern export hiện có.
- [ ] Confirm trước khi xóa.
- [ ] Sau xóa, grid trip refresh và lũy kế tàu không tính chuyến đó.
- [ ] Ghi audit log.

**Files likely touched:**

- `src/StationApp.Application/UseCases/DeleteClayVehicleTripUseCase.cs`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

**Verification:**

- [ ] Unit test xóa chuyến pending.
- [ ] Unit test không xóa chuyến đã hoàn tất.

### Phase 3: Đồng bộ layout XAML với Cân xuất khẩu

#### Task 8: Đưa layout Cân mỏ sét về cấu trúc giống ExportWeighingView

**Mô tả:** Sắp lại layout để người dùng nhìn và thao tác giống Cân xuất khẩu.

**Acceptance criteria:**

- [ ] Header filter giống export: ngày, tìm tàu, tìm xe/chuyến nếu cần, làm mới.
- [ ] Panel thông tin tàu giống panel thông tin cắt lệnh:
  - Tàu/Sà lan
  - Đơn vị vận chuyển
  - Hàng hóa
  - Lũy kế
  - Số chuyến
  - Ghi chú
- [ ] Panel cân vẫn dùng logic mỏ đá: cân 1 lần/2 lần theo TL bì xe nội bộ.
- [ ] Toolbar chức năng đặt cùng vùng và thứ tự tương tự export.
- [ ] Grid tàu dùng style `SystemDataGridStyle`.
- [ ] Grid chuyến dùng style `DetailDataGridStyle` như export.

**Files likely touched:**

- `src/StationApp.UI/Views/ClayWeighingView.xaml`
- `src/StationApp.UI/Views/ClayWeighingView.xaml.cs`

**Verification:**

- [ ] Build XAML pass.
- [ ] Manual visual compare với Cân xuất khẩu.

#### Task 9: Bỏ UI và code in phiếu khỏi Cân mỏ sét

**Mô tả:** Theo yêu cầu, mỏ sét không cần in phiếu cân/phiếu giao nhận.

**Acceptance criteria:**

- [ ] Không còn nút `IN PC` trên Cân mỏ sét.
- [ ] Không thêm `IN PGN`.
- [ ] Có thể giữ code in cũ nếu chưa gây hại, nhưng không expose UI; tốt nhất dọn sau khi xác nhận không dùng.

**Files likely touched:**

- `src/StationApp.UI/Views/ClayWeighingView.xaml`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

**Verification:**

- [ ] Manual: toolbar không có nút in.

#### Task 10: Thêm chụp ảnh và xem ảnh theo chuyến mỏ sét

**Mô tả:** Bổ sung chức năng chụp ảnh khi cân và xem ảnh lịch sử theo chuyến, tương tự các màn cân đang có camera.

**Acceptance criteria:**

- [ ] Khi cân/lưu chuyến mỏ sét, ảnh từ camera cấu hình cho mỏ sét được lưu gắn với `WeighingSessionId`.
- [ ] Nút `XEM ẢNH` hiển thị/enable khi selected trip có cân lần 1 hoặc có ảnh.
- [ ] Mở modal xem ảnh dùng lại `CameraImageHistoryViewModel`.
- [ ] Không làm lỗi thao tác selected trip khi mở/đóng modal ảnh.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`
- `src/StationApp.UI/Views/ClayWeighingView.xaml`
- Có thể dùng lại service/repository ảnh hiện có.

**Verification:**

- [ ] Manual: tạo chuyến, cân, mở `XEM ẢNH` thấy ảnh đúng chuyến.

#### Task 11: Thêm nghiệp vụ Hoàn cho chuyến mỏ sét

**Mô tả:** Cho phép đánh dấu chuyến mỏ sét là hàng hoàn, hiển thị màu/cột và tính lũy kế tàu theo trọng lượng âm hoặc cách tính đã thống nhất trong nghiệp vụ Hoàn hiện có.

**Acceptance criteria:**

- [ ] Grid chuyến có cột checkbox `HOÀN`.
- [ ] Checkbox không gây lỗi selected item khi click, áp dụng cùng pattern đã xử lý ở Cân xuất khẩu/mỏ đá.
- [ ] Chỉ cho toggle Hoàn trên chuyến đã có sản lượng hợp lệ và tàu chưa chốt.
- [ ] Chuyến Hoàn hiển thị text màu đỏ ở grid chuyến.
- [ ] Lũy kế tàu tính lại sau khi toggle Hoàn.
- [ ] Ghi audit log và hiển thị ở màn Lịch sử chỉnh sửa nếu đang theo pattern hiện có.

**Files likely touched:**

- `src/StationApp.Application/UseCases/ToggleClayReturnedBrokenTripUseCase.cs`
- `src/StationApp.Infrastructure/Repositories/CutOrderRepository.cs`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`
- `src/StationApp.UI/Views/ClayWeighingView.xaml`
- `src/StationApp.UI/Views/ClayWeighingView.xaml.cs`

**Verification:**

- [ ] Unit test toggle Hoàn.
- [ ] Manual: check/uncheck Hoàn không đổi nhầm selected trip, lũy kế tàu đổi đúng.

### Phase 4: Kiểm thử và hardening

#### Task 12: Bổ sung test use case mỏ sét

**Mô tả:** Cover các hành vi mới để tránh lệch logic sau này.

**Acceptance criteria:**

- [ ] Test tạo tàu.
- [ ] Test tạo chuyến xe có TL bì hiệu lực.
- [ ] Test tạo chuyến xe chưa có TL bì.
- [ ] Test complete line sau cân.
- [ ] Test chuyển chuyến.
- [ ] Test xóa chuyến.
- [ ] Test toggle Hoàn.
- [ ] Test chốt tổng không cho chốt khi còn chuyến dở dang.

**Files likely touched:**

- `tests/StationApp.Application.Tests/...`

**Verification:**

- [ ] `dotnet test tests\StationApp.Application.Tests\StationApp.Application.Tests.csproj --filter Clay`

#### Task 13: Verification end-to-end

**Mô tả:** Chạy build/test và checklist thao tác tay.

**Acceptance criteria:**

- [ ] Build UI pass.
- [ ] Test liên quan pass.
- [ ] Manual flow:
  - tạo tàu;
  - tạo chuyến xe nội bộ;
  - cân lần 1;
  - nếu cân 2 lần thì cân lần 2;
  - xem lũy kế tàu;
  - chuyển chuyến sang tàu khác;
  - xóa chuyến pending;
  - chụp/xem ảnh;
  - đánh dấu Hoàn;
  - chốt tổng.

**Verification commands:**

- [ ] `dotnet build src\StationApp.UI\StationApp.UI.csproj -v:minimal -p:SkipDatabaseSchemaUpdate=true`
- [ ] `dotnet test tests\StationApp.Application.Tests\StationApp.Application.Tests.csproj --filter Clay -v:minimal`

## Rủi ro và cách xử lý

| Rủi ro | Ảnh hưởng | Cách xử lý |
| --- | --- | --- |
| Reuse quá nhiều code cũ của `ClayWeighingViewModel` đang clone từ mỏ đá | Dễ lệch selection và command state | Tách `SelectedTrip/Trips` giống export, giữ phần cân xe nội bộ như service/use case riêng |
| Chuyển chuyến làm sai lũy kế tàu | Báo cáo/chốt tổng sai | Cập nhật `Line.CutOrderId` và refresh cả tàu nguồn/tàu đích, có unit test |
| Xóa chuyến đã hoàn tất gây mất số liệu | Sai dữ liệu cân | Chỉ cho xóa chuyến chưa cân lần 2/chưa complete |
| Mỏ sét không cần in nhưng code cũ còn command in | Người dùng thao tác nhầm | Bỏ nút khỏi XAML trước, sau duyệt sẽ dọn code in nếu chắc chắn không dùng |
| Text/encoding tiếng Việt cũ bị mojibake | UI xấu, khó đọc | Khi sửa XAML/chuỗi mới dùng entity Unicode hoặc UTF-8 đúng; không lan mojibake |

## Câu hỏi cần chốt trước khi code

Đã chốt ngày 04/07/2026:

1. Nút tạo tàu ghi **TẠO TÀU**.
2. **Có** cho chuyển chuyến đã hoàn tất nếu tàu nguồn và tàu đích chưa chốt.
3. Xóa chuyến xe: **đúng**, chỉ cho xóa chuyến chưa cân lần 2.
4. **Có**, cần chụp ảnh và xem ảnh.
5. **Có**, cần nghiệp vụ Hoàn.

## Thứ tự triển khai đề xuất

1. Task 1-3: hoàn thiện nền nghiệp vụ trip/tàu.
2. Task 4-5: đồng bộ ViewModel command/selection.
3. Task 8-9: chỉnh UI giống export và bỏ in.
4. Task 6-7: thêm chuyển chuyến/xóa chuyến.
5. Task 10-11: thêm ảnh và Hoàn.
6. Task 12-13: test và verify end-to-end.
