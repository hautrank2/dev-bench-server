---
paths:
  - "DevBench.Api/Repositories/**/*.cs"
  - "DevBench.Api/Models/**/*.cs"
  - "DevBench.Api/Services/**/*.cs"
  - "DevBench.Api/Validation/**/*.cs"
---

# MongoDB và tương thích dữ liệu

Database dùng chung định dạng với backend Node cũ. `ICloudStore` là boundary,
`MongoCloudStore` là implementation duy nhất.

| Collection | Entity | Index (tên cố định) |
| --- | --- | --- |
| `users` | `UserRecord` | `googleSub_1` unique |
| `desktop_logins` | `LoginRecord` | `state_1` unique sparse; `expiresAt_1` TTL |
| `sessions` | `SessionRecord` | `tokenHash_1` unique; `expiresAt_1` TTL |
| `project_backups` | `BackupRecord` | `userId_1_createdAt_-1` |

- Giữ tên collection, field BSON, index, ID dạng UUID string, ngày BSON Date.
- Index tạo trong `InitializeAsync` trước khi nhận request. Đổi options của index
  đã có mà giữ tên sẽ làm startup lỗi trên database cũ; không tự drop index.
- `state` phải vắng mặt (không phải `null`) sau khi tiêu thụ để sparse index hoạt
  động: giữ `BsonIgnoreIfNull` và `Unset`.
- Upsert account theo Google sub, `Id` chỉ `SetOnInsert`; duplicate key (11000) do
  login đồng thời thì đọc lại user, không đổi ID account cũ.
- Backup append-only: không replace, upsert hay xóa. Danh sách tối đa 500 bản mới
  nhất; tra chi tiết bằng cả `Id` và `UserId`.
- Snapshot: ghi bằng `BsonDocument.Parse` trên JSON đã chuẩn hóa; đọc qua
  `BackupRecord.Json()` rồi `BackupValidator.Parse`. Siết validator có thể làm
  backup cũ không đọc được: danh sách bỏ qua bản đó và log warning chỉ gồm ID,
  restore trả 400. Document sai kiểu BSON vẫn làm hỏng cả query.
- Không tự migrate/drop dữ liệu hay thử thao tác phá hủy trên database người dùng.
