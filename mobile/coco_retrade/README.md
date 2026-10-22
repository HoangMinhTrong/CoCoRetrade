# CoCo ReTrade Mobile

Ứng dụng Flutter cho Android và iOS, dùng chung dữ liệu và quy tắc với ASP.NET Core API của CoCo ReTrade. Ứng dụng không kết nối trực tiếp đến SQL Server. Giá, trạng thái sản phẩm, quyền nhân viên và số tiền đơn hàng đều do backend quyết định.

## Màn hình đã có

- Trang chủ, tìm kiếm theo SKU/tên/thương hiệu, bộ lọc, chi tiết sản phẩm, tủ đồ người bán, theo dõi và yêu thích.
- Đăng ký, kích hoạt OTP, đăng nhập email/số điện thoại, đặt lại và đổi mật khẩu. Phiên Identity cookie được giữ trong kho lưu trữ an toàn của điện thoại; mọi thao tác ghi đều gửi antiforgery token.
- Ký gửi 3–10 ảnh, tủ đồ, giỏ hàng, trả giá/đề xuất lại, đặt hàng theo giá niêm yết hoặc giá thỏa thuận, chọn lấy tại CoCo hoặc giao tận nơi.
- Đơn hàng, thông báo mở trực tiếp màn hình liên quan, ví và yêu cầu rút, sổ địa chỉ, hỗ trợ, ảnh đại diện.
- Tin nhắn người bán và hộp hỗ trợ nhận sự kiện SignalR, tự đồng bộ lại khi có tin mới; đồng bộ dự phòng sau 30 giây nếu mất kết nối trực tiếp.
- Giao diện nhân viên theo permission: kiểm định, duyệt/từ chối, xem tem SKU/QR, quản lý sản phẩm, đơn hàng, người dùng, ví, đối soát, doanh thu và nhật ký.

## Chuẩn bị môi trường

1. Flutter SDK đã được cài tại `C:\src\flutter`. Cài [Android Studio và Android SDK](https://docs.flutter.dev/platform-integration/android/setup). Trong Android Studio, mở **More Actions → SDK Manager**; cài Android SDK Platform, Build-Tools, Command-line Tools và Platform-Tools. Sau đó tự đọc/chấp nhận giấy phép bằng `flutter doctor --android-licenses`, rồi chạy `flutter doctor` cho đến khi Android toolchain sẵn sàng. **Máy Windows có thể build Android; build iOS cần máy macOS và Xcode.**
2. Phần Android của dự án đã được tạo. Trong thư mục `mobile/coco_retrade`, chạy:

   ```powershell
   flutter pub get
   flutter analyze
   ```

   Trên macOS, chạy `flutter create --project-name coco_retrade --platforms=ios .` để tạo phần iOS.

3. Quyền Internet đã có trong Android manifest. Bản debug cho phép HTTP để kết nối máy chủ cục bộ; bản phát hành dùng backend HTTPS.

4. Trên iOS, thêm `NSPhotoLibraryUsageDescription` và `NSCameraUsageDescription` vào `ios/Runner/Info.plist` để chọn ảnh/chụp ảnh.

## Chạy với backend hiện có

Trong terminal ở thư mục solution, chạy ASP.NET Core và SQL Server như bản web:

```powershell
dotnet run --project .\src\CoCoReTrade.Web --launch-profile http
```

Android emulator truy cập máy Windows qua `http://10.0.2.2:5001` (giá trị mặc định trong `lib/api.dart`):

```powershell
cd mobile\coco_retrade
flutter run
```

Để tạo file cài đặt Android sau khi đã kiểm tra bằng `flutter analyze`:

```powershell
flutter build apk --debug --dart-define=COCO_API_BASE_URL=https://TEN-LINK.trycloudflare.com
```

Thay URL mẫu bằng URL backend đang chạy. APK nằm ở `build/app/outputs/flutter-apk/app-debug.apk`. Bản phát hành cần dùng URL backend HTTPS ổn định và cấu hình ký ứng dụng Android trước khi phân phối.

Với **điện thoại thật**, `localhost` và `10.0.2.2` không trỏ tới máy Windows. Dùng URL HTTPS của backend đang hoạt động, chẳng hạn URL Cloudflare Tunnel đang thử nghiệm:

```powershell
flutter run --dart-define=COCO_API_BASE_URL=https://TEN-LINK.trycloudflare.com
```

URL Quick Tunnel thay đổi sau mỗi lần khởi động; chạy lại app với URL mới. Khi đưa lên cửa hàng ứng dụng, dùng địa chỉ backend ổn định và HTTPS. Không đưa SQL connection string hoặc mật khẩu admin vào app Flutter.

## Tích hợp cần cấu hình ngoài mã nguồn

- SMTP phải hoạt động để gửi OTP cho tài khoản mới và đặt lại mật khẩu, giống bản web.
- Ứng dụng hiện hiển thị thông báo lưu trong SQL và cập nhật huy hiệu khi mở app. Push notification hệ điều hành cần thêm FCM/APNs và thông tin dịch vụ tương ứng.
- Rút tiền vẫn là quy trình nhân viên xác nhận sau khi chuyển khoản thực tế; app không giả lập chuyển khoản ngân hàng.
- Quick Tunnel chỉ dùng xem thử. App di động cần backend, SQL Server và nơi lưu ảnh hoạt động liên tục để vận hành chính thức.

## Các gói Flutter

`flutter_secure_storage` giữ cookie, `image_picker` chọn ảnh, `flutter_svg` hiển thị logo từ bản web, `qr_flutter` hiển thị tem SKU. Phiên bản trong `pubspec.yaml` được chọn theo thông tin trên [pub.dev](https://pub.dev/).
