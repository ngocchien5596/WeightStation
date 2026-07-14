# Kế hoạch: Giới hạn trọng lượng Hoàn theo chuyến gần nhất cho mỏ đá và mỏ sét

## 1. Bối cảnh

Ở luồng Cân mỏ đá và Cân mỏ sét, người dùng có thể đánh dấu một lượt/chuyến xe là `Hoàn`. Hiện tại hệ thống đang dùng trực tiếp trọng lượng hàng thực cân của chuyến Hoàn để cộng vào KPI/cột `Hoàn` và trừ khỏi `Thực nhập`.

Nghiệp vụ mới cần chốt:

- Vẫn cho cân và lưu số cân thực tế của chuyến Hoàn như bình thường.
- Khi ghi nhận vào KPI/báo cáo, trọng lượng Hoàn tối đa chỉ bằng trọng lượng hàng của chuyến xe gần nhất trước đó của cùng xe.
- Nếu chuyến Hoàn cân thực tế thấp hơn hoặc bằng chuyến gần nhất, ghi nhận đúng số thực cân.
- Nếu chuyến Hoàn cân thực tế lớn hơn chuyến gần nhất, chỉ ghi nhận bằng trọng lượng hàng chuyến gần nhất và giải thích rõ trong modal xác nhận Hoàn.

Ví dụ:

```text
Xe 01 - chuyến nhập gần nhất: 20.000 tấn
Xe 01 - chuyến Hoàn thực cân: 25.000 tấn

TL hàng thực cân vẫn là 25.000 tấn.
TL Hoàn ghi nhận cho KPI/báo cáo là 20.000 tấn.
```

## 2. Nguyên tắc triển khai

- Không bắt lỗi/chặn người dùng khi trọng lượng Hoàn thực cân lớn hơn chuyến gần nhất.
- Sau khi người dùng xác nhận Hoàn, hệ thống phải cập nhật lại trọng lượng Hoàn cho phù hợp:
  - Nếu `TL hàng hoàn thực cân <= TL hàng chuyến gần nhất`: giữ đúng số thực cân.
  - Nếu `TL hàng hoàn thực cân > TL hàng chuyến gần nhất`: cập nhật số Hoàn được lưu/ghi nhận bằng `TL hàng chuyến gần nhất`.
- Không hiểu đây là logic chỉ tính động ở báo cáo. Dữ liệu sau khi xác nhận Hoàn phải phản ánh số Hoàn đã được giới hạn để các màn KPI/báo cáo/lịch sử dùng thống nhất.
- Modal xác nhận Hoàn phải hiển thị rõ số thực cân, số chuyến gần nhất và số sẽ được ghi nhận sau xác nhận.
- Công thức thống nhất:

```text
TL Hoàn sau xác nhận = Min(TL hàng thực cân của chuyến Hoàn, TL hàng chuyến gần nhất trước đó của cùng xe)
Thực nhập = Hàng - Hoàn ghi nhận
```

## 3. Hiện trạng code liên quan

### Mỏ đá

- Checkbox Hoàn ở `CrusherWeighingView`.
- Command xử lý ở `CrusherWeighingViewModel.ToggleReturnedBrokenTripAsync`.
- Use case lưu flag ở `ToggleCrusherReturnedBrokenTripUseCase`.
- Báo cáo mỏ đá xử lý tại `CrusherInboundReportServices`.
- KPI Trang chủ mỏ đá tính ở `DashboardViewModel`, hiện đang cộng nguyên `NetWeight` của các session Hoàn.

### Mỏ sét

- Checkbox Hoàn ở `ClayWeighingView`.
- Command xử lý ở `ClayWeighingViewModel.ToggleReturnedBrokenTripAsync`.
- Use case lưu flag ở `ToggleClayReturnedBrokenTripUseCase`.
- Báo cáo mỏ sét xử lý tại `ClayInboundReportServices`.
- Hoàn ở mỏ sét lưu trên `WeighingSessionLine.IsReturnedBrokenTrip`, trọng lượng hàng lấy theo `ActualAllocatedWeight`/`NetWeight` của chuyến.

### Điểm đang cần sửa

- `MergeReturnedBrokenTrips` của báo cáo mỏ đá và mỏ sét hiện cộng thẳng trọng lượng Hoàn đang lưu vào dòng chuyến gần nhất.
- Modal xác nhận Hoàn hiện chỉ hỏi xác nhận, chưa giải thích trường hợp bị giới hạn.
- KPI mỏ đá hiện cộng thẳng `NetWeight` của chuyến Hoàn.

## 4. Định nghĩa "chuyến xe gần nhất"

Chuyến gần nhất là chuyến thỏa các điều kiện:

- Cùng xe nội bộ hoặc cùng biển số đang dùng trong luồng đó.
- Phát sinh trước thời điểm hoàn tất của chuyến Hoàn.
- Không bị xóa/hủy.
- Đã hoàn thành cân lần 2 và có trọng lượng hàng dương.
- Không phải chuyến Hoàn.

Riêng với mỏ sét:

- Chuyến gần nhất phải nằm trong cùng một tàu/cùng `CutOrderId`.
- Không lấy chuyến của tàu khác để giới hạn TL Hoàn.

Nếu chuyến Hoàn là chuyến đầu tiên của xe và không tìm thấy chuyến gần nhất:

- Modal phải hiển thị giải thích:

```text
Không có dữ liệu chuyến xe gần nhất trước đó của xe này. Vui lòng kiểm tra lại.
```

- Không thực hiện cập nhật TL Hoàn theo chuyến gần nhất vì không có dữ liệu đối chiếu.
- Không nên ghi nhận Hoàn trong trường hợp này cho đến khi người dùng kiểm tra lại dữ liệu.

## 5. Task chi tiết

### Task 1: Tạo helper tính TL Hoàn sau xác nhận

**Mô tả:** Tách công thức `Min(TL hoàn thực cân, TL chuyến gần nhất)` thành helper dùng chung cho modal, use case cập nhật dữ liệu và các điểm tổng hợp số liệu.

**Acceptance criteria:**

- [ ] Có helper nhận `returnedWeightTon` và `previousTripWeightTon`.
- [ ] Nếu có chuyến gần nhất, kết quả không vượt quá trọng lượng chuyến gần nhất.
- [ ] Nếu không có chuyến gần nhất, trả trạng thái không đủ dữ liệu để modal cảnh báo người dùng kiểm tra lại.
- [ ] Helper trả được cả thông tin có bị giới hạn hay không để modal/audit log giải thích.

**Files dự kiến:**

- `src/StationApp.Application/...` hoặc service/helper phù hợp đang dùng cho báo cáo.

**Verification:**

- [ ] Unit/helper test hoặc test logic trực tiếp qua service.

### Task 2: Bổ sung dữ liệu đối chiếu và cập nhật TL Hoàn mỏ đá

**Mô tả:** Trước khi hiện confirm ở `CrusherWeighingViewModel`, tìm chuyến gần nhất của cùng xe để tính trước `TL Hoàn sau xác nhận`. Sau khi người dùng xác nhận, use case phải cập nhật lại TL Hoàn được lưu cho lượt cân mỏ đá theo số đã tính.

**Acceptance criteria:**

- [ ] Khi tick Hoàn, modal hiển thị TL hàng thực cân của lượt đang chọn.
- [ ] Modal hiển thị TL hàng chuyến gần nhất nếu tìm thấy.
- [ ] Nếu `TL hoàn thực cân > TL chuyến gần nhất`, modal giải thích rõ:

```text
Trọng lượng hoàn thực cân là 25.000 tấn, lớn hơn trọng lượng hàng của chuyến gần nhất trước đó là 20.000 tấn.
Hệ thống sẽ ghi nhận Hoàn là 20.000 tấn để không vượt quá lượng hàng của chuyến gần nhất.
```

- [ ] Nếu `TL hoàn thực cân <= TL chuyến gần nhất`, modal nói sẽ ghi nhận theo số thực cân.
- [ ] Sau khi xác nhận, nếu bị giới hạn, dữ liệu TL Hoàn lưu lại bằng TL chuyến gần nhất chứ không giữ TL thực cân lớn hơn.
- [ ] Bỏ đánh dấu Hoàn giữ nội dung confirm ngắn như hiện tại.

**Files dự kiến:**

- `src/StationApp.UI/ViewModels/CrusherWeighingViewModel.cs`
- `src/StationApp.Application/UseCases/ToggleCrusherReturnedBrokenTripUseCase.cs`
- Có thể cần thêm query/helper trong repository/service nếu danh sách hiện có chưa đủ dữ liệu.

**Verification:**

- [ ] Manual: tick Hoàn với case thực cân nhỏ hơn chuyến gần nhất.
- [ ] Manual: tick Hoàn với case thực cân lớn hơn chuyến gần nhất.

### Task 3: Bổ sung dữ liệu đối chiếu và cập nhật TL Hoàn mỏ sét

**Mô tả:** Làm tương tự mỏ đá cho `ClayWeighingViewModel`, nhưng lưu ý mỏ sét là theo `SessionLineId` và chuyến thuộc tàu. Sau khi người dùng xác nhận, use case phải cập nhật lại TL Hoàn được lưu/ghi nhận cho line mỏ sét theo số đã giới hạn.

**Acceptance criteria:**

- [ ] Modal Hoàn của mỏ sét hiển thị TL hàng thực cân của chuyến đang chọn.
- [ ] Modal hiển thị TL hàng chuyến gần nhất trước đó của cùng xe, trong cùng tàu.
- [ ] Nếu bị giới hạn, modal giải thích vì sao `Hoàn ghi nhận` không bằng `TL hoàn thực cân`.
- [ ] Sau khi xác nhận, nếu bị giới hạn, `ActualAllocatedWeight` hoặc trường trọng lượng Hoàn tương ứng của chuyến mỏ sét được cập nhật về TL chuyến gần nhất.
- [ ] Không ảnh hưởng thao tác selected item của grid.

**Files dự kiến:**

- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`
- `src/StationApp.Application/UseCases/ClayVesselFlowUseCases.cs`
- Có thể cần thêm query/helper trong repository/service nếu danh sách hiện có chưa đủ dữ liệu.

**Verification:**

- [ ] Manual: tick Hoàn trên chuyến mỏ sét đã cân xong.
- [ ] Manual: đổi selected row rồi tick Hoàn, đảm bảo không cập nhật nhầm dòng.

### Task 4: Rà soát báo cáo mỏ đá sau khi TL Hoàn đã được cập nhật

**Mô tả:** Vì TL Hoàn đã được cập nhật ngay khi xác nhận, báo cáo mỏ đá ưu tiên dùng trọng lượng Hoàn đã lưu. Tuy nhiên vẫn cần thêm guard ở `MergeReturnedBrokenTrips` để tổng Hoàn không vượt Hàng nếu có dữ liệu cũ hoặc dữ liệu bất thường.

**Acceptance criteria:**

- [ ] Cột `Hoàn (tấn)` không vượt quá cột `Hàng (tấn)` của dòng chuyến được merge, kể cả dữ liệu cũ.
- [ ] `Thực nhập (tấn)` không âm.
- [ ] Nếu nhiều chuyến Hoàn liên tiếp cho cùng một chuyến gần nhất, tổng Hoàn ghi nhận không vượt quá Hàng của chuyến đó.
- [ ] Tổng `Hoàn` và `Thực nhập` tính theo số Hoàn ghi nhận.

**Files dự kiến:**

- `src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs`

**Verification:**

- [ ] Test dữ liệu: Hàng 20, Hoàn 25 => report Hoàn 20, Thực nhập 0.
- [ ] Test dữ liệu: Hàng 20, Hoàn 12 => report Hoàn 12, Thực nhập 8.

### Task 5: Rà soát báo cáo mỏ sét sau khi TL Hoàn đã được cập nhật

**Mô tả:** Vì TL Hoàn đã được cập nhật ngay khi xác nhận, báo cáo mỏ sét ưu tiên dùng trọng lượng Hoàn đã lưu. Tuy nhiên vẫn cần guard ở `MergeReturnedBrokenTrips` để tổng Hoàn không vượt Hàng nếu có dữ liệu cũ hoặc bất thường.

**Acceptance criteria:**

- [ ] Cột `Hoàn (tấn)` không vượt quá cột `Hàng (tấn)` của dòng chuyến gần nhất.
- [ ] `Thực nhập (tấn)` không âm.
- [ ] Tổng cộng báo cáo dùng số Hoàn ghi nhận.
- [ ] Filter theo tàu/sản phẩm/đơn vị vận chuyển vẫn giữ nguyên hành vi hiện tại.

**Files dự kiến:**

- `src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs`

**Verification:**

- [ ] Manual preview báo cáo mỏ sét với chuyến Hoàn lớn hơn chuyến gần nhất.
- [ ] Export báo cáo nếu có, kiểm tra số liệu giống preview.

### Task 6: Rà soát KPI mỏ đá trên Trang chủ

**Mô tả:** KPI `Hoàn` và `Thực nhập` mỏ đá trên dashboard phải dùng TL Hoàn đã cập nhật sau xác nhận. Nếu dashboard đọc dữ liệu cũ chưa được giới hạn, cần dùng cùng guard để không hiển thị Hoàn vượt chuyến gần nhất.

**Acceptance criteria:**

- [ ] `Hoàn` trên dashboard không vượt quá lượng hàng của chuyến gần nhất tương ứng, kể cả dữ liệu cũ.
- [ ] `Thực nhập = Nhập - Hoàn ghi nhận`.
- [ ] Không làm thay đổi KPI các luồng khác.

**Files dự kiến:**

- `src/StationApp.UI/ViewModels/DashboardViewModel.cs`
- Có thể cần query dữ liệu completed sessions theo thứ tự thời gian đầy đủ trong ngày.

**Verification:**

- [ ] Manual: ngày có 1 chuyến nhập 20 tấn và 1 chuyến Hoàn 25 tấn, dashboard Hoàn = 20 tấn.

### Task 7: Audit log và lịch sử chỉnh sửa

**Mô tả:** Khi tick Hoàn, audit log phải ghi thêm số liệu đối chiếu để sau này giải thích được vì sao TL Hoàn đã được cập nhật về số nhỏ hơn TL thực cân.

**Acceptance criteria:**

- [ ] Audit log mỏ đá có `ReturnedActualWeight`, `PreviousTripWeight`, `ReturnedRecognizedWeight`, và giá trị trước/sau khi cập nhật TL Hoàn.
- [ ] Audit log mỏ sét có các trường tương tự.
- [ ] Màn Lịch sử chỉnh sửa không lỗi khi đọc thêm field mới.
- [ ] Nếu có hiển thị chi tiết, nội dung tiếng Việt không lỗi encoding.

**Files dự kiến:**

- `src/StationApp.Application/UseCases/ToggleCrusherReturnedBrokenTripUseCase.cs`
- `src/StationApp.Application/UseCases/ClayVesselFlowUseCases.cs`
- `src/StationApp.UI/ViewModels/WeighingSessionEditHistoryViewModel.cs` nếu cần hiển thị thêm.

**Verification:**

- [ ] Tick Hoàn và kiểm tra bản ghi lịch sử chỉnh sửa.

## 6. Checkpoint sau khi code

- [ ] `dotnet build src\StationApp.UI\StationApp.UI.csproj --no-restore` thành công.
- [ ] Tick/bỏ tick Hoàn ở mỏ đá không lỗi selected item.
- [ ] Tick/bỏ tick Hoàn ở mỏ sét không lỗi selected item.
- [ ] Modal confirm hiển thị giải thích khi Hoàn thực cân lớn hơn chuyến gần nhất.
- [ ] Báo cáo mỏ đá preview/export tính đúng `Hàng`, `Hoàn`, `Thực nhập`.
- [ ] Báo cáo mỏ sét preview/export tính đúng `Hàng`, `Hoàn`, `Thực nhập`.
- [ ] Dashboard mỏ đá tính đúng KPI `Nhập`, `Hoàn`, `Thực nhập`.

## 7. Rủi ro và lưu ý

| Rủi ro | Mức độ | Cách xử lý |
| --- | --- | --- |
| Không tìm thấy chuyến gần nhất do filter ngày/tàu | Trung bình | Tìm chuyến gần nhất theo dữ liệu trước thời điểm chuyến Hoàn, không chỉ theo filter báo cáo nếu cần nghiệp vụ chính xác. |
| Nhiều chuyến Hoàn liên tiếp làm tổng Hoàn vượt Hàng | Cao | Khi merge, giới hạn theo phần còn lại: `min(Hoàn thực cân, Hàng - Hoàn đã ghi nhận)`. |
| Mỏ đá dùng session-level, mỏ sét dùng line-level | Trung bình | Tách adapter dữ liệu từng luồng nhưng dùng chung công thức tính. |
| Modal confirm cần query thêm dữ liệu gây chậm | Thấp | Ưu tiên dùng danh sách đang có trên grid; chỉ query repo khi danh sách chưa đủ chuyến trước đó. |
| Encoding tiếng Việt ở các message đang có lỗi | Trung bình | Khi sửa các message liên quan Hoàn, ghi lại bằng Unicode/UTF-8 đúng, không vá kiểu từng chữ rời. |

## 8. Câu hỏi cần chốt trước khi code

- [x] Nếu chuyến Hoàn là chuyến đầu tiên của xe, không có chuyến gần nhất, modal giải thích: `Không có dữ liệu chuyến xe gần nhất trước đó của xe này. Vui lòng kiểm tra lại.`
- [x] Với mỏ sét, chuyến gần nhất phải được giới hạn trong cùng một tàu/cùng `CutOrderId`.
