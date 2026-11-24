# Kiểm tra backend .NET — 2026-11-24

- .NET SDK 10.0.300 trên Windows.
- Restore từ NuGet cache thành công; package lockfile cho API và test được lưu.
- Build Release solution: 0 warning, 0 error. Publish API Release thành công.
- 11 nhóm HTTP integration với memory test store: qua.
- 12 nhóm HTTP integration với MongoDB 8.0.17 thật: qua. Mỗi fixture dùng
  database ngẫu nhiên và tự xóa database đó.
- Kiểm tra state/PKCE, pending login, callback dùng một lần, concurrent
  completion chỉ cấp một session, account upsert, token hash, logout từng máy,
  expiry không phụ thuộc TTL, Google thất bại/hủy, ownership, snapshot bất biến,
  restore v1/v2, validation/2 MiB/rate limit, Google authorization URL.
- Seed BSON theo backend Node cũ, xác nhận account/session/backup/index đọc
  được và Google đăng nhập lại giữ nguyên user ID.
- Electron desktop smoke qua UI → IPC → HTTP API .NET: qua (token mã hóa,
  restart, backup/restore, logout và account isolation).

Google identity giả chỉ trong test executable. Google OAuth với client thật
chưa nghiệm thu. HTTP production dùng thư viện Google để kiểm tra ID token;
test tự động không đăng nhập Google thật.

Sandbox chặn socket/Git/process Windows nên integration chạy ngoài sandbox.
NuGet audit bị tắt riêng trong lệnh restore offline tại máy kiểm tra; cấu hình
project/CI không tắt audit. Docker daemon chưa chạy, chưa chạy Docker build/Compose
trên máy này. Workflow CI đã tạo nhưng chưa chạy trên GitHub. Chưa deploy hosting.

Lệnh tái kiểm tra tại repo này:

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet run --project tests/DevBench.Api.Tests -c Release --no-build
$env:TEST_MONGODB_URI = "mongodb://127.0.0.1:27017"
dotnet run --project tests/DevBench.Api.Tests -c Release --no-build
Remove-Item Env:TEST_MONGODB_URI
```

Google client secret/MongoDB URI thật không được sao chép vào repo này.
