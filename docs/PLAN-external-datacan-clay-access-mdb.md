# Plan tích hợp dữ liệu cân cũ Mỏ sét từ file Access MDB

## 1. Mục tiêu

Nâng cấp màn **Lịch sử cân (PM cũ)** để xem được dữ liệu cân cũ của **Mỏ sét** từ file Access `dbcta.mdb`, tương tự cách hiện tại đang xem dữ liệu cũ của **Trạm cân NMC** và **Mỏ đá** từ SQL Server.

Phạm vi lần này là **đọc dữ liệu để xem lại**, không import cố định vào database mới và không sửa dữ liệu trong file MDB.

## 2. Hiện trạng đã rà soát

- Màn hiện tại dùng `ExternalDatacanViewModel` và `ExternalDatacanQueryService`.
- Nguồn dữ liệu hiện tại:
  - `Trạm cân NMC`: đọc SQL Server qua `ExternalDatacanConnection`.
  - `Mỏ đá`: đọc SQL Server qua `ExternalCrusherConnection`.
- File `dbcta.mdb` đã kiểm tra được bằng `Microsoft.ACE.OLEDB.16.0`.
- Mật khẩu mở file MDB đã thử đúng: `cta@2014`.
- Bảng cần đọc: `tbl_weigh`.
- Số dòng hiện tại trong `tbl_weigh`: khoảng 2.820 dòng.

## 3. Cấu trúc dữ liệu MDB cần dùng

Bảng `tbl_weigh` có các cột chính:

| Cột MDB | Ý nghĩa dự kiến | Mapping lên DTO hiện tại |
| --- | --- | --- |
| `ID` | ID dòng cân trong phần mềm cũ | Dùng để sort phụ, không hiển thị chính |
| `SO_PHIEU` | Số phiếu | `TicketNo` |
| `BIEN_SO` | Biển số xe | `VehiclePlate` |
| `LOAI_HANG` | Hàng hóa/sản phẩm | `ProductName` |
| `BEN_BAN` | Đơn vị bán/đơn vị vận chuyển | `CustomerName` |
| `BEN_MUA` | Bên mua | Có thể đưa vào ghi chú/chi tiết nếu cần |
| `KL_TONG` | Trọng lượng tổng | `Weight1` |
| `KL_BI` | Trọng lượng bì | `Weight2` |
| `KL_HANG` | Trọng lượng hàng | `NetWeight` |
| `NGAY_VAO` | Giờ cân vào, dạng `yyyyMMddHHmmss` | `Weight1Time` |
| `NGAY_RA` | Giờ cân ra, dạng `yyyyMMddHHmmss`, có thể rỗng | `Weight2Time` |
| `PHAN_LOAI` | Loại cân, ví dụ `Cân nhập` | `GroupName` |
| `TRANG_THAI` | Trạng thái phần mềm cũ | Có thể map text hoặc để chi tiết |
| `NGUOI_CAN` | Người cân | `OperatorName` |

## 4. Quyết định thiết kế

1. Thêm nguồn dữ liệu **Mỏ sét** vào dropdown nguồn dữ liệu trên màn **Lịch sử cân (PM cũ)**.
2. Đường dẫn file MDB đặt trong `appsettings.json`, để khi có file mới chỉ cần thay file trên server hoặc đổi path cấu hình.
3. Ứng dụng đọc file MDB qua đường dẫn server dạng UNC.
4. Trước khi query, ứng dụng copy file MDB từ server về thư mục tạm trên máy local rồi mở bản copy.
   - Tránh lock file trên server.
   - Tránh lỗi nếu nhiều máy cùng xem.
   - Giảm rủi ro đọc file đang bị ghi/đang copy dở.
5. Dữ liệu từ MDB chỉ đọc, không update, không ghi audit log.
6. Không log mật khẩu MDB hoặc full connection string ra log/toast/UI.
7. Giữ nguyên DTO và grid hiện tại nếu đủ dùng, chỉ mở rộng service để hỗ trợ thêm nhánh Access.

## 5. Cấu hình đề xuất

Thêm cấu hình mới vào `src/StationApp.UI/appsettings.json`:

```json
"ExternalClayAccess": {
  "FilePath": "\\\\10.0.0.3\\17. data dung chung\\Chienbn\\Phan_mem_can\\DB_MoSet\\dbcta.mdb",
  "Password": "cta@2014",
  "CopyToTempBeforeRead": true
}
```

Ghi chú:

- Path chính thức cần chốt lại theo thư mục server thực tế.
- Máy client cần có quyền đọc file trong shared folder.
- Nếu sau này muốn bảo mật hơn, có thể chuyển password sang biến môi trường hoặc cơ chế protected config.

## 6. Luồng vận hành file mới trên server

Khi có file MDB mới:

1. Đặt file mới vào cùng thư mục server đã cấu hình.
2. Nên copy lên với tên tạm, ví dụ `dbcta_20260716.tmp`.
3. Sau khi copy xong hoàn toàn thì rename thành `dbcta.mdb`.
4. Lưu file cũ vào thư mục archive nếu cần đối soát.
5. Không cần build/publish lại app nếu path và password không đổi.

## 7. Chuẩn bị môi trường cho máy chưa có ACE OLEDB

Yêu cầu mới: máy trạm khác chưa cài **Microsoft Access Database Engine/ACE OLEDB 64-bit** vẫn phải có cách dùng được, không để người dùng tự mò lỗi provider.

### 7.1 Quyết định triển khai

1. Bộ publish cần kèm thư mục `prerequisites`.
2. Trong `prerequisites` đặt bộ cài Microsoft Access Database Engine 64-bit, ví dụ:
   - `AccessDatabaseEngine_X64.exe`
   - hoặc tên file chính thức đang dùng tại thời điểm triển khai.
3. App vẫn không nhúng ACE OLEDB vào trong exe vì ACE là provider COM/driver hệ thống, cần được cài trên Windows.
4. Khi mở màn **Lịch sử cân (PM cũ)** hoặc khi chọn nguồn **Mỏ sét**, app kiểm tra provider ACE trước.
5. Nếu thiếu provider, app hiển thị thông báo rõ:
   - Máy chưa có Microsoft Access Database Engine/ACE OLEDB 64-bit.
   - Vui lòng chạy file cài trong thư mục `prerequisites`.
   - Nếu app được publish qua shared folder, chỉ rõ đường dẫn file cài đặt.
6. Cung cấp script cài đặt để kỹ thuật viên chạy nhanh trên máy trạm.

### 7.2 Phương án cài đặt đề xuất

Tạo script:

```powershell
scripts\install-access-database-engine-prerequisite.ps1
```

Script này sẽ:

- Kiểm tra máy đã có `Microsoft.ACE.OLEDB.16.0` hoặc `Microsoft.ACE.OLEDB.12.0` chưa.
- Nếu có rồi thì báo đã sẵn sàng.
- Nếu chưa có, tìm file cài trong thư mục `prerequisites`.
- Chạy installer ở chế độ quiet/passive nếu hỗ trợ.
- Sau khi cài xong, kiểm tra lại provider.
- Trả exit code rõ ràng để dễ hỗ trợ từ xa.

### 7.3 Tích hợp vào publish

Nâng cấp `scripts\publish-shared-folder-release.ps1` hoặc thêm bước phụ để:

- Copy thư mục `prerequisites` lên cùng thư mục release.
- Ghi vào `publish-release-log.md` rằng bản này cần/đã kèm prerequisite ACE OLEDB.
- Không bắt buộc cài tự động khi update app, vì cài driver hệ thống có thể cần quyền admin.

Cấu trúc release đề xuất:

```text
Phan_mem_can/
  latest.json
  publish-release-log.md
  releases/
    StationApp.UI-1.1.x-win-x64.zip
  prerequisites/
    AccessDatabaseEngine_X64.exe
    install-access-database-engine-prerequisite.ps1
    README-AccessDatabaseEngine.md
```

### 7.4 Trải nghiệm người dùng khi thiếu provider

Khi người dùng chọn nguồn **Mỏ sét** mà máy chưa có ACE:

- Không crash app.
- Không hiện lỗi kỹ thuật dài kiểu provider not registered.
- Hiển thị thông báo ngắn:

```text
Máy này chưa cài Microsoft Access Database Engine 64-bit nên chưa đọc được dữ liệu Mỏ sét từ file MDB.
Vui lòng chạy bộ cài trong thư mục prerequisites của bản phát hành, sau đó mở lại ứng dụng.
```

Nếu tìm được path release/prerequisites thì hiển thị thêm đường dẫn.

### 7.5 Tiêu chí nghiệm thu riêng cho môi trường

- [ ] Máy đã cài ACE: chọn **Mỏ sét** đọc MDB bình thường.
- [ ] Máy chưa cài ACE: app báo lỗi thân thiện, không crash.
- [ ] Bộ publish có thư mục `prerequisites`.
- [ ] Có script kiểm tra/cài ACE cho kỹ thuật viên.
- [ ] Tài liệu vận hành ghi rõ khi nào cần cài và cách cài.
- [ ] Không tự động cài driver hệ thống khi người dùng chỉ bấm cập nhật app.

## 8. Các bước thực hiện

### Task 1: Bổ sung cấu hình đọc MDB

**Mô tả:** Thêm cấu hình `ExternalClayAccess` và model/options tương ứng để service đọc được path, password, cờ copy temp.

**Acceptance criteria:**

- [ ] Có cấu hình rõ ràng cho path MDB, password, copy temp.
- [ ] Khi thiếu path hoặc file không tồn tại, app báo lỗi dễ hiểu.
- [ ] Không in password ra log/toast.

**Files dự kiến:**

- `src/StationApp.UI/appsettings.json`
- `src/StationApp.Infrastructure/Services/ExternalDatacanQueryService.cs`

### Task 2: Chuẩn bị prerequisite ACE OLEDB cho bộ publish

**Mô tả:** Thêm script kiểm tra/cài Microsoft Access Database Engine 64-bit và cập nhật quy trình publish để kèm thư mục `prerequisites`.

**Acceptance criteria:**

- [ ] Có script kiểm tra provider ACE OLEDB.
- [ ] Script báo rõ đã cài/chưa cài/cài lỗi.
- [ ] Bộ publish shared folder có thể kèm thư mục `prerequisites`.
- [ ] Tài liệu hướng dẫn cài đặt không yêu cầu người dùng tự tìm link tải.
- [ ] Không tự động cài driver khi người dùng bấm cập nhật app.

**Files dự kiến:**

- `scripts/install-access-database-engine-prerequisite.ps1`
- `scripts/publish-shared-folder-release.ps1`
- `docs/APP-VERSIONING.md` hoặc tài liệu vận hành phù hợp

### Task 3: Thêm nguồn dữ liệu Mỏ sét trên UI

**Mô tả:** Thêm option **Mỏ sét** vào dropdown nguồn dữ liệu màn **Lịch sử cân (PM cũ)**.

**Acceptance criteria:**

- [ ] Dropdown có đủ 3 nguồn: `Trạm cân NMC`, `Mỏ đá`, `Mỏ sét`.
- [ ] Chọn `Mỏ sét` sẽ gọi nhánh đọc MDB.
- [ ] Các text tiếng Việt trên màn không bị lỗi encoding.
- [ ] Nếu thiếu ACE OLEDB, app hiển thị hướng dẫn cài prerequisite thay vì lỗi kỹ thuật.

**Files dự kiến:**

- `src/StationApp.UI/ViewModels/Settings/ExternalDatacanViewModel.cs`
- `src/StationApp.UI/Views/Settings/ExternalDatacanView.xaml`

### Task 4: Tách luồng query SQL Server và Access MDB

**Mô tả:** Refactor `ExternalDatacanQueryService` để route theo nguồn dữ liệu:

- NMC/Mỏ đá: giữ nguyên SQL Server.
- Mỏ sét: đọc Access MDB.

**Acceptance criteria:**

- [ ] Luồng SQL hiện tại không bị thay đổi kết quả.
- [ ] Luồng Mỏ sét dùng provider ACE OLEDB.
- [ ] Có fallback thử `Microsoft.ACE.OLEDB.16.0`, sau đó `Microsoft.ACE.OLEDB.12.0`.
- [ ] Nếu máy chưa cài ACE OLEDB thì báo rõ cần cài Microsoft Access Database Engine 64-bit.
- [ ] Có hàm preflight kiểm tra provider trước khi mở file MDB.

**Files dự kiến:**

- `src/StationApp.Infrastructure/Services/ExternalDatacanQueryService.cs`

### Task 5: Copy MDB về temp trước khi đọc

**Mô tả:** Implement bước copy file MDB từ server về local temp trước khi mở connection.

**Acceptance criteria:**

- [ ] Mỗi lần query dùng một file temp riêng, tránh trùng giữa nhiều lần xem.
- [ ] File temp được xóa sau khi query xong.
- [ ] Nếu copy lỗi, thông báo rõ có thể do quyền truy cập server/file đang bị khóa.
- [ ] Không mở trực tiếp file server nếu `CopyToTempBeforeRead = true`.

**Files dự kiến:**

- `src/StationApp.Infrastructure/Services/ExternalDatacanQueryService.cs`

### Task 6: Implement mapper dữ liệu `tbl_weigh`

**Mô tả:** Query `tbl_weigh`, parse ngày giờ và map sang `ExternalDatacanRecordDto`.

**Acceptance criteria:**

- [ ] Parse được `NGAY_VAO`, `NGAY_RA` dạng `yyyyMMddHHmmss`.
- [ ] `NGAY_RA` rỗng/null không làm crash app.
- [ ] Dữ liệu cân chưa hoàn thành vẫn hiển thị được, các trường thiếu để trống/0 hợp lý.
- [ ] Sort mặc định theo thời gian cân ra/cân vào mới nhất trước, sau đó theo `ID`.
- [ ] Filter theo ngày, số xe, số phiếu, khách hàng/hàng hóa vẫn hoạt động.

**Files dự kiến:**

- `src/StationApp.Infrastructure/Services/ExternalDatacanQueryService.cs`
- `src/StationApp.Application/DTOs/Dtos.cs` nếu DTO hiện tại thiếu trường cần hiển thị

### Task 7: Xử lý phân trang và hiệu năng

**Mô tả:** Vì file hiện tại khoảng 2.820 dòng, phiên bản đầu có thể đọc dữ liệu đã lọc rồi phân trang trong memory. Nếu file tăng lớn, có thể nâng cấp keyset paging sau.

**Acceptance criteria:**

- [ ] Với file hiện tại, thao tác xem không bị chậm rõ rệt.
- [ ] Có giới hạn an toàn hoặc cảnh báo nếu dữ liệu lọc quá lớn.
- [ ] Tổng số bản ghi trả về đúng với filter.

**Files dự kiến:**

- `src/StationApp.Infrastructure/Services/ExternalDatacanQueryService.cs`

### Task 8: Cải thiện thông báo lỗi vận hành

**Mô tả:** Chuẩn hóa lỗi thường gặp khi đọc MDB.

**Acceptance criteria:**

- [ ] Sai mật khẩu MDB: báo rõ không mở được file Access.
- [ ] Thiếu ACE OLEDB: báo rõ cần cài Access Database Engine.
- [ ] Không truy cập được file server: báo rõ kiểm tra quyền/path/mạng.
- [ ] File đang copy dở hoặc hỏng: báo rõ kiểm tra lại file nguồn.

**Files dự kiến:**

- `src/StationApp.Infrastructure/Services/ExternalDatacanQueryService.cs`
- `src/StationApp.UI/ViewModels/Settings/ExternalDatacanViewModel.cs`

### Task 9: Test và kiểm chứng

**Mô tả:** Bổ sung test cho logic parse/map, và kiểm thử thủ công với file `dbcta.mdb`.

**Acceptance criteria:**

- [ ] Unit test parse ngày `yyyyMMddHHmmss`.
- [ ] Unit test map row thiếu `NGAY_RA`.
- [ ] Manual test chọn `Mỏ sét` xem được dữ liệu.
- [ ] Manual test filter theo khoảng ngày.
- [ ] Manual test filter theo số xe/số phiếu.
- [ ] Build app thành công.

**Files dự kiến:**

- `tests/StationApp.Application.Tests/...` hoặc test project phù hợp hiện có
- `src/StationApp.Infrastructure/Services/ExternalDatacanQueryService.cs`

### Task 10: Cập nhật tài liệu vận hành

**Mô tả:** Ghi lại cách đặt file MDB mới trên server và cách chuẩn bị ACE OLEDB trên máy client.

**Acceptance criteria:**

- [ ] Có hướng dẫn thay file MDB mới.
- [ ] Có hướng dẫn quyền đọc shared folder.
- [ ] Có hướng dẫn chạy script cài Microsoft Access Database Engine 64-bit từ thư mục `prerequisites`.
- [ ] Có hướng dẫn kiểm tra máy đã có ACE OLEDB hay chưa.
- [ ] Có note không cần publish lại app nếu path không đổi.

**Files dự kiến:**

- `SRSdocs/HDSD_TramCan.md` hoặc tài liệu hướng dẫn vận hành phù hợp

## 9. Rủi ro và cách xử lý

| Rủi ro | Mức độ | Cách xử lý |
| --- | --- | --- |
| Máy client chưa cài ACE OLEDB | Cao | Bộ publish kèm `prerequisites`, có script kiểm tra/cài, app báo lỗi thân thiện kèm hướng dẫn |
| File MDB trên server đang bị copy dở | Trung bình | Khuyến nghị copy file tạm rồi rename; app copy local trước khi đọc |
| File MDB đổi password | Trung bình | Cho cấu hình password trong appsettings; báo lỗi rõ khi không mở được |
| Dữ liệu ngày trong MDB sai format | Trung bình | Parse an toàn, dòng lỗi format vẫn hiển thị phần dữ liệu còn lại nếu có thể |
| File MDB tương lai lớn hơn nhiều | Trung bình | Bản đầu phân trang memory; nếu lớn, nâng cấp keyset paging theo `ID`/ngày |
| Lộ password trong log | Cao | Không log connection string/password; không hiển thị password trên UI |

## 10. Tiêu chí nghiệm thu cuối

- [ ] Người dùng chọn nguồn **Mỏ sét** trên màn **Lịch sử cân (PM cũ)** và xem được dữ liệu từ `dbcta.mdb`.
- [ ] Filter theo ngày và các tiêu chí hiện có hoạt động ổn.
- [ ] Dữ liệu hiển thị đúng số phiếu, biển số, hàng hóa, khách hàng/đơn vị, cân lần 1, cân lần 2, TL hàng, ngày cân, người cân.
- [ ] App không khóa file MDB trên server.
- [ ] Có thông báo lỗi dễ hiểu khi thiếu provider, sai path, sai password hoặc mất quyền đọc.
- [ ] Máy trạm chưa có ACE OLEDB có thể được chuẩn bị bằng bộ cài/script đi kèm bản publish.
- [ ] Không ảnh hưởng luồng xem dữ liệu cũ của NMC và Mỏ đá.

## 11. Cần chốt trước khi code

1. Path server chính thức để lưu `dbcta.mdb`: `\\10.0.0.3\17. data dung chung\Chienbn\Phan_mem_can\DB_MoSet`.
2. Password MDB dùng trong `appsettings.json`.
3. Với Mỏ sét, cột `BEN_BAN` hiển thị trên grid là **Khách hàng**.
4. Chỉ giữ các cột hiện tại của màn **Lịch sử cân (PM cũ)**, chưa hiển thị thêm `BEN_MUA`, `PHAN_LOAI`, `TRANG_THAI`.
5. File cài Microsoft Access Database Engine 64-bit không thể đóng gói như một DLL .NET bình thường trong app. ACE OLEDB là provider COM/OLE DB cấp hệ điều hành, cần được cài/đăng ký trên Windows. Phương án triển khai là kèm bộ cài trong `prerequisites`, script kiểm tra/cài, và app kiểm tra provider trước khi đọc MDB.
