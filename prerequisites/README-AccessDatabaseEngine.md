# Microsoft Access Database Engine prerequisite

Chức năng xem dữ liệu cân cũ Mỏ sét từ file `dbcta.mdb` cần Microsoft Access Database Engine/ACE OLEDB 64-bit được cài trên máy trạm Windows.

## File cần đặt trong thư mục này

Đặt bộ cài chính thức nội bộ vào thư mục này với một trong các tên sau:

- `AccessDatabaseEngine_X64.exe`
- `accessdatabaseengine_X64.exe`
- `AccessDatabaseEngine.exe`

Không commit file exe vào repo nếu bộ cài được quản lý riêng theo chính sách nội bộ.

## Kiểm tra máy đã có ACE chưa

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-access-database-engine-prerequisite.ps1 -CheckOnly
```

## Cài đặt

Mở PowerShell bằng quyền Administrator tại thư mục `prerequisites`, rồi chạy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-access-database-engine-prerequisite.ps1
```

Sau khi cài xong, mở lại ứng dụng trạm cân.
