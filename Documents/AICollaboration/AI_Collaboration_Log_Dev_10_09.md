# AI Collaboration Log — Tutorial Level 2, HUD cố định và gameplay corrections — 10/09/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Responsible Codex tasks:** `01a08452-6082-7d02-a126-b9a7d3b4f261`,
  `01a07b07-a490-71a2-8232-25bd8dcfb6f3`
- **Local date:** 10/09/2026 (`Asia/Saigon`, UTC+7)
- **Coverage:** toàn bộ 64 yêu cầu trong ngày của hai task

## Entry 1 — Bake Tower Action/HUD đúng prefab và giữ qua level

### Vấn đề đang gặp

Unlink mất background/không nhận click, action buttons bị stretch, scene Level 1/2 giữ override cũ và Hero container xuất
hiện dù Hero chưa unlock.

### Prompt đã dùng

Owner yêu cầu chỉ sửa authored prefab/scene cho layout, thêm Horizontal Layout/chống stretch, apply đúng vào các level và
không để `TowerNetworkHudView` tự đổi cấu trúc bằng code.

### Phản hồi quan trọng của AI

AI truy lỗi về prefab instance override và serialized reference. GameplayUI prefab được dùng làm nguồn chuẩn; action row
giữ kích thước button/sprite, còn scene instances bỏ override gây mất background hoặc thay layout.

### Phương án được chọn / sửa / loại

- Chọn bake background, raycast target, anchors và layout vào `GameplayUI.prefab`.
- Chọn giữ action visible sau first tap cho tới unselect hoặc bấm action.
- Chọn ẩn cả Hero card lẫn background theo unlock config; không để empty Hero shell.
- Loại runtime xây/reparent/resize Tower HUD.

### Lý do

Một prefab source of truth giúp HUD giống nhau ở mọi level và tránh scene override đè bản sửa.

### Kết quả triển khai / kiểm tra

Owner xác nhận Unlink đã hoạt động sau khi prefab instance được sửa. Các visual/action assets vẫn nằm trong nhóm thay đổi
cần commit theo feature.

## Entry 2 — Audio outcome, damage, Start Wave và music ducking

### Vấn đề đang gặp

Thiếu âm win/lose/frog damage/Start Wave; Wave music dừng quá sớm ở wave cuối.

### Prompt đã dùng

Owner cung cấp các file âm thanh và yêu cầu play đúng event; wave cuối phải tiếp tục phát tới khi modal kết quả hiện rồi
mới hạ volume còn 10%.

### Phản hồi quan trọng của AI

Cue được map vào catalog và phát ở presenter/system sở hữu event. Music transition được dời từ thời điểm Wave kết thúc sang
thời điểm Outcome HUD thật sự hiển thị.

### Phương án được chọn / sửa / loại

- Chọn `SoundId` cho Win, Lose, FrogDamaged và StartWave.
- Chọn duck wave loop còn 10% khi modal xuất hiện, không stop ngay final-wave completion.
- Giữ preparation music quay lại giữa các wave thường.

### Lý do

Âm thanh phải theo feedback người chơi nhìn thấy, tránh khoảng im lặng trước kết quả.

### Kết quả triển khai / kiểm tra

Catalog và event wiring được cập nhật; PlayMode listening test không được coi là hoàn tất trong log này.

## Entry 3 — Save/unlock progression và Tower HUD campaign

### Vấn đề đang gặp

Người chơi có thể thoát tutorial giữa chừng; unlock tạm không được lưu vĩnh viễn, nhưng tutorial hoàn tất phải persist và
không chạy lại. Level sau phải giữ card state của level trước.

### Prompt đã dùng

Owner chốt Generator mở ở Wave 1 preparation, Sink Wave 3 preparation, Fire Wave 4 preparation, Water Wave 6 preparation,
Wind khi vào Level 3, Hero Crab sau clear Level 7; action Sell/Upgrade/Unlink mở từ Wave 6 preparation.

### Phản hồi quan trọng của AI

Unlock campaign được tính từ persistent completion và level progression; unlock trong Level 1 tutorial là session-only cho
tới khi hoàn tất. Thoát giữa chừng quay lại baseline lock và chạy tutorial lại.

### Phương án được chọn / sửa / loại

- Chọn save một trạng thái completion/unlock ổn định, không serialize từng frame tutorial tạm.
- Chọn Tower HUD cố định qua level; chỉ state của card thay đổi.
- Chọn reserve 200 vàng cuối Wave 3 để bảo đảm mua Fire ở Wave 4.

### Lý do

Persist completion thay vì partial step tránh save bị kẹt ở trạng thái nửa tutorial và đơn giản hóa resume.

### Kết quả triển khai / kiểm tra

Unlock behavior được đưa vào presenter/progression/save flow; owner tự kiểm tra tutorial từ save reset.

## Entry 4 — Tutorial Level 2 Wave 1 và Wave 3

### Vấn đề đang gặp

Level 2 cần tutorial upgrade ở Wave 1 và tutorial Thermal Shock ở Wave 3; các cell tutorial phải được reserve trước, tower
placement miễn phí và sequence phải tiếp tục dù người chơi đặt tower khác ở Wave 1–2.

### Prompt đã dùng

Owner yêu cầu focus Fire authored → tap Upgrade; Wave 3 giới thiệu Magic Resistant enemy, đặt Water/Fire/Sink/Generator
theo target, rồi link Generator→Fire→Water→Fire→Sink. Hero phải ẩn nhưng phần Tower HUD khác vẫn hiện.

### Phản hồi quan trọng của AI

`LevelTwoUpgradeTutorial` và `LevelTwoThermalShockTutorial` dùng target/condition riêng. Required/reserved placements dùng
cùng anchor cell; tutorial placement được miễn phí. Text Magic Resistant được canh trái và giới hạn safe area.

### Phương án được chọn / sửa / loại

- Giữ Fire authored cho upgrade Wave 1, nhưng Wave 3 bổ sung một Fire tutorial tại vị trí Sink cũ.
- Water target giữ footprint `(38,27)–(39,28)`; Generator `(38,34)–(39,35)`.
- Sink target được sửa nhiều lần trong ngày và cuối ngày chốt anchor `(38,24)`, footprint
  `(38,24)`, `(39,24)`, `(38,25)`, `(39,25)`.
- Chọn hand drag card→highlight cho từng placement; sau đủ tower mới dạy các direct link theo thứ tự.

### Lý do

Target duy nhất cho mask, hand, reserve và validation loại bỏ lỗi “đặt đúng highlight nhưng tutorial không tiến”.

### Kết quả triển khai / kiểm tra

Working tree hiện dùng `new GridCell(38, 24, 0)` cho `Level Two Sink Tutorial Placement Target`; footprint 2×2 làm các
cell block/highlight theo đúng bốn ô chốt cuối. End-to-end PlayMode do owner tự kiểm tra.

## Entry 5 — Wave 3 retry cho Level 2 và tutorial focus Fire hit

### Vấn đề đang gặp

Level 2 Wave 3 cần thua/retry cục bộ khi cóc bị hit một lần. Level 1 cần focus lần đầu bất kỳ enemy nào dính Fire, kể cả
hit chí mạng, đồng thời fade toàn Gameplay UI.

### Prompt đã dùng

Owner yêu cầu Level Status tween vào giữa, health tụt về 0, overlay trở lại và nút replay chỉ reset Wave 3. Với Fire hit,
phải pause trước khi enemy chết, focus icon Fire rồi fade UI trở lại khi unfocus.

### Phản hồi quan trọng của AI

Combat timeline phát first-fire-hit signal trước lethal resolution; focus system giữ enemy sống trong frame tutorial. UI
fade dùng authored CanvasGroup. Retry presenter bảo toàn level và khôi phục HUD/overlay sau click.

### Phương án được chọn / sửa / loại

- Chọn first Fire impact trên bất kỳ enemy, không giới hạn Armored.
- Chọn deferred lethal frame để target không biến mất trong focus.
- Bỏ gọi UI trong `TutorialFocusSystem.Dispose()` sau `MissingReferenceException` vì view có thể đã bị destroy khi scope
  teardown.

### Lý do

Publish focus trước death và không chạm destroyed view trong Dispose xử lý đúng thứ tự lifecycle thay vì chỉ thêm null guard
rải rác.

### Kết quả triển khai / kiểm tra

Source đã bỏ lệnh `SetTutorialFocusVisible(true)` khỏi `Dispose`; compile check trước đó sạch. Full gameplay path vẫn cần
owner xác nhận trên PlayMode.

## Entry 6 — Sink, speed support, pause overlay và tower upgrade VFX

### Vấn đề đang gặp

Sink reservation làm projectile phải chờ slot; speed support chưa stack theo rule; Pause phải ẩn trong tutorial overlay;
upgrade tower cần effect mới.

### Prompt đã dùng

Owner yêu cầu Sink nhận đạn tức thì; mỗi speed buff sau cộng thêm 25% của buff đầu; hide Pause khi black overlay; khóa drag
lung tung ở step tap tower và play `Resources/Prefabs/VFX/Upgrade` khi upgrade.

### Phản hồi quan trọng của AI

Reservation/queue được bỏ khỏi link-to-sink path. Speed stack được đưa vào stat/status rule. Tutorial input gate giới hạn
action theo step. Upgrade event phát VFX qua shared emitter tại ground centre.

### Phương án được chọn / sửa / loại

- Chọn direct Sink intake.
- Chọn additive stack `base + 0.25 × base` cho mỗi buff kế tiếp.
- Chọn authored Pause visibility theo overlay state và input gate cho tap-only step.
- Chọn VFX prefab từ Resources, không tạo particle hierarchy bằng code.

### Lý do

Các thay đổi bám vào event/rule hiện hữu, không thêm subsystem mới.

### Kết quả triển khai / kiểm tra

Code, config và prefab liên quan đang nằm trong working tree cần chia commit. `Upgrade.prefab` là asset mới chưa track ở
đầu phiên commit này.

