# Menu — menu chính, chọn game, bản lưu và cài đặt

Menu là màn hình đầu tiên bạn thấy: menu tiêu đề với **Chơi**, **Tải**, **Cài đặt**, **Mod và bộ sưu tập** và **Thoát**. Tại đây bạn chọn một trong các game đã cài, bắt đầu hoặc tiếp tục từ chỗ đã dừng, tải một bản lưu, đổi ngôn ngữ, âm thanh và cài đặt hiển thị. Mod khác cũng có thể thêm nút và trang cài đặt của riêng mình.

Menu thuộc lớp vỏ luôn được tải, nên nó có mặt từ trước khi game nào bắt đầu.

![Menu chính: Play, Load, Settings, Mods & Collections và Exit bên trái, tranh tiêu đề Cthangover bên phải.](assets/main-menu.jpg)

## Bắt đầu game

**Chơi** mở **Chọn game**. Mỗi game trong các bộ sưu tập là một thẻ với ảnh bìa, mô tả, mức độ sẵn sàng và số bản lưu.

![Hai thẻ game trong Choose a game: Cthangover và My Novel, đều "Ready to play", "No saves", có nút New game và Configure.](assets/game-picker.jpg)

- **Trò chơi mới** bắt đầu game từ cảnh đầu tiên. Menu chuyển sang bộ mod của game ngay trong cùng cửa sổ — không cần khởi động lại.
- **Tiếp tục** trên thẻ tiếp tục bản lưu mới nhất của game đó; **Bản lưu** liệt kê chúng.
- **Cài phần còn thiếu** xuất hiện khi game cần những mod bạn chưa có và tải chúng về.
- **Cấu hình** mở cấu trúc game trong trình quản lý mod.

Trong khi bạn xem menu, mod của các game đang được chuẩn bị ở nền; gợi ý phía dưới báo khi nào khởi động sẽ nhanh.

**Tiếp tục** ngay trên menu chính xuất hiện sau khi bạn đã chơi: nó quay về game và bản lưu bạn dùng lần cuối.

## Tải một bản lưu

**Tải** hiển thị bản lưu của từng game một; chọn game ở bộ chọn phía trên rồi nhấp vào một bản lưu để tải.

![Cửa sổ tải của My Novel với một bản lưu, "Day 1, 00:00", và gợi ý "Click a save to load it".](assets/load.jpg)

Nếu mod của game đã thay đổi từ lúc tạo bản lưu, cửa sổ **Game đã thay đổi** sẽ liệt kê cái được thêm (bắt đầu từ đầu), cái bị gỡ (dữ liệu được giữ và trở lại nếu bật lại) hay được cập nhật, và sao lưu bản lưu trước khi tải. Những bản lưu từ trước khi có khái niệm game nằm dưới **Bản lưu không có game**; bạn chọn game chúng thuộc về và tệp sẽ chuyển tới đó.

Trong lúc chơi, lưu và tải dùng chung một cửa sổ qua menu trong game: nhấp vào một ô để lưu, ô đã dùng bị ghi đè sau khi xác nhận, **+ Bản lưu mới** thêm ô mới.

## Cài đặt

**Cài đặt** có bốn thẻ. Thay đổi được áp dụng và lưu ngay.

- **Chung** — ngôn ngữ giao diện.
- **Âm thanh** — bật/tắt và âm lượng của Hiệu ứng, Nhạc và Âm nền.
- **Hiển thị** — chế độ cửa sổ (cửa sổ, phóng to, không viền hay toàn màn hình độc quyền), màn hình, kích thước cửa sổ, V-Sync và giới hạn khung hình, kèm **Đặt lại cài đặt hiển thị**.
- **Mod** — các trang cài đặt mà mod đã cài thêm vào, với **Khôi phục mặc định**.

![Thẻ hiển thị: chế độ cửa sổ, màn hình, kích thước cửa sổ, V-Sync và giới hạn khung hình.](assets/settings-display.jpg)

![Thẻ Mod: trang riêng của mod Menu với "Data of disabled mechanics" và nút xóa.](assets/settings-mods.jpg)

Trang riêng của Menu trên thẻ **Mod** là **Dữ liệu của cơ chế đã tắt → Xóa**: nó xóa khỏi mọi bản lưu dữ liệu của những mod không còn thuộc game nào. Chỉ dùng khi bạn chắc chắn: cơ chế được bật lại sẽ bắt đầu từ đầu.

## Menu dựa vào gì

Menu vẽ các màn hình và quyết định từng nút làm gì. Danh sách game và mức sẵn sàng đến từ các bộ sưu tập; việc bắt đầu, chuyển và lưu game do phiên chơi game cùng các dịch vụ lưu của engine đảm nhiệm; nhạc màn hình tiêu đề là playlist `MainMenu` của Music Pack, thứ Menu yêu cầu. **Mod và bộ sưu tập** do Mod Manager cung cấp.

## Cho tác giả mod: thêm nút hoặc trang cài đặt

Các nút đến từ một sổ đăng ký chứ không phải một màn hình cố định, nên mod có thể thêm, thay thế hay sắp xếp lại chúng. Mod Manager thêm nút của mình bằng một mục `main_menu` trong manifest:

```json
"main_menu": [
  {
    "id": "mods_hub",
    "scene_path": "scenes/mods_hub_screen.tscn",
    "button": { "icon": "mod_manager", "locale": "mod_manager/menu_title", "index": 100 }
  }
]
```

Cài đặt của một mod xuất hiện trên thẻ **Mod** khi mod đăng ký định nghĩa cài đặt vào sổ đăng ký cài đặt mod, như Menu làm với hành động xóa dữ liệu. Văn bản trên nút và cài đặt lấy từ bản dịch của chính mod đó.

## Khi có trục trặc

1. **Không có game nào dưới Chơi.** Chưa cài hoặc chưa bật bộ sưu tập nào thuộc loại *game*; hãy mở **Mod và bộ sưu tập**.
2. **Thẻ game báo Thiếu mod hoặc Phiên bản không tương thích.** Dùng **Cài phần còn thiếu**, hoặc **Cấu hình** để xem vấn đề.
3. **Thiếu Tiếp tục.** Nó xuất hiện sau khi bạn đã chơi ít nhất một lần.
4. **Cửa sổ không giữ nguyên kích thước.** Kiểm tra **Hiển thị → Chế độ cửa sổ**; kích thước cửa sổ chỉ áp dụng cho chế độ cửa sổ.
