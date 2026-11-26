# Cấu hình môi trường

Chạy các lệnh bên dưới từ gốc repo `dev-bench-server`, nơi có `DevBench.Api.slnx`.
API đọc cấu hình qua `IConfiguration` trong `DevBench.Api/Configuration/ApiOptions.cs`.

## Đặt giá trị ở đâu?

| Môi trường | Nơi đặt cấu hình |
| --- | --- |
| Cấu hình chung, không có secret | `DevBench.Api/appsettings.json`, được commit và publish |
| Local .NET (khuyến nghị) | .NET User Secrets, nằm ngoài repo |
| Local dùng file JSON | `DevBench.Api/appsettings.Development.json`, được Git bỏ qua |
| Docker Compose | `.env` ở gốc repo, được Git và Docker build context bỏ qua |
| Azure App Service | Settings → Environment variables → App settings |

Theo cấu hình mặc định của ASP.NET Core, độ ưu tiên từ cao xuống thấp là:
tham số dòng lệnh → biến môi trường → User Secrets (Development) →
`appsettings.{Environment}.json` → `appsettings.json`.
`DevBench.Api/Properties/launchSettings.json` chỉ thiết lập môi trường Development và cổng local;
không đặt secret vào file này. `dotnet run` không tự đọc `.env`.

## Các khóa

Cấu hình JSON chia thành section như Authly. Biến môi trường dùng `__` thay cho `:`.

| Khóa JSON / User Secrets | Biến môi trường | Bắt buộc / mặc định |
| --- | --- | --- |
| `Api:PublicOrigin` | `Api__PublicOrigin` | Bắt buộc, origin HTTPS; local cho phép HTTP localhost. Không thêm `/api`. |
| `MongoDb:ConnectionString` | `MongoDb__ConnectionString` | Bắt buộc, URI MongoDB. |
| `MongoDb:DatabaseName` | `MongoDb__DatabaseName` | Mặc định `dev_bench`. |
| `Google:ClientId` | `Google__ClientId` | Bắt buộc, Google OAuth Web application client ID. |
| `Google:ClientSecret` | `Google__ClientSecret` | Bắt buộc, secret của cùng OAuth client. |
| — | `ASPNETCORE_ENVIRONMENT` | `Development` cho local, `Production` cho Azure. |

Tên cũ `PUBLIC_API_ORIGIN`, `MONGODB_URI`, `MONGODB_DB`, `GOOGLE_CLIENT_ID`,
`GOOGLE_CLIENT_SECRET` tiếp tục được hỗ trợ để deployment cũ hoạt động.
Nếu khai báo cả hai cách, tên cũ được ưu tiên; khi chuyển sang tên mới,
xóa tên cũ tương ứng để tránh ghi đè ngoài ý muốn.

## Local với User Secrets

Project `DevBench.Api/DevBench.Api.csproj` đã có `UserSecretsId`, không cần chạy `dotnet user-secrets init`.
Thay các giá trị mẫu trước khi chạy:

```powershell
dotnet user-secrets set "Api:PublicOrigin" "http://localhost:4318" --project DevBench.Api
dotnet user-secrets set "MongoDb:ConnectionString" "mongodb://127.0.0.1:27017" --project DevBench.Api
dotnet user-secrets set "MongoDb:DatabaseName" "dev_bench" --project DevBench.Api
dotnet user-secrets set "Google:ClientId" "your-client-id.apps.googleusercontent.com" --project DevBench.Api
dotnet user-secrets set "Google:ClientSecret" "your-client-secret" --project DevBench.Api
dotnet run --project DevBench.Api
```

MongoDB phải chạy trước. Trên Windows, Secret Manager lưu tại
`%APPDATA%\Microsoft\UserSecrets\dev-bench-api-local\secrets.json`, không nằm trong
repo và không được publish. User Secrets là công cụ development, không mã hóa dữ liệu.
Launch profile bật Development nên ASP.NET Core tự nạp User Secrets khi `dotnet run`.

Nếu thích chỉnh file JSON:

```powershell
Copy-Item DevBench.Api/appsettings.Development.json.example DevBench.Api/appsettings.Development.json
# Mở DevBench.Api/appsettings.Development.json và điền các giá trị thật.
dotnet run --project DevBench.Api
```

Không điền secret vào file `.example` được commit. File Development thật được
loại khỏi Git, Docker build context và output publish. User Secrets và biến môi
trường có độ ưu tiên cao hơn file JSON này.

## Local với biến môi trường PowerShell

```powershell
$env:Api__PublicOrigin = "http://localhost:4318"
$env:MongoDb__ConnectionString = "mongodb://127.0.0.1:27017"
$env:MongoDb__DatabaseName = "dev_bench"
$env:Google__ClientId = "your-client-id.apps.googleusercontent.com"
$env:Google__ClientSecret = "your-client-secret"
dotnet run --project DevBench.Api
```

Các biến này áp dụng cho terminal hiện tại và các process con.

## Docker Compose

```powershell
Copy-Item .env.example .env
# Điền Google client ID và secret trong .env.
docker compose up --build -d
```

Compose truyền `.env` vào container. `MONGODB_URI` được Compose ghi đè thành
`mongodb://mongo:27017` để dùng MongoDB nội bộ. API ở `http://localhost:4318`.
Để dùng Atlas/DB ngoài, chạy image API riêng và truyền biến môi trường từ host.

## Azure App Service

1. Tạo Web App với Publish = Code, Linux, runtime .NET 10.
2. Copy Default domain trong Overview để lấy origin HTTPS thật.
3. Vào Settings → Environment variables → App settings và thêm năm biến môi trường ứng dụng
   ở bảng trên (cột dùng dấu `__`); đặt `ASPNETCORE_ENVIRONMENT=Production`. Không upload `.env` hay
   `appsettings.Development.json` lên Azure. File `DevBench.Api/appsettings.Production.json.example`
   minh họa cấu trúc JSON production; trên Azure nên dùng App settings.
4. Cho phép các outbound IP của App Service trong MongoDB Atlas IP Access List.
5. Trong Google Cloud Console, thêm Authorized redirect URI:
   `https://<default-domain>/auth/google/callback`. Nếu consent screen đang Testing,
   thêm tài khoản vào Test users.
6. Publish và deploy từ máy có .NET SDK 10, Azure CLI:

```powershell
az login
dotnet publish DevBench.Api/DevBench.Api.csproj -c Release -o artifacts/publish
Compress-Archive -Path artifacts/publish/* -DestinationPath artifacts/backend.zip -Force
az webapp deploy --resource-group "<resource-group>" --name "<app-name>" --src-path artifacts/backend.zip --type zip
```

App settings trên Azure được truyền vào app dưới dạng biến môi trường.
Sau khi lưu cấu hình và deploy, kiểm tra `https://<default-domain>/health` trả
`{"status":"ok"}`. API sẽ không khởi động nếu thiếu khóa bắt buộc hoặc MongoDB/index lỗi.
Trong Dev Bench desktop, nhập origin vào Settings → Account & backup → Backup server URL.

## GitHub Actions: tự deploy main

Workflow `.github/workflows/ci.yml` gồm job build và job deploy.

Repo chỉ giữ một workflow deploy. Workflow `DevBench.yml` được Visual Studio tạo
đã được bỏ để tránh hai deployment cùng chạy khi push `main`. Không tạo thêm
workflow deploy cùng đích trong Visual Studio; có thể dùng Publish trực tiếp
thủ công khi cần, hoặc để `ci.yml` deploy sau khi push.

- `api`: restore locked mode, build Release, publish, build Docker image, kiểm tra
  output không chứa cấu hình local rồi đóng gói deployment ZIP.
- `deploy`: chỉ chạy sau khi `api` thành công, cho push hoặc Run workflow trên
  `main` của repo `hautrank2/dev-bench-server`. Job đăng nhập OIDC, deploy ZIP và
  kiểm tra `/health`. Pull request chỉ chạy job `api`.

Đích deployment được ghim trong workflow, không phụ thuộc `az account set` trên
máy lập trình viên:

| Mục | Giá trị |
| --- | --- |
| GitHub repo/branch | `hautrank2/dev-bench-server`, `main` |
| Azure subscription | `Azure subscription 1` (`4b7c100d-ac87-475c-aa79-c3aa35180a1a`) |
| Resource group | `rg-nzwalks-eastus-dev-001` |
| Web App | `DevBench` (Linux, `DOTNETCORE\|10.0`) |
| Origin | `https://devbench-acdndvcbhrhgbkeq.westus3-01.azurewebsites.net` |
| Managed identity | `id-devbench-github-deploy` |

Đã tạo identity và cấu hình Azure:

- Federated credential `github-main`, issuer `https://token.actions.githubusercontent.com`,
  subject `repo:hautrank2/dev-bench-server:ref:refs/heads/main`, audience
  `api://AzureADTokenExchange`.
- Role `Website Contributor` chỉ scope vào Web App `DevBench`, không cấp quyền
  Contributor trên toàn subscription hay resource group.
- Workflow chỉ cho job deploy quyền `id-token: write`. Các GUID trong `env` là
  định danh tài nguyên công khai, không phải password/client secret. Token đăng
  nhập được GitHub/Azure cấp qua OIDC; không cần thêm GitHub secret hoặc tải publish profile.
- Không hủy deployment đang chạy khi có commit mới. Workflow mới chờ; các
  pull request được phép hủy lần kiểm tra cũ khi có commit mới.

Để kích hoạt trên GitHub, commit/push cấu trúc project hiện tại cùng workflow
lên `main` (gồm `DevBench.Api/`, solution và lockfile, không chỉ file workflow).
Sau đó vào GitHub → Actions → **API build and Azure deploy** xem tiến trình.
Có thể chạy lại bằng **Run workflow** với branch `main`. Thay đổi ở nhánh khác
không deploy production.

MongoDB/Google secret và `Api__PublicOrigin` vẫn cấu hình trong App settings của
Web App, không đi vào artifact. Web App phải kết nối được MongoDB Atlas và có đủ
cấu hình thì health check sau deploy mới thành công. Khi đổi tên repo, branch,
identity hoặc Web App, cập nhật workflow và OIDC subject/role tương ứng.

Tham khảo [GitHub Actions cho App Service](https://learn.microsoft.com/en-us/azure/app-service/deploy-github-actions)
và [Azure Login với OIDC](https://learn.microsoft.com/en-us/azure/developer/github/connect-from-azure-openid-connect).

Tham khảo: [ASP.NET Core User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0),
[Azure App Service app settings](https://learn.microsoft.com/en-us/azure/app-service/configure-common),
[ZIP deploy](https://learn.microsoft.com/en-us/azure/app-service/deploy-zip).
