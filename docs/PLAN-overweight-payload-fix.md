# Plan: Sửa Logic Trọng Tải Cho Phép (TTCP) & Bổ Sung Test Case Tách Tải Quá Tải

## 1. Bối cảnh & Định nghĩa chuẩn về Tải Trọng Cho Phép
Theo quy định kiểm định an toàn kỹ thuật phương tiện giao thông đường bộ:
- **Tải trọng cho phép của xe (`TtcpWeight`)**: Là khối lượng **HÀNG HÓA** tối đa mà xe/rơ moóc được phép chở theo thiết kế và giấy kiểm định. **Không bao gồm trọng lượng bản thân xe (Tare) và người trên cabin**.
- **Ngưỡng TTCP 10% (`Ttcp10WeightSnapshot`)**: `= TtcpWeight * 1.10` (Khối lượng hàng hóa tối đa cho phép + 10% dung sai).
- **Khối lượng hàng hóa thực tế (`NetWeight`)**: `= Weight2 (Tổng trọng lượng xe + hàng) - Weight1 (TL bản thân xe)`.
- **Khối lượng quá tải (`OverweightAmount`)**: `= NetWeight - Ttcp10WeightSnapshot` (Lượng hàng chở vượt quá ngưỡng 10% cho phép).
- **Khối lượng hàng tối đa cho phép của Phiếu 1 khi Tách tải**: `= Ttcp10WeightSnapshot` (Khối lượng hàng hóa của Phiếu 1 không vượt quá ngưỡng hàng hóa 10% cho phép).

---

## 2. Rà soát & Sửa đổi Logic Mã Nguồn (`WeighingSessionOverweightService.cs`)

### A. Sửa công thức Quá tải & Ngưỡng Tách tải
1. **`RefreshSessionOverweightState` (Hàm tính Khối lượng Quá tải)**:
   - *Code cũ (Sai)*: `overweightAmount = session.Weight2 - session.Ttcp10WeightSnapshot` (Lấy Gross Weight trừ Net Cargo Limit).
   - *Code mới (Đúng)*: `overweightAmount = session.NetWeight - session.Ttcp10WeightSnapshot` (Lấy Net Cargo Weight trừ Net Cargo Limit).

2. **`BuildSplitPlan` (Hàm lập Phương án Tách tải cho Phiếu 1)**:
   - *Code cũ (Sai)*: `target = session.Ttcp10WeightSnapshot - session.Weight1` (Trừ tiếp Tare Weight khỏi Cargo Limit).
   - *Code mới (Đúng)*: `target = session.Ttcp10WeightSnapshot` (Phiếu 1 được chở tối đa bằng đúng hạn mức hàng hóa 10% cho phép).

---

## 3. Ma trận Test Case Bao phủ Mọi Trường hợp Quá Tải & Tách Tải

Bộ Unit Test trong file [`WeighingSessionOverweightServiceTests.cs`](file:///g:/Source-code/pmcan_C%23/tests/StationApp.Application.Tests/WeighingSessionOverweightServiceTests.cs) sẽ được cập nhật và bổ sung các kịch bản sau:

| STT | Kịch bản Test Case | Điều kiện Hàng hóa thực xuất ($W_{net}$) | Kết quả Mong đợi Quá tải (`OverweightAmount`) | Kết quả Mong đợi Phương án Tách tải (`BuildSplitPlan`) |
|---|---|---|---|---|
| **1** | **Không quá tải** | $W_{net} \le P_{allowed10}$ (Ví dụ: $30,000 \le 33,000$ kg) | `IsOverweight = false`, `OverweightAmount = 0` | Không cần tách tải (`NOT_APPLICABLE`). |
| **2** | **Quá tải dưới 1 lần ngưỡng TTCP** (Nhẹ, tách được 2 phiếu đều hợp lệ) | $P_{allowed10} < W_{net} \le 2 \times P_{allowed10}$ (Ví dụ: $W_{net} = 36,000$ kg vs $P_{allowed10} = 33,000$ kg) | `IsOverweight = true`, `OverweightAmount = 3,000` kg | Tách thành 2 phiếu: Phiếu 1 ($W_{net1} \le 33,000$ kg) và Phiếu 2 ($W_{net2} \le 33,000$ kg). **Cả 2 phiếu đều nằm trong ngưỡng hợp lệ 10%**. |
| **3** | **Quá tải đúng bằng 2 lần ngưỡng TTCP** (Ranh giới tối đa 2 phiếu hợp lệ) | $W_{net} = 2 \times P_{allowed10}$ (Ví dụ: $W_{net} = 66,000$ kg vs $P_{allowed10} = 33,000$ kg) | `IsOverweight = true`, `OverweightAmount = 33,000` kg | Tách thành 2 phiếu: Phiếu 1 = $33,000$ kg, Phiếu 2 = $33,000$ kg (Cả 2 phiếu vừa đúng hạn mức tối đa). |
| **4** | **Quá tải nhiều hơn 1 lần ngưỡng TTCP** (Rất nặng, $> 2 \times P_{allowed10}$) | $W_{net} > 2 \times P_{allowed10}$ (Ví dụ: $W_{net} = 75,000$ kg vs $P_{allowed10} = 33,000$ kg) | `IsOverweight = true`, `OverweightAmount = 42,000` kg | Tách thành 2 phiếu: Phiếu 1 = $33,000$ kg (tối đa), Phiếu 2 nhận phần dư = $42,000$ kg ($> 33,000$ kg, Phiếu 2 vẫn dư quá tải). |
| **5** | **Tách tải thủ công (Manual Override)** | Người dùng nhập số kg mong muốn cho Phiếu 1 (Ví dụ: $W_{net1} = 31,000$ kg) | `IsOverweight = true` | Phiếu 1 = $31,000$ kg, Phiếu 2 = $W_{net} - 31,000$ kg. Bảo toàn tổng khối lượng hàng. |
| **6** | **Hủy/Làm sạch phiếu tách cũ khi Reallocate** | Cập nhật lại phân bổ khi phiên cân đã xác nhận tách | Trạng thái chuyển về `NOT_APPLICABLE` | Xóa logic (Soft delete) các chứng từ tách phụ (`SplitDerived`). |

---

## 4. Kế hoạch Thực thi & Kiểm thử

1. **Cập nhật Mã Nguồn Core Logic**:
   - Chỉnh sửa `RefreshSessionOverweightState` và `BuildSplitPlan` trong [`WeighingSessionOverweightService.cs`](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/Services/WeighingSessionOverweightService.cs).
2. **Cập nhật & Bổ sung Unit Tests**:
   - Sửa và thêm đầy đủ test cases 1..6 trong [`WeighingSessionOverweightServiceTests.cs`](file:///g:/Source-code/pmcan_C%23/tests/StationApp.Application.Tests/WeighingSessionOverweightServiceTests.cs).
3. **Chạy Kiểm thử Tự động**:
   - `dotnet test tests/StationApp.Application.Tests/StationApp.Application.Tests.csproj`
4. **Build Kiểm tra Toàn bộ Solution**:
   - `dotnet build`
