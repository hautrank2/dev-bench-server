---
paths:
  - "DevBench.Api/Configuration/**"
  - "DevBench.Api/appsettings*"
  - "DevBench.Api/Properties/launchSettings.json"
  - "DevBench.Api/**/*.csproj"
  - "DevBench.Api/packages.lock.json"
  - "DevBench.Api.slnx"
  - "global.json"
  - ".env*"
  - "Dockerfile"
  - "compose.yaml"
  - ".dockerignore"
  - ".gitignore"
  - ".github/workflows/**"
  - "docs/configuration.md"
---

# Cấu hình, build và deploy

Đọc `docs/configuration.md` và `Configuration/ApiOptions.cs` trước khi đổi key.

## Key

| Section | Alias cũ (ưu tiên hơn) | Bắt buộc |
| --- | --- | --- |
| `Api:PublicOrigin` | `PUBLIC_API_ORIGIN` | Có |
| `MongoDb:ConnectionString` | `MONGODB_URI` | Có |
| `MongoDb:DatabaseName` | `MONGODB_DB` | Không, mặc định `dev_bench` |
| `Google:ClientId` | `GOOGLE_CLIENT_ID` | Có |
| `Google:ClientSecret` | `GOOGLE_CLIENT_SECRET` | Có |

- Env dùng `__` thay `:`. Alias cũ thắng kể cả khi được đặt là chuỗi rỗng.
- Origin phải HTTPS, không path/query/userinfo; HTTP chỉ cho `localhost` và
  `127.0.0.1`. Callback là `<origin>/auth/google/callback`.
- Thêm key phải cập nhật `ApiOptions`, `appsettings.json`, hai file `.example`,
  `.env.example` (nếu Compose cần) và `docs/configuration.md`.
- Không đặt default production khiến app âm thầm dùng sai DB hoặc OAuth client.
- Secret local dùng User Secrets hoặc `appsettings.Development.json` (Git bỏ qua).
  `.example` không chứa credential thật.

## Build, Docker, CI, Azure

- Config local, `.env` và file mẫu phải bị loại khỏi publish (`.csproj`) và image
  (`.dockerignore`).
- Dockerfile restore locked mode từ `DevBench.Api/`; nghe HTTP 8080, user `app`;
  TLS do host/proxy xử lý, proxy giữ path từ root.
- Compose chỉ cho local: ghi đè `MONGODB_URI` sang service `mongo`, port bind
  loopback.
- CI (`.github/workflows/ci.yml`): restore, build, publish, build image. Không
  thêm bước test hay service MongoDB chỉ để test.
- Workflow deploy Azure chỉ cho push/manual run trên `main`, sau build thành
  công; PR không deploy. Giữ OIDC theo repo/branch và role scope Web App; không
  thay bằng publish password hay cấp quyền trên toàn subscription.
- Chỉ giữ `ci.yml` làm workflow deploy cho Web App DevBench. Không tạo thêm
  workflow từ Visual Studio cùng chạy khi push `main` và deploy trùng đích.
- Azure App Service: .NET 10, cấu hình bằng App settings; không upload file secret.
