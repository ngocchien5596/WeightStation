# Plan: Thêm Mã Trạm Vào Số Lượt Cân / Phiếu Cân / Phiếu Giao Nhận

## Tóm tắt

Chuẩn hóa cách sinh business number cho tất cả chứng từ chính bằng cách thêm `StationCode` vào trước số nghiệp vụ lưu trong DB và payload sync, theo mẫu:

- `{CurrentStationCode}-LC26060066`
- `{CurrentStationCode}-PC26060001`
- `{CurrentStationCode}-PGN26060001`

UI nội bộ và chứng từ in chỉ hiển thị phần rút gọn sau dấu `-`, ví dụ:

- `LC26060066`
- `PC26060001`
- `PGN26060001`

Mục tiêu chính là loại bỏ nguy cơ trùng business number giữa nhiều DB local khi sync về central cho dữ liệu phát sinh mới, đồng thời vẫn giữ trải nghiệm người dùng hiện tại ở tầng hiển thị.

## Kết luận cho câu hỏi trùng số khi sync 3 DB local

Với dữ liệu mới phát sinh, cách làm này sẽ tránh trùng số giữa 3 trạm nếu đáp ứng đầy đủ các điều kiện sau:

- mỗi trạm có `StationCode` riêng và đúng;
- tất cả generator đều đọc `StationCode` theo current station context hiện hành của người dùng;
- central và local đều lưu business number đầy đủ có prefix trạm;
- không còn điểm nào sinh số theo format cũ.

Lưu ý:

- dữ liệu cũ giữ nguyên theo yêu cầu, nên business number lịch sử không tự động hết nguy cơ trùng liên trạm;
- nếu hai DB local cấu hình trùng `StationCode` thì vẫn có rủi ro trùng số;
- khóa đồng bộ chính vẫn nên tiếp tục dựa trên `Guid Id` và `StationCode`, business number chỉ là mã nghiệp vụ để hiển thị và tra cứu.

## Phạm vi nghiệp vụ

Áp dụng cho tất cả số nghiệp vụ chính:

- `SessionNo` của lượt cân
- `TicketNo` của phiếu cân
- `DeliveryNo` của phiếu giao nhận

Quy tắc đã chốt:

- DB local lưu số đầy đủ có mã trạm
- payload sync gửi số đầy đủ có mã trạm
- UI nội bộ và chứng từ in chỉ hiển thị số rút gọn
- ô tìm kiếm / nhập tay chấp nhận cả số đầy đủ và số rút gọn
- không backfill dữ liệu cũ, chỉ áp dụng cho dữ liệu phát sinh mới

## Thay đổi kỹ thuật

### 1. Generator sinh số

Cập nhật các class generator để sinh số theo mẫu:

- session: `{CurrentStationCode}-LC26060066`
- weigh ticket: `{CurrentStationCode}-PC26060001`
- delivery ticket: `{CurrentStationCode}-PGN26060001`

Cần sửa:

- `src/StationApp.Infrastructure/Services/InfrastructureServices.cs`

Yêu cầu cụ thể:

- `WeighingSessionNumberGenerator` sinh `StationCode + "-LC" + yyMM + seq4`
- `TicketNumberGenerator` sinh `StationCode + "-" + ticket_prefix + yyMM + seq4`
- `DeliveryNumberGenerator` sinh `StationCode + "-" + delivery_prefix + yyMM + seq4`
- `StationCode` phải lấy theo mã trạm vận hành hiện hành của người dùng tại thời điểm sinh số, ưu tiên dùng `IStationScope.GetCurrentStationCodeAsync(ct)`; không hardcode và không lấy theo dòng trạm đang chọn ở màn quản trị

### 2. Counter key và `document_counters`

Việc thêm prefix `StationCode` vào business number không thay thế được vai trò của counter. Hệ thống vẫn cần `document_counters` để:

- sinh số tuần tự mà không phải quét `MAX(...)` mỗi lần tạo chứng từ;
- tránh race condition khi nhiều thao tác cùng sinh số;
- giữ quy tắc reset sequence theo từng loại / từng tháng;
- hỗ trợ môi trường một DB có thể thao tác nhiều trạm khác nhau.

Quyết định chốt cho plan này:

- vẫn cần `counterKey`;
- bắt buộc đưa `StationCode` vào `counterKey`;
- mỗi trạm phải có dải số riêng theo trạm / loại / tháng;
- mục tiêu numbering sau thay đổi là cho phép các cặp số như:
  - `QN01-PC26060001`
  - `QN02-PC26060001`
  cùng tồn tại hợp lệ;
- không dùng chung sequence giữa nhiều trạm trong cùng DB;
- không khuyến nghị bỏ hẳn `counterKey` trừ khi thiết kế lại hoàn toàn bảng `document_counters` sang mô hình cột riêng `StationCode + DocumentType + Period`, việc này không cần thiết cho thay đổi hiện tại.

Counter key theo mẫu:

- `WeighingSession_{CurrentStationCode}_LC2606`
- `WeighTicket_{CurrentStationCode}_PC2606`
- `DeliveryTicket_{CurrentStationCode}_PGN2606`

Quy tắc triển khai bắt buộc:

- các giá trị trên chỉ là ví dụ format, không phải giá trị hardcode;
- `counterKey` phải được tạo động theo current station code tại thời điểm người dùng phát sinh chứng từ mới;
- nguồn lấy mã trạm phải đi qua `IStationScope.GetCurrentStationCodeAsync(ct)`;
- không được hardcode `QN01` trong generator, bootstrap hay logic tạo `counterKey`.

Ví dụ mong muốn:

- khi user đang thao tác ở trạm `QN02` và phát sinh lượt cân mới trong tháng `2606`, hệ thống phải dùng `counterKey = WeighingSession_QN02_LC2606`;
- nếu key này chưa tồn tại trong `document_counters` thì phải được tạo mới động ở lần sinh số đầu tiên;
- kết quả số lượt cân sinh ra tương ứng phải là `QN02-LC26060001`;
- tương tự, khi user đang thao tác ở trạm `QN03`, hệ thống phải dùng `WeighingSession_QN03_LC2606`, không được dùng `WeighingSession_QN01_LC2606`.

Cần sửa:

- `src/StationApp.Infrastructure/Services/DocumentCounterService.cs`: không cần đổi thuật toán lock/increment, chỉ đổi input `counterKey`
- `src/StationApp.Infrastructure/Persistence/SchemaCompatibilityBootstrapper.cs`

Self-heal `document_counters` phải:

- parse được cả format cũ và format mới;
- không làm hỏng DB đã có dữ liệu cũ;
- cập nhật `LastValue` đúng theo prefix mới có `StationCode`.

### 3. DB constraints và index

Cần đảm bảo uniqueness đúng với mô hình multi-station.

Yêu cầu:

- giữ `weighing_sessions` unique theo `{StationCode, SessionNo}`;
- đổi `weigh_tickets` unique từ `TicketNo` sang `{StationCode, TicketNo}`;
- đổi `delivery_tickets` unique từ `DeliveryNo` sang `{StationCode, DeliveryNo}`.

Cần sửa:

- `src/StationApp.Infrastructure/Persistence/Configurations/WeighingSessionEntityConfigurations.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/WeighTicketEntityConfiguration.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/MasterDataEntityConfigurations.cs`
- bootstrap migration / compatibility logic tạo-dropping index cũ

Rà soát độ dài cột:

- `SessionNo` đang `nvarchar(50)` là đủ;
- `TicketNo` đang `nvarchar(20)` cần xác nhận đủ cho format mới;
- `DeliveryNo` đang `nvarchar(30)` là đủ.

Nếu `TicketNo` 20 ký tự không đủ trong tất cả trường hợp prefix cấu hình thực tế, tăng `max length` ngay trong cùng đợt thay đổi này.

### 4. Search, lookup và nhập tay

Tất cả nơi đang tra cứu theo `SessionNo`, `TicketNo`, `DeliveryNo` phải chấp nhận:

- số đầy đủ: `{CurrentStationCode}-LC26060066`
- số rút gọn: `LC26060066`

Quy tắc xử lý:

- nếu input có dấu `-`, ưu tiên tra cứu theo full number;
- nếu input không có dấu `-`, tra cứu theo:
  - exact old value cho dữ liệu cũ;
  - hoặc suffix sau dấu `-` cho dữ liệu mới.

Cần rà soát và cập nhật:

- `src/StationApp.Infrastructure/Repositories/WeighingSessionRepository.cs`
- `src/StationApp.Infrastructure/Repositories/TicketRepository.cs`
- các repository / filter / report đang `Contains` trực tiếp vào `SessionNo`, `TicketNo`, `DeliveryNo`
- luồng gắn cắt lệnh vào lượt cân bằng số lượt cân ở `IncomingVehicleListViewModel`

### 5. Hiển thị UI và chứng từ in

Tạo helper / formatter dùng chung để lấy số hiển thị rút gọn:

- nếu chuỗi có dạng `XXX-<businessNo>` thì lấy phần sau dấu `-`;
- nếu là dữ liệu cũ không có `-` thì giữ nguyên.

Áp dụng cho:

- UI nội bộ đang bind trực tiếp `SessionNo`, `TicketNo`, `DeliveryNo`;
- danh sách lượt cân, danh sách phiếu, danh sách xe ra, sync info, báo cáo, màn cân trạm đập, cân mỏ sét, cân nội địa, export...
- preview in và chứng từ in.

Yêu cầu in ấn:

- raw number trong DB và sync vẫn là full number;
- `DisplayNumber` và field in hiển thị cho người dùng dùng short number.

Cần sửa các điểm liên quan:

- `src/StationApp.Application/Printing/PrintContracts.cs`
- các ViewModel / DTO trình bày đang trả thẳng raw business number ra UI

### 6. Sync và tích hợp central

Sync contract không cần đổi shape nếu hiện tại đã truyền raw `TicketNo` / `SessionNo` / `DeliveryNo`, chỉ đổi giá trị thành full number.

Cần rà soát toàn bộ endpoint / SQL / upsert / central logic nếu có giả định ngầm format cũ, đặc biệt các kiểu:

- `LIKE 'LC%'`
- `LEFT(..., 6)`
- `RIGHT(..., 4)`
- `LEN(...) = 10`

Nếu có, cập nhật để parse format mới an toàn và vẫn tương thích với dữ liệu cũ.

## File / khu vực cần rà soát

Tối thiểu cần làm rõ và chỉnh sửa ở các khu vực sau:

- `src/StationApp.Infrastructure/Services/InfrastructureServices.cs`
- `src/StationApp.Infrastructure/Persistence/SchemaCompatibilityBootstrapper.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/WeighTicketEntityConfiguration.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/MasterDataEntityConfigurations.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/WeighingSessionEntityConfigurations.cs`
- `src/StationApp.Infrastructure/Repositories/WeighingSessionRepository.cs`
- `src/StationApp.Infrastructure/Repositories/TicketRepository.cs`
- các repository / query / report service đang filter hoặc hiển thị business number
- `src/StationApp.Application/Printing/PrintContracts.cs`
- các ViewModel / View XAML đang bind trực tiếp `SessionNo`, `TicketNo`, `DeliveryNo`

## Kế hoạch thực hiện

### Phase 1 - Chuẩn hóa quy tắc số mới

- cập nhật 3 generator;
- đưa `StationCode` vào full business number;
- đổi `counterKey` theo trạm;
- thêm log sinh số nếu cần để dễ đối chiếu khi rollout.

### Phase 2 - Chuẩn hóa persistence và compatibility

- đổi unique index ticket / delivery sang composite index có `StationCode`;
- cập nhật bootstrap logic tạo / sửa index;
- cập nhật self-heal `document_counters`;
- verify độ dài cột và tương thích dữ liệu cũ.

### Phase 3 - Chuẩn hóa tra cứu và giao diện

- tạo helper format display number;
- cập nhật lookup / search chấp nhận cả full và short;
- cập nhật UI nội bộ hiển thị short number;
- cập nhật chứng từ in hiển thị short number.

### Phase 4 - Rà soát sync / central

- kiểm tra payload sync mới;
- kiểm tra central parser / SQL / import / báo cáo;
- sửa các nơi đang giả định format cũ.

### Phase 5 - Kiểm thử và rollout

- test local 3 trạm khác `StationCode`;
- test tạo mới lượt cân / phiếu cân / phiếu giao nhận;
- test tìm kiếm full / short;
- test in ấn;
- test sync local -> central.

## Test cases

### 1. Sinh số và uniqueness

- tạo session mới ở 3 DB local có `QN01`, `QN02`, `QN03`;
- cùng thời điểm, cùng tháng, cùng sequence khởi tạo;
- kết quả business number phải khác nhau tuyệt đối.

Lặp lại tương tự cho:

- phiếu cân;
- phiếu giao nhận.

### 2. Hiển thị

- UI hiển `LC26060066`, không hiển `{CurrentStationCode}-LC26060066`;
- phiếu in hiển số rút gọn;
- dữ liệu cũ không prefix vẫn hiển thị bình thường.

### 3. Search / lookup

- search bằng `LC26060066` tìm được record raw `{CurrentStationCode}-LC26060066`;
- search bằng `{CurrentStationCode}-LC26060066` tìm được cùng record;
- gắn cắt lệnh bằng session short number vẫn chạy;
- search ticket và delivery bằng full / short đều đúng.

### 4. Compatibility

- DB chỉ có dữ liệu cũ vẫn khởi động được;
- DB trộn dữ liệu cũ + mới vẫn khởi động được;
- `document_counters` self-heal đúng;
- build pass các project:
  - `StationApp.Infrastructure`
  - `StationApp.Application`
  - `StationApp.UI`
  - `StationApp.Sync`

### 5. Sync end-to-end

- tạo dữ liệu mới ở local và đẩy sync lên central;
- central nhận full business number có `StationCode`;
- không phát sinh conflict do trùng business number giữa 3 trạm.

## Rủi ro và lưu ý

- Nếu hai trạm cùng cấu hình nhầm một `StationCode`, business number vẫn có thể trùng.
- Dữ liệu cũ không được backfill nên báo cáo / truy vấn lịch sử nếu gom nhiều trạm chỉ bằng business number cũ vẫn có thể mơ hồ.
- Các script SQL / report / API ngoài hệ thống nếu đang hardcode format cũ cần được rà soát trước rollout.
- Cần thông báo rõ cho vận hành rằng từ thời điểm rollout, DB lưu full number nhưng màn hình và phiếu in vẫn hiện số rút gọn.

## Giả định đã chốt

- Chỉ áp dụng format mới cho dữ liệu phát sinh mới.
- UI nội bộ và chứng từ in hiển thị short number.
- DB và sync giữ full number.
- Ô tìm kiếm / nhập tay chấp nhận cả full và short.
- Không thêm cột mới chỉ để lưu short number; short number được derive từ raw value.
