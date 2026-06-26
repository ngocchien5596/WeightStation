# Kế Hoạch Chi Tiết: Tối Ưu Hóa Trình Tự Phân Bổ Số Cân Và Tạo Phiếu Cân Khi Tách Tải (Overweight Split)

## 1. Tổng quan (Overview)

Khi trạm cân thực hiện **Tách tải (Split Load)** do xe bị quá tải trọng cho phép (TTCP 10%), hệ thống sẽ tự động phân bổ các dòng hàng (cut orders) vào các nhóm phiếu cân (ví dụ: Nhóm 1 và Nhóm 2).
Hiện tại, trong giao diện xem trước tách tải (Modal Tách tải):
- Toàn bộ các dòng thuộc cùng một Nhóm (SplitSequence) đều đang hiển thị trùng giá trị `Cân lần 1` và `Cân lần 2` lấy theo giá trị tổng của nhóm đó. Ví dụ: Dòng 1 (12,200 kg) và Dòng 2 (16,269 kg) cùng Nhóm 1 đều hiển thị Cân lần 1: 10,000, Cân lần 2: 38,469.
- Dưới DB, hệ thống chỉ tạo **02 Phiếu Cân (WeighTicket)** tương ứng với 2 nhóm, dẫn đến việc in ấn và đối chiếu thông tin của từng đơn hàng/khách hàng bị gộp chung hoặc sai lệch số cân thực nhận của từng mã.

**Đề xuất cải tiến:**
1. **Phân bổ tuần tự theo dòng (Sequential Display)**: Tính toán và hiển thị `Cân lần 1` và `Cân lần 2` lũy kế tuần tự cho từng dòng hàng trong bảng xem trước.
   - Dòng 1 (12,200 kg): Cân lần 1 = 10,000, Cân lần 2 = 22,200.
   - Dòng 2 (16,269 kg): Cân lần 1 = 22,200, Cân lần 2 = 38,469.
   - Dòng 3 (3,231 kg): Cân lần 1 = 38,469, Cân lần 2 = 41,700.
2. **Tạo Phiếu cân riêng biệt cho từng dòng (One WeighTicket per Line Item)**: Dưới Database, hệ thống sẽ tạo ra số lượng Phiếu Cân (`WeighTicket`) bằng đúng số lượng dòng hàng hiển thị trong grid (3 phiếu thay vì 2 phiếu trong ví dụ trên). Mỗi phiếu sẽ mang thông tin riêng về Mã cắt lệnh, Khách hàng, Sản phẩm, Số cân lần 1/lần 2 tuần tự chính xác của dòng hàng đó.

---

## 2. Loại dự án (Project Type)
- **Dự án**: WPF / BACKEND (C# / .NET)

---

## 3. Tiêu chí nghiệm thu (Success Criteria)

1. **Hiển thị chính xác trên UI**: Grid xem trước trên Modal Tách tải hiển thị `Cân lần 1` và `Cân lần 2` lũy kế tuần tự cho từng dòng hàng (như mô tả của người dùng).
2. **Lưu trữ DB chuẩn xác**: 
   - Tạo đúng số lượng `WeighTicket` tương ứng với số lượng dòng hàng thực tế sau khi phân tách.
   - Mỗi `WeighTicket` lưu đúng `CutOrderId`, `CustomerName`, `ProductName`, `Weight1`, `Weight2`, `NetWeight` và liên kết với `DeliveryTicket` qua `DeliveryTicketId`.
3. **Mã số phiếu hợp lệ**: Cấp đủ số lượng mã số phiếu cân tuần tự từ bộ sinh số cho từng phiếu được tạo ra.
4. **Unit Tests vượt qua**: Cập nhật và chạy thành công các unit test liên quan.

---

## 4. Phương án Thiết kế & Luồng dữ liệu

### A. Cải tiến Logic Xem trước (Preview)
Trong file `PreviewWeighingSessionOverweightSplitUseCase.cs`:
- Thay đổi cách duyệt và gán `w1`, `w2` cho các dòng xem trước:
  1. Khởi tạo biến chạy `currentStart = session.Weight1`.
  2. Duyệt qua từng Nhóm (Group) tăng dần theo `SplitSequence`.
  3. Trong mỗi Nhóm, duyệt qua từng Dòng hàng (Line) tăng dần theo `SequenceNo`.
  4. Tính toán:
     - `w1 = currentStart`
     - `w2 = currentStart + (Line.AllocatedWeight)` (nếu OUTBOUND, ngược lại trừ nếu INBOUND).
     - Cập nhật `currentStart = w2`.
  5. Trả về đối tượng `OverweightSplitPreviewLineItem` chứa `w1`, `w2` tương ứng cho dòng đó.

### B. Cải tiến Logic Lưu phiếu cân (Resolve/Confirm)
Trong file `ResolveWeighingSessionOverweightSplitUseCase.cs`:
- **Số lượng số phiếu cần cấp phát**:
  - Thay đổi số lượng vé cân yêu cầu từ `plan.Groups.Count` thành `plan.Groups.Sum(x => x.Lines.Count)` (tức là cấp số phiếu theo số lượng dòng hàng).
- **Vòng lặp tạo Phiếu Cân & Phiếu Giao hàng**:
  - Duyệt tuần tự qua từng `group` và từng `part` trong `group.Lines`:
    ```csharp
    var currentStartWeight = masterTicket.Weight1.GetValueOrDefault();
    foreach (var group in plan.Groups.OrderBy(x => x.SplitSequence))
    {
        foreach (var part in group.Lines.OrderBy(x => x.SequenceNo))
        {
            var line = lines.First(x => x.Id == part.SessionLineId);
            // Lấy thông tin chi tiết lineItem (từ lineLookup) để lấy đúng Khách hàng, Sản phẩm
            ...
            var splitTicket = BuildSplitWeighTicket(
                session,
                masterTicket,
                group,
                part, // Truyền part thay vì group
                currentStartWeight,
                nextTicketNumbers.Dequeue(),
                now,
                lineItem); // Truyền lineItem để copy đúng thông tin
            
            splitWeighTickets.Add(splitTicket);
            currentStartWeight = splitTicket.Weight2.GetValueOrDefault();
            
            // Tạo DeliveryTicket tương ứng
            var deliveryTicket = new DeliveryTicket { ... };
            splitTicket.DeliveryTicketId = deliveryTicket.Id;
            splitDeliveryTickets.Add(deliveryTicket);
        }
    }
    ```
- **Hàm `BuildSplitWeighTicket`**:
  - Cập nhật tham số nhận `OverweightSplitLinePlan linePlan` và `WeighingSessionLineItem lineItem`.
  - Thay vì lấy thông tin Khách hàng/Sản phẩm/Khối lượng từ `masterTicket` và `group`, ta lấy trực tiếp từ `lineItem` và `linePlan.AllocatedWeight`.

### C. Làm tròn số cân gợi ý về hàng chục (Lấy số hàng đơn vị là 0)
Trong file `WeighingSessionOverweightService.cs`, ở phương thức `BuildSuggestedFirstSplitWeight`:
1. **Thuật toán tạo ứng viên**: Sau khi tính toán `candidate = decimal.Round(ttcp10WeightSnapshot * (1m - factor), 3, MidpointRounding.AwayFromZero)`, thực hiện làm tròn số cân này về bội số của 10 (hàng chục):
   `candidate = decimal.Round(candidate / 10m, 0, MidpointRounding.AwayFromZero) * 10m;`
2. **Thuật toán fallback**: Tương tự, khi không tìm được ứng viên ngẫu nhiên đạt điều kiện, logic tính `fallbackWeight` cũng sẽ tìm một bội số của 10 ngẫu nhiên nằm trong khoảng `[integerLowerBound, integerUpperBound]`.

---

## 5. Phân rã nhiệm vụ (Task Breakdown)

### Phase 1: Logic & DTO changes (Backend Core)
| Task ID | Tên nhiệm vụ | Agent phụ trách | Kỹ năng liên quan | Độ ưu tiên | Phụ thuộc | INPUT -> OUTPUT -> VERIFY |
|---------|--------------|-----------------|-------------------|------------|-----------|---------------------------|
| T1.1 | Cập nhật preview logic cho grid | `performance-optimizer` | clean-code | P0 | Không | **INPUT**: File [PreviewWeighingSessionOverweightSplitUseCase](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/UseCases/WeighingSessionOverweightUseCases.cs).<br>**OUTPUT**: Sửa cách tính `w1` và `w2` tuần tự lũy kế cho từng dòng.<br>**VERIFY**: Chạy thử xem trước trên UI hiển thị chính xác các khoảng cân lũy kế. |
| T1.2 | Cập nhật logic lưu phiếu cân thực tế | `backend-specialist` | clean-code | P0 | T1.1 | **INPUT**: `ResolveWeighingSessionOverweightSplitUseCase` trong [WeighingSessionOverweightUseCases.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/UseCases/WeighingSessionOverweightUseCases.cs).<br>**OUTPUT**: Tạo phiếu cân riêng lẻ cho từng dòng hàng, phân bổ số phiếu và số cân tuần tự, liên kết với delivery ticket tương ứng.<br>**VERIFY**: Biên dịch dự án thành công. |
| T1.3 | Thay đổi logic gợi ý chia tải ngẫu nhiên để số cân có hàng đơn vị là 0 | `performance-optimizer` | clean-code | P0 | Không | **INPUT**: File [WeighingSessionOverweightService.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/Services/WeighingSessionOverweightService.cs).<br>**OUTPUT**: Thực hiện làm tròn số cân đề xuất ngẫu nhiên và số cân fallback về bội số của 10.<br>**VERIFY**: Unit tests cho hàm gợi ý đảm bảo số cân sinh ra luôn chia hết cho 10. |

### Phase 2: Tests & Validation
| Task ID | Tên nhiệm vụ | Agent phụ trách | Kỹ năng liên quan | Độ ưu tiên | Phụ thuộc | INPUT -> OUTPUT -> VERIFY |
|---------|--------------|-----------------|-------------------|------------|-----------|---------------------------|
| T2.1 | Cập nhật và sửa lỗi Unit Tests | `test-engineer` | testing-patterns | P1 | T1.2 | **INPUT**: File test [WeighingSessionOverweightServiceTests.cs](file:///g:/Source-code/pmcan_C%23/tests/StationApp.Application.Tests/WeighingSessionOverweightServiceTests.cs).<br>**OUTPUT**: Sửa các assertion kiểm tra số lượng và số cân của phiếu cân sau tách tải để khớp với logic mới.<br>**VERIFY**: Lệnh `dotnet test` chạy thành công không có lỗi. |

---

## 6. Kế hoạch xác thực (Verification Plan)

### Kiểm tra tự động
- Chạy unit test của dự án Application:
  ```powershell
  dotnet test tests\StationApp.Application.Tests
  ```

### Kiểm tra thủ công
1. Sử dụng một lượt cân thử nghiệm có nhiều dòng hàng (ví dụ: 2-3 dòng hàng) có tổng khối lượng vượt quá giới hạn tải trọng xe.
2. Mở Modal Tách tải trên UI.
3. Xác nhận hiển thị trong grid: `Cân lần 1` và `Cân lần 2` của từng dòng phải nối đuôi nhau tuần tự (ví dụ: dòng 1 kết thúc ở 22,200 thì dòng 2 bắt đầu từ 22,200).
4. Click xác nhận tách tải.
5. Truy vấn DB local (hoặc xem danh sách phiếu) kiểm tra:
   - Số lượng phiếu cân được sinh ra phải bằng số lượng dòng hàng.
   - Thông tin Khách hàng, Sản phẩm và Số cân trên từng phiếu cân phải khớp chính xác với dòng hàng đó.
