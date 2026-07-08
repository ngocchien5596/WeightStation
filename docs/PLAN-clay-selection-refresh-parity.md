# Kế hoạch xử lý selected item và Làm mới cho màn Cân mỏ sét

## Tổng quan

Mục tiêu là làm màn `Cân mỏ sét` xử lý selected item của grid `tàu/sà lan` và grid `chuyến xe` ổn định giống màn `Cân xuất khẩu`: không bị hiển thị sai dòng đang chọn, không tự chọn lại item cũ sau khi đã clear, không nhận kết quả load cũ khi người dùng đổi selection nhanh, và nút `Làm mới` trên header reset/làm mới dữ liệu theo cùng cách đã xử lý ở Cân xuất khẩu.

## Phạm vi

Trong phạm vi:

- Rà và sửa ViewModel `ClayWeighingViewModel`.
- Rà và sửa View `ClayWeighingView.xaml` / code-behind `ClayWeighingView.xaml.cs`.
- Port các pattern đã ổn ở `ExportWeighingViewModel` và `ExportWeighingView.xaml.cs`.
- Bổ sung test nếu có thể kiểm chứng ở tầng ViewModel/use case; build UI sau khi sửa.

Ngoài phạm vi:

- Không đổi nghiệp vụ cân, tạo tàu, tạo chuyến, đổi số xe, chuyển chuyến, xóa chuyến.
- Không đổi schema DB.
- Không đổi layout lớn ngoài các binding/event cần thiết cho selection.

## Hiện trạng đã rà soát

### Cân xuất khẩu đang có pattern ổn định

Các điểm quan trọng:

- `ExportWeighingViewModel` có `_suppressSelectedCutOrderTripLoad` để tránh `OnSelectedCutOrderChanged` tự load chuyến khi đang preserve cắt lệnh.
- `LoadCutOrdersAsync(Guid? preserveCutOrderId, bool loadTripsForSelectedCutOrder)` chọn lại cắt lệnh theo ID sau khi reload.
- `LoadTripsAsync(Guid cutOrderId, Guid? selectedTripId)` có `_tripLoadVersion` để bỏ qua kết quả load cũ.
- `ClearSelectedTrip()` set `SelectedTrip = null`, `SelectedTripIndex = -1`, tăng `ClearTripSelectionRequest`.
- `ExportWeighingView.xaml.cs` nghe `ClearTripSelectionRequest`, clear `SelectedItem`, `SelectedIndex`, `CurrentItem`, `CurrentCell`, `UnselectAll`, và move default collection view về `-1` qua nhiều priority dispatcher.
- `OnTripsGridPreviewMouseLeftButtonDown` chủ động set `SelectedItem`, `CurrentItem`, `row.IsSelected`, và đồng bộ `ViewModel.SelectedTrip`.
- `OnSelectedTripChanged` có guard `_isTripSelectionResetting` để bỏ selection phát sinh trong lúc reset.
- Nút `Làm mới` gọi `RefreshCommand`, luồng refresh clear search/selection/form rồi load lại danh sách.

### Cân mỏ sét hiện còn lệch

Các điểm đang có:

- `ClayWeighingView.xaml` có grid tàu bind `SelectedVessel` và grid chuyến bind `SelectedTrip`.
- Grid chuyến đã có `IsSynchronizedWithCurrentItem="False"`, nhưng chưa có code-behind xử lý selection reset giống Export.
- `ClayWeighingViewModel.OnSelectedTripChanged` đang set `SelectedSession` theo `SelectedTrip`, nhưng chưa có cơ chế clear UI grid nhiều priority.
- `LoadVesselsAsync` đang preserve tàu nhưng chưa có suppress load trips tương tự Export.
- `LoadSessionsAsync` load chuyến theo `SelectedVessel`, nhưng chưa có load version để bỏ kết quả cũ.
- `RefreshAsync` hiện reset một số trạng thái rồi `LoadVesselsAsync`, nhưng chưa đồng nhất với cách `RefreshAndClearSelectionAsync` của Export: clear search/selection/form và đảm bảo grid UI không còn selected row cũ.

## Quyết định thiết kế

- Dùng cùng pattern với Cân xuất khẩu để giảm khác biệt UI và tránh lỗi WPF selected/current item.
- Không dựa riêng vào binding `SelectedItem`; code-behind sẽ chủ động đồng bộ row click và clear grid selection.
- Khi refresh dữ liệu theo ID, luôn preserve bằng khóa ổn định:
  - Tàu/sà lan: `CutOrderId`.
  - Chuyến xe: `SessionId`.
- Khi clear selection, clear cả ViewModel state và DataGrid state.
- Khi load chuyến bất đồng bộ, dùng version để bỏ kết quả cũ nếu người dùng đổi tàu trước khi request cũ trả về.

## Task List

### Task 1: Bổ sung state điều phối selection cho `ClayWeighingViewModel`

**Mô tả:** Thêm các field/properties tương tự Export để quản lý reset selection và chống stale load.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

**Acceptance criteria:**

- [ ] Có `ClearTripSelectionRequest` để View có thể clear selection grid chuyến.
- [ ] Có `_isTripSelectionResetting`, `BeginTripSelectionReset()`, `CompleteTripSelectionReset()`.
- [ ] Có `_tripLoadVersion` cho `LoadSessionsAsync`.
- [ ] Có `_suppressSelectedVesselTripLoad` hoặc biến tương đương khi preserve tàu mà chưa muốn auto load chuyến.
- [ ] `RefreshCommandStates` hoặc nhóm Notify command được gọi sau khi reset/loading.

**Verification:**

- [ ] Build UI không lỗi.
- [ ] Không đổi behavior nghiệp vụ.

**Dependencies:** None.

**Estimated scope:** Medium.

### Task 2: Chuẩn hóa load tàu và chuyến theo pattern preserve/reselect

**Mô tả:** Điều chỉnh `LoadVesselsAsync` và `LoadSessionsAsync` để preserve selection theo ID, không auto load chuyến sai lúc preserve, và bỏ kết quả load cũ.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

**Acceptance criteria:**

- [ ] `LoadVesselsAsync(Guid? preserveCutOrderId = null, bool loadTripsForSelectedVessel = true)` chọn lại tàu bằng `CutOrderId`.
- [ ] Khi `SelectedVessel == null`, clear `Trips`, `Sessions`, `SelectedTrip`, `SelectedSession`, và form cân liên quan.
- [ ] `LoadSessionsAsync(Guid cutOrderId, Guid? selectedTripId = null)` chỉ apply kết quả nếu version còn mới và `SelectedVessel.CutOrderId` vẫn khớp.
- [ ] Khi truyền `selectedTripId`, chọn lại đúng chuyến sau reload.
- [ ] Khi không truyền `selectedTripId`, clear selected trip thay vì để WPF tự chọn dòng cũ.

**Verification:**

- [ ] Build UI không lỗi.
- [ ] Manual checklist: chọn tàu A, nhanh tay chọn tàu B trong lúc load; danh sách chuyến cuối cùng phải thuộc tàu B.

**Dependencies:** Task 1.

**Estimated scope:** Medium.

### Task 3: Chuẩn hóa clear/selection state của chuyến xe

**Mô tả:** Tách helper cho selected trip giống Export để tránh stale UI và stale ViewModel.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

**Acceptance criteria:**

- [ ] Có `ClearSelectedTrip()` set `SelectedTrip = null`, `SelectedTripIndex = -1`, `SelectedSession = null`, tăng `ClearTripSelectionRequest`.
- [ ] `OnSelectedTripChanged` bỏ selection phát sinh khi `_isTripSelectionResetting`.
- [ ] `OnSelectedTripChanged` đồng bộ `SelectedSession` đúng theo `SessionId`.
- [ ] Khi selected trip null, các nút `Chuyển chuyến`, `Đổi số xe`, `Xóa chuyến xe`, `Xem ảnh`, `Hoàn`, `Cân lần 2`, `Lưu` được notify lại.
- [ ] Không còn tình huống form cân hiển thị dữ liệu chuyến cũ khi grid đã clear selection.

**Verification:**

- [ ] Manual checklist: chọn chuyến, bấm Làm mới, grid chuyến không còn highlight dòng cũ nếu không preserve chuyến.
- [ ] Manual checklist: click một dòng chuyến sau refresh, form cân hiển thị đúng dòng vừa click.

**Dependencies:** Task 1.

**Estimated scope:** Medium.

### Task 4: Port code-behind xử lý selection grid chuyến từ Cân xuất khẩu

**Mô tả:** Cập nhật `ClayWeighingView.xaml.cs` để clear và đồng bộ selection DataGrid giống `ExportWeighingView.xaml.cs`.

**Files likely touched:**

- `src/StationApp.UI/Views/ClayWeighingView.xaml`
- `src/StationApp.UI/Views/ClayWeighingView.xaml.cs`

**Acceptance criteria:**

- [ ] Grid chuyến có `SelectionChanged="OnTripsGridSelectionChanged"`.
- [ ] Grid chuyến có `PreviewMouseLeftButtonDown="OnTripsGridPreviewMouseLeftButtonDown"`.
- [ ] Code-behind nghe `ClearTripSelectionRequest`.
- [ ] Code-behind clear `SelectedItem`, `SelectedIndex`, `CurrentCell`, `CurrentItem`, `UnselectAll`, `CollectionView.MoveCurrentToPosition(-1)`.
- [ ] Clear được queue ở các priority `Send`, `Loaded`, `ContextIdle`, `ApplicationIdle`.
- [ ] Click checkbox `Hoàn` vẫn dùng `PreviewMouseLeftButtonDown` hiện có và không làm selected item nhảy sai.

**Verification:**

- [ ] Build UI không lỗi.
- [ ] Manual checklist: chọn chuyến, click checkbox Hoàn, selected trip không bị đổi sai ngoài ý muốn.
- [ ] Manual checklist: click vùng text/dòng trên grid, ViewModel `SelectedTrip` khớp row đang highlight.

**Dependencies:** Task 1, Task 3.

**Estimated scope:** Medium.

### Task 5: Xử lý selected item của grid tàu/sà lan

**Mô tả:** Đảm bảo grid tàu không làm chuyến xe hiển thị sai sau refresh hoặc sau khi chọn tàu khác.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`
- `src/StationApp.UI/Views/ClayWeighingView.xaml`

**Acceptance criteria:**

- [ ] Grid tàu có `IsSynchronizedWithCurrentItem="False"` giống grid cắt lệnh Export.
- [ ] Khi chọn tàu mới, clear selected trip/session trước khi load chuyến mới.
- [ ] Khi load preserve tàu sau thao tác tạo/chuyển/đổi/xóa, không tự load chuyến 2 lần.
- [ ] Khi tàu được chọn không còn trong danh sách sau refresh, clear tàu và clear chuyến/form.

**Verification:**

- [ ] Manual checklist: chọn tàu A và chuyến A1, chọn tàu B; grid chuyến không còn highlight A1 và chỉ hiển thị chuyến của B.
- [ ] Manual checklist: refresh khi tàu đang bị filter mất; form tàu/chuyến clear hợp lý.

**Dependencies:** Task 2, Task 3.

**Estimated scope:** Small.

### Task 6: Làm mới header giống Cân xuất khẩu

**Mô tả:** Sửa `RefreshCommand` của Mỏ sét để reset search/selection/form theo pattern Export, tránh giữ stale selection khi bấm `LÀM MỚI`.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`
- `src/StationApp.UI/Views/ClayWeighingView.xaml`

**Acceptance criteria:**

- [ ] Bấm `LÀM MỚI` clear `SearchVehicle` và `SearchSessionNo` nếu đang dùng cho grid chuyến.
- [ ] Bấm `LÀM MỚI` clear `SearchVessel` hoặc thống nhất theo Export: nếu Export đang clear search thì Clay cũng clear search.
- [ ] Clear `SelectedVessel`, `SelectedTrip`, `SelectedSession`.
- [ ] Clear form xe nội bộ, pending weights, active session.
- [ ] Load lại danh sách tàu từ đầu.
- [ ] Camera preview không bị restart thừa nếu không cần thiết.

**Verification:**

- [ ] Manual checklist: nhập filter, chọn tàu/chuyến, bấm Làm mới; filter và selection clear theo đúng thiết kế.
- [ ] Build UI không lỗi.

**Dependencies:** Task 1-5.

**Estimated scope:** Small.

### Task 7: Rà các thao tác preserve selection sau nghiệp vụ

**Mô tả:** Áp dụng helper reload/reselect cho các thao tác đã có: tạo chuyến, chuyển chuyến, xóa chuyến, Hoàn, lưu cân, đổi số xe, chốt tổng.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

**Acceptance criteria:**

- [ ] Tạo chuyến: reload tàu, reload chuyến, chọn đúng chuyến mới.
- [ ] Chuyển chuyến: reload tàu đích, reload chuyến, chọn đúng chuyến vừa chuyển nếu còn thuộc tàu đang chọn.
- [ ] Xóa chuyến: reload tàu/chuyến và clear selection nếu chuyến đã xóa.
- [ ] Hoàn: reload tàu/chuyến và chọn lại đúng chuyến.
- [ ] Lưu cân: reload tàu/chuyến và chọn lại đúng chuyến vừa lưu.
- [ ] Đổi số xe: reload tàu/chuyến và chọn lại đúng chuyến vừa đổi.
- [ ] Chốt tổng: reload danh sách tàu và clear/đổi selection nếu tàu không còn hiển thị.

**Verification:**

- [ ] Manual checklist từng thao tác trên không làm highlight sai dòng.
- [ ] Các command enable/disable đúng sau mỗi thao tác.

**Dependencies:** Task 2-6.

**Estimated scope:** Medium.

### Task 8: Test và kiểm tra hồi quy UI

**Mô tả:** Thêm test ở tầng phù hợp và chạy build/test.

**Files likely touched:**

- `tests/StationApp.Application.Tests/...` nếu có logic use case liên quan.
- Có thể không có unit test cho code-behind WPF; cần manual checklist rõ.

**Acceptance criteria:**

- [ ] Không làm hỏng `CrusherClayWeighingUseCasesTests`.
- [ ] Build `StationApp.UI.csproj` thành công.
- [ ] Không còn lỗi selected item hiển thị sai khi refresh/chọn nhanh.

**Verification:**

- [ ] `dotnet test tests\StationApp.Application.Tests\StationApp.Application.Tests.csproj --filter CrusherClayWeighingUseCasesTests -v:minimal`
- [ ] `dotnet build src\StationApp.UI\StationApp.UI.csproj -v:minimal -p:SkipDatabaseSchemaUpdate=true`
- [ ] Manual checklist trên màn Cân mỏ sét.

**Dependencies:** Task 1-7.

**Estimated scope:** Small.

## Checkpoint đề xuất

### Checkpoint 1: Sau Task 1-3

- [ ] Build UI sạch.
- [ ] Logic ViewModel compile.
- [ ] Clear selected trip không gây vòng lặp property changed.

### Checkpoint 2: Sau Task 4-6

- [ ] Bấm `LÀM MỚI` clear đúng selection và form.
- [ ] Click grid chuyến chọn đúng dòng.
- [ ] Chọn tàu khác không còn hiển thị chuyến cũ.

### Checkpoint 3: Sau Task 7-8

- [ ] Các nghiệp vụ tạo/chuyển/xóa/hoàn/lưu/đổi xe/chốt tổng không làm sai selected item.
- [ ] Tests pass.
- [ ] Build UI sạch.

## Rủi ro và cách giảm thiểu

| Rủi ro | Mức độ | Giảm thiểu |
|---|---|---|
| WPF DataGrid tự giữ `CurrentItem` dù ViewModel đã clear | Cao | Port cơ chế clear nhiều Dispatcher priority từ Export |
| Kết quả load chuyến cũ ghi đè chuyến của tàu mới | Cao | Dùng `_tripLoadVersion` và kiểm tra `SelectedVessel.CutOrderId` trước khi apply |
| Preserve tàu làm `OnSelectedVesselChanged` tự load chuyến 2 lần | Trung bình | Dùng `_suppressSelectedVesselTripLoad` |
| Checkbox `Hoàn` làm selected row nhảy sai | Trung bình | Giữ handler `PreviewMouseLeftButtonDown` riêng cho checkbox và không để checkbox tự toggle visual |
| Refresh header làm mất selection trong thao tác cần preserve | Trung bình | Tách rõ `RefreshAndClearSelectionAsync` và `ReloadTripsAndReselectAsync` |

## Open questions

- Khi bấm `LÀM MỚI`, có cần clear `Ngày thống kê` về hôm nay không? Cân xuất khẩu hiện chủ yếu clear search/selection, không nhất thiết reset ngày.
- `SearchVessel` có cần clear giống search xe không, hay giữ filter để người dùng refresh trong phạm vi filter hiện tại? Plan đang đề xuất bám Export: clear filter khi bấm `LÀM MỚI`, nhưng có thể điều chỉnh nếu vận hành muốn giữ.
