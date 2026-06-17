# Plan: Cơ chế sinh số chứng từ an toàn, chống trùng lặp dưới môi trường concurrent

Kế hoạch chi tiết để tối ưu hóa cơ chế sinh số lượt cân (SessionNo), số phiếu cân (TicketNo) và số phiếu giao nhận (DeliveryNo) bằng cách sử dụng bảng bộ đếm (`document_counters`) trong database, thực hiện tăng số nguyên tử (atomic update) để loại bỏ hoàn toàn lỗi tranh chấp dữ liệu (Race Condition).

## Overview
- **Mục tiêu**: Thay thế cơ chế sinh số hiện tại (đọc số lớn nhất + 1 trong bộ nhớ) bằng cơ chế sử dụng bảng đếm tập trung trong Database.
- **Yêu cầu cốt lõi**:
  - Đảm bảo an toàn 100% khi có nhiều luồng hoặc nhiều máy cùng yêu cầu sinh số cùng một tích tắc.
  - Tự động reset số đếm về `1` khi bước sang tháng mới (dựa trên cấu trúc khóa `CounterKey` có hậu tố năm-tháng `yyMM`).
  - Loại bỏ hoàn toàn vòng lặp truy vấn `AnyAsync` đắt đỏ trong C#.
  - Giảm thiểu tối đa locks và tranh chấp trên database (chỉ dùng Row Lock cho từng dòng counter cụ thể).

## Success Criteria
- [ ] Bảng `dbo.document_counters` được tự động khởi tạo khi chạy phần mềm.
- [ ] Các class `TicketNumberGenerator`, `DeliveryNumberGenerator`, `WeighingSessionNumberGenerator` chuyển sang dùng cơ chế tăng số nguyên tử.
- [ ] Chạy kiểm thử tải (hoặc giả lập concurrent requests) sinh số đồng thời không phát sinh bất kỳ số trùng nào và không bị lỗi Unique Constraint khi lưu.
- [ ] Số chứng từ tự động chuyển về `0001` khi đổi sang tháng mới (ví dụ từ `QN2606xxxx` sang `QN26070001`).

## File Structure

```plaintext
src/
├── StationApp.Domain/
│   └── Entities/
│       └── DocumentCounter.cs (NEW - Định nghĩa thực thể bộ đếm chứng từ)
│
├── StationApp.Application/
│   └── Interfaces/
│       └── IDocumentCounterService.cs (NEW - Interface dịch vụ sinh số nguyên tử)
│
├── StationApp.Infrastructure/
│   ├── Persistence/
│   │   ├── Configurations/
│   │   │   └── DocumentCounterConfiguration.cs (NEW - Cấu hình bảng trong EF Core)
│   │   └── SchemaCompatibilityBootstrapper.cs (Thêm lệnh SQL khởi tạo bảng)
│   └── Services/
│       ├── DocumentCounterService.cs (NEW - Thực thi SQL thô tăng số nguyên tử dùng ROWLOCK và OUTPUT)
│       └── InfrastructureServices.cs (Refactor các hàm sinh số hiện có để dùng IDocumentCounterService)
```

## Proposed Changes

### 1. Database & Domain Config
- **DocumentCounter.cs** [NEW]:
  - Định nghĩa thực thể lưu trữ bộ đếm:
    ```csharp
    public class DocumentCounter
    {
        public string CounterKey { get; set; } = null!;
        public int LastValue { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    ```
- **DocumentCounterConfiguration.cs** [NEW]:
  - Cấu hình PK `CounterKey` với độ dài tối đa 100 ký tự và tên bảng `document_counters`.
- **SchemaCompatibilityBootstrapper.cs**:
  - Thêm lệnh SQL khởi tạo bảng `dbo.document_counters` tự động khi chạy ứng dụng để đảm bảo độ tương thích cơ sở dữ liệu.

### 2. Service Layer & Logic Sinh số
- **IDocumentCounterService.cs** [NEW]:
  - Định nghĩa phương thức: `Task<int> GetNextSequenceAsync(string counterKey, CancellationToken ct);`
- **DocumentCounterService.cs** [NEW]:
  - Thực thi phương thức bằng câu lệnh SQL raw:
    - Sử dụng `UPDATE dbo.document_counters SET LastValue = LastValue + 1 ... OUTPUT inserted.LastValue ... WHERE CounterKey = @CounterKey` kèm `ROWLOCK`.
    - Bọc logic `INSERT` trong `TRY CATCH` phòng trường hợp 2 máy cùng kích hoạt khóa của tháng mới ở cùng một thời điểm.
- **InfrastructureServices.cs** (Refactor):
  - Tiêm `IDocumentCounterService` vào các lớp:
    - `TicketNumberGenerator`
    - `DeliveryNumberGenerator`
    - `WeighingSessionNumberGenerator`
  - Thay thế logic cũ bằng cách gọi `GetNextSequenceAsync` với key tương ứng và format chuỗi trả về.

---

## Task Breakdown

### Phase 1: Database & Service Foundation

#### Task 1.1: Tạo Domain Entity và cấu hình EF Core
- **Agent**: `database-architect`
- **Skill**: `database-design`
- **Output**:
  - `src/StationApp.Domain/Entities/DocumentCounter.cs`
  - `src/StationApp.Infrastructure/Persistence/Configurations/DocumentCounterConfiguration.cs`
  - Đăng ký DbSet trong `StationDbContext.cs`.
- **Verify**: Dự án biên dịch thành công.

#### Task 1.2: Cấu hình Bootstrapper khởi tạo bảng tự động
- **Agent**: `database-architect`
- **Skill**: `database-design`
- **Input**: `SchemaCompatibilityBootstrapper.cs`
- **Output**: Tích hợp câu lệnh tạo bảng `dbo.document_counters` nếu chưa tồn tại.
- **Verify**: Khởi chạy ứng dụng local, bảng `dbo.document_counters` được tạo thành công trong DB SQL Server.

---

### Phase 2: Implementation of Atomic Counter Service

#### Task 2.1: Tạo IDocumentCounterService và thực thi
- **Agent**: `backend-specialist`
- **Skill**: `api-patterns`
- **Output**:
  - `IDocumentCounterService.cs`
  - `DocumentCounterService.cs` (Chứa SQL thô nguyên tử sử dụng ROWLOCK và OUTPUT).
- **Verify**: Viết unit test giả lập gọi đồng thời nhiều thread sinh số từ `DocumentCounterService` kiểm tra tính toàn vẹn dữ liệu.

#### Task 2.2: Refactor các lớp sinh số hiện tại
- **Agent**: `backend-specialist`
- **Skill**: `api-patterns`
- **Dependencies**: Task 2.1
- **Input**: `InfrastructureServices.cs`
- **Output**:
  - Thay đổi `TicketNumberGenerator` sử dụng key: `$"WeighTicket_{ticketPrefix}"`
  - Thay đổi `DeliveryNumberGenerator` sử dụng key: `$"DeliveryTicket_{deliveryPrefix}"`
  - Thay đổi `WeighingSessionNumberGenerator` sử dụng key: `$"WeighingSession_{sessionPrefix}"`
- **Verify**: Chạy lại các integration test hiện có của hệ thống kiểm tra các hàm sinh số vẫn tạo định dạng chính xác.

---

## Verification Plan

### Automated Tests
- Thực thi build ứng dụng:
  ```powershell
  dotnet build
  ```
- Viết 1 test tích hợp (Integration Test) mô phỏng 20 tác vụ chạy song song (`Task.WhenAll`) cùng gọi sinh số để xác nhận:
  - Không sinh ra bất kỳ số trùng lặp nào.
  - Toàn bộ 20 số được sinh ra liên tiếp nhau mà không bị ngắt quãng hoặc lỗi DB.

### Manual Verification
1. Xóa (hoặc rename) bảng `dbo.document_counters` hiện tại (nếu có).
2. Chạy ứng dụng để bootstrapper tự động sinh bảng.
3. Thực hiện cân thử lượt cân mới, lưu phiếu và xuất phiếu.
4. Kiểm tra dữ liệu bảng `dbo.document_counters` xem các dòng counter tương ứng đã được tự động chèn và cập nhật đúng giá trị đếm hay chưa.
