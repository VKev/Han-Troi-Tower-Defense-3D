# AI Collaboration Log — Dựng lại gameplay HUD, Journey map và hệ thống sao — 06/09/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Agent:** Claude Code (`claude-opus-5`)
- **Session ID:** `176795ea-a2a9-45cb-a573-739a29a6a15f`
- **Local date:** 06/09/2026 (`Asia/Saigon`, UTC+7)
- **Coverage:** 64 yêu cầu trong ngày (00:18 – 23:57), gộp theo feature thay vì chép raw transcript

## Entry 1 — Dựng lại toàn bộ gameplay HUD theo art Production

### Vấn đề đang gặp

HUD gameplay còn là bản prototype, trong khi toàn bộ art đã hoàn thiện và nằm sẵn trong `Assets/Art/UI/Production`.

### Prompt đã dùng

"nhìn ảnh và dựng lại toàn bộ gameplay UI , art đã trong production hết rồi", kèm "nhớ dùng better context đọc khi chưa
hiểu, refresh nếu cần", "art ở đây hết: Assets/Art/UI/Production" và "chỉ wire UI vào gameplay thôi, ko đụng tới code
nha".

### Phản hồi quan trọng của AI

Khoảng 20 sprite trong `GameplayUI.prefab` được trỏ lại sang art Production, thêm các node `Backdrop`/`Glyph`/`Icon`/
`Bar`, và `Tower Buttons` được cấu trúc lại thành 4 nhóm card. Ràng buộc "không đụng code" được giữ cho tới khi chính
owner chọn phương án phải sửa code.

### Phương án được chọn / sửa / loại

- Chọn sửa trực tiếp trong prefab dùng chung để cả 10 level cùng hưởng, thay vì sửa từng scene.
- Owner yêu cầu "gán trong inspector luôn" — reference được ghi thành serialized data trong scene/prefab chứ không gán
  bằng code lúc runtime.

### Lý do

HUD là prefab dùng chung; sửa một nơi tránh việc 10 level trôi khác nhau.

### Kết quả triển khai / kiểm tra

Phát hiện một cạm bẫy lặp lại suốt cả ngày: **prefab instance override trong scene đang mở sẽ âm thầm chặn thay đổi từ
prefab**. Mọi chỉnh sửa prefab sau đó đều đi qua `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset` thay vì ghi
thẳng YAML, vì Unity từng ghi đè ngược lại thay đổi YAML (Canvas `renderMode` tự quay về giá trị cũ).

## Entry 2 — Screen Space Camera, răng cưa và FXAA trên mobile

### Vấn đề đang gặp

Owner chuyển canvas sang Screen Space - Camera để UI hưởng antialiasing của camera, nhưng khi về Overlay thì UI bị răng
cưa, còn ở chế độ Camera thì overlay bị "vỡ hạt" trên mobile.

### Prompt đã dùng

"mobile tôi cần antilising nào nhẹ nhất FXAA được chứ?", "bây h thử chuyển lại qua screen space overlay đi, mà ko đổi gì
hết", "UI bị hở ra, và overlay trên mobile bị vỡ hạt, tại sao, đó là lý do tôi dùng screen space camera để có AA. Sửa
việc bar hở rồi giải thích, khoan sửa gì thêm".

### Phản hồi quan trọng của AI

FXAA là lựa chọn nhẹ nhất trong URP cho mobile. Yêu cầu "khoan sửa gì thêm" được tôn trọng: chỉ sửa đúng chỗ thanh bị hở
rồi giải thích, không đụng phần còn lại.

### Phương án được chọn / sửa / loại

- Giữ Screen Space - Overlay theo quyết định của owner, không đổi thêm thứ gì khác.
- Owner tự xử lý phần răng cưa sau đó ("ok tôi tự sửa vụ răng cưa rồi").

## Entry 3 — Thanh máu cóc bị kéo giãn hai đầu bo tròn

### Vấn đề đang gặp

Thanh máu dùng `fillAmount` nên phần xanh bị kéo giãn, hai đầu bo tròn biến dạng; owner đã tự thêm border để 9-slice
nhưng "không hoạt động".

### Prompt đã dùng

"thanh máu đang stretch nên nhìn nó kỳ, tôi đã thêm border để stretch ra thì vẫn giữ nguyên 2 đầu, nhưng ko hoạt động?"

### Phản hồi quan trọng của AI

`Image.Type.Filled` **bỏ qua sprite border**, nên 9-slice không thể có tác dụng khi còn dùng Filled. Có hai đường: đổi
cách dựng art, hoặc đổi sang điều khiển chiều rộng bằng anchor. Owner chọn phương án 2 (sửa 3 dòng code).

### Phương án được chọn / sửa / loại

Trong `LevelStatusHudView.cs`, thay `fillAmount` bằng việc chỉ dịch anchor phải:

```csharp
float ratio = maximumHealth <= 0 ? 0f : Mathf.Clamp01((float)currentHealth / maximumHealth);
RectTransform fill = healthFill.rectTransform;
fill.anchorMax = new Vector2(ratio, 1f);
```

### Lý do

Giữ `anchorMin` ở 0 và chỉ kéo cạnh phải thì hai đầu bo tròn giữ đúng bán kính đã author.

### Kết quả triển khai / kiểm tra

Đo lại khoang chứa: vùng trong của art là 607×62 trên nền 661×117, tức 201×17.5 đơn vị — không phải 209.5×24.5 như cấu
hình cũ, đó là lý do phần xanh tràn ra ngoài viền vàng.

## Entry 4 — Vùng tối dưới giá tiền, cỡ UI cho mobile và căn giữa coin

### Vấn đề đang gặp

UI mẫu có vầng tối dưới chỗ tiền và mọi thứ to hơn để dễ nhìn trên mobile; bản hiện tại chưa có, chữ lại sát rìa phải,
coin bị lệch xuống.

### Prompt đã dùng

"đang hơi sát quá, chữ nên cách rìa phải tí"; "hãy nhìn UI mẫu trong ảnh, có các vầng tối dưới chỗ tiền, sửa UI của tôi
cũng có, và trong UI mẫu, mọi thứ để bự hơn để phục vụ cho mobile dễ nhìn"; "các coin hơi lệch xuống dưới, sửa cho nó căn
giữa vertical ở vùng đen"; "nhớ sáng tạo dùng thêm shadow".

### Phương án được chọn / sửa / loại

- Thêm `Cost Shade` (Image Filled/Vertical) và `Cost Row` dùng `HorizontalLayoutGroup` để coin + số tự căn giữa.
- Dùng component `UnityEngine.UI.Shadow` cho chiều sâu thay vì vẽ thêm sprite.

### Kết quả triển khai / kiểm tra

Hai lần sai phải tự sửa: bóng của panel "không thấy" vì falloff vẽ 145 đơn vị nhưng chỉ tràn ra 50 — chỉnh
`pixelsPerUnitMultiplier` từ 2.2 xuống 5.8; và lớp làm tối "không ăn" thực ra vẫn ăn — alpha 0.5 trên nền đen chỉ tối đi
~27% theo cảm nhận sRGB, nên nâng lên 0.72.

## Entry 5 — Responsive cho iPad và safe area của panel Pause

### Vấn đề đang gặp

Trên iPad, bảng máu cóc chạm vào UI trụ; panel tối phía dưới của menu Pause lại nằm lọt trong safe area.

### Prompt đã dùng

"handle ressponsive luôn, khi ipad thì máu cóc đang dính tới UI trụ rồi"; "sửa sao cho panel tối ở dưới ko nằm trong safe
area".

### Phản hồi quan trọng của AI

Với CanvasScaler `ScaleWithScreenSize` ref 1920×1080 match 0.5, canvas ở 4:3 là 1663×1247 còn iPhone 13 Pro Max là
2118×979 — hai đầu tỉ lệ này là nơi layout vỡ.

### Kết quả triển khai / kiểm tra

Lần kiểm tra responsive đầu tiên **không hợp lệ**: gán `sizeDelta` cho RectTransform của Canvas không có tác dụng, phải
gán `cam.targetTexture` trước. Sau khi sửa cách đo mới ra kết quả tin được.

## Entry 6 — Card trụ: nền, icon lệch, ảnh stretch và trạng thái khoá

### Vấn đề đang gặp

Nút trụ thiếu card nền theo mockup mới, icon lửa/nước lệch, ảnh card bị kéo giãn; trạng thái khoá thì hiển thị thừa
(coin, chữ LOCK, placeholder).

### Prompt đã dùng

"tôi vừa thêm backgorund và icon, sửa UI gameplay để giống như ảnh"; "i con lửa nước đang bị lệch, và ảnh card ở trong
đang bị stretch, dùng slice" / "hoặc preserve aspect"; "tôi vừa sửa UI lửa nước gió, làm tương tự cho các UI khác của
generator, sink , crabhero"; "when lock, jusst gray the icon, and dont show coin, dont show lock..."; "bỏ icon sau luôn,
khóa bây h là icon, còn màu nên vẫn giữ màu sắc, chỉ là nó tối hơn thôi".

### Phản hồi quan trọng của AI

`Button.transition = ColorTint` **ghi đè `targetGraphic.color`**, nên màu set bằng script luôn bị xoá; trạng thái khoá
phải sống trong `colors.disabledColor`. Trong `TowerPlacementDragButtonView.cs`, các hàm tô màu bị gỡ bỏ và thay bằng
`ApplyLockedVisibility` chỉ bật/tắt `.enabled`.

### Phương án được chọn / sửa / loại

- Chọn để `disabledColor` quyết định độ tối, bỏ `LockedTint`, `LockedIconTint`, `CacheUnlockedColors` và các field cache
  màu.
- Loại cách tô màu bằng script vì luôn bị `ColorTint` ghi đè.

### Kết quả triển khai / kiểm tra

Hai lần chẩn đoán sai được ghi nhận và sửa:

- Card khoá trông "tan nhợt" là do `disabledColor` alpha 0.502, chỉnh về `(0.44, 0.44, 0.44, 1)`.
- Mất card background ban đầu bị đổ oan cho sprite atlas (đã nâng lên 4096 mà không đổi gì, sau đó revert). Nguyên nhân
  thật là `CardBackground.png` bị cap `maxTextureSize` 512 ở Android nên chỉ còn 35% kích thước.
- Sau hai lần thử 9-slice hỏng, AI **tự đính chính**: vòng ring chỉ nằm cách mép 10–20px, tức nằm trong border 42, nên
  slice vẫn đúng; thứ tưởng là artifact thật ra là ring render đúng. Chốt Sliced với `ppuMultiplier` 2.2.

## Entry 7 — Journey map: Sky, Layer2 và re-skin bằng atlas mới

### Vấn đề đang gặp

Bầu trời của Journey map không fit màn hình mà vẫn giữ tỉ lệ; lớp Layer2 hở đáy màn trên iPad/4:3; toàn bộ chrome cần
re-skin theo atlas mới.

### Prompt đã dùng

"sửa lài bầu trời Sky ở Journey map để nó scale mà vẫn preserve aspect, scale fit screen mà vẫn presserve aspect nha";
"sửa sao cho layer 2 move vị trí y xuống dưới đáy màn hình trên mọi device, để khi lên ipad thì ko bị hở đáy"; "hở rồi
4:3"; "sửa layer 2 thôi, 1 nằm sau 2 nên ko sao"; "cập nhật UI lại cho giống ảnh... ko cần sửa background nữa background
đúng rồi" + "ở trong Assets/Art/UI/Production/ApplicationUI nha".

### Phản hồi quan trọng của AI

`preserveAspect` chỉ làm aspect-**fit**; muốn aspect-**fill** (cover) phải dùng `AspectRatioFitter.EnvelopeParent`. Sky
được tách thành node riêng với `EnvelopeParent` aspect 1.747. `JourneyParallaxView` được bổ sung "Sky" vào danh sách lớp
đứng yên:

```csharp
private static readonly string[] StillLayerPrefixes = { "Fog", "Sky" };
```

### Kết quả triển khai / kiểm tra

Kết luận đầu tiên về Layer2 ("đã phủ kín") là **sai** vì AI tự ép `ConstantPixelSize` thay vì dùng `ScaleWithScreenSize`
đang ship. Nguyên nhân thật gồm hai thứ: override `y = 160` còn sót trong `Bootstrap.unity`, và 24px padding trong suốt ở
đáy `layer1.png`. Atlas mới 1672×941 được map thành 13 sprite (node các trạng thái, sao, coin, nút back, thanh ngăn,
pill) và `LevelButton.prefab` được gán lại đủ 5 trạng thái node.

## Entry 8 — Ba sao theo máu cóc còn lại và nút Xuất quân

### Vấn đề đang gặp

Chưa có hệ thống sao cho từng level, và nút xuất quân chưa dùng art Production.

### Prompt đã dùng

"trong production có xuất quân UI, và hãy 3 sao dưới chân mỗi level, sửa data để khi clear màn mà ko mất máu cóc thì 3
sao, mất nhưng vẫn còn trên hoặc bằng 50% máu là 2sao, dưới 50% là 1 sao"; "sửa nút xuất quân để dùng ui trong production
thôi".

### Phản hồi quan trọng của AI

Kiểm tra `UnlockProgress.cs`: save chỉ theo dõi `unlockedLevels` và `clearedLevels`, **không có chỗ nào lưu số sao theo
level** — nên đây là thêm dữ liệu mới chứ không phải sửa hiển thị.

## Entry 9 — Splash screen và màn Game Start

### Vấn đề đang gặp

Splash screen đang nằm trong scene, và màn Game Start chưa bấm-chỗ-nào-cũng-được để vào level menu.

### Prompt đã dùng

"bỏ spashscreen vào project setting thay vì để trong scene, và sau đó sửa game start để bấm bất cứ đâu để vào level
menu".

### Phương án được chọn / sửa / loại

Chuyển splash sang Project Settings (đường chính thức của Unity) thay vì để scene tự dựng, rồi cho toàn bộ vùng màn hình
Game Start nhận tap.

## Entry 10 — Màn hình Victory

### Vấn đề đang gặp

Chưa có UI victory; sau khi owner tự chỉnh thì bảng bị cảm giác kéo ngang.

### Prompt đã dùng

"làm UI victory dùng các component có sẵn của Productions và còn thiếu đã bổ sung ở victory trong production"; "bây h làm
màn hình viectory bự ra về chiều dọc, dọc xuống thêm một miến cho đỡ cảm giác bị stretch horizontal".

## Entry 11 — Kéo trụ: footprint và vòng tầm

### Vấn đề đang gặp

Khi kéo trụ từ UI ra chưa thấy footprint ô, và chưa thấy tầm link của trụ đang kéo.

### Prompt đã dùng

"tôi cần bây h khi kéo tower ra từ UI, hiện footprint và cũng hiện luôn tầm range link của trụ"; "khi kéo ra ko hiện tầm
link của trụ tôi đang kéo, chỉ mới hiện foot print?"; "dùng cell texture trong UI production để display Cell foot print
(tạo material hoặc gì đó), chỉnh alpha và màu đúng"; "sửa UI hiện thị range sao cho nó giống vầy"; "chuyển nó thành
prefab, và tôi muốn khi click/ touch vào trụ đã đặt, show range của nó luôn"; "make sure nó hoạt động khi tôi đổi range
trụ thì nó thu nhỏ" / "hoặc phóng to"; "cái màu của hình tròn đang dark lại tôi muốn thay vì dark lại thì ngã trắng như
trong ảnh".

### Phương án được chọn / sửa / loại

- Footprint dùng cell texture trong Production qua material riêng, chỉnh alpha/màu thay vì vẽ mới.
- Vòng range được tách thành prefab dùng chung cho cả lúc kéo và lúc chọn trụ đã đặt.
- Bán kính lấy theo range thực của trụ để đổi stat là vòng đổi theo.

## Entry 12 — Chuyển cảnh mây: thử rồi loại bỏ

### Vấn đề đang gặp

Owner muốn hai bức mây từ hai bên trôi vào để che lúc load, thay cho panel loading.

### Prompt đã dùng

"Assets/Art/UI/Production/CloudWall.png có UI này là 1 bên, tôi muốn làm 2 bên, khi bấm vào game start thì mây từ 2 bên
trôi vào, và mở ra để show level menu..."; "chưa thấy được effect mây?"; "bấm vẫn ko thấy effect"; "không stretch cloud,
chỉ làm nó to lên và preserve aspect"; rồi chốt: "thôi bỏ cloud wall và xóa cloud wall đi, dùng fade tối sang
transparency và transparency sang tối thôi".

### Phương án được chọn / sửa / loại

- Loại hẳn CloudWall sau vài vòng không đạt, xoá asset liên quan.
- Chọn fade đen ↔ trong suốt cho mọi chuyển cảnh.

### Lý do

Quyết định của owner sau khi hiệu ứng mây không thuyết phục qua nhiều vòng chỉnh.

## Entry 13 — Skip wave không hiện panel chiến thắng, và nút nâng cấp khi max cấp

### Prompt đã dùng

"khi bấm skip wave, chiến thắng panel ko hiện ra?"; "sửa nút nâng cấp, nếu max cấp hoặc không nâng cấp được thì icon mũi
tên cũng nên xám luôn".

### Phương án được chọn / sửa / loại

Icon mũi tên đi theo cùng trạng thái disabled của nút thay vì chỉ làm xám nền, để trạng thái "không nâng cấp được" đọc
được ngay.

## Entry 14 — Tìm VFX miễn phí cho link và vòng chọn trụ

### Prompt đã dùng

"ok search cho tôi vfx free, tôi cần vfx cho link, cho select trụ".

### Phản hồi quan trọng của AI

Ghi chú ràng buộc nền tảng cho mọi lựa chọn VFX sau này: **VFX Graph cần compute shader + SSBO**, không hợp với OpenGL ES
theo tài liệu Unity; project đang nhắm Vulkan + OpenGLES3, nên ưu tiên particle system thường.

## Ghi chú phương pháp trong ngày

- Mọi kết luận về layout đều đo bằng chương trình (decoder PNG thuần Python trong scratchpad, `GetWorldCorners`, đổi
  clear color camera sang màu magenta để lộ khe hở) thay vì nhìn ảnh đoán.
- Ảnh kiểm tra được render bằng cách load prefab vào preview scene riêng với camera và RenderTexture tạm, không để lại
  dấu vết trong scene của owner.
- Shell chỉ có Python 2.7: `re.split` với lookahead zero-width không cắt được chuỗi, phải dùng span của `re.finditer`.
