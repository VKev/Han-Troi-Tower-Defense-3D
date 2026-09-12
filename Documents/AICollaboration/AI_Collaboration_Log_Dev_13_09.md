# AI Collaboration Log — Seating trụ đặt sẵn, icon nguyên tố, giới hạn hero và fix Level 10 — 13/09/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Agent:** Claude Code (`claude-opus-5`)
- **Session ID:** `0c44cb13-bb6e-42c3-ae27-0355645d3cfb`
- **Local date:** 13/09/2026 (`Asia/Saigon`, UTC+7)
- **Coverage:** 12 yêu cầu trong ngày (00:11 – 02:16)

## Entry 1 — Trụ đặt sẵn ở Level 8 bị lún xuống đất

### Vấn đề đang gặp

Trụ nước đặt sẵn qua board painter ở Level 8 lún khoảng 1.0 đơn vị dưới mặt ô.

### Prompt đã dùng

"Trụ nước đặt sẵn ở level 8 bị lún xuống đất (đặt ở board painter)".

### Phản hồi quan trọng của AI

Hai đường đặt trụ không giống nhau: `TowerInstanceFactory` (đặt từ card) nâng instance cho **đáy renderer** chạm mặt ô,
còn `BoardSceneSynchronizer` (board painter) chỉ đặt **pivot** lên mặt ô. Mesh trụ được author quanh pivot nên lún đúng
nửa chiều cao. Đo được: Water cần nâng `1.015`, Earth `0.852`, các trụ khác ~0.

### Phương án được chọn / sửa / loại

- Thêm `TowerSurfaceAlignment` dùng chung, đo đáy phần thực sự được vẽ (bỏ renderer tắt và object inactive, chạy được cả
  trên prefab asset).
- Cộng lift trong `BoardGeometryPlanner` (không phải lúc tạo instance) để `HasMatchingAuthoredTowers` vẫn nhận instance
  là up-to-date, tránh sync lại mỗi lượt.

### Lý do

Một luật seating duy nhất cho cả hai đường thì trụ đặt sẵn đứng y như trụ người chơi xây.

### Kết quả triển khai / kiểm tra

Sau sync: pivot `-2.215`, đáy mesh `-3.23` = đúng mặt ô. Thêm test
`Synchronizer_SeatsATowerWhoseMeshStraddlesItsPivotOnTheSurface`. Phát hiện kèm: helper đo silhouette trong
`GridPlacementRulesTests` vẫn đếm biển warning inactive (thấp hơn mesh ~5mm) và chính nó đang quyết định cao độ đặt trụ —
đã lọc theo `activeInHierarchy`. Một số test còn sót `GetComponent<Text>()` sau đợt chuyển TMP cũng được sửa.

## Entry 2 — Camera Level 8 lỗi do camera debug của AI

### Vấn đề đang gặp

Owner báo camera Level 8 bị lỗi.

### Prompt đã dùng

"và camera level 8 có vẻ bị lỗi".

### Phản hồi quan trọng của AI

Đây là lỗi do chính AI gây ra: một `__InspectionCamera` tạo tạm để chụp ảnh kiểm tra trụ nước đã bị lưu vào
`Level_008.unity`, khiến scene có hai camera enabled.

### Phương án được chọn / sửa / loại

Mở Level 8 additive để xoá camera thừa rồi lưu, không đụng scene Bootstrap đang mở và chưa save của owner.

### Kết quả triển khai / kiểm tra

Scene còn đúng một `Main Camera`; transform của nó so với HEAD giống hệt, tức camera của level không bị đụng.

## Entry 3 — Thanh ngang dưới nút Bắt đầu đợt (chưa kết luận)

### Vấn đề đang gặp

Owner thấy một thanh ngang mảnh ngay dưới nút BẮT ĐẦU ĐỢT.

### Prompt đã dùng

"có thanh ngang dưới bắt đầu đợt, sửa bỏ thanh ngang đi" (kèm ảnh crop).

### Phản hồi quan trọng của AI

Quét toàn bộ `GameplayUI.prefab` và `ApplicationUI.prefab`: không có Image/divider mảnh-và-rộng nào. Nút nằm ở
`y = 32..160` tính từ đáy safe area và **dưới nó không còn element UI nào**; `Build Bar` alpha 0, `Start Wave Hint` nằm ở
`y = 236` tức phía trên nút.

### Phương án được chọn / sửa / loại

Không sửa mò. AI báo khả năng cao là seam trong scene (mép board) chứ không phải UI, và xin ảnh full màn hình hoặc quyền
vào Play Mode để xác định.

### Lý do

Xoá nhầm một element dùng chung trong HUD đắt hơn nhiều so với việc hỏi thêm một câu.

### Kết quả triển khai / kiểm tra

Treo chờ owner xác nhận.

## Entry 4 — Icon nguyên tố trên đầu địch bị đạn đè

### Vấn đề đang gặp

Icon nguyên tố trên đầu kẻ địch bị đạn và hiệu ứng vẽ đè lên.

### Prompt đã dùng

"làm UI trên đầu kẻ địch (icon nguyên tố) nằm trên các thứ trong map để đạn ko đè lên nó".

### Phản hồi quan trọng của AI

Icon và mọi VFX đạn đều ở queue 3000 với `ZWrite Off`, nên thứ tự vẽ do khoảng cách camera quyết định. Cả 8 prefab địch
dùng chung một material `ElementIcons.mat` nên chỉ cần sửa một chỗ.

### Phương án được chọn / sửa / loại

- Thêm `EnemyStatusIconOverlay.shader`: `Queue = Transparent+500`, `ZTest Always`, `ZWrite Off`, giữ nguyên blend và
  `Cull Off`; `CBUFFER_START(UnityPerMaterial)` để còn tương thích SRP Batcher.
- Loại phương án kéo icon lên cao trong world space vì không đảm bảo được.

### Kết quả triển khai / kiểm tra

Bẫy phải xử lý: material giữ `m_CustomRenderQueue: 3000` sẽ đè queue của shader, nên đặt về `-1`. Shader compile sạch,
material queue 3500, texture và tint giữ nguyên.

## Entry 5 — Mỗi level chỉ một trụ hero

### Vấn đề đang gặp

Chưa có giới hạn số trụ crab/hero mỗi level.

### Prompt đã dùng

"trụ crab/hero là mỗi level chỉ được đặt 1 trụ, có thể bán và đặt lại, max là 1 trụ".

### Phương án được chọn / sửa / loại

- `HasHeroTower` private thành `HasTowerOfFamily`/`CountTowersOfFamily` public; đếm trụ **đang đứng** nên bán là trả lại
  suất.
- `IsFamilyBuildLimitReached` chặn ngay ở `BeginTowerPlacementDrag`, đặt **trước** kiểm tra vàng.
- Thẻ hero bị làm mờ nhưng vẫn bấm được, vì cú tap chính là thứ giải thích lý do.

### Lý do

Hết suất thì bao nhiêu vàng cũng không mua được, báo "không đủ vàng" sẽ khiến người chơi đi cày vàng vô ích.

### Kết quả triển khai / kiểm tra

Viết test làm lộ một bug thật: `TrySellSelected` **chưa bao giờ** gỡ node khỏi network — node chỉ rời đi khi `OnDestroy`
của view chạy, tức cuối frame ở play mode. Trụ vừa bán vẫn là node sống hết frame đó và hero vô hình vẫn nằm trong
`CreateHeroAttackTowerSnapshot`. Đã thêm `TowerRuntimeViewRegistry.Unregister` và gọi unregister ngay tại chỗ bán.

## Entry 6 — Tạm ẩn warning icon trên trụ

### Prompt đã dùng

"ẩn warning icon trên các trụ đi, tạm thời disable nó, dù valid chain hay invalid".

### Phương án được chọn / sửa / loại

Thêm cờ `InvalidChainWarningEnabled = false` trong `TowerChainStatePresentationSystem`: mọi trụ được báo là valid. Giữ
nguyên toàn bộ phần còn lại của feature, bật lại chỉ cần đổi cờ.

### Kết quả triển khai / kiểm tra

`SetChainValid` là call site duy nhất và node warning vốn author ở trạng thái inactive, nên không còn đường nào làm nó
hiện.

## Entry 7 — Thẻ hero hiện "Tối đa" thay cho giá

### Prompt đã dùng

"khi người chơi đặt cua ra, thì card cua xám màu... mất giá coin và icon coin, thay vào đó là để chữ Max ở chỗ coin đã
mất"; correction: "tiếng việt để chũ tối đa đi".

### Phương án được chọn / sửa / loại

- Thêm `SetAtBuildLimit` và gom việc vẽ hàng giá vào một chỗ duy nhất (`ApplyCostRow`).
- Thêm `ApplyTowerBuildLimits` vào `ITowerNetworkHudView`, presenter lấy từ chính `IsFamilyBuildLimitReached`.
- Tắt hẳn GameObject của coin chứ không chỉ ẩn Image.

### Lý do

Coin có `LayoutElement` trong một `HorizontalLayoutGroup` căn giữa: coin vô hình mà còn đó sẽ giữ chỗ và đẩy chữ lệch.

### Kết quả triển khai / kiểm tra

Đo trước khi chốt: "Tối đa" rộng ~52px ở fontSize 20 trong thẻ rộng 116px nên không cần autosize. EditMode suite 352/352.

## Entry 8 — Tower Action ở Level 10 sai so với prefab

### Vấn đề đang gặp

UI tower action ở Level 10 vỡ, khác prefab và khác các level còn lại.

### Prompt đã dùng

"ở level 10 UI tower action đang lỗi, ko đúng với prefab và các level khác, fix".

### Phản hồi quan trọng của AI

Prefab instance `Gameplay UI` trong Level 10 override ba nút `Sell`/`Unlink`/`Upgrade` về `sizeDelta = (0,0)`, trong khi
prefab và các level khác là 112×110. Icon con vẫn giữ kích thước riêng nên tràn ra ngoài nút. Quét cả 10 level: chỉ
Level 10 dính; kiểm git thì override này đã có trong HEAD nên có từ trước.

### Phương án được chọn / sửa / loại

`PrefabUtility.RevertObjectOverride` đúng ba RectTransform rồi lưu scene, mở additive để không đụng phần còn lại.

### Kết quả triển khai / kiểm tra

Diff xoá đúng 18 entry override, không thêm dòng nào. So lại toàn bộ 13 node con của `Tower Actions` với prefab: khớp
100%.

## Entry 9 — Ghi log và chia commit

### Prompt đã dùng

"Thêm mọi chats ở chatssesion này vào AI collab log theo ngày. Và commit theo feature, đọc luật commit ở root readme, ko
co author, ngắn gọn."

### Phương án được chọn / sửa / loại

Tách transcript theo ngày địa phương (UTC+7) thành log 12/09 và 13/09, rồi commit theo từng feature với subject tiếng
Việt một dòng theo convention ở `README.md`, không có trailer attribution.
