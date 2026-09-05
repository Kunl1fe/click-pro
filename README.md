# LU Click Pro · Champagne Edition

Auto clicker Windows với nền than, điểm nhấn vàng champagne và 6 profile độc lập.

![Giao diện](docs/screenshot.png)

## Chạy ứng dụng

**[Tải ZIP chạy ngay](https://github.com/Kunl1fe/click-pro/raw/refs/heads/main/dist/LU-Click-Pro-Windows-x64.zip)** · **[Tải EXE trực tiếp](https://github.com/Kunl1fe/click-pro/raw/refs/heads/main/LU-Click-Pro.exe)**

Giải nén gói chạy là thấy ngay `LU-Click-Pro.exe` và `DOC-TRUOC.txt`, không cần mở thư mục con hay giải nén lần nữa.
Nếu dùng **Code → Download ZIP**, mở thư mục `click-pro-main` sau khi giải nén: file `LU-Click-Pro.exe` cũng nằm ngay tại đó.
Yêu cầu Windows 10/11 64-bit và .NET Framework 4.x. Không cần cài SDK để chạy.

| Phím | Chức năng |
| --- | --- |
| F1–F6 | Chạy/dừng profile tương ứng |
| F7 | Lấy tọa độ con trỏ cho profile đang chọn |
| F8 | Dừng |

Chọn ô profile để chỉnh thiết lập. Current click theo con trỏ; Point click tại tọa độ cố định.
Chỉ một profile chạy tại một thời điểm. Khi nhấn phím của profile khác, ứng dụng chuyển sang profile đó.

**Khóa profile (v1.4):** chọn profile rồi bấm **KHÓA CHỈNH SỬA**. Các thông số và việc lấy tọa độ bằng F7 sẽ bị khóa. F1–F6 vẫn chạy/dừng bình thường. Bấm **ĐÃ KHÓA · MỞ KHÓA** để chỉnh lại. Trạng thái khóa được lưu riêng cho từng profile ngay khi bật/tắt khóa.

Cấu hình lưu khi thoát tại `%APPDATA%\LuClickPro\settings.ini`. File cấu hình cá nhân không nằm trong repo.
Tốc độ thực tế phụ thuộc Windows và ứng dụng nhận click. EXE chưa ký số.

## Build

Chạy `powershell -ExecutionPolicy Bypass -File .\source\build.ps1` trên Windows 64-bit có compiler .NET Framework.
Kết quả là `LU-Click-Pro.exe` ở thư mục gốc.
Chạy `powershell -ExecutionPolicy Bypass -File .\source\package.ps1` để build và cập nhật cả ZIP trong `dist`.

## Bản 1.3

- Giao diện Champagne: nền than, viền vàng, thẻ bo góc và trạng thái chọn tương phản.
- Thanh đóng/thu nhỏ luôn tách khỏi vùng nội dung cuộn.
- Sáu profile; click trái/phải/giữa, đơn/đúp, khoảng nghỉ hoặc CPS, giới hạn click và tùy chỉnh vị trí.

Đã kiểm tra build và render cửa sổ 920×715, 680×500 trên máy phát triển. Chưa kiểm tra trên mọi mức DPI hoặc mọi ứng dụng đích.
