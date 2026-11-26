# DevBench API

Backend độc lập cho Google sign-in, session theo thiết bị và cloud backup của
Dev Bench desktop. Giao tiếp và tài liệu bàn giao bằng tiếng Việt; thông báo
HTTP/browser bằng tiếng Anh.

Nguồn sự thật, theo thứ tự: implementation hiện tại → `docs/api-contract.md` →
`docs/configuration.md` → `README.md`. Nếu code lệch contract, báo người dùng,
không tự chọn một bên rồi sửa.

## Lệnh

Chạy từ gốc repo (nơi có `DevBench.Api.slnx`):

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet publish DevBench.Api/DevBench.Api.csproj -c Release --no-restore -o artifacts/publish
```

```powershell
dotnet run --project DevBench.Api
```

- `dotnet run` dùng profile Development, cổng 4318; cần MongoDB đang chạy và đủ
  cấu hình (xem `docs/configuration.md`). `.env` chỉ dành cho Docker Compose.
- Không có project test. Không chạy `dotnet test`, không thêm test project hay
  test framework vào repo khi chưa được yêu cầu.
- Đổi package phải cập nhật `DevBench.Api/packages.lock.json` bằng
  `dotnet restore --force-evaluate`; CI và Docker restore ở locked mode.

## Stack và cấu trúc

ASP.NET Core .NET 10 (controllers), C# nullable, implicit usings, warnings as
errors. Package ngoài chỉ có `MongoDB.Driver` và `Google.Apis.Auth`.
Một project `DevBench.Api/`; không có và không tạo lại `src/`, `tests/`.

| Đường dẫn trong `DevBench.Api/` | Trách nhiệm |
| --- | --- |
| `Program.cs`, `ApiApplication.cs` | Entry point; Kestrel, DI, rate limit, pipeline |
| `Controllers/` | `Auth` (`/auth/*`, `/me`), `Backups`, `Health` |
| `Services/`, `Services/Interfaces/` | `AuthService`, `SessionService`, `BackupService`, `GoogleProvider`, `ApiException` |
| `Repositories/` | `ICloudStore` và `MongoCloudStore` (mọi query và index) |
| `Models/`, `Models/Dtos/` | Entity MongoDB, model nội bộ; request/response HTTP |
| `Validation/BackupValidator.cs` | Kiểm tra, chuẩn hóa snapshot v1/v2, giới hạn 2 MiB |
| `Security/TokenSecurity.cs` | Sinh secret 256 bit; SHA-256 base64url |
| `Configuration/ApiOptions.cs` | Đọc và kiểm tra cấu hình |
| `Middlewares/ApiExceptionMiddleware.cs` | Security headers, ánh xạ exception → HTTP |

## Luồng chính

- **Startup:** đọc `ApiOptions` và tạo index MongoDB trước khi nhận request; thiếu
  cấu hình hoặc DB lỗi thì app không khởi động. `/health` chỉ trả `ok` tĩnh,
  không kiểm tra lại DB khi đang chạy.
- **Login:** `start` lưu `LoginRecord` (state, nonce, Google PKCE verifier,
  challenge của desktop; 5 phút) → Google gọi `callback` (tiêu thụ state, đổi
  code, upsert user, gắn `UserId`) → desktop poll `complete` bằng verifier
  (202 khi chưa xong; khi xong xóa login nguyên tử và cấp session 30 ngày).
- **Xác thực:** không dùng authentication middleware hay `[Authorize]`. Controller
  hoặc service truyền header `Authorization` vào `ISessionService.RequireAsync`.
  Endpoint mới cần đăng nhập phải tự gọi, nếu quên thì endpoint public.
- **Backup:** `POST` xác thực → đọc `JsonObject` → `BackupValidator.Parse` →
  lưu `BsonDocument`. Đọc ra cũng chạy lại validator để nâng snapshot cũ lên v2.

## Bất biến không được phá

- Google state, nonce, S256 PKCE, xác minh ID token; state và completion dùng một
  lần bằng thao tác MongoDB nguyên tử.
- Server chỉ lưu hash token; logout chỉ thu hồi session hiện tại; kiểm tra expiry
  trong query, không chờ TTL.
- Owner của backup luôn là `session.UserId`; backup của account khác trả 404.
- Backup bất biến, append-only; server không thực thi folder/command trong snapshot.
- Lỗi JSON `{error:string}`, callback `text/plain`, giữ rate limit
  (100/phút/IP; start login 10/phút) và security headers.
- Production DI dùng `MongoCloudStore` và `GoogleProvider` thật. Không fake
  identity, in-memory fallback hay bypass đăng nhập.
- Không log secret, token, bearer header, OAuth code, query callback hay thông tin
  kết nối. Không commit cấu hình thật.
- Contract và dữ liệu MongoDB phải tương thích với desktop đang dùng. Không thêm
  `/api`, envelope, JWT, Redis, cookie auth hay CORS mở.
- FE ở repo riêng: không tham chiếu source FE, không áp rule React/Electron/UI vào
  đây, không sửa repo FE nếu không được giao.

## Vấn đề đã biết (chưa sửa)

Xác minh ngày 2026-11-26. Không tự sửa khi task không yêu cầu; sửa xong thì xóa
mục tương ứng và cập nhật contract nếu hành vi đổi.

Đã tái hiện qua HTTP với store giả tạm thời ngoài repo:

1. Route không tồn tại (404), sai method (405) và sai content type ở `/auth/*`
   (415) không có body `{error}`.

Thấy qua đọc code, chưa tái hiện:

2. `AuthService.CallbackAsync` bắt mọi exception và không log, nên lỗi MongoDB hay
   sai client secret đều không để lại dấu vết.
3. `GET /backups` tải và validate toàn bộ snapshot của tối đa 500 bản (mỗi bản
   tới 2 MiB) chỉ để trả metadata.
4. Rate limit theo `RemoteIpAddress`; sau reverse proxy chưa cấu hình forwarded
   headers, mọi người dùng chung một hạn mức.
5. `ArgumentException`/`JsonException` bất kỳ, kể cả do bug nội bộ, thành 400 và
   không được log.

## Rule và skill

Rule trong `.claude/rules/` tự nạp theo `paths`; `rule.md` luôn áp dụng.

| File | Nội dung |
| --- | --- |
| `rule.md` | Cách làm việc, kiểm tra và bàn giao |
| `source-code-rule.md` | C#, layer, DI, xử lý lỗi |
| `api-contract.md` | Route, DTO, status, tương thích desktop |
| `security.md` | OAuth, session, ownership, log |
| `mongodb.md` | Collection, index, tương thích dữ liệu |
| `configuration.md` | Key cấu hình, Docker, CI, Azure |

Skill `.claude/skills/backend-api/SKILL.md`: dùng khi thêm/sửa endpoint hoặc luồng
controller → service → repository. `AGENTS.md` là bản tóm tắt cho agent khác; khi
đổi quy ước ở đây, kiểm tra file đó còn khớp.
