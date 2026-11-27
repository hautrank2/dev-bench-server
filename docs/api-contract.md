# Desktop HTTP contract

JSON camelCase, API tại root origin. HTTP do Electron main gọi, không cần CORS.

| Method | Path | Request / response |
| --- | --- | --- |
| GET | `/health` | `200 {status:"ok"}` |
| POST | `/auth/desktop/start` | `{challenge,deviceId,deviceName}` → `200 {loginId,authorizationUrl}` |
| GET | `/auth/google/callback?state=...&code=...` | Kết quả Google, text/plain cho browser |
| POST | `/auth/desktop/complete` | `{loginId,verifier}` → `202 {pending:true}` hoặc `200 {token,account}` |
| GET | `/me` | Bearer token → `200 {id,email,name}` hoặc `401` |
| POST | `/auth/logout` | Xóa phiên hiện tại, `204`, idempotent |
| GET | `/backups` | Bearer token → tối đa 500 backup mới nhất |
| POST | `/backups` | Bearer token + snapshot → `201 BackupInfo` |
| GET | `/backups/{id}` | Snapshot v2; account khác nhận `404` |

- Device/login/backup/set/project/repository IDs: UUID string.
- Challenge: base64url SHA-256 verifier, 43 ký tự; verifier: 43–128 ký tự base64url.
- Login hạn 5 phút; state và completion tiêu thụ nguyên tử một lần.
- Token 256 bit, chỉ lưu hash, session hạn 30 ngày, logout chỉ thu hồi phiên đó.
- Account: `{id,email,name}`; Google `sub` là khóa account duy nhất.
- BackupInfo: `{id,name,sourceDevice,platform,projectCount,repositoryCount,createdAt}`.
- Lỗi JSON `{error:string}`: 400 input/login lỗi, 401 phiên lỗi, 404 backup
  không thuộc account/không tồn tại, 413 quá 2 MiB, 429 rate limit.
- Rate limit 100 request/phút/IP; riêng start login 10/phút/IP.
- Giới hạn 2 MiB tính theo byte UTF-8 của request body; snapshot sau chuẩn hóa
  cũng đo như `JSON.stringify` của desktop.
- `POST /backups` yêu cầu JSON; thiếu/sai `Content-Type` hoặc charset trả 400.
- `GET /backups` bỏ qua bản có snapshot không đọc được thay vì làm lỗi cả danh
  sách; restore đúng bản đó trả 400.

## Snapshot v2

```json
{
  "kind": "dev-bench-project-set",
  "schemaVersion": 2,
  "exportedAt": "2026-11-24T00:00:00.000Z",
  "sourceDevice": "Work laptop",
  "platform": "win32",
  "appearance": {"theme": "system", "terminalTheme": "app"},
  "set": {
    "id": "40e03bc9-d686-4a35-a281-a84b6c700951",
    "name": "Work",
    "projects": [{"id": "77e1b9b0-09bb-4df2-8f38-347f677ddc42", "name": "OpsDesk", "description": ""}],
    "repositories": [{
      "id": "b064638c-cfc0-4618-8cbf-07f97880d363",
      "groupId": "77e1b9b0-09bb-4df2-8f38-347f677ddc42",
      "name": "Client", "folder": "D:\\Work\\client", "workingDirectory": "apps/web", "command": "pnpm dev",
      "url": "http://localhost:3000", "includeInRunAll": true
    }]
  }
}
```

Tối đa 1.000 project và 1.000 repository. ID duy nhất trong từng danh sách,
groupId phải thuộc project. Tên set/project/sourceDevice 1–100 ký tự, repo name
1–120, description tối đa 500, command tối đa 8.000. Folder bắt buộc; URL rỗng
hoặc HTTP/HTTPS. `workingDirectory` là chuỗi tùy chọn, trim, tối đa 4096 ký tự;
thiếu field hoặc chuỗi rỗng thì desktop chạy tại folder repo. Đường dẫn tương
đối được desktop resolve từ folder, đường dẫn tuyệt đối dùng trực tiếp. API giữ
field này khi lưu/restore v1/v2, không kiểm tra filesystem và không thực thi.
Null hoặc giá trị khác chuỗi bị từ chối. Không cần tăng schemaVersion.
Theme system/light/dark; terminalTheme app/midnight/paper/rose;
platform win32/darwin/linux/other. Field ngoài schema bị loại bỏ; owner lấy từ
session, không tin client userId.

V1: `set.projects` là repository; API chuyển thành project groups/repositories
trước khi lưu/trả, kể cả snapshot cũ trong DB. Không thực thi folder/command.
Backup không chứa source files, session hoặc Git credentials.

Thay đổi contract phải cập nhật tài liệu và xác minh luồng desktop liên quan khi
có môi trường phù hợp; không yêu cầu tạo lại thư mục test trong repo BE. Backend validation
bằng C#; `shared/` của desktop chỉ chia sẻ giữa Electron main/preload/renderer.
