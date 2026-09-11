# AI Collaboration Log — Tutorial nền tảng, gameplay feedback và mobile GPU — 08/09/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Responsible Codex tasks:** `01a07b07-a490-71a2-8232-25bd8dcfb6f3`,
  `01a0825d-17dd-77a3-b57b-5716908cb710`
- **Local date:** 08/09/2026 (`Asia/Saigon`, UTC+7)
- **Coverage:** toàn bộ 82 yêu cầu trong ngày, gồm 80 trao đổi gameplay và 2 trao đổi profiling

## Entry 1 — Hoàn tất authored tower runtime và sửa test flow

### Vấn đề đang gặp

Tower đặt bằng Board Painter chưa nối link hoặc tham gia combat đầy đủ; một số PlayMode test phụ thuộc hierarchy/input cũ.

### Prompt đã dùng

Owner yêu cầu tower pre-placed hoạt động như tower bình thường, sửa `KeyNotFoundException`, bảo đảm chỉ một EventSystem,
đồng bộ layer và sửa/xóa test journey drag nếu test không còn đúng.

### Phản hồi quan trọng của AI

AI giải thích callback `LoadLevel(request, completion)` và phân biệt lỗi gameplay với test assumption. Authored tower được
đưa vào cùng runtime network để link và bắn; test được cập nhật theo hierarchy/prefab hiện hành.

### Phương án được chọn / sửa / loại

- Chọn sửa integration và test expectation tại nguồn.
- Chọn một EventSystem duy nhất.
- Loại việc coi mọi test fail là lỗi game khi gameplay path hiện hành vẫn đúng.

### Lý do

Test phải bảo vệ behavior hiện tại, không giữ cấu trúc UI đã bị thay thế.

### Kết quả triển khai / kiểm tra

Luồng authored tower được nối tiếp trong runtime. Các lỗi được giải thích theo mức dễ hiểu trước khi owner chọn phần cần
sửa.

## Entry 2 — Xây tutorial overlay, focus mask và hand interaction

### Vấn đề đang gặp

Level 1 cần tutorial tuần tự có black overlay, vùng sáng mềm, text, click-to-complete typing và hand chỉ xuất hiện cho hành
động thật sự cần tương tác.

### Prompt đã dùng

Owner yêu cầu đọc architecture rồi lập plan, sau đó triển khai: giới thiệu enemy sắp tới, focus cóc trong world, chỉ nút
Start Wave, hướng dẫn drag Generator→Sink, ẩn/hiện từng phần HUD, canh text trong safe area và dùng DOTween easing.

### Phản hồi quan trọng của AI

Tutorial được tách thành progress/step/state và Unity-facing overlay/target/hand. Target được đăng ký bằng ID; overlay chỉ
được bật ở step cần focus, còn hand dùng click hoặc drag animation theo action.

### Phương án được chọn / sửa / loại

- Chọn mask mềm và authored UI thay cho runtime dựng lại toàn bộ HUD.
- Sửa thứ tự nhiều lần theo feedback: delay ban đầu → enemy preview → cóc world → Start Wave → placement/link.
- Chỉ giữ Pause cùng Wave Toggle/Grid ở các step owner chỉ định; các vùng card dưới đáy được ẩn riêng.
- Bỏ glow/ring ở những target không cần hand; hạ alpha viền link và bỏ viền Start Wave.

### Lý do

State rõ ràng giúp mỗi correction thay đổi một step thay vì làm UI global nhấp nháy hoặc mất text.

### Kết quả triển khai / kiểm tra

Tutorial chạy được qua các bước đầu nhưng tiếp tục được tinh chỉnh trong các ngày 09–10/09. Một số lần chỉ kiểm tra source
hoặc serialized asset theo yêu cầu không test sau mỗi fix.

## Entry 3 — Font, Frog feedback và tower upgrade data

### Vấn đề đang gặp

Font gameplay không đồng nhất; cóc cần phản hồi rõ khi bị hit; dữ liệu upgrade có field trùng nghĩa và tier tăng quá nhiều
stat cùng lúc.

### Prompt đã dùng

Owner yêu cầu import Baloo 2 và đổi toàn bộ gameplay text; sửa lỗi mất chữ bằng scene authoring; thêm flash đỏ, animation
be-hit, DOTween shake/curve cho Level Status; bỏ linear/cost-per-level cũ và chỉ giữ tier cost, tier chỉ tăng damage.

### Phản hồi quan trọng của AI

Font/material reference được chuyển sang Baloo 2; UI text bị thiếu được truy về prefab/scene thay vì runtime overwrite.
Health feedback được phát tại damage event. Upgrade data được thu gọn về tier cost và stat progression hiện dùng.

### Phương án được chọn / sửa / loại

- Chọn sửa authored references cho text/UI.
- Chọn flash/shake đúng thời điểm cóc nhận damage.
- Chọn chỉ Tier One/Two và giữ attack speed/range bằng nhau giữa tier; damage là stat tăng.
- Loại các field economy/upgrade tuyến tính không còn consumer.

### Lý do

Giảm dữ liệu trùng và đặt feedback tại event thật giúp Inspector dễ hiểu hơn, tránh runtime ghi đè visual.

### Kết quả triển khai / kiểm tra

Các thay đổi data/UI được đưa vào working history; phần Blender frog animation được yêu cầu hai lần nhưng không có bằng
chứng hoàn tất đầy đủ trong task log này.

## Entry 4 — Khôi phục tutorial sau conflict và chuẩn bị lịch sử Git

### Vấn đề đang gặp

Sau pull/conflict, UI tutorial và thứ tự step bị mất hoặc lệch so với local trước conflict.

### Prompt đã dùng

Owner yêu cầu giữ local, phục hồi tutorial cũ, không push cho tới khi được gọi, chuẩn bị commit ngắn theo feature và gồm cả
stage/unstage ngoài nhóm.

### Phản hồi quan trọng của AI

AI đối chiếu lại source/scene để phục hồi chuỗi delay, focus enemy, text cạnh card, Start Wave reveal và cleanup overlay/hand.

### Phương án được chọn / sửa / loại

- Chọn local behavior làm nguồn đúng sau conflict.
- Chọn giữ commit boundary theo feature.
- Tạm hoãn push theo chỉ dẫn trong ngày.

### Lý do

Tutorial là feature xuyên script, prefab và scene; phục hồi chỉ một file không đủ để lấy lại behavior trước conflict.

### Kết quả triển khai / kiểm tra

Chuỗi tutorial được phục hồi từng phần và tiếp tục correction ngày 09/09. `CanvasGroup` thiếu ở `Group Sources` xuất hiện ở
cuối ngày và trở thành hạng mục bake UI tiếp theo.

## Entry 5 — Chẩn đoán GPU bottleneck trên thiết bị yếu

### Vấn đề đang gặp

Profiler trên low-end mobile cho thấy GPU bottleneck cao; owner muốn danh sách thử nghiệm trước khi thay asset/setting.

### Prompt đã dùng

Owner yêu cầu chỉ tư vấn các điểm nên thử, sau đó hỏi đúng vị trí tắt Main Light shadow.

### Phản hồi quan trọng của AI

AI phân biệt GPU work với VSync/frame pacing, đề xuất A/B theo thứ tự: render scale, shadow, overdraw/transparent VFX,
post-processing, lighting và shader. Hướng dẫn ưu tiên `Mobile_RPAsset.asset → Lighting → Main Light → Cast Shadows`.

### Phương án được chọn / sửa / loại

- Chọn thay đổi từng setting một và đo lại trên đúng thiết bị.
- Không sửa file hay setting trong task profiling này.

### Lý do

Đo A/B giữ nguyên nhân rõ ràng và tránh tối ưu mù trên Editor GPU khác thiết bị đích.

### Kết quả triển khai / kiểm tra

Task chỉ đưa hướng dẫn; không có asset mutation. Sự cố Toon shader tím khi tắt Main Light shadow được điều tra sâu hơn ở
task khác trong ngày 09–10/09.

