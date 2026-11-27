# Dev Bench API

Backend độc lập **ASP.NET Core .NET 10 + MongoDB** cho Dev Bench desktop.
Không cần Node.js, pnpm hoặc source desktop để build/chạy.

## Chức năng

- Google OpenID Connect qua browser, tự tạo account lần đầu; state, nonce, S256 PKCE.
- Phiên riêng cho mỗi thiết bị, hạn 30 ngày, server chỉ lưu SHA-256 token.
- Backup bất biến theo tài khoản/máy, validation, giới hạn 2 MiB, restore v1/v2.
- Rate limiting và security headers. HTTP contract tại [docs/api-contract.md](docs/api-contract.md).

## Cấu trúc

Solution ở gốc, project API nằm trong `DevBench.Api/`, theo bố cục repo Authly.
Không có thư mục `src`.

```text
DevBench.Api.slnx
DevBench.Api/
  DevBench.Api.csproj
  Program.cs                     Entry point
  ApiApplication.cs              Đăng ký DI và HTTP pipeline
  Controllers/                   Auth, backups, health; nhận/trả HTTP
  Services/                      Xử lý nghiệp vụ và Google OAuth
    Interfaces/                  Hợp đồng service cho dependency injection
  Repositories/                  ICloudStore và MongoCloudStore
  Models/                        Entity MongoDB và model nội bộ
    Dtos/                        Request/response cho desktop
  Configuration/                 Đọc/kiểm tra cấu hình
  Middlewares/                   Xử lý lỗi và security headers
  Security/                      Sinh secret và hash token/PKCE
  Validation/                    Kiểm tra/chuẩn hóa backup v1/v2
  Properties/launchSettings.json  Profile Development, cổng 4318
  appsettings.json               Cấu hình chung, không chứa secret thật
  appsettings.*.json.example     Mẫu Development và Production
CLAUDE.md                       Hướng dẫn Claude cho backend
.claude/rules/                  Quy tắc C#, API, security, MongoDB và cấu hình
.claude/skills/backend-api/      Workflow sửa endpoint và nghiệp vụ backend
docs/                            API contract, cấu hình và kết quả kiểm tra
Dockerfile                       Build/runtime, chạy dưới user app
compose.yaml                     API + MongoDB local
.env.example                     Mẫu biến môi trường cho Compose
.github/workflows/ci.yml          Build, đóng gói và deploy Azure từ main
```

Controller gọi service qua interface; service truy cập MongoDB qua `ICloudStore`.
Production luôn dùng MongoDB và Google thật. Repo BE không có thư mục/project test.

## Chạy bằng .NET SDK

Cần .NET SDK 10 và MongoDB đang chạy. Từ gốc repo, cấu hình bằng User Secrets:

```powershell
dotnet user-secrets set "Api:PublicOrigin" "http://localhost:4318" --project DevBench.Api
dotnet user-secrets set "MongoDb:ConnectionString" "mongodb://127.0.0.1:27017" --project DevBench.Api
dotnet user-secrets set "Google:ClientId" "your-web-client-id.apps.googleusercontent.com" --project DevBench.Api
dotnet user-secrets set "Google:ClientSecret" "your-client-secret" --project DevBench.Api
dotnet restore --locked-mode
dotnet run --project DevBench.Api
```

`global.json` chọn .NET 10, cho phép feature band mới hơn. Launch profile bật
Development và cổng 4318. `/health` trả `{"status":"ok"}` sau khi API kiểm tra
cấu hình, kết nối MongoDB và tạo index thành công.

Swagger UI chỉ bật trong Development: [http://localhost:4318/swagger/index.html](http://localhost:4318/swagger/index.html).
Đường dẫn `/swagger` tự chuyển đến giao diện này; OpenAPI JSON ở `/swagger/v1/swagger.json`.
Sau khi thay đổi code/package, dừng API cũ rồi chạy lại `dotnet run --project DevBench.Api`.

## Đặt biến môi trường ở đâu?

- **Local .NET:** User Secrets (khuyến nghị) hoặc copy
  `DevBench.Api/appsettings.Development.json.example` thành
  `DevBench.Api/appsettings.Development.json` rồi điền cấu hình. File thật được Git bỏ qua.
- **Docker Compose:** copy `.env.example` thành `.env` ở gốc repo.
- **Azure App Service:** Settings → Environment variables → App settings.
  Ví dụ `Api__PublicOrigin`, `MongoDb__ConnectionString`, `Google__ClientId`,
  `Google__ClientSecret`. Dấu `__` ánh xạ sang section JSON.

`appsettings.json` chỉ chứa cấu hình chung và khóa rỗng cho secret. `dotnet run`
không tự nạp `.env`. Không điền secret vào file mẫu được commit.
Xem [hướng dẫn cấu hình local, Docker và deploy Azure](docs/configuration.md)
để biết đủ khóa, độ ưu tiên và lệnh deploy. Các biến cũ như `MONGODB_URI`,
`GOOGLE_CLIENT_SECRET` vẫn dùng được.

Google OAuth client loại **Web application**, Authorized redirect URI local:
`http://localhost:4318/auth/google/callback`. Nếu consent screen đang Testing,
thêm email vào Test users. Production dùng origin HTTPS và callback cùng origin,
không thêm `/api`.

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
dotnet publish DevBench.Api/DevBench.Api.csproj -c Release -o artifacts/publish
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

## Tự deploy khi main thay đổi

Workflow `.github/workflows/ci.yml` chạy khi push/merge vào `main`:
restore locked mode → build Release → publish → kiểm tra Docker build → upload
package → đăng nhập Azure bằng OIDC → deploy Web App `DevBench` → kiểm tra `/health`.
Pull request vào `main` chỉ build/kiểm tra, không có quyền deploy.

Đây là workflow deploy duy nhất. Nếu Visual Studio đề nghị tạo thêm GitHub
Actions workflow, dùng workflow hiện có để tránh deploy trùng vào cùng Web App.

Đích hiện tại: `https://devbench-acdndvcbhrhgbkeq.westus3-01.azurewebsites.net`.
OIDC đã được cấu hình cho đúng repo `hautrank2/dev-bench-server`, nhánh `main`;
không cần Azure CLI, publish profile hoặc mật khẩu trên máy để chạy workflow.
Các giá trị cấu hình ứng dụng và secret vẫn đặt trong Azure App settings.

Sau khi commit/push các thay đổi repo này lên `main`, xem kết quả trong GitHub
→ Actions → **API build and Azure deploy**. Có thể bấm **Run workflow** với nhánh
`main` để chạy lại. Chi tiết cấu hình trong [docs/configuration.md](docs/configuration.md#github-actions-tự-deploy-main).

## Kiểm tra

Repo BE không có thư mục/project test. Từ gốc repo:

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet publish DevBench.Api/DevBench.Api.csproj -c Release --no-restore -o artifacts/publish
```

Khi có MongoDB và cấu hình hợp lệ, chạy `dotnet run --project DevBench.Api`,
kiểm tra `/health` trả `{"status":"ok"}`, sau đó xác minh flow Google sign-in,
backup/restore và logout liên quan bằng tài khoản/dữ liệu được phép dùng.
Không tạo hoặc xóa dữ liệu thật để làm fixture. Không thêm fake identity hoặc
store trong bộ nhớ vào production để thay cho cấu hình thiếu.

CI restore, build, publish và build Docker image; không chạy test project đã bỏ.
Build/publish không thay thế xác minh MongoDB, Google OAuth hoặc deployment thật.
Kết quả và giới hạn kiểm tra được ghi trong [docs/verification.md](docs/verification.md).

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
