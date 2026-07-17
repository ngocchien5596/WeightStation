# PLAN - Authorization RBAC (ADMIN, MANAGER, OPERATOR)

Tài liệu này là bản cập nhật RBAC hiện hành cho StationApp sau khi bổ sung role `MANAGER`.

## 1. Role của hệ thống

| Role | Tên hiển thị | Mục đích |
|---|---|---|
| `ADMIN` | Quản trị hệ thống | Toàn quyền hệ thống, cấu hình kỹ thuật, quản lý tài khoản và trạm |
| `MANAGER` | Quản lý | Quản lý nghiệp vụ theo các trạm được phân quyền |
| `OPERATOR` | Vận hành | Vận hành cân thông thường theo trạm |

## 2. Nguyên tắc chung

- Mọi user phải có `RoleCode` thuộc `ADMIN`, `MANAGER`, `OPERATOR`.
- Mọi user phải được gán ít nhất một trạm để thao tác nghiệp vụ.
- Manager có thể được gán nhiều trạm và chỉ thấy dữ liệu của trạm đang chọn.
- Menu visibility không đủ để coi là phân quyền. Các command/use case nhạy cảm vẫn phải guard bằng `StationAuthorization`.
- Các thao tác ảnh hưởng dữ liệu nghiệp vụ phải ghi audit log có `Actor`, `RoleCode`, `StationCode`, `Action`, `EntityName`, `EntityId` và payload đủ để hậu kiểm.

## 3. Capability chính

| Capability | Admin | Manager | Operator |
|---|---:|---:|---:|
| Xem/vận hành các màn cân | Có | Có | Có |
| Cân tự động | Có | Có | Có |
| Cân tay | Có | Có | Không |
| Xem báo cáo | Có | Có | Có |
| Xem lịch sử chỉnh sửa/audit log của trạm | Có | Có | Không |
| Cập nhật ứng dụng | Có | Có | Có |
| Quản lý tài khoản | Có | Không | Không |
| Gán trạm cho tài khoản | Có | Không | Không |
| Danh mục trạm / feature theo trạm | Có | Không | Không |
| Tham số hệ thống | Có | Không | Không |
| Cấu hình camera | Có | Không | Không |
| Cấu hình thiết bị cân | Có | Không | Không |
| Cấu hình in/layout | Có | Không | Không |
| Đồng bộ / diagnostics kỹ thuật | Có | Không | Không |

## 4. Master data theo trạm

| Trạm | Admin | Manager | Operator |
|---|---:|---:|---:|
| `QN01` | Có | Có | Có |
| `QN02` | Có | Có | Không |
| `QN03` | Có | Có | Không |
| Trạm khác | Có | Có | Không, trừ khi có rule riêng |

Master data gồm: danh mục xe, khách hàng, sản phẩm.

## 5. Cân tay

- `ADMIN` và `MANAGER` được chọn chế độ Cân tay.
- `OPERATOR` không thấy và không dùng được Cân tay.
- Use case lưu cân phải từ chối nếu `OPERATOR` cố gọi manual mode bằng đường khác.
- Lượt cân tay phải ghi nhận user, role, mode và station trong dữ liệu/audit liên quan.

## 6. Menu và màn hình

- Manager thấy các màn vận hành, báo cáo, master data, lịch sử chỉnh sửa và cập nhật ứng dụng theo trạm được phân quyền.
- Manager không thấy các màn Admin-only: quản lý tài khoản, danh mục trạm, tham số hệ thống, camera, thiết bị cân, cấu hình in/layout, đồng bộ, External Datacan, diagnostics.
- Operator QN01 thấy master data.
- Operator QN02/QN03 không thấy master data.
- Operator không thấy lịch sử chỉnh sửa/audit log.

## 7. Account management

- Chỉ Admin được tạo/sửa/khóa/mở/reset mật khẩu tài khoản.
- Dropdown role gồm `ADMIN`, `MANAGER`, `OPERATOR`.
- Search role gồm `Tất cả`, `ADMIN`, `MANAGER`, `OPERATOR`.
- Luôn còn ít nhất một tài khoản `ADMIN` active.
- Không role nào được tự đổi role hoặc tự gán thêm trạm cho chính mình.

## 8. Station scope

- Mọi query nghiệp vụ, báo cáo và audit log phải lọc theo station context hiện tại.
- Khi user đổi trạm, menu, dashboard, report và selection phải reload.
- Background sync không phụ thuộc vào role/session người dùng đang đăng nhập.

## 9. Verification

- Test Admin: toàn quyền như hiện tại.
- Test Manager nhiều trạm: chọn/đổi trạm, Cân tay, master data, báo cáo, audit log.
- Test Manager không vào được màn Admin-only.
- Test Operator QN01: vào master data, không Cân tay, không audit log.
- Test Operator QN02/QN03: không master data, không Cân tay, không audit log.
- Test tất cả role cập nhật ứng dụng được.
