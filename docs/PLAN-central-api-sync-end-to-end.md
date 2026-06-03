# PLAN: Hoàn thiện sync end-to-end `Local DB -> Outbox -> Central API -> Central DB`

## 1. Mục tiêu

Hoàn thiện chức năng sync dữ liệu từ database local tại trạm lên database server trung tâm theo đúng kiến trúc hiện có:

- App trạm ghi dữ liệu vào local DB
- Nghiệp vụ enqueue dữ liệu vào `sync_outbox`
- Worker nền của app trạm đẩy payload lên `Central API`
- `Central API` xác thực bằng API key và ghi vào DB server trung tâm

Phase này không đổi kiến trúc sang kết nối SQL Server trực tiếp từ app trạm.

## 2. Kết luận kiến trúc chốt cuối

Giữ nguyên kiến trúc:

- `Local DB -> Outbox -> Central API -> Central DB`

Không làm:

- `Local DB -> SQL Server server trực tiếp`

Lý do:

- Client-side sync đã tồn tại và đang chạy theo API trong `src/StationApp.Sync/Services/CentralApiClient.cs`
- Worker outbox đã tồn tại trong `src/StationApp.Sync/Services/SyncOutboxWorker.cs`
- App đã có hook gửi `X-Api-Key` qua `ApiKeyDelegatingHandler` trong `src/StationApp.UI/App.xaml.cs`
- Solution hiện chưa có project server nhận sync, nên phần còn thiếu là Central API và UI cấu hình cho client

## 3. Review hiện trạng code

### 3.1 Client sync flow hiện tại

Luồng hiện tại đã có:

- Nghiệp vụ local cập nhật `SyncStatus = SYNC_QUEUED`
- `SyncOutboxWorker` tự dựng bản ghi outbox nếu aggregate còn `SYNC_QUEUED`
- Worker lấy pending outbox, gọi `ICentralApiClient.PushAggregateAsync(...)`
- Nếu thành công thì:
  - `SyncOutbox.Status = SUCCESS`
  - aggregate được cập nhật `SYNC_SUCCESS`
- Nếu thất bại thì:
  - `SyncOutbox.Status = FAILED_RETRYABLE` hoặc `FAILED_FINAL`
  - aggregate được cập nhật `SYNC_FAILED`

File chính:

- `src/StationApp.Sync/Services/SyncOutboxWorker.cs`
- `src/StationApp.Infrastructure/Repositories/SyncOutboxRepository.cs`
- `src/StationApp.Domain/Entities/SyncOutbox.cs`
- `src/StationApp.Domain/Enums/OutboxStatus.cs`
- `src/StationApp.Domain/Enums/SyncStatus.cs`

### 3.2 Central API client hiện tại

`CentralApiClient` hiện đang push theo aggregate type:

- `CutOrder` -> `POST /api/vehicle-registrations`
- `WeighTicket` -> `POST /api/weigh-tickets`
- `DeliveryTicket` -> `POST /api/delivery-tickets`
- `Vehicle` -> `POST /api/vehicles`
- `Customer` -> `POST /api/customers`
- `Product` -> `POST /api/products`

File:

- `src/StationApp.Sync/Services/CentralApiClient.cs`
- `src/StationApp.Domain/Constants/SyncAggregateTypes.cs`

Hành vi cấu hình hiện tại:

- Đọc `central_api_url` từ `IAppConfigRepository`
- Nếu URL không hợp lệ thì trả lỗi `CONFIG_INVALID`
- Gắn header `Idempotency-Key`

Điểm đã có nhưng chưa hoàn chỉnh:

- `X-Api-Key` đã được tự động gắn ở runtime qua `ApiKeyDelegatingHandler` trong `src/StationApp.UI/App.xaml.cs`
- Tuy nhiên app chưa có UI chính thức để lưu `central_api_key`

### 3.3 Payload hiện tại client đang gửi

Payload hiện tại không dùng DTO sync riêng. Client serialize thẳng entity local sang JSON camelCase:

- `CutOrder`
- `WeighTicket`
- `DeliveryTicket`
- `Vehicle`
- `Customer`
- `Product`

File:

- `src/StationApp.Infrastructure/Services/InfrastructureServices.cs` (`SyncPayloadFactory`)
- `src/StationApp.Application/Interfaces/ISyncPayloadFactory.cs`

Hệ quả:

- Server side phải tương thích với shape của entity local hiện tại
- Không nên redesign payload nếu không thật sự cần

### 3.4 System settings hiện tại

Màn hình `System Settings` hiện chỉ quản lý:

- `station_code`
- `ticket_prefix`
- `delivery_prefix`
- `tolerance_kg_per_bag`
- `sync_interval`
- `registration_inbound_poll_seconds`
- `OverweightSplitStepWeight`

File:

- `src/StationApp.UI/ViewModels/Settings/SystemSettingsViewModel.cs`
- `src/StationApp.UI/Views/Settings/SystemSettingsView.xaml`
- `src/StationApp.Application/DTOs/Dtos.cs` (`UpdateSystemSettingsRequest`)
- `src/StationApp.Application/UseCases/SettingsAuthorizationUseCases.cs`

Thiếu:

- `central_api_url`
- `central_api_key`
- validate URL/key
- test connection

### 3.5 Diagnostics và dashboard hiện tại

`DiagnosticsViewModel` hiện:

- đọc `central_api_url`
- hiển thị pending sync, failed sync, last success/failure
- không hiển thị trạng thái API key
- không có action test connection

File:

- `src/StationApp.UI/ViewModels/DiagnosticsViewModel.cs`
- `src/StationApp.UI/Views/DiagnosticsView.xaml`

`DashboardViewModel` hiện:

- tự ping `GET {central_api_url}/health`
- dùng `HttpClient` riêng nên không tận dụng `ApiKeyDelegatingHandler`
- chỉ kiểm tra trạng thái kết nối cơ bản, không có chẩn đoán auth/config chi tiết

File:

- `src/StationApp.UI/ViewModels/DashboardViewModel.cs`

### 3.6 Cấu hình seed và key naming hiện tại

`app_config` đang có lệch khóa cấu hình:

- seed: `sync_interval_seconds`
- worker/UI save-load: `sync_interval`
- worker có fallback đọc cả `sync_interval` và `sync_outbox_interval_seconds`

File:

- `src/StationApp.Infrastructure/Persistence/Configurations/OtherEntityConfigurations.cs`
- `src/StationApp.Sync/Services/SyncOutboxWorker.cs`
- `src/StationApp.UI/ViewModels/Settings/SystemSettingsViewModel.cs`
- `src/StationApp.Application/UseCases/SettingsAuthorizationUseCases.cs`

Điểm cần chốt trong phase này:

- Chuẩn hóa key sync interval
- Bổ sung key seed cho `central_api_url`
- Bổ sung key seed cho `central_api_key`

### 3.7 Authorization hiện tại

Phân quyền hiện có đã phù hợp với yêu cầu:

- `ADMIN` được quản trị system settings
- `OPERATOR` không được quản trị system settings

File:

- `src/StationApp.Application/Security/StationAuthorization.cs`

### 3.8 Solution hiện tại

Hiện solution chỉ có:

- `StationApp.Application`
- `StationApp.Contracts`
- `StationApp.DbMigrator`
- `StationApp.Device`
- `StationApp.Domain`
- `StationApp.Infrastructure`
- `StationApp.Sync`
- `StationApp.UI`
- `StationApp.Updater`

Chưa có:

- `StationApp.CentralApi`

## 4. Gap analysis

Để chạy end-to-end thực tế, hiện còn thiếu:

1. Chưa có Central API server để nhận dữ liệu từ trạm
2. Chưa có UI save/load cho `central_api_url`
3. Chưa có UI save/load cho `central_api_key`
4. Chưa có health/test connection flow dùng chung service chuẩn
5. Chưa có response contract đủ rõ cho server sync
6. Chưa có chiến lược upsert/idempotency ở phía server
7. Chưa có test end-to-end client -> API -> central DB
8. Chưa chuẩn hóa khóa config sync interval

## 5. Thiết kế implementation phía client

### 5.1 Chuẩn hóa app config keys

Tạo constants mới trong `src/StationApp.Domain/Constants/AppConfigKeys.cs`:

- `CentralApiUrl = "central_api_url"`
- `CentralApiKey = "central_api_key"`
- `SyncIntervalSeconds = "sync_interval_seconds"`
- `RegistrationInboundPollSeconds = "registration_inbound_poll_seconds"`

Quy tắc:

- Ngừng dùng string literal rải rác cho 4 key trên
- Giữ backward compatibility cho dữ liệu cũ bằng cách:
  - load ưu tiên key mới chuẩn
  - fallback đọc key cũ nếu cần
  - save về key chuẩn duy nhất

### 5.2 Bổ sung seed mặc định

Mở rộng seed `app_config` tại `src/StationApp.Infrastructure/Persistence/Configurations/OtherEntityConfigurations.cs`:

- `central_api_url = ""`
- `central_api_key = ""`
- chuẩn hóa `sync_interval_seconds = "30"`
- nếu vẫn cần tương thích cũ thì plan migration data từ `sync_interval` sang `sync_interval_seconds`

Lưu ý:

- `central_api_key` không được hard-code giá trị thật trong source
- chỉ seed rỗng

### 5.3 Mở rộng DTO và use case lưu system settings

Mở rộng `UpdateSystemSettingsRequest` trong `src/StationApp.Application/DTOs/Dtos.cs`:

- thêm `CentralApiUrl`
- thêm `CentralApiKey`

Mở rộng `UpdateSystemSettingsUseCase` trong `src/StationApp.Application/UseCases/SettingsAuthorizationUseCases.cs`:

- validate URL base hợp lệ
- cho phép API key rỗng nếu business chấp nhận cấu hình sau
- nếu user nhập URL thì normalize về dạng absolute URL
- lưu vào `AppConfigKeys.CentralApiUrl`
- lưu vào `AppConfigKeys.CentralApiKey`
- chuẩn hóa lưu `sync_interval_seconds`

Validation tối thiểu:

- URL không rỗng khi admin bật sync thực tế
- URL phải là absolute URI
- không nhận loopback nếu đây là app trạm thật
- key không được chỉ toàn khoảng trắng

### 5.4 Mở rộng `SystemSettingsViewModel`

Sửa `src/StationApp.UI/ViewModels/Settings/SystemSettingsViewModel.cs`:

- thêm property:
  - `CentralApiUrl`
  - `CentralApiKey`
  - `MaskedCentralApiKeyPreview` nếu cần UX tốt hơn
- `LoadAsync()` đọc key mới từ `AppConfigKeys`
- `SaveAsync()` truyền đủ field mới xuống use case
- thêm command `TestCentralApiConnectionCommand`

### 5.5 Mở rộng `SystemSettingsView.xaml`

Sửa `src/StationApp.UI/Views/Settings/SystemSettingsView.xaml`:

- thêm textbox `Central API URL`
- thêm `PasswordBox` hoặc control masked cho `Central API Key`
- thêm nút `Kiểm tra kết nối Central API`
- hiển thị rõ đây là cấu hình sync server trung tâm

Yêu cầu UX:

- Chỉ `ADMIN` thao tác được
- Nếu binding với `PasswordBox` khó, dùng behavior/helper rõ ràng, không hack code-behind rời rạc
- Message kết quả:
  - `Đã lưu cấu hình Central API.`
  - `Kết nối Central API thành công.`
  - `Không thể kết nối Central API. Vui lòng kiểm tra URL hoặc API key.`

### 5.6 Tạo service kiểm tra kết nối Central API

Tạo abstraction mới trong client, ví dụ:

- `src/StationApp.Application/Interfaces/ICentralApiHealthChecker.cs`
- implementation tại `src/StationApp.Sync/Services/CentralApiHealthChecker.cs`

Nhiệm vụ:

- đọc URL từ config
- dùng cùng pipeline `HttpClient` có `ApiKeyDelegatingHandler`
- gọi `GET /health`
- trả kết quả chi tiết:
  - success
  - invalid URL
  - unauthorized
  - timeout
  - unreachable

Lý do tách service:

- không để `DashboardViewModel` tự tạo `HttpClient`
- tái sử dụng cho `System Settings`, `Dashboard`, `Diagnostics`

### 5.7 Refactor `CentralApiClient`

Sửa `src/StationApp.Sync/Services/CentralApiClient.cs`:

- thay string literal `central_api_url` bằng `AppConfigKeys.CentralApiUrl`
- giữ `Idempotency-Key`
- giữ mapping endpoint hiện tại để không phá client
- bổ sung parse response body tốt hơn nếu server trả JSON structured

Không làm trong phase này:

- không đổi endpoint path nếu chưa cần
- không thay toàn bộ contract sang message envelope mới

### 5.8 Refactor `DashboardViewModel`

Sửa `src/StationApp.UI/ViewModels/DashboardViewModel.cs`:

- bỏ `new HttpClient()` tự phát
- dùng `ICentralApiHealthChecker`
- nếu có `401/403` thì hiển thị khác với lỗi network
- nếu URL chưa cấu hình thì hiển thị `Chưa cấu hình`

### 5.9 Mở rộng `DiagnosticsViewModel` và `DiagnosticsView`

Sửa:

- `src/StationApp.UI/ViewModels/DiagnosticsViewModel.cs`
- `src/StationApp.UI/Views/DiagnosticsView.xaml`

Bổ sung:

- trạng thái `Central API URL: đã cấu hình/chưa`
- trạng thái `Central API Key: đã cấu hình/chưa`
- chỉ hiển thị masked key hoặc không hiển thị giá trị
- có thể thêm `Last health check result`

### 5.10 Đồng bộ naming khóa sync interval

Phải dọn một lần trong các file:

- `src/StationApp.Sync/Services/SyncOutboxWorker.cs`
- `src/StationApp.UI/ViewModels/Settings/SystemSettingsViewModel.cs`
- `src/StationApp.Application/UseCases/SettingsAuthorizationUseCases.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/OtherEntityConfigurations.cs`

Mục tiêu:

- chỉ còn một key chuẩn `sync_interval_seconds`
- giữ fallback đọc key cũ trong 1 phase chuyển tiếp

## 6. Thiết kế Central API

### 6.1 Tạo project mới

Tạo project mới:

- `src/StationApp.CentralApi`

Đề xuất stack:

- ASP.NET Core Web API trên `.NET 8`
- dùng DI, logging, options/config chuẩn
- dùng SQL Server cho central DB

Thêm vào solution chính.

### 6.2 Cấu trúc project đề xuất

Tối thiểu:

- `Program.cs`
- `appsettings.json`
- `appsettings.Development.json`
- `Configuration/`
- `Authentication/`
- `Controllers/`
- `Contracts/`
- `Persistence/`
- `Services/`

Tùy mức reuse có thể tham chiếu:

- `StationApp.Domain`
- `StationApp.Contracts`

Ưu tiên:

- reuse entity/domain hiện có nếu tương thích với central schema
- nếu central DB cần context riêng thì tạo `CentralSyncDbContext` riêng trong project API

### 6.3 Central DB strategy

Phương án ưu tiên:

- Tạo `CentralSyncDbContext` riêng trong `StationApp.CentralApi`
- Reuse entity classes từ `StationApp.Domain` nếu shape phù hợp
- Mapping bảng ở central có thể dùng cùng tên bảng hoặc bảng riêng, nhưng phải chốt dứt điểm trong migration server

Lý do không dùng luôn `StationDbContext`:

- giảm rủi ro kéo toàn bộ local-only concerns sang server
- cho phép central DB evolve độc lập hơn

### 6.4 Health endpoint

Phải có:

- `GET /health`

Mục tiêu:

- app trạm test connection
- dashboard check online/offline
- giám sát service

Response tối thiểu:

```json
{
  "success": true,
  "service": "StationApp.CentralApi",
  "database": "ok"
}
```

### 6.5 API key authentication

Thiết kế phase đầu:

- Header: `X-Api-Key`

Server side:

- đọc expected key từ config
- validate ở middleware hoặc endpoint filter
- nếu thiếu/sai:
  - trả `401` hoặc `403`
  - log có context nhưng không log full key

File dự kiến:

- `src/StationApp.CentralApi/Authentication/ApiKeyAuthenticationMiddleware.cs`
- `src/StationApp.CentralApi/Configuration/CentralApiOptions.cs`

### 6.6 Sync endpoints bắt buộc

Giữ tương thích client hiện tại:

- `POST /api/vehicle-registrations`
- `POST /api/weigh-tickets`
- `POST /api/delivery-tickets`
- `POST /api/vehicles`
- `POST /api/customers`
- `POST /api/products`

Trong phase đầu, 3 endpoint đầu là bắt buộc. 3 endpoint master data nên làm luôn vì client đã có mapping endpoint tương ứng.

### 6.7 DTO server side

Vì client đang serialize trực tiếp entity local, có 2 lựa chọn:

1. Reuse domain entity trực tiếp làm request model
2. Tạo DTO server side mirror shape của entity local

Khuyến nghị:

- Tạo DTO server side riêng để tránh coupling quá chặt giữa API contract và EF entity

DTO cần ít nhất cho:

- `CutOrderSyncDto`
- `WeighTicketSyncDto`
- `DeliveryTicketSyncDto`
- `VehicleSyncDto`
- `CustomerSyncDto`
- `ProductSyncDto`

Map field theo shape JSON camelCase hiện client đang gửi.

### 6.8 Idempotency và upsert strategy

Bắt buộc idempotent.

Chiến lược đề xuất:

- nhận `Idempotency-Key` header
- với mỗi endpoint:
  - tìm record theo `Id`
  - nếu không có `Id` thì fallback theo business key phù hợp
  - nếu record chưa có -> insert
  - nếu record đã có -> update

Ưu tiên khóa:

- `CutOrder`: `Id`, fallback `ErpCutOrderId` nếu quy tắc nghiệp vụ chấp nhận
- `WeighTicket`: `Id`, fallback `IdempotencyKey`, có thể thêm `TicketNo` như business index
- `DeliveryTicket`: `Id`, fallback `DeliveryNo`
- `Vehicle`: `Id`, fallback `VehiclePlate + MoocNumber`
- `Customer`: `Id`, fallback `CustomerCode`
- `Product`: `Id`, fallback `ProductCode`

Thêm unique index phù hợp ở central DB để bảo vệ chống duplicate.

### 6.9 Response contract

Tạo response contract dùng chung, ví dụ:

```json
{
  "success": true,
  "message": "Upserted successfully.",
  "operation": "insert",
  "aggregateType": "WeighTicket",
  "aggregateId": "guid"
}
```

Tối thiểu cần:

- `success`
- `message`
- `operation` (`insert`/`update`)
- `aggregateId`
- `errorCode` nếu fail

Client không cần hiểu sâu ngay, nhưng response này giúp log và diagnostics có ích hơn.

### 6.10 Logging và error handling

Phía server:

- log aggregate type, aggregate id, status code
- không log full API key
- không log payload đầy đủ nếu payload chứa dữ liệu nhạy cảm
- trả lỗi rõ:
  - invalid payload
  - unauthorized
  - duplicate conflict
  - DB write failure

Phía client:

- giữ nguyên nguyên tắc: sync fail thì outbox không được mark success
- ghi `LastError` đủ đọc được

## 7. File phạm vi cần sửa và tạo

### 7.1 Client side sửa

- `src/StationApp.Domain/Constants/AppConfigKeys.cs`
- `src/StationApp.Application/DTOs/Dtos.cs`
- `src/StationApp.Application/UseCases/SettingsAuthorizationUseCases.cs`
- `src/StationApp.UI/ViewModels/Settings/SystemSettingsViewModel.cs`
- `src/StationApp.UI/Views/Settings/SystemSettingsView.xaml`
- `src/StationApp.UI/ViewModels/DiagnosticsViewModel.cs`
- `src/StationApp.UI/Views/DiagnosticsView.xaml`
- `src/StationApp.UI/ViewModels/DashboardViewModel.cs`
- `src/StationApp.Sync/Services/CentralApiClient.cs`
- `src/StationApp.Sync/Services/SyncOutboxWorker.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/OtherEntityConfigurations.cs`
- `src/StationApp.UI/App.xaml.cs`
- `src/StationApp.Infrastructure/Repositories/OtherRepositories.cs`

### 7.2 Client side tạo mới

- `src/StationApp.Application/Interfaces/ICentralApiHealthChecker.cs`
- `src/StationApp.Sync/Services/CentralApiHealthChecker.cs`
- nếu cần:
  - `src/StationApp.Application/DTOs/CentralApiHealthDtos.cs`
  - helper/binding cho `PasswordBox`

### 7.3 Server side tạo mới

- `src/StationApp.CentralApi/StationApp.CentralApi.csproj`
- `src/StationApp.CentralApi/Program.cs`
- `src/StationApp.CentralApi/appsettings.json`
- `src/StationApp.CentralApi/Configuration/CentralApiOptions.cs`
- `src/StationApp.CentralApi/Authentication/ApiKeyAuthenticationMiddleware.cs`
- `src/StationApp.CentralApi/Controllers/HealthController.cs`
- `src/StationApp.CentralApi/Controllers/VehicleRegistrationsController.cs`
- `src/StationApp.CentralApi/Controllers/WeighTicketsController.cs`
- `src/StationApp.CentralApi/Controllers/DeliveryTicketsController.cs`
- `src/StationApp.CentralApi/Controllers/VehiclesController.cs`
- `src/StationApp.CentralApi/Controllers/CustomersController.cs`
- `src/StationApp.CentralApi/Controllers/ProductsController.cs`
- `src/StationApp.CentralApi/Contracts/*.cs`
- `src/StationApp.CentralApi/Persistence/CentralSyncDbContext.cs`
- `src/StationApp.CentralApi/Persistence/Configurations/*.cs`
- `src/StationApp.CentralApi/Services/*UpsertService.cs`

## 8. Thứ tự triển khai chi tiết

### Bước 1. Chuẩn hóa config key và seed

Kết quả mong đợi:

- có constants chính thức cho Central API URL/key
- có seed config rỗng
- chuẩn hóa `sync_interval_seconds`

Done khi:

- app khởi động vẫn không lỗi
- không còn đọc key string literal rải rác ở các điểm chính

### Bước 2. Mở rộng client settings

Kết quả mong đợi:

- admin nhập và lưu được `central_api_url`
- admin nhập và lưu được `central_api_key`
- operator không sửa được

Done khi:

- save/load round-trip được từ UI xuống `app_config`

### Bước 3. Tạo health checker dùng chung

Kết quả mong đợi:

- dashboard, diagnostics và system settings đều dùng cùng logic kiểm tra kết nối
- phân biệt được lỗi config/network/auth

Done khi:

- không còn `new HttpClient()` trực tiếp trong dashboard để ping API

### Bước 4. Scaffold Central API

Kết quả mong đợi:

- chạy được project `StationApp.CentralApi`
- `GET /health` trả `200`
- API key auth hoạt động

Done khi:

- gọi `/health` có key đúng thì pass, key sai thì fail theo thiết kế

### Bước 5. Tạo endpoint sync và upsert services

Kết quả mong đợi:

- nhận được payload từ client
- map DTO
- ghi insert/update vào central DB

Done khi:

- gọi lặp lại cùng payload không sinh duplicate

### Bước 6. Nối client với server

Kết quả mong đợi:

- app trạm save được URL/key
- worker đẩy thành công tới Central API thật
- outbox và `SyncStatus` phản ánh đúng

Done khi:

- có thể tạo local dữ liệu và thấy central DB nhận bản ghi

### Bước 7. Diagnostics và hardening

Kết quả mong đợi:

- nhìn được trạng thái sync và trạng thái cấu hình Central API
- biết nhanh lỗi do URL, key hay server

Done khi:

- màn diagnostics có đủ thông tin support/admin cần

## 9. Test plan

### 9.1 Client config

- Save `central_api_url` thành công
- Save `central_api_key` thành công
- URL sai format bị chặn
- OPERATOR không save được

### 9.2 Health/auth

- `/health` thành công với URL đúng
- URL sai host -> lỗi reachable
- key sai -> lỗi auth
- key trống -> lỗi auth nếu server yêu cầu

### 9.3 Sync outbox

- tạo `CutOrder` local -> enqueue/push thành công
- tạo `WeighTicket` local -> enqueue/push thành công
- tạo `DeliveryTicket` local -> enqueue/push thành công
- khi API fail -> outbox giữ trạng thái retry/final đúng
- khi retry thành công -> aggregate chuyển `SYNC_SUCCESS`

### 9.4 Idempotency/upsert

- gửi lại cùng payload 2 lần không tạo duplicate
- gửi payload update với cùng `Id` thì record central được update
- duplicate `Idempotency-Key` không làm hỏng dữ liệu

### 9.5 End-to-end

- local tạo dữ liệu
- worker push qua Central API
- central DB có bản ghi
- diagnostics local phản ánh đúng trạng thái cuối

## 10. Quality gate

Không được coi là hoàn tất nếu còn một trong các điểm sau:

- chỉ có UI cấu hình mà chưa có Central API
- có Central API nhưng app chưa lưu được URL/key
- health check vẫn dùng `HttpClient` rời rạc
- auth API key không hoạt động
- retry/outbox mark success sai
- sync duplicate chưa được kiểm soát
- OPERATOR vẫn sửa được cấu hình sync
- chưa có test end-to-end thực tế

## 11. Đề xuất chia milestone

### Milestone 1: Client config readiness

- chuẩn hóa app config keys
- UI nhập URL/key
- save/load
- health check service dùng chung
- dashboard/diagnostics hiển thị đúng

### Milestone 2: Central API baseline

- scaffold project API
- auth API key
- `/health`
- kết nối central DB

### Milestone 3: Business sync endpoints

- vehicle registrations
- weigh tickets
- delivery tickets
- idempotent upsert

### Milestone 4: Master data sync endpoints

- vehicles
- customers
- products

### Milestone 5: End-to-end verification

- manual test thật với DB server
- integration tests
- hardening log và diagnostics

## 12. Kết luận

Để hoàn thiện sync dữ liệu từ local DB lên DB server theo đúng source of truth hiện tại, hướng đi ngắn nhất và đúng nhất là:

1. Giữ nguyên kiến trúc `Local DB -> Outbox -> Central API -> Central DB`
2. Bổ sung cấu hình `central_api_url` và `central_api_key` ở app trạm
3. Tạo `StationApp.CentralApi` làm server trung gian ghi dữ liệu vào DB server
4. Dùng idempotent upsert để tránh duplicate khi retry
5. Hoàn tất test end-to-end thay vì chuyển sang direct SQL sync

Tài liệu này là baseline implementation plan cho phase hoàn thiện sync server trung tâm.
