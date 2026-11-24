# Dev Bench API

Backend độc lập **ASP.NET Core .NET 10 + MongoDB** cho Dev Bench desktop.
Không cần Node.js, pnpm hoặc source desktop để build/chạy.

## Chức năng

- Google OpenID Connect qua browser, tự tạo account lần đầu; state, nonce, S256 PKCE.
- Phiên riêng cho mỗi thiết bị, hạn 30 ngày, server chỉ lưu SHA-256 token.
- Backup bất biến theo tài khoản/máy, validation, giới hạn 2 MiB, restore v1/v2.
- Rate limiting và security headers. HTTP contract tại [docs/api-contract.md](docs/api-contract.md).

## Cấu trúc

```text
src/DevBench.Api/             API, Google provider, MongoDB store
tests/DevBench.Api.Tests/     Integration executable và harness cho desktop
docs/                        HTTP contract và kết quả kiểm tra
Dockerfile                   Build/runtime, chạy dưới user app
compose.yaml                 API + MongoDB local, volume dữ liệu
.github/workflows/ci.yml      Build, test MongoDB và build Docker
```

## Chạy bằng .NET SDK

Cần .NET SDK 10 và MongoDB đang chạy. Tại thư mục repo:

```powershell
$env:PUBLIC_API_ORIGIN = "http://localhost:4318"
$env:MONGODB_URI = "mongodb://127.0.0.1:27017"
$env:MONGODB_DB = "dev_bench"
$env:GOOGLE_CLIENT_ID = "your-web-client-id.apps.googleusercontent.com"
$env:GOOGLE_CLIENT_SECRET = "your-client-secret"
dotnet restore --locked-mode
dotnet run --project src/DevBench.Api --urls http://localhost:4318
```

`global.json` chọn .NET 10, cho phép feature band mới hơn. Biến môi trường được
đọc trực tiếp; `dotnet run` không tự nạp `.env`. File `.env.example` dùng cho
Docker Compose hoặc công cụ nạp env của bạn. Không commit secret thật vào Git.

`http://localhost:4318/health` trả `{"status":"ok"}`. API kiểm tra config,
kết nối MongoDB và tạo index trước khi nhận request.

Google OAuth client loại **Web application**, Authorized redirect URI:
`http://localhost:4318/auth/google/callback`. Nếu consent screen đang Testing,
thêm email vào Test users. Production đặt `PUBLIC_API_ORIGIN=https://your-api.example.com`
và đăng ký callback cùng origin. HTTPS bắt buộc bên ngoài localhost.

## Docker

```powershell
Copy-Item .env.example .env
# Điền GOOGLE_CLIENT_ID/GOOGLE_CLIENT_SECRET trong .env bằng editor.
docker compose up --build -d
```

API ở localhost:4318, MongoDB ở localhost:27017. Compose dùng MongoDB nội bộ
thay cho URI trong `.env`; dữ liệu nằm trong volume `mongo-data`. Compose này
phục vụ local, MongoDB không có tài khoản và chỉ publish ra loopback.
Với Atlas/DB ngoài, chạy Dockerfile riêng và truyền URI tại host.

```powershell
dotnet publish src/DevBench.Api -c Release -o artifacts/publish
docker build -t dev-bench-server .
```

Runtime container nghe HTTP 8080 dưới user không có quyền root; TLS do host/proxy
cung cấp. Proxy giữ nguyên path từ root, không thêm `/api`. Không ghi callback
query/token vào access log. Rate limit mặc định tính theo IP kết nối; nếu dùng
proxy, chỉ cấu hình forwarded headers khi đã biết địa chỉ proxy tin cậy.

## Kết nối desktop

Trong Dev Bench: Settings → Account & backup → Backup server URL → Save server
→ Continue with Google. Nhập origin backend, không thêm `/api`. Hai repo giao
tiếp bằng HTTP, không tham chiếu source/path của nhau. Desktop local vẫn dùng
được khi API không chạy. Backend secret/MongoDB URI không nằm trong desktop.

## Kiểm thử

```powershell
dotnet build -c Release
dotnet run --project tests/DevBench.Api.Tests -c Release
```

Tests là executable integration, trả exit code 1 nếu lỗi; chạy bằng `dotnet run`,
không phải `dotnet test`. Dùng HTTP/Kestrel thật, Google provider giả và store
trong bộ nhớ. Để kiểm tra database thật, dùng MongoDB local dành cho test:

```powershell
$env:TEST_MONGODB_URI = "mongodb://127.0.0.1:27017"
dotnet run --project tests/DevBench.Api.Tests -c Release
Remove-Item Env:TEST_MONGODB_URI
```

Mỗi fixture tạo/xóa riêng database `dev_bench_test_<random>`. CI chạy chế độ
MongoDB thật. Kiểm tra PKCE/state dùng một lần, concurrent completion, upsert,
hash token, logout từng máy, expiry, rate limit, validation, backup v1/v2 và
cách ly tài khoản. Google OAuth thật cần nghiệm thu thủ công với client thật.

Cloud smoke giữa hai repo: terminal tại repo API, khởi động harness mới:

```powershell
dotnet run --project tests/DevBench.Api.Tests -- --serve
```

Harness chỉ bind localhost:14318, giả identity test và không nằm trong production
publish/Docker. Tại repo desktop:

```powershell
$env:DEV_BENCH_TEST_API_ORIGIN = "http://127.0.0.1:14318"
pnpm test:cloud-e2e
```

## Chuyển từ backend Node cũ

Giữ MongoDB URI/database và public origin để tiếp tục dùng account, session,
backup cũ. Collections/index/fields vẫn là `users`, `sessions`, `desktop_logins`,
`project_backups`, ID string, ngày BSON Date. User cũ có field `id` thừa vẫn đọc
được. Snapshot v1 được chuẩn hóa khi đọc. Dừng Node trước khi chuyển traffic,
đợi login đang diễn ra hoặc đăng nhập lại. Desktop giữ nguyên HTTP contract.

Repo API là Git repo mới. Sau khi tạo GitHub repo rỗng:

```powershell
git add .
git commit -m "Initialize ASP.NET Core Dev Bench API"
git remote add origin https://github.com/YOUR_ACCOUNT/dev-bench-server.git
git push -u origin main
```

Tham khảo chính thức: [ASP.NET rate limiting](https://learn.microsoft.com/aspnet/core/performance/rate-limit?view=aspnetcore-10.0),
[Google token validation](https://docs.cloud.google.com/dotnet/docs/reference/Google.Apis/latest/Google.Apis.Auth.GoogleJsonWebSignature),
[MongoDB .NET driver](https://www.mongodb.com/docs/drivers/csharp/current/).
