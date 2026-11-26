# Cách làm việc

Kiến trúc, lệnh và bất biến nằm trong `CLAUDE.md`. Yêu cầu hiện tại của người
dùng được ưu tiên khi khác với quy ước repo.

## Khi sửa

- Trao đổi bằng tiếng Việt; API/browser messages bằng tiếng Anh.
- Chỉ sửa BE và tài liệu/config liên quan trong phạm vi yêu cầu.
- Sửa nhiều file thì nêu ngắn hướng xử lý rồi làm tiếp; không tạo bước duyệt kế
  hoạch riêng hay buộc viết file kế hoạch.
- Gặp lỗi ngoài phạm vi thì báo, không sửa kèm.
- Không tự commit, push hay deploy.

## Kiểm tra

- Sửa tài liệu/rule: kiểm tra đường dẫn, tên type/method và tham chiếu còn đúng.
- Sửa code, project hoặc pipeline: chạy restore/build/publish trong `CLAUDE.md`.
- Sửa runtime: khi có MongoDB và cấu hình hợp lệ, kiểm tra `/health` và flow vừa
  sửa bằng account/dữ liệu được phép dùng. Không tạo hay xóa dữ liệu trong
  database thật để thử.
- Build/publish không chứng minh được Google OAuth, MongoDB thật, Docker hay
  Azure. Không báo các phần đó thành công nếu chưa chạy.
- Thay đổi runtime/build: thêm mục mới vào đầu `docs/verification.md`, ghi đúng
  những gì đã chạy và phần chưa kiểm tra. Các mục cũ là lịch sử, không sửa.

## Bàn giao

Báo ngắn gọn thay đổi, kiểm tra đã chạy, giới hạn còn lại và tác động tới desktop
nếu contract hoặc hành vi đổi.
