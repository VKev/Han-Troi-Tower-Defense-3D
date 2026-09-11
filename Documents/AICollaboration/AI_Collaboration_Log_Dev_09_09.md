# AI Collaboration Log — Audio gameplay, tutorial Level 1 và Toon mobile — 09/09/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Responsible Codex tasks:** `01a08452-6082-7d02-a126-b9a7d3b4f261`,
  `01a07b07-a490-71a2-8232-25bd8dcfb6f3`
- **Local date:** 09/09/2026 (`Asia/Saigon`, UTC+7)
- **Coverage:** toàn bộ 100 yêu cầu trong ngày của hai task

## Entry 1 — Thiết kế và triển khai audio service 2D cho mobile

### Vấn đề đang gặp

Project chưa có nơi quản lý sound theo ID, cần phát đồng thời vài clip, loop music theo game phase và giữ chi phí phù hợp
mobile.

### Prompt đã dùng

Owner yêu cầu plan trước rồi duyệt implement; hỏi về pooling; sau đó wire Lobby music, Game Start click, level selection,
Deploy, preparation music, tutorial typing và pitch cho từng entry.

### Phản hồi quan trọng của AI

Thiết kế dùng `SoundCatalog` ScriptableObject, `SoundId`, service scoped/persistent và số AudioSource hữu hạn cho one-shot
overlap. Music loop được quản lý theo flow/menu/level thay vì tạo AudioSource mới cho từng lần gọi.

### Phương án được chọn / sửa / loại

- Chọn catalog ID-driven với volume, pitch, cooldown, priority, concurrency, protected và loop.
- Chọn source reuse/voice budget cho SFX, không xây object pool phức tạp cho âm thanh 2D nhỏ.
- Thêm AudioListener authored sau khi Console báo scene không có listener.
- Dừng PlayMode verification theo yêu cầu `ko cần test` tại thời điểm đó.

### Lý do

Một catalog tập trung và voice budget đủ cho trường hợp 2–3 âm đồng thời, ít object/lifecycle hơn pool GameObject tùy biến.

### Kết quả triển khai / kiểm tra

Audio architecture và các cue đầu tiên được wire. Các cue projectile/reaction/win/lose/frog/start-wave tiếp tục được bổ
sung ngày 10/09.

## Entry 2 — Placement tutorial Generator/Sink và enemy description

### Vấn đề đang gặp

Wave 1–3 tutorial cần placement đúng footprint, description không flash, panel bám card trên nhiều aspect ratio và HUD chỉ
reveal đúng phần được học.

### Prompt đã dùng

Owner yêu cầu hand drag từ card vào bốn cell, tutorial link Generator→Sink, giới thiệu Sink sau enemy detail, description
authored trong scene/prefab, resize theo content, DOTween open/close và khoảng cách panel cạnh icon.

### Phản hồi quan trọng của AI

Target placement dùng anchor cell và footprint 2×2; required/reserved placement cùng đọc target đó. Enemy description được
đổi sang authored object, content-driven height và transition thay vì runtime dựng hierarchy.

### Phương án được chọn / sửa / loại

- Generator tutorial chốt tại footprint `(35,29)–(36,30)` trong vòng yêu cầu ngày này; Sink được đổi sang footprint
  `(40,29)–(41,30)` trước các correction về sau.
- Description được bật/tắt bằng code nhưng layout/background do prefab/scene sở hữu.
- Chọn giữ description đủ lâu để đọc; chuyển step sau delay thay vì đóng ngay click frame.
- Bỏ ring thừa quanh enemy description và sửa CanvasGroup thiếu ở `Group Sinks`, `Next Wave Grid`, `Enemy Description`.

### Lý do

Dùng cùng cell source cho mask, hand, block và placement validation tránh highlight đúng nhưng đặt không được.

### Kết quả triển khai / kiểm tra

Placement và description behavior được cải thiện qua nhiều correction. Các lỗi compile `Text`/`RectTransform` và DOTween
CanvasGroup được sửa; runtime end-to-end vẫn tiếp tục được owner test trực tiếp.

## Entry 3 — Mở rộng Level 1 Wave 2–4 tutorial và retry Wave 3

### Vấn đề đang gặp

Tutorial phải dạy placement trước, sau đó cho người chơi tự link ở Wave 2; Wave 3 giới thiệu Sink/Generator; Wave 4 dạy
Fire và unlink/relink. Nếu enemy chạm cóc ở Wave 3 thì chỉ wave đó phải chơi lại.

### Prompt đã dùng

Owner liên tục sửa sequence, timing của text, overlay, HUD visibility, Start Wave, card locked/unlocked, placement và link;
yêu cầu Level Status bay ra giữa, health về 0 và hiện nút chơi lại.

### Phản hồi quan trọng của AI

Tutorial modes/conditions được mở rộng theo từng wave. Retry giữ level session nhưng reset riêng Wave 3, trả Level Status
về vị trí cũ và tiếp tục tutorial nếu thắng. Unlock tạm trong tutorial chỉ được persist khi Level 1 tutorial hoàn tất.

### Phương án được chọn / sửa / loại

- Wave 2 chỉ tutor đặt; text yêu cầu tự nối và Start Wave chờ chain hợp lệ.
- Wave 3 tutor đặt Sink và Generator, sau đó cho tự do trong giới hạn tiền/cell; vùng dành cho Wave 4 được reserve.
- Wave 4 hiện text và Fire drag đồng thời, sau placement chỉ cho tap Generator → Unlink → link Generator→Fire→Sink.
- Text tutorial giữ đến khi placement/link sequence kết thúc; overlay tắt ở những đoạn owner chỉ định.
- Retry dùng Level Status hiện hữu thay vì popup thua mới tách biệt.

### Lý do

Luồng theo wave giữ tutorial gắn với trạng thái gameplay thật và cho phép retry cục bộ mà không mất toàn bộ level.

### Kết quả triển khai / kiểm tra

Feature được commit ban đầu trong commit lớn `a19aaa77`; các correction tiếp tục ở working tree và ngày 10/09. Lỗi
`CanvasGroup`/destroyed Button được xử lý trong các vòng sau.

## Entry 4 — Tower HUD, unlock state và action UI

### Vấn đề đang gặp

HUD bị stretch/rebuild khác giữa level; Hero card xuất hiện trước khi unlock; Unlink/Upgrade/Sell cần authored layout và
không flash khi chọn tower.

### Prompt đã dùng

Owner yêu cầu bake UI trong prefab/scene, không tạo visual hierarchy bằng code; Generator có vòng xanh, Sink vòng đỏ;
level unlock theo preparation và save chỉ vĩnh viễn sau khi tutorial hoàn tất.

### Phản hồi quan trọng của AI

HUD được chuyển về prefab làm nguồn layout, code chỉ bind trạng thái. Unlock được tách giữa session tutorial tạm thời và
persistent completion. CanvasGroup/reference cần được bake vào đúng object thay vì `GetComponent` giả định.

### Phương án được chọn / sửa / loại

- Chọn fixed authored Tower HUD qua các level; chỉ card được unlock hoặc Hero thật sự mở mới thay đổi cấu trúc hiển thị.
- Chọn Generator unlock tại Wave 1 preparation, Sink tại Wave 3 preparation, Fire tại Wave 4 preparation.
- Chọn ring mesh/VFX authored dưới tower prefab; không instantiate ring bằng gameplay code.

### Lý do

Prefab-owned layout ngăn từng scene giữ override cũ và tránh stretch do runtime đổi kích thước group.

### Kết quả triển khai / kiểm tra

HUD và unlock flow được triển khai từng phần. Owner tiếp tục báo/sửa Unlink background và cross-level prefab override vào
ngày 10/09.

## Entry 5 — Toon shader ARMv7 và audio combat

### Vấn đề đang gặp

Toon material chỉ tím trên ARMv7/OpenGLES3, đặc biệt khi tắt Main Light shadow; đồng thời gameplay còn thiếu cue tower,
link, projectile và reaction.

### Prompt đã dùng

Owner cung cấp device status `UNSUPPORTED`, PowerVR Rogue GE8322; yêu cầu log cạnh FPS, hỗ trợ GLES3/Vulkan/Metal và wire
các âm tower touch, link success, projectile, Thermal Shock, Wave/Preparation music.

### Phản hồi quan trọng của AI

Shader được bổ sung mobile pass/fallback và guard shadow sampling theo keyword; diagnostic xuất cả UI và logcat. Audio cue
được phát từ domain event hiện hữu thay vì polling presentation.

### Phương án được chọn / sửa / loại

- Chọn on-device diagnostic vì Editor không tái hiện lỗi.
- Hạ shader target theo hướng URP Lit và giữ các graphics API mobile cần thiết.
- Chọn phase state qua `IWaveSystem.CreateState().Phase` sau lỗi compile vì interface không có property `Phase` trực tiếp.

### Lý do

Lỗi support/variant phải được đo trên GPU/API đích; event-driven cue giữ âm thanh đồng bộ với hành động gameplay.

### Kết quả triển khai / kiểm tra

Static shader/audio fixes được áp dụng nhưng Toon fallback chưa có xác nhận cuối trên thiết bị đích. Audio phase/cue tiếp
tục được tinh chỉnh ngày 10/09.

