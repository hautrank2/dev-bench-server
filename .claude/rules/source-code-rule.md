---
paths:
  - "DevBench.Api/**/*.cs"
  - "DevBench.Api/**/*.csproj"
---

# C# và cấu trúc layer

## Quy ước

- Namespace theo thư mục dưới `DevBench.Api`; file-scoped namespace; PascalCase;
  interface tiền tố `I`; phương thức bất đồng bộ hậu tố `Async`.
- Một public type mỗi file; class `sealed`; DTO và model bất biến dùng `record`;
  DI qua constructor, ưu tiên primary constructor.
- Entity MongoDB là class có `[BsonIgnoreExtraElements]` và `[BsonElement]` tên
  camelCase cho từng field.
- Giữ nullable và warnings as errors. Xử lý null tại boundary; không thêm `!` mới
  để che cảnh báo. Không để using, field hay dependency thừa.
- Comment giải thích lý do hoặc invariant. Không thêm layer, package hay mapping
  framework chỉ để đổi bố cục.

## Layer

- Controller: route, binding, status, response. Không truy vấn MongoDB.
- Service: nghiệp vụ, gọi `ICloudStore`, không phụ thuộc controller hay
  `HttpContext` (nhận header, query dưới dạng giá trị thường).
- Repository: query, update nguyên tử, index.
- DTO ở `Models/Dtos/`, entity ở `Models/`. Không trả entity ra HTTP; chuyển đổi
  bằng method như `UserRecord.Account()`, `BackupRecord.Info()`.

## DI và async

- Đăng ký trong `ApiApplication.cs`: `ApiOptions`, `ICloudStore`, `TimeProvider`
  là singleton; auth/session/backup service là scoped; `GoogleProvider` là typed
  `HttpClient` (timeout 15 giây). Không inject scoped vào singleton.
- Lấy thời gian qua `TimeProvider` được inject. Giá trị mặc định
  `DateTime.UtcNow` trong `BackupRecord` phải được service ghi đè.
- Không dùng `.Result`, `.Wait()` hay `Task.Run` để bọc I/O.
- Code hiện chỉ truyền cancellation token ở đổi code Google và đọc body backup.
  Method mới có I/O nên nhận và truyền token; không đổi hàng loạt chữ ký cũ khi
  task không yêu cầu.
- Tham số `configure` và `AddApplicationPart` trong `BuildAsync` là di sản của
  test harness đã bỏ. Không dựa vào chúng để thay implementation production.

## Xử lý lỗi

`ApiExceptionMiddleware` ánh xạ exception, controller không cần `try/catch`:

| Ném ra | Client nhận |
| --- | --- |
| `ApiException(status, message)` | `status`, `{error: message}` |
| `ArgumentException`, `JsonException` | 400 `Invalid request` (message bị bỏ) |
| `BadHttpRequestException` | status của nó; 413 có thông báo riêng về backup |
| Khác | 500 thông báo chung; log chỉ tên type |

- Lỗi dự kiến cần message cho người dùng thì dùng `ApiException`.
- Model binding lỗi trả `{error:"Invalid request"}` qua
  `InvalidModelStateResponseFactory`.
- Không log message hay object exception: có thể chứa URI MongoDB hoặc dữ liệu OAuth.
