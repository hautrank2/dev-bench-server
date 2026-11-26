# Kiểm tra backend .NET

## Bỏ workflow deploy trùng trước khi push — 2026-11-26

- Đã bỏ `.github/workflows/DevBench.yml` do Visual Studio sinh; chỉ giữ `ci.yml`
  để deploy Web App `DevBench` từ `main`.
- Xác nhận thư mục workflow chỉ còn `ci.yml`; actionlint và `git diff --check` qua.
- Build Release: 0 warning, 0 error; publish API thành công.
- Chưa commit/push hoặc chạy deployment mới. Lần kiểm tra Azure trước đó thấy
  `/health` trả 404; workflow sẽ kiểm tra lại sau deployment đầu tiên từ `main`.

## GitHub Actions deploy Azure từ main — 2026-11-26

- Workflow `ci.yml`: build/publish và Docker build trước khi deploy; PR chỉ kiểm
  tra; push hoặc manual run trên `main` mới deploy Web App `DevBench` và kiểm tra `/health`.
- Build Release local: 0 warning, 0 error; publish thành công. Actionlint 1.7.12
  kiểm tra workflow thành công; binary được đối chiếu SHA-256 với release chính thức.
- Đã tạo managed identity `id-devbench-github-deploy`, federation chỉ cho
  `repo:hautrank2/dev-bench-server:ref:refs/heads/main`, và role Website Contributor
  giới hạn đúng Web App. Đã đọc lại issuer/subject/audience và scope role để xác minh.
- Azure xác nhận Web App Linux runtime .NET 10 và có đủ tên app settings ứng dụng;
  không đọc/in giá trị secret trong kết quả kiểm tra.
- Chưa commit/push thay đổi nên workflow mới chưa chạy trên GitHub, chưa xác minh
  đăng nhập OIDC từ runner, Docker build trên runner hoặc deployment/health sau deploy.
  Workflow bắt đầu sau khi cấu trúc BE và workflow được push lên `main`.

## Sửa giới hạn backup, content type và danh sách backup — 2026-11-26

- Giới hạn 2 MiB đo như `JSON.stringify`; `POST /backups` sai content type/charset
  trả 400 thay vì 500; danh sách bỏ qua snapshot không đọc được và log ID.
- Restore locked mode, build Release (0 warning, 0 error) và publish thành công.
- Kiểm tra HTTP trên Kestrel bằng chương trình tạm ngoài repo, thay `ICloudStore`
  bằng store trong bộ nhớ: body nhiều `&`/tiếng Việt dưới giới hạn được 201; đúng
  2.097.152 byte được 201, thêm 1 byte nhận 413; ký tự đặc biệt và emoji restore
  đúng; sai/thiếu content type, charset lạ, body rỗng/`null` nhận 400; request
  chưa đăng nhập vẫn nhận 401; danh sách có một bản hỏng trả các bản còn lại.
- Cách đo kích thước khớp `JSON.stringify` (Node 24) trên 3.000 document ngẫu nhiên
  gồm chuỗi, số nguyên, boolean, null, mảng và object.
- Chưa kiểm tra với MongoDB thật, Google OAuth, desktop, Docker hay Azure.

## Hướng dẫn Claude và bỏ tham chiếu test — 2026-11-26

Repo hiện không còn thư mục/project test theo yêu cầu của người dùng.
`CLAUDE.md`, `.claude/rules/` và skill `backend-api` hướng dẫn riêng cho ASP.NET
Core, không dùng convention UI của FE. `AGENTS.md`, solution, CI và README
được đồng bộ để không gọi test project đã bỏ.

- Build Release solution thành công: 0 warning, 0 error; publish API thành công.
- Skill `backend-api` qua validator của skill-creator.
- Không chạy integration test hoặc Google/MongoDB smoke trong lần cập nhật này.

Các kết quả integration trong các mục bên dưới là lịch sử trước khi bỏ test,
không phải kiểm tra được chạy lại trong lần cập nhật hướng dẫn Claude.

## Tái cấu trúc theo Authly — 2026-11-26

- Solution ở gốc, project `DevBench.Api/`, bỏ hoàn toàn `src`.
- Chuyển Minimal API thành Controllers → Services/Interfaces → Repositories;
  tách Models/Dtos, Configuration, Middlewares, Security và Validation.
- `dotnet build -c Release -m:1 /nr:false`: 0 warning, 0 error;
  restore NuGet và vulnerability audit thành công khi chạy ngoài sandbox.
- 13 nhóm kiểm tra qua với memory store: 11 nhóm cũ và 2 nhóm mới kiểm tra
  MVC model binding/error contract, cấu hình section/env/legacy.
- Các kiểm tra HTTP vẫn chạy Kestrel thật, Google và store giả chỉ ở test executable.
  Sandbox chặn socket localhost nên chạy integration ngoài sandbox.
- Publish Release thành công. Kiểm tra bằng file cấu hình tạm xác nhận output
  có API DLL và appsettings.json; không có test, .env, file .example,
  appsettings.Development.json, appsettings.Production.json hay appsettings.Local.json.
  File tạm được xóa sau khi kiểm tra.
- Dockerfile, solution, project reference và README đã cập nhật đường dẫn mới.
- Docker daemon và MongoDB localhost chưa chạy; chưa chạy lại Docker build hoặc
  integration MongoDB thật trong lần thay đổi này. Google OAuth thật và deploy
  Azure vẫn cần kiểm tra với tài khoản/cấu hình thực tế.

## Kiểm tra ban đầu — 2026-11-24

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

Lệnh kiểm tra theo cấu trúc hiện tại (không còn project test):

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet publish DevBench.Api/DevBench.Api.csproj -c Release --no-restore -o artifacts/publish
```

Google client secret/MongoDB URI thật không được sao chép vào repo này.
