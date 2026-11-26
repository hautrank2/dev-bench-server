---
paths:
  - "DevBench.Api/**/*.cs"
  - "Dockerfile"
  - "compose.yaml"
---

# OAuth, session và ownership

## OAuth

- Giữ state, nonce, S256 PKCE. Có hai cặp PKCE độc lập: desktop ↔ API
  (`Challenge`/verifier) và API ↔ Google (`GoogleVerifier`). Không gộp, không gửi
  `GoogleVerifier` cho client.
- Xác minh ID token bằng `GoogleJsonWebSignature.ValidateAsync` với audience là
  client ID, yêu cầu email verified. Chỉ đọc claim `nonce` sau bước xác minh.
- State tiêu thụ bằng `FindOneAndUpdate`, completion bằng `FindOneAndDelete`.
  Không tách thành đọc rồi xóa; không nới predicate challenge/expiry/user.
- Login hỏng hoặc bị hủy phải xóa `LoginRecord`.
- Trang callback chỉ trả thông báo cố định; không phản chiếu query hay chi tiết lỗi.

## Session và ownership

- Token từ `TokenSecurity.Secret()`; chỉ lưu `TokenSecurity.Hash`. Token thô chỉ
  xuất hiện trong response `complete`.
- Hạn 30 ngày, mỗi thiết bị một session; logout không thu hồi thiết bị khác.
- Tra session theo hash và thời gian hết hạn, không dựa vào TTL index.
- Mọi list/find backup scope bằng `session.UserId`; bỏ qua owner từ client.

## Bề mặt công khai

- Production DI dùng MongoDB/Google thật; startup lỗi khi config/DB sai. Không
  fallback store, fake provider hay login bypass.
- Không log bearer header, secret, OAuth code, query callback, body token endpoint
  hay exception có thể chứa MongoDB credentials. Thêm log thì chỉ ghi type, mã lỗi
  hoặc ID không nhạy cảm. Giữ `System.Net.Http.HttpClient` ở mức `Warning`.
- Không chạy command, đọc folder hay khởi động process từ nội dung backup.
- Giữ security headers (`no-store`, `nosniff`, `no-referrer`, CSP `default-src
  'none'`) và rate limit. Endpoint public mới tốn tài nguyên cần policy riêng như
  `login`.
- Chỉ tin forwarded headers khi đã biết proxy tin cậy; không tin mọi nguồn để
  chữa rate limit sau proxy. Không nới CORS để chữa lỗi deploy.
- Container chạy dưới user `app`.
