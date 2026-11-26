---
paths:
  - "DevBench.Api/Controllers/**/*.cs"
  - "DevBench.Api/Models/Dtos/**/*.cs"
  - "DevBench.Api/Services/**/*.cs"
  - "DevBench.Api/Middlewares/**/*.cs"
  - "DevBench.Api/Validation/**/*.cs"
  - "DevBench.Api/ApiApplication.cs"
  - "docs/api-contract.md"
---

# HTTP contract

`docs/api-contract.md` là bản đầy đủ (route, payload, giới hạn snapshot). Đọc
trước khi đổi route, DTO, status hoặc validation.

- API ở root origin: `/health`, `/auth/desktop/start`, `/auth/google/callback`,
  `/auth/desktop/complete`, `/me`, `/auth/logout`, `/backups`, `/backups/{id}`.
  `/me` khai báo bằng route tuyệt đối trong `AuthController`.
- JSON camelCase, không envelope. Lỗi `{error:string}`, không đổi sang
  ProblemDetails (`SuppressMapClientErrors` đang bật).
- Pending completion: 202 `{pending:true}`. Tạo backup: 201. Logout: 204, kể cả
  khi token sai hoặc đã hết hạn. Callback: `text/plain`, 200 hoặc 400.
- 400 input/login lỗi (gồm ID backup không phải UUID), 401 session lỗi, 404 backup
  không tồn tại hoặc khác account, 413 quá 2 MiB, 429 rate limit.
- DTO request để property nullable và service tự kiểm tra; đổi sang non-nullable
  sẽ kích hoạt validation ngầm của MVC.
- `POST /backups` nhận body thô, không `[FromBody]`, để xác thực trước khi đọc.
- Giới hạn body 2 MiB đặt ở Kestrel nên áp cho mọi endpoint. `BackupValidator`
  đo thêm theo cách `JSON.stringify` của desktop (`JsonSize`); không thay bằng
  độ dài `ToJsonString()` vì chuỗi đó bị escape và dài hơn.
- `POST /backups` thiếu/sai `Content-Type` hoặc charset trả 400 `Invalid request`.
- Message trong `ApiException` hiển thị cho người dùng desktop; đổi chữ cũng là
  đổi hành vi.
- `BackupValidator` bỏ field ngoài schema. Không tự thêm field FE đang có nhưng
  backend chưa hỗ trợ nếu chưa được yêu cầu.
- Electron main gọi HTTP: không mở CORS, không thêm cookie auth.
- Đổi contract thì cập nhật tài liệu và nêu tác động tới desktop; không sửa repo
  FE ngoài phạm vi được giao.
- Một số hành vi hiện tại chưa khớp contract; xem "Vấn đề đã biết" trong `CLAUDE.md`.
