# PHASE 1 TECHNICAL SPECIFICATION V2

**Scope**: Station App nền tảng + local DB + domain/use cases cốt lõi + simulator/device foundation + sync foundation
**Source of truth**: bám đúng Phase 0 final đã khóa
**Out of scope của Phase 1**: Central API full, Admin Web full, print production-ready, reopen flow, warning thiếu hàng

---

## 1. Mục tiêu của Phase 1
Phase 1 phải tạo ra một solution chạy được, có thể:
- khởi động app WPF
- kết nối local SQL Server Express
- tạo và cập nhật `weigh_tickets`
- mô phỏng cân bằng `SimulatorScaleDevice`
- ghi cân lần 1, cân lần 2, complete, cancel
- tính `net_weight` đúng theo `transaction_type`
- lưu `sync_outbox`
- có nền tảng để Phase 2 tích hợp cân thật và sync thật

Kết thúc Phase 1, hệ thống chưa cần production-complete, nhưng phải đạt:
- đúng domain, đúng schema, đúng use case, đúng layering
- không phải prototype một file

---

## 2. Kiến trúc solution chốt cho Phase 1

### 2.1 Project list
```text
StationApp.sln
src/
  StationApp.Domain/
  StationApp.Application/
  StationApp.Infrastructure/
  StationApp.Device/
  StationApp.Sync/
  StationApp.Contracts/
  StationApp.UI/
tests/
  StationApp.Domain.Tests/
  StationApp.Application.Tests/
  StationApp.Device.Tests/
  StationApp.Sync.Tests/
```

### 2.2 Vai trò từng project
- **StationApp.Domain**: Entities, enums, value objects đơn giản, domain rules thuần, exceptions thuần, repository abstractions tối thiểu. (Không chứa EF Core, WPF, HTTP, SQL Server, SerialPort).
- **StationApp.Application**: Use cases, DTOs nội bộ, validators, interfaces cho service ngoài domain, orchestration nghiệp vụ.
- **StationApp.Infrastructure**: EF Core DbContext, repository implementations, SQL Server persistence, config repository, audit logger, version provider, clock, current user context, central API client.
- **StationApp.Device**: IScaleDevice, ScaleReading, DeviceStatus, SerialScaleDevice, SimulatorScaleDevice, parser, stability detector.
- **StationApp.Sync**: Outbox worker, retry policy, payload factory, network status abstraction, sync coordinator.
- **StationApp.Contracts**: Request/response models trao đổi với central API, shared message contracts, transport DTOs.
- **StationApp.UI**: WPF app, views, viewmodels, DI bootstrap, navigation, commands, converters.

---

## 3. Project dependencies
- `StationApp.Domain` -> no dependency
- `StationApp.Application` -> `StationApp.Domain`
- `StationApp.Device` -> `StationApp.Domain`
- `StationApp.Contracts` -> no dependency (or Domain-free simple contracts)
- `StationApp.Infrastructure` -> `StationApp.Domain`, `StationApp.Application`, `StationApp.Contracts`
- `StationApp.Sync` -> `StationApp.Domain`, `StationApp.Application`, `StationApp.Contracts`
- `StationApp.UI` -> `StationApp.Domain`, `StationApp.Application`, `StationApp.Device`, `StationApp.Infrastructure`, `StationApp.Sync`, `StationApp.Contracts`

**Quy tắc**: UI không gọi DbContext trực tiếp. Device không biết EF. Sync không biết View/ViewModel. Domain không biết framework.

---

## 4. Domain model chi tiết

### 4.1 Enums
- **TicketStatus**: `TICKET_CREATED = 1`, `LOADING_STARTED = 2`, `TICKET_COMPLETED = 3`, `TICKET_CANCELLED = 4`
- **SyncStatus**: `SYNC_QUEUED = 1`, `SYNC_SUCCESS = 2`, `SYNC_FAILED = 3`
- **TransactionType**: `OUTBOUND = 1`, `INBOUND = 2`
- **TransportMethod**: `ROAD = 1`, `WATERWAY = 2`
- **WeightMode**: `AUTO = 1`, `MANUAL = 2`
- **OutboxStatus**: `PENDING = 1`, `PROCESSING = 2`, `SUCCESS = 3`, `FAILED_RETRYABLE = 4`, `FAILED_FINAL = 5`

### 4.2 Entities
- **WeighTicket**: Id, TicketNo, ErpVehicleRegistrationId, VehiclePlate, MoocNumber, DriverName, CustomerCode, CustomerName, ProductCode, ProductName, PlannedWeight, BagCount, Notes, TransactionType, TransportMethod, IsCancelled, Status, IdempotencyKey, SyncStatus, Weight1, Weight1User, Weight1Time, Weight1UpdatedAt, Weight1Mode, Weight1IsStable, Weight2, Weight2User, Weight2Time, Weight2UpdatedAt, Weight2Mode, Weight2IsStable, NetWeight, AppVersion, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy.
- **SyncOutbox**: Id, AggregateId, AggregateType, PayloadJson, IdempotencyKey, Status, RetryCount, LastError, NextRetryAt, CreatedAt, UpdatedAt.
- **AuditLog**: Id, Actor, Action, EntityType, EntityId, DetailJson, CreatedAt.
- **AppConfig**: ConfigKey, ConfigValue, UpdatedAt.
- **DeviceConfig**: Id, DeviceName, ComPort, Baudrate, Parity, DataBits, StopBits, FrameEndChar, ParserType, StabilityThreshold, StableCycles, IsActive, CreatedAt, UpdatedAt.
- **User**: Id, Username, DisplayName, RoleCode, IsActive, CreatedAt, UpdatedAt.

---

## 5. Domain rules formalized

### 5.1 CreateTicket
- **Preconditions**: ticket_no sinh được, idempotency_key sinh được, transaction_type hợp lệ, status = TICKET_CREATED, sync_status = SYNC_QUEUED.
- **Postconditions**: ticket tồn tại trong DB, outbox enqueue, audit log ghi.

### 5.2 CaptureWeight1
- **Preconditions**: ticket tồn tại, status = TICKET_CREATED, chưa cancel, nhận stable/unstable.
- **Postconditions**: weight_1 set, weight_1_user/time/mode/is_stable/updated_at set, status -> LOADING_STARTED, sync_status -> SYNC_QUEUED, outbox enqueue, audit log ghi.

### 5.3 CaptureWeight2
- **Preconditions**: ticket tồn tại, status = LOADING_STARTED, chưa cancel, weight_1 đã có.
- **Postconditions**: weight_2 set, weight_2_user/time/mode/is_stable/updated_at set, sync_status -> SYNC_QUEUED, outbox enqueue, audit log ghi.

### 5.4 CompleteTicket
- **Preconditions**: ticket tồn tại, status = LOADING_STARTED, có weight_1 và weight_2.
- **Business calculation**: OUTBOUND => `NetWeight = Weight2 - Weight1`. INBOUND => `NetWeight = Weight1 - Weight2`.
- **Validation**: net_weight >= 0. Nếu net_weight > planned_weight + tolerance => warning flag (không block).
- **Postconditions**: net_weight set, status = TICKET_COMPLETED, sync_status = SYNC_QUEUED, outbox enqueue, audit log ghi.

### 5.5 CancelTicket
- **Preconditions**: status là TICKET_CREATED hoặc LOADING_STARTED.
- **Postconditions**: is_cancelled = true, status = TICKET_CANCELLED, sync_status = SYNC_QUEUED, outbox enqueue, audit log ghi.

---

## 6. Class list chi tiết
- **Domain services/helpers**: `WeightCalculator`, `TicketStateGuard`.
- **Use Cases**: `CreateTicketUseCase`, `CaptureWeight1UseCase`, `CaptureWeight2UseCase`, `CompleteTicketUseCase`, `CancelTicketUseCase`, v.v.
- **Interfaces**: `ITicketRepository`, `ISyncOutboxRepository`, `IUnitOfWork`, `ITicketNumberGenerator`, `IAppVersionProvider`, `ICurrentUserContext`, v.v.
- **Infrastructure**: `StationDbContext`, EntityConfigurations, Repositories, Migrations.
- **Device**: `IScaleDevice`, `ScaleReading`, `SimulatorScaleDevice`, `SerialScaleDevice`, `StabilityDetector`.
- **Sync**: `SyncOutboxWorker`, `CentralApiClient`, `SyncCoordinator`.
- **UI**: ViewModels (`Main`, `Weighing`, `TicketList`...), Views (`MainWindow`, `WeighingView`...).

---

## 7. Repository contract chi tiết
- `ITicketRepository`: AddAsync, UpdateAsync, GetByIdAsync, GetByTicketNoAsync...
- `ISyncOutboxRepository`: EnqueueAsync, GetPendingAsync, MarkProcessingAsync, MarkSuccessAsync...
- `IUnitOfWork`: SaveChangesAsync, ExecuteInTransactionAsync.

---

## 8. DDL / migration specification
- **weigh_tickets**: `ticket_no` unique, `idempotency_key` unique, `vehicle_plate`/`status`/`sync_status` indexed.
- **Precision**: `decimal(18,3)` cho trọng lượng. Nullability rõ ràng.
- **app_config**: Seed bắt buộc `station_code`, `ticket_prefix = QN`, `tolerance_kg`, `sync_interval_seconds`...

---

## 9. Device specification
- `IScaleDevice`: ConnectAsync, StartAsync, event `WeightReceived`.
- `SimulatorScaleDevice`: Phát weight theo state machine (cả stable/unstable).
- `StabilityDetector`: Phân tích chuỗi reading để output `isStable`.

---

## 10. Sync specification
- **Tách nghĩa trạng thái**: `weigh_tickets.sync_status` (Nghiệp vụ) khác với `sync_outbox.status` (Message Queue).
- **Worker**: Dispatch thành công -> ticket.sync_status = SYNC_SUCCESS.

---

## 11. UI scope Phase 1
- **Bắt buộc**: MainWindow, TicketListView, WeighingView, SettingsView, LoginView (hoặc mock session).
- **WeighingView**: Danh sách ticket, vùng hiển thị weight live, ST/M status, các action cân và cảnh báo.

---

## 12. Technical decisions phải khóa
- **ticket_no**: `QNyyMM0001` sinh từ `TicketNumberGenerator` (tăng +1 trong transaction).
- **app_version**: Lấy từ assembly metadata (`IAppVersionProvider`).
- **CurrentUser**: Mock local session cho Phase 1 (`ICurrentUserContext`).

---

## 13. Test matrix tối thiểu
- Domain tests: Net weight calculation, state transitions.
- Application tests: Use cases, idempotency.
- Device tests: Simulator emitting, stability detector rules.
- Sync tests: Outbox enqueue, worker batching.

---

## 14. Coding order đề xuất mới (5 Sprints)

| Sprint | Nhiệm vụ chính | Deliverables |
| :--- | :--- | :--- |
| **Sprint 1 — Solution foundation** | Tạo `.sln`, `.csproj`, DI base, Enums, Entities, Interfaces. DbContext + Migration. | App boot được, DB local tạo được. |
| **Sprint 2 — Domain + Application core** | Helpers (TicketNumberGenerator...), Use cases, EF Repos, Audit. | Test use cases pass. |
| **Sprint 3 — Device foundation** | SimulatorScaleDevice, StabilityDetector, SerialScaleDevice proof. | Có dòng weight live từ simulator. |
| **Sprint 4 — UI shell** | MainWindow, TicketListView, WeighingView. Workflow trọn vẹn. | UI chạy end-to-end với simulator. |
| **Sprint 5 — Sync foundation** | SyncOutboxWorker, CentralApiClient, Sync update. | Outbox tạo và sync mock thành công. |

---

## 15. Definition of Done cho Phase 1
- [ ] Solution build sạch.
- [ ] Migration chạy được trên SQL Server Express.
- [ ] Tạo ticket thành công với format `QNyyMM0001`.
- [ ] Capture cân 1/cân 2 hoạt động với simulator.
- [ ] Complete tính `net_weight` đúng.
- [ ] Cancel hoạt động đúng.
- [ ] Lưu đủ cờ `is_stable` và `app_version`.
- [ ] Outbox message được tạo.
- [ ] Sync worker skeleton chạy được.
- [ ] Test matrix tối thiểu pass.
