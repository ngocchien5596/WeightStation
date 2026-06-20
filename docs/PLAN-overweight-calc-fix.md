# Kế hoạch sửa đổi công thức tính toán quá tải (Overweight)

Kế hoạch này mô tả các thay đổi cần thiết để điều chỉnh lại công thức tính toán quá tải của hệ thống cân từ việc so sánh khối lượng tịnh (`NetWeight`) sang việc so sánh tổng khối lượng xe cân lần 2 (`Weight2`) với ngưỡng tải trọng cho phép (`Ttcp10WeightSnapshot`).

## User Review Required

> [!IMPORTANT]
> - **Ảnh hưởng tới thuật toán chia tải (Splitting)**: Khi chuyển sang sử dụng `Weight2` (tổng tải trọng cân lần 2), thuật toán chia tải trọng cho các phiếu split vẫn phải đảm bảo tính toán trên khối lượng hàng thực tế cần vận chuyển (`NetWeight`), nhưng giới hạn tải trọng tịnh của từng phiếu split (tối đa) sẽ được xác định bằng: `Ttcp10WeightSnapshot` (tổng giới hạn xe) - `Weight1` (xác xe).
> - **Đồng bộ Unit Tests**: Cần cập nhật lại giá trị giả lập trong các test cases tương ứng ở `WeighingSessionOverweightServiceTests` để đảm bảo logic test khớp với công thức mới.

## Open Questions

> [!NOTE]
> Không có câu hỏi mở nào tồn tại, yêu cầu nghiệp vụ đã rất rõ ràng:
> `overweightAmount = Weight2 - Ttcp10WeightSnapshot`

## Proposed Changes

---

### Core Services

#### [MODIFY] [WeighingSessionOverweightService.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/Services/WeighingSessionOverweightService.cs)

- **Cập nhật logic `RefreshSessionOverweightState`**:
  - Thay đổi công thức tính `overweightAmount` (Dòng 52-55):
    ```csharp
    var overweightAmount = decimal.Round(
        session.Weight2!.Value - session.Ttcp10WeightSnapshot!.Value,
        3,
        MidpointRounding.AwayFromZero);
    ```
- **Cập nhật logic `BuildSplitPlan`**:
  - Thêm kiểm tra điều kiện `session.Weight1.HasValue` tại dòng validation ban đầu.
  - Cập nhật cách tính `target` để đại diện cho tải trọng tịnh tối đa cho phép trên mỗi phiếu:
    ```csharp
    var target = session.Ttcp10WeightSnapshot.Value - session.Weight1!.Value;
    ```
  - Đảm bảo `target > 0m`, nếu không sẽ ném ngoại lệ tương ứng.

---

### Unit Tests

#### [MODIFY] [WeighingSessionOverweightServiceTests.cs](file:///g:/Source-code/pmcan_C%23/tests/StationApp.Application.Tests/WeighingSessionOverweightServiceTests.cs)

- **Cập nhật Helper `CreateReadySession`**:
  - Cho phép gán `Weight2` động theo công thức: `Weight2 = Weight1 + netWeight` thay vì hardcode `32_000m`.
- **Cập nhật các Test Cases**:
  - Điều chỉnh tham số đầu vào `ttcp10` trong lời gọi `CreateReadySession` để phản ánh đúng tổng tải trọng giới hạn xe thay vì chỉ là giới hạn khối lượng tịnh như trước:
    - `RefreshSessionOverweightState_SetsPendingWhenNetWeightExceedsThreshold` -> Đặt `ttcp10: 32_000m` (với netWeight: 22_500m, Weight1: 10_000m -> Weight2: 32_500m -> quá tải 500m).
    - `RefreshSessionOverweightState_SetsNotApplicableWhenNetWeightDoesNotExceedThreshold` -> Đặt `ttcp10: 32_000m` (với netWeight: 21_500m -> Weight2: 31_500m -> không quá tải).
    - `RefreshSessionOverweightState_InvalidatesSplitDocuments_WhenResolvedSessionIsReallocated` -> Đặt `ttcp10: 32_000m`.
    - `BuildSplitPlan_SplitsIntoExactlyTwoTickets_AndPreservesBagCounts` -> Đặt `ttcp10: 32_000m`.
    - `BuildSplitPlan_SystemSuggestion_UsesRandomFactorWithinConfiguredRange` -> Đặt `ttcp10: 37_500m`.
    - `BuildSplitPlan_ManualOverride_UsesRequestedWeight_AndHidesRandomFactor` -> Đặt `ttcp10: 37_500m`.
    - `BuildSplitPlan_ThrowsWhenSecondTicketStillExceedsThreshold` -> Đặt `ttcp10: 32_000m` (với netWeight: 44_500m -> Weight2: 54_500m, vượt quá khả năng chứa tối đa 44_000m của 2 lượt cân).
    - `ResolveWeighingSessionOverweightNoSplit_SetsConfirmedStatus` -> Đặt `ttcp10: 32_000m`.
    - `ResolveWeighingSessionOverweightSplit_AssignsDistinctTicketAndDeliveryNumbers` -> Đặt `ttcp10: 37_500m`.
    - `CanMoveToOutYard_RequiresResolvedOverweightState` -> Đặt `ttcp10: 32_000m`.

## Verification Plan

### Automated Tests
- Chạy lệnh test ứng dụng:
  `dotnet test tests/StationApp.Application.Tests/StationApp.Application.Tests.csproj`

### Manual Verification
- Thực hiện build lại chương trình để đảm bảo không bị lỗi biên dịch:
  `dotnet build`
