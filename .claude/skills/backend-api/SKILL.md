---
name: backend-api
description: Implement or modify DevBench backend HTTP endpoints and controller-service-repository flows in this ASP.NET Core repo. Use for API, authentication, session or backup behavior changes, not documentation-only edits or frontend UI work.
---

# Thay đổi API DevBench

Đọc [CLAUDE.md](../../../CLAUDE.md) (nhất là "Bất biến" và "Vấn đề đã biết") và
[HTTP contract](../../../docs/api-contract.md) trước khi sửa.

## 1. Lần luồng hiện có

Controller → interface trong `Services/Interfaces/` → service → `ICloudStore` →
`MongoCloudStore`. Tái sử dụng trước khi thêm abstraction. Nếu request/response
đổi, xác định tác động tới Electron main trước khi viết code.

## 2. Thực hiện

Theo thứ tự, bỏ qua bước không áp dụng:

1. DTO trong `Models/Dtos/` (record; property request để nullable).
2. Entity/field trong `Models/` với `[BsonElement]`; đọc được document cũ thiếu field.
3. Method trong `ICloudStore` và `MongoCloudStore`; index mới đặt tên trong
   `InitializeAsync`.
4. Interface và service; lỗi dự kiến ném `ApiException`; thời gian lấy từ
   `TimeProvider`.
5. Controller mỏng. Endpoint cần đăng nhập phải gọi `ISessionService.RequireAsync`
   trước khi đọc body hay dữ liệu; query scope bằng `session.UserId`.
6. Đăng ký DI và rate limit policy trong `ApiApplication.cs`.
7. Cập nhật `docs/api-contract.md`; đổi cấu hình thì cập nhật
   [hướng dẫn môi trường](../../../docs/configuration.md) và template.

## 3. Tự soát

- Thiếu/sai token → 401; resource account khác → 404.
- Body sai JSON, sai content type, quá cỡ → 4xx với `{error}`, không rơi vào 500.
- Thao tác dùng một lần vẫn nguyên tử khi gọi đồng thời.
- Dữ liệu cũ trong MongoDB vẫn đọc được.
- Không log hay trả về secret, token, chi tiết kết nối.

## 4. Kiểm tra và bàn giao

Từ gốc repo:

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet publish DevBench.Api/DevBench.Api.csproj -c Release --no-restore -o artifacts/publish
```

Có cấu hình và MongoDB hợp lệ thì kiểm tra `/health` và flow vừa sửa bằng dữ liệu
được phép dùng; không ghi/xóa dữ liệu thật làm fixture. Không thêm test project vào
repo. Thiếu credential không phải lý do thêm bypass; ghi rõ phần chưa xác minh.

Ghi kết quả vào `docs/verification.md` cho thay đổi runtime/build. Báo thay đổi,
kiểm tra và giới hạn bằng tiếng Việt. Không tự commit/push/deploy.
