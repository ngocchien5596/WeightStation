# KẾ HOẠCH CẬP NHẬT LOGIC XỬ LÝ QUÁ TẢI (OVERWEIGHT) - WEIGHING SESSION

## MỤC TIÊU
Cập nhật lại toàn bộ logic tách quá tải (Overweight Splitting) trong dự án StationApp, loại bỏ logic tách theo `vehicle_registration` cũ và áp dụng thống nhất theo mô hình `weighing_session`. Việc tách tải chỉ diễn ra ở lớp chứng từ (`weigh_tickets` và `delivery_tickets`), không tác động đến Session hay Line gốc.

---

## A. REVIEW HIỆN TRẠNG
*   **Logic quá tải cũ:** Vẫn còn tàn dư logic tính toán quá tải bám vào `vehicle_registration` trong các UseCase cũ, một số chỗ trong `WeighingViewModel` vẫn còn liên kết đến logic này.
*   **DB hiện tại:** Đã có cơ bản bảng `weighing_sessions` và `weighing_session_lines` tuy nhiên các trường liên quan đến Overweight mới chỉ dừng lại ở mức cơ bản, thiếu các trường Tracking (Resolved At/By, Status), và thiếu field cấu hình `OverweightSplitStepWeight`.
*   **Phạm vi:** Chỗ nào còn sử dụng `CurrentPrimaryWeighTicketId` hay `HasOverweightCase` của `vehicle_registrations` cần được cách ly và chuyển hướng sang sử dụng trạng thái của `weighing_sessions`.

---

## B. DB UPGRADE DESIGN
Cần tạo các bản Migration Entity Framework:

1.  **Bảng `weighing_sessions`**
    *   `[NEW]` `Ttcp10WeightSnapshot` (decimal?): Lưu trữ giá trị cấu hình ngưỡng TTCP 10% tại thời điểm vận hành.
    *   `[NEW]` `IsOverweight` (bool): Đánh dấu session bị quá tải.
    *   `[NEW]` `OverweightAmount` (decimal): Khối lượng vượt ngưỡng.
    *   `[NEW]` `OverweightResolutionStatus` (Enum/int): Trạng thái xử lý quá tải (NOT_APPLICABLE, PENDING, SPLIT_CONFIRMED, NO_SPLIT_CONFIRMED).
    *   `[NEW]` `OverweightResolvedAt` (DateTime?): Thời gian xử lý.
    *   `[NEW]` `OverweightResolvedBy` (string?): Người xử lý.

2.  **Bảng `delivery_tickets`**
    *   `[NEW]` `AllocatedWeight` (decimal?): Trọng lượng được phân bổ cho phiếu này (khi split).
    *   `[NEW]` `AllocatedBagCount` (int?): Số bao được phân bổ.

3.  **Bảng `weigh_tickets`**
    *   `[RENAME]` `TtcpWeightSnapshot` -> `Ttcp10WeightSnapshot`.

4.  **Bảng `AppConfig`** (Dữ liệu Seed)
    *   Thêm Key: `OverweightSplitStepWeight` (Type: string/decimal).
    *   Giá trị Default: Cần định nghĩa một Const `DEFAULT_OVERWEIGHT_SPLIT_STEP_WEIGHT` (VD: 0.0025 tương đương 0.25%).

5.  **Nguyên tắc KHÔNG:**
    *   TUYỆT ĐỐI không tạo thêm các bảng như `weighing_session_document_groups` hay `weighing_session_document_group_lines`.

---

## C. STATUS MODEL DESIGN & D. VIETNAMESE UI MAPPING

### 1. `weighing_sessions.SessionStatus`
| DB Value | UI (Tiếng Việt) |
| :--- | :--- |
| `PENDING_WEIGHT1` | Chờ cân lần 1 |
| `PENDING_WEIGHT2` | Chờ cân lần 2 |
| `ALLOCATION_PENDING` | Chờ phân bổ |
| `READY_TO_COMPLETE`| Sẵn sàng hoàn tất |
| `COMPLETED` | Đã hoàn tất |
| `CANCELLED` | Đã hủy |

### 2. `vehicle_registrations.RegistrationStatus`
| DB Value | UI (Tiếng Việt) |
| :--- | :--- |
| `REGISTERED` | Đã đăng ký |
| `IN_SESSION` | Đang trong lượt cân |
| `COMPLETED` | Đã hoàn tất |
| `CANCELLED` | Đã hủy |

### 3. `weighing_session_lines.LineStatus`
| DB Value | UI (Tiếng Việt) |
| :--- | :--- |
| `PENDING` | Chưa phân bổ |
| `ALLOCATED` | Đã phân bổ |
| `CANCELLED` | Đã hủy |

### 4. `OverweightResolutionStatus`
| DB Value | UI (Tiếng Việt) |
| :--- | :--- |
| `NOT_APPLICABLE` | Không quá tải |
| `PENDING` | Chờ xử lý quá tải |
| `SPLIT_CONFIRMED` | Đã xác nhận tách tải |
| `NO_SPLIT_CONFIRMED` | Đã xác nhận không tách |

### 5. `weigh_tickets.RecordRole`
| DB Value | UI (Tiếng Việt) |
| :--- | :--- |
| `MASTER_SESSION` | Phiếu cân tổng |
| `SPLIT_DERIVED` | Phiếu cân tách tải |

### 6. `delivery_tickets.RecordRole`
| DB Value | UI (Tiếng Việt) |
| :--- | :--- |
| `NORMAL` | Phiếu giao nhận thường |
| `SPLIT_DERIVED` | Phiếu giao nhận tách tải |

---

## E. OVERWEIGHT FLOW DESIGN

### Thời điểm Check Quá Tải
*   Chỉ kiểm tra khi Session thỏa mãn: Đã có `Weight1`, `Weight2`, `NetWeight` và tất cả các line đã phân bổ xong (LineStatus = ALLOCATED).
*   **Rule:** `Session.NetWeight > Session.Ttcp10WeightSnapshot`.
*   Nếu `False`: Đặt `IsOverweight = false`, `OverweightResolutionStatus = NOT_APPLICABLE`.
*   Nếu `True`: Đặt `IsOverweight = true`, `OverweightAmount = NetWeight - Ttcp10WeightSnapshot`, `OverweightResolutionStatus = PENDING`.

### UX Xử lý Quá Tải & Modal
*   Không auto-split sau cân lần 2.
*   Khi `OverweightResolutionStatus == PENDING`: Hiển thị cảnh báo trên màn hình, Enable nút **[Xử lý quá tải]**.
*   Khi bấm nút: Mở Modal hiển thị thông tin TTCP 10%, NetWeight, Khối lượng quá tải, `OverweightSplitStepWeight`, cùng Preview 2 phiếu sẽ split.
*   3 lựa chọn:
    1.  **Tách quá tải:** Chấp nhận preview (có thể edit), validate -> Sinh Split Docs -> Đặt status `SPLIT_CONFIRMED`.
    2.  **Không tách:** Không sinh split docs -> Đặt status `NO_SPLIT_CONFIRMED`.
    3.  **Để sau:** Giữ nguyên trạng thái `PENDING`.

### Out Yard Gating (Chuyển xe ra)
*   **KHÓA:** Nút "Chuyển xe ra" bị mờ (disabled) nếu `OverweightResolutionStatus == PENDING`.
*   **MỞ:** Chỉ khi Status là `NOT_APPLICABLE`, `SPLIT_CONFIRMED`, hoặc `NO_SPLIT_CONFIRMED` và Session đang ở trạng thái `READY_TO_COMPLETE`.

### Reset Logic (Invalidation Flow)
*   Nếu User thay đổi số cân (`Weight1`, `Weight2`) hoặc chỉnh sửa thông tin phân bổ line sau khi đã có quyết định xử lý quá tải (`SPLIT_CONFIRMED` hoặc `NO_SPLIT_CONFIRMED`):
    1.  Vô hiệu hóa kết quả trước đó.
    2.  Soft Delete (Xóa mềm) toàn bộ Split Docs (Weigh Tickets và Delivery Tickets có Role là `SPLIT_DERIVED`).
    3.  Chạy lại logic kiểm tra Overweight từ đầu (về lại `NOT_APPLICABLE` hoặc `PENDING`).

---

## F. SPLIT SUGGESTION DESIGN (Công thức sinh 2 phiếu)

*   **Tham số:** `OverweightSplitStepWeight` (lấy từ cấu hình).
*   **Công thức:**
    *   **Phiếu 1 (P1):** `SplitTicket1NetWeight = ROUND_TO_UNIT( Ttcp10WeightSnapshot * (1 - OverweightSplitStepWeight) )`
    *   **Phiếu 2 (P2):** `SplitTicket2NetWeight = NetWeight - SplitTicket1NetWeight`
*   **Thuật toán phân line (Vào 2 phiếu):**
    1.  Lấy danh sách `weighing_session_lines` (Order by `SequenceNo ASC`).
    2.  Lấp đầy Phiếu 1 trước. Nếu Line fit trọn vẹn thì nhét hết vào P1.
    3.  Nếu Line lớn hơn sức chứa còn lại của P1 -> Cắt Line làm 2 phần (về mặt chứng từ): Phần đầu lấp đầy P1, phần thừa chuyển sang P2.
    4.  Các Line còn lại sau đó đi hết vào P2.
*   **Bag Count Rule:** Tính `AllocatedBagCount` cho mỗi phần split theo tỷ lệ trọng lượng (làm tròn). Đảm bảo tổng số lượng bao chia ra phải bằng với số lượng bao ở Line gốc.
*   **Validation bắt buộc (Trước khi Submit):**
    *   `P1.NetWeight > 0` và `P1.NetWeight < Ttcp10WeightSnapshot`
    *   `P2.NetWeight > 0` và `P2.NetWeight <= Ttcp10WeightSnapshot` (Nếu P2 lớn hơn ngưỡng thì chặn không cho lưu, báo lỗi: *"Lượt cân này không thể tách hợp lệ thành 2 phiếu theo tham số hiện tại"*).

---

## G. IMPLEMENTATION PLAN & STEPS

### Bước 1: Data Migration & Enums
1.  Sửa đổi Class `WeighingSession`, `DeliveryTicket`, `WeighTicket` trong thư mục `Domain/Entities`.
2.  Cập nhật Enums (`WeighingSessionStatus`, `RegistrationStatus`, v.v.) trong `Domain/Enums`.
3.  Add Entity Configuration/Migration cho `AppConfig` (Seed value `OverweightSplitStepWeight`).
4.  Tạo và Run Migration.

### Bước 2: Core Domain Services
1.  Sửa đổi `WeighingSessionOverweightService.cs`: Đập bỏ thuật toán Greedy Split nhiều phiếu cũ. Thay thế bằng thuật toán cắt đúng 2 phiếu bằng công thức có `OverweightSplitStepWeight`.
2.  Viết hàm Validate chia tải trọng.

### Bước 3: Application UseCases
1.  Cập nhật `ResolveWeighingSessionOverweightSplitUseCase`: Xóa mềm chứng từ cũ, áp dụng thuật toán chia Line và sinh `SPLIT_DERIVED` Documents (Weigh + Delivery).
2.  Tạo/Cập nhật cơ chế Reset Logic: Khi `CaptureWeight1/2UseCase` hoặc `AllocateWeighingSessionUseCase` chạy, kích hoạt Invalidation Trigger để reset các chứng từ cũ nếu có biến động.

### Bước 4: UI / ViewModels
1.  Cập nhật `WeighingViewModel.cs`: Xử lý Logic ẩn hiện nút **[Xử lý quá tải]**, khóa nút Out Yard, Mapping Status Tiếng Việt.
2.  Tạo giao diện View/ViewModel Modal cho **OverweightSplitDialog** (Cho phép edit giá trị chia thủ công, hiển thị tham số).
3.  Thêm màn hình cấu hình `OverweightSplitStepWeight` trong View System Settings (Tham số hệ thống).

---

## H. VERIFICATION PLAN (TEST NOTES)

*   **Test Case 1:** Nhập Session không quá tải -> Trạng thái `NOT_APPLICABLE`, có thể in phiếu bình thường, ra cổng thành công.
*   **Test Case 2:** Session quá tải, chọn **Không tách** -> Sinh 1 PGN và 1 PCân tổng, đánh dấu Overweight, Status `NO_SPLIT_CONFIRMED`.
*   **Test Case 3:** Session quá tải, chọn **Tách 2 phiếu** -> Verify phiếu 1 có NetWeight nhỏ hơn TTCP, Phiếu 2 chứa phần thừa. Verify tính tổng lại 2 phiếu = NetWeight tổng.
*   **Test Case 4:** Quá tải siêu nặng (Phiếu 2 vẫn vượt ngưỡng TTCP 10%) -> Hệ thống báo lỗi chặn không cho chia làm 2 phiếu.
*   **Test Case 5:** Reset Logic: Đang `SPLIT_CONFIRMED`, quay lại sửa `Weight2` thấp xuống dưới ngưỡng -> Các phiếu tách cũ phải bị IsDeleted = true, Session quay về `NOT_APPLICABLE`.
*   **Test Case 6:** Giao diện: Nút "Chuyển xe ra" bị khóa khi đang có chữ `PENDING` ở quá tải. Đổi ngôn ngữ trạng thái chuẩn tiếng Việt trên Grid.
