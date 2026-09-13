# AI Collaboration Log — Dựng lại HUD gameplay theo art mới, nâng cấp trụ và dọn lịch sử commit — 05/09/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Agent:** Claude Code (`claude-opus-5`)
- **Session ID:** `6d136104-c04a-47a1-ac72-24c24033521d`
- **Local date:** 05/09/2026 (`Asia/Saigon`, UTC+7)
- **Coverage:** 24 yêu cầu, từ 00:29 tới khuya; 5 commit feature lúc 19:51–19:52. Nối tiếp
  `AI_Collaboration_Log_GameplayUI_04_09.md` (phần đầu cùng phiên, thuộc ngày 04/09).
- **Prefab chạm vào:** `Assets/Resources/Prefabs/GameplayUI.prefab`,
  `Assets/Resources/Prefabs/ApplicationUI.prefab`, `Assets/Resources/Prefabs/NextWaveSlot.prefab`
- **Scene chạm vào:** `Assets/Scenes/Levels/Level_001.unity`
- **Art owner đưa vào:** `nextwave0/1.png`, `waveinforicon.png`, `FrogStat.png`, `playpause.png`,
  `buttons.png`, `UpgradeSellUnlink.png`

## Entry 1 — Đổi chỗ cụm next wave và cụm HP/coin

### Prompt đã dùng

"tôi cần UI nextwave nằm ở góc trái trên màn hình, trong khi đó move hp và coin xuống góc trái dưới."

### Phản hồi quan trọng của AI

Yêu cầu thực chất là **đổi chỗ hai cụm**, vì hiện trạng đã ngược đúng như vậy. Và một xung đột phải
chặn trước: `Chain Status` (42,80) và `Queue Status` (42,50) **nằm bên trong** rect của `Preview Panel`
(x 24..364, y 24..224) — chúng là chữ vẽ trên tấm panel đó, không phải phần tử độc lập. Chuyển mà bỏ
lại thì hai dòng này lơ lửng và đè lên HP/coin.

### Phương án được chọn / sửa / loại

Chuyển cả cụm. Sửa **cả authoring script lẫn prefab** để có hiệu lực ngay mà không phải rebuild toàn
HUD, và sửa doc comment của class — nó vẫn ghi "status cluster top-left, wave preview bottom-left",
tức sẽ nói dối người đọc sau.

### Kết quả triển khai / kiểm tra

Kiểm Level_001 không có override rect của các object này (nếu có thì sửa prefab sẽ không hiện). Tính
vùng chiếm thực tế từng cụm: không chỗ nào đè nhau.

## Entry 2 — Preview enemy bằng icon, nút next wave xổ ra/xổ vào

### Prompt đã dùng

"đã có nextwave ui trong chỗ art ui… tôi muốn preview enemy sẽ dùng icon và nút nextwave để xổ ra xổ
vào hide/show các preview enemies như hình" → "xổ xuống chưa có icon, bạn gán cho tôi luôn, ko cần
background đen, ko cần detail enemy."

### Phản hồi quan trọng của AI

Hai nửa: dựng UI, và có đường dữ liệu từ wave sắp tới ra icon. Chặn ngay ở nửa dữ liệu: sprite sheet
đặt `spriteMode: 2` (Multiple) nhưng **chưa slice** — 0 sub-sprite, nên chưa thể tham chiếu từng icon.
Phải slice trước, bằng `TextureImporter.spritesheet`, rồi mới gán được.

### Kết quả triển khai / kiểm tra

Chạy thật qua Bootstrap — thứ chưa làm được ở mấy lượt trước:

```
collapsed:     grid=False  chevronY= 1.0  slots=1 [EnemyIcon1]
after tap 1:   grid=True   chevronY=-1.0
after tap 2:   grid=False  chevronY= 1.0
```

Ô đầu hiện `EnemyIcon1` (con chuột) — khớp chính xác vì wave 1 của level 1 là **Basic ×4**. Nền đen đã
bỏ, dòng chi tiết enemy đã trống.

## Entry 3 — Icon to hơn, bỏ hai dòng chain/queue

### Prompt đã dùng

"icon to hơn đi và bỏ valid chain, hay queue gì luôn."

### Phương án được chọn / sửa / loại

Cell `66×66` → **`84×92`**, và chọn ô **không vuông** theo đúng tỉ lệ art (201×218). Trước đây
`preserveAspect` phải letterbox icon trong ô vuông nên icon luôn nhỏ hơn ô — đổi tỉ lệ ô thì icon lấp
gần kín mà không phải phóng ô to thêm.

### Kết quả triển khai / kiểm tra

Lần đo đầu slot ra `0×0` — không vội kết luận, vì `GridLayoutGroup` chưa layout trong frame vừa bật
grid. Đọc lại ở frame sau: `cell=(84, 92)`, đúng.

## Entry 4 — Mỗi slot preview là một prefab, và bỏ dòng chữ "Next wave preview"

### Prompt đã dùng

"làm mỗi slot là 1 prefab để tôi chỉnh 1 được tất cả" → (ngắt) → "bỏ chữ này cho tôi, task cũ ko cần
nữa."

### Phương án được chọn / sửa / loại

`NextWaveSlot.prefab` cắt từ `Slot 0` nhưng đặt ở **trạng thái trung tính**: `m_Sprite: 0`,
`m_Enabled: 0`, `preserveAspect: 1`, `raycastTarget: 0` — vì `Render()` mới là chỗ điền sprite và bật
ô. Lại áp bài học: ghi asset một lượt, instance hoá lượt sau.

### Kết quả triển khai / kiểm tra

8/8 instance, 0 object "Slot N" thường còn sót, `previewSlots[0..7]` đúng thứ tự. Nói thẳng phần chưa
làm: `Level_001.unity` đang dirty chặn việc nạp Bootstrap nên **chưa chạy Play** cho hai việc này —
sau chuyện save làm prune override lần trước, để owner quyết có lưu hay không thay vì tự lưu.

## Entry 5 — Mặc định xổ xuống, vào wave tự thu lên

### Prompt đã dùng

"sửa next wave để mặc định luôn xổ xuống, khi vào wave, nó tự động xổ lên lại (hide, và ko cho user
click)."

### Phương án được chọn / sửa / loại

- **Loại:** dùng lại `CanStartWave`. Nó do `WaveSystem` truyền vào và có thể false vì lý do khác.
- **Chọn:** thêm `WaveHudState.PreviewAvailable = (Phase == Preparation)` — **cùng một vị từ** mà
  `CreatePreviewIcons` đang dùng, gom vào một biến chung để hai chỗ không thể lệch (grid mở trên roster
  rỗng, hoặc đóng trên roster đầy).
- **Chọn:** chỉ cưỡng chế mở/đóng **khi availability đổi**, vì `Render` chạy mỗi wave event — nếu ép
  mỗi lần thì người chơi không tự đóng được.

### Kết quả triển khai / kiểm tra

Lần kiểm đầu cho thấy vào wave **không** làm grid thu lại. Nhưng đó là **test không hợp lệ, không phải
bug**: `Wave Status = "LINK A VALID CHAIN"`, `Start Wave interactable = False` — wave bị từ chối vì
chưa có chain hợp lệ nên phase vẫn Preparation, grid mở là đúng. Thử nút cheat cũng chưa chạm được
nhánh cần kiểm, nên thôi lái qua UI mà gọi thẳng `Render`:

```
rendered previewAvailable=false: grid=False clickable=False chevronY= 1.0
rendered previewAvailable=true:  grid=True  clickable=True  chevronY=-1.0
```

Và một chi tiết tự bắt: dòng `before` đã thành không-click-được dù lần đo trước còn `True` — kiểm ra
`Outcome HUD activeInHierarchy=True`, tức nút cheat **đã** ăn và run đã sang Victory. Vậy một `Refresh()`
thật của hệ thống đã tự thu grid, không phải giá trị do AI bơm vào.

## Entry 6 — Dựng frog stat theo ảnh, và chia đáy màn hình làm ba

### Prompt đã dùng

"hãy làm frog stat giống trong ảnh, asset đã có trong UI" + "bố cục layout sao cho phần bottom chia
thành 3: trái dưới frog stat, giữa dưới towers, phải dưới nút start."

### Phản hồi quan trọng của AI

`FrogStat.png` đã slice sẵn 5 sprite, nhưng tên là `Merged image (3)_N` — vô nghĩa. Map bằng **vị trí
và tỉ lệ rect**, không đoán theo tên: `_4` vòng tròn viền vàng, `_2` con frog, `_3` khung thanh (ratio
6.44), `_0` dải xanh, `_5` panel chữ nhật **không dùng** vì ảnh mẫu không có nền. Coin là
`StartCoin_1`; `StartCoin_0` là ngôi sao.

### Phương án được chọn / sửa / loại

- Giữ nguyên object `Level Status HUD` khi dựng lại bên trong, để component và DI không đổi.
- Số HP thành **tuỳ chọn** (null-guard) — ảnh mẫu không có số nào trên thanh.
- Sửa `BuildStatusCluster` trong authoring script: nó vẫn dựng Health Panel / Gold Panel cũ, để yên
  thì lần rebuild kế tiếp xoá sạch frog stat.

### Kết quả triển khai / kiểm tra

Bố cục 3 phần: **0 overlap ở cả 4 tỉ lệ**, kể cả 4:3 nơi trước đây đè 65px. `HealthFillColor` và
`GoldColor` thành khai báo mồ côi (CS0414) sau khi cắt — đã gỡ. Chạy thật vào level 1 để xác nhận HP
fill và số coin render đúng qua cụm mới.

## Entry 7 — Phóng cụm cóc to hơn 50%

### Prompt đã dùng

"tổng thể bự hơn 50% cho cóc stats ở dưới."

### Phương án được chọn / sửa / loại

Nhân 1.5 cả **kích thước lẫn vị trí nội bộ** (kể cả font số vàng 24 → 36) để tỉ lệ trong cụm không
đổi. **Giữ nguyên** lề góc `(24,24)`: đó là khoảng cách tới viền màn hình, không thuộc tỉ lệ nội bộ
của cụm — nhân nó lên là làm sai một thứ khác.

### Kết quả triển khai / kiểm tra

Cụm 236×84 → 354×126. Đo lại chỗ đáng lo: ở 4:3 khoảng hở rect giữa cụm và Build Bar chỉ còn **3px**
— báo cho owner kèm nguyên văn "biên độ đã hết, phóng thêm nữa là đụng thật", và sửa comment trong
authoring script vì nó đang ghi con số cũ đã sai. Nói rõ lượt này **chưa chạy Play**, chỉ đọc lại
prefab, vì thay đổi thuần là số rect.

> Con số 3px này về sau được đo lại và **sai**: mốc thật không phải `Build Bar` (Image của nó
> `enabled = false`, không vẽ gì) mà là `Tower Buttons`, còn hở 16px. Xem Entry 8.

## Entry 8 — Vòng tròn cóc phải cao bằng box nguyên tố

### Vấn đề đang gặp

Owner muốn nền tròn của chân dung cóc cao bằng các thẻ trụ ở thanh dưới.

### Prompt đã dùng

"bự hơn nữa cho background tròn của cóc có heigh bằng các height của box nguyên tố."

### Phản hồi quan trọng của AI

Giả định ban đầu của AI sai và bị chính phép đo phủ định. `GameplayHudPrototypeLayout` đặt
`childControlHeight = true`, ngụ ý box cao `108 − 16 = 92`. Nhưng prefab đã lưu có
`childControlHeight = false` và `childForceExpandHeight = false`, nên `HorizontalLayoutGroup` **không**
điều khiển chiều cao: mỗi box giữ `130.70` của riêng nó. Đó là lý do vòng tròn 120 trông nhỏ hơn box
dù nghe như phải lớn hơn.

### Phương án được chọn / sửa / loại

- Portrait `120 → 131`, Frog `93 → 102`, cụm `Level Status HUD` `354×126 → 365×136`.
- Đẩy Health Bar / Coin / Gold sang phải 11px — đúng phần vòng tròn nở ra. Không đẩy thì khoảng hở
  vòng-tròn↔thanh-máu tụt từ 13px xuống 2px.
- Canh cùng dải dọc với box (26.5–157.5 so với 26.65–157.35) dù owner chỉ yêu cầu bằng chiều cao:
  cùng cao mà lệch chân thì vô nghĩa.

### Kết quả triển khai / kiểm tra

Sprite vòng tròn hơi cao hơn rộng (61.73×62.88) và bật `preserveAspect`, nên chiều cao là cạnh quyết
định: vẽ ra đúng 131, khớp box 130.70 trong 0.3px. Cảnh báo kèm: lượt trước AI báo "hở 3px ở 4:3"
giữa cụm cóc và Build Bar — đo lại thì **sai**, vì `Build Bar` có `Image.enabled = false` (không vẽ
gì); mốc thật là `Tower Buttons`, còn hở 16px.

## Entry 9 — Thanh máu cóc hở viền

### Vấn đề đang gặp

Phần xanh không lấp kín lòng máng, lộ nền nâu quanh mép.

### Prompt đã dùng

"sửa thanh máu cóc sao cho phần xanh nằm trong background của nó mà ko hở như hình 1, hình 2 đang hở."

### Phản hồi quan trọng của AI

Không phải sai vài pixel mà là sai hình học. Đo pixel từ atlas: lòng máng `_3` là **viên nhộng bo
tròn** (x 13..585, y 12..80), còn sprite xanh `_0` là **hình chữ nhật đặc** 648×158. Một hình chữ nhật
không bao giờ vừa kín một lòng bo tròn: nới cho hết hở giữa thì bốn góc đè lên viền vàng, thu lại cho
góc gọn thì hở giữa. Cả hai lỗi đang xảy ra cùng lúc — hở 9px trên/dưới, góc vượt ra 5px ở hai đầu.

### Phương án được chọn / sửa / loại

- **Chọn:** cho phần xanh mượn hình dáng của khung — thêm `Health Cavity` làm `Mask` dùng chính sprite
  `_3` bóp vào đúng cỡ lòng máng (209.5×24.5), `Health Fill` thành con của nó và kéo đầy.
- **Loại:** chỉnh số rect cho vừa. Đã tính: mọi cấu hình chữ nhật đều để lại hở hoặc tràn.

### Kết quả triển khai / kiểm tra

`healthFill` vẫn trỏ đúng component sau khi đổi cha (kiểm bằng `SerializedObject`), và script authoring
không hề wire trường này nên rebuild không phá cấu trúc mới. Dựng composite so sánh trước/sau ở 4x.

## Entry 10 — Preview wave thiếu icon, rồi mất sạch icon

### Vấn đề đang gặp

Owner hỏi vì sao MagicResistant không có icon; sau đó tự cắt lại atlas.

### Prompt đã dùng

"ok nextwave preview ko show icon của enemy magic regisstance?" → "tôi vừa sửa lại thêm 7 enemy rồi đó"
→ "là enemy icon 5 đó nha" → "SummonerBoss là nextwave1_15, mini boss là nextwave1_2".

### Phản hồi quan trọng của AI

Lần đầu: atlas cũ chỉ có 6 icon cho 7 enemy nên MagicResistant bỏ trống — đúng như đã nêu trước đó.
Lần hai, sau khi owner cắt lại: **cả 6 gán cũ đã mất**, không riêng MagicResistant. Slice mới tên
`nextwave1_0/_1/_2/_3/_13/_15/_16`, không còn `EnemyIcon0..5`, nên mọi `fileID` cũ đứt và cả 7 enemy
đều `<NONE>`.

### Phương án được chọn / sửa / loại

Dựng contact sheet 7 icon theo thứ tự đọc để nhìn rồi mới gán, thay vì đoán từ tên slice tự sinh.
"Icon 5" của owner khớp hình có vòng hào quang — hợp với kháng phép. Hai gán cuối owner tự sửa
(SummonerBoss ↔ MiniBoss).

### Lý do

Tên slice là số ô lưới tự sinh, không mang thông tin gì; định nghĩa enemy cũng không có mô tả. Nhìn
hình là căn cứ duy nhất.

### Kết quả triển khai / kiểm tra

7 enemy dùng 7 icon khác nhau, không trùng không sót — kiểm sau khi lưu.

## Entry 11 — Gộp thông tin wave vào plaque NEXT WAVE

### Prompt đã dùng

"tôi muốn UI này kế bên UI next wave button, dùng lại background của nút next wave, như trong ảnh."

### Phản hồi quan trọng của AI

Plaque đang để `Simple`; nới rộng sẽ **kéo méo hoa văn góc**. Sprite đã có sẵn border 9-slice
(114/126/117/108) nên chỉ cần chuyển `Sliced` với `pixelsPerUnitMultiplier = 6.01` — con số khớp đúng
tỉ lệ nén dọc (553/92), giữ góc đúng tỉ lệ thật.

### Phương án được chọn / sửa / loại

- **Chọn:** `Next Wave Toggle` 300×92 → 550×92; `Wave Counter`/`Enemies Left` **reparent** chứ không
  tạo mới, nên wire của view không đứt.
- **Chọn:** xoá `Wave Panel`, kéo theo: `statusText` và `waveProgressFill` thành tuỳ chọn (null-guard),
  `WireViews` trỏ đường dẫn mới, bỏ `BuildWavePanel`, dọn `SunkenColor`/`ConfigureFillImage` thành mã
  chết, cập nhật `WaveHudViewTests`.

### Kết quả triển khai / kiểm tra

Lần in số đầu tiên tự lộ lỗi: **Chevron 489..529 đè lên `Enemies Left` 496..536** vì còn giữ offset −71
tính cho plaque 300 cũ. Thu plaque về 550 và neo chevron theo mép phải. Render thật bằng Unity (preview
scene + RenderTexture) — lần này không phải composite tự dựng.

## Entry 12 — Level 1 vẫn hiện plaque cũ vì scene đè prefab

### Vấn đề đang gặp

Vào Level 1 thì plaque hẹp, chữ và icon tràn ra ngoài khung, chevron nhảy vào giữa "NEXT WAVE".

### Prompt đã dùng

"lỗi rồi đây là những gì tôi thấy khi vào level 1, ko đúng như ảnh 2."

### Phản hồi quan trọng của AI

Không phải prefab sai. Instance `Gameplay UI` trong `Level_001.unity` có 8 property override:
`m_SizeDelta = 257.5×70.4`, `m_PixelsPerUnitMultiplier = 8.49`, `Chevron.m_AnchoredPosition.x = -24`.
Override trong scene **luôn thắng prefab**, nên plaque bị ép hẹp trong khi các phần tử con vẫn ở toạ
độ prefab.

### Phương án được chọn / sửa / loại

Gỡ đúng 8 override đó rồi lưu scene. Quét cả 10 level: chỉ Level 1 dính; Level 9 chỉ có
`m_StaticEditorFlags` vô hại.

### Lý do

Đây là dấu vết chỉnh tay trong Scene view. Mỗi lần kéo rect trong scene, Unity ghi override và từ đó
scene đó **âm thầm phớt lờ** mọi thay đổi prefab của cùng property — chỉ một level lệch, rất khó thấy.
Cùng cơ chế đã gây sự cố `PlacementHudView` ở Level 1 trước đó.

### Kết quả triển khai / kiểm tra

Đọc lại từ file đã lưu: 10/10 level đều `550×92, Sliced, mult 6.01, chevron −28`.

## Entry 13 — Số enemy: từ "đang sống" sang "còn lại trong wave"

### Vấn đề đang gặp

Ô số cạnh icon nhóm người luôn hiện `00` lúc chuẩn bị.

### Prompt đã dùng

"sao lại 00, tôi muốn nó hiện số enemy sẽ xuất hiện trong wave tiếp theo" → "ko đợi spawn ra mới hiển
thị, phải hiển thị trước luôn, và khi bấm start wave, số đó ko được reset về 0 rồi tăng lên khi spawn,
chỉ giảm khi enemy chết thôi, trừ TH enemy là sommoner boss thì mới tăng."

### Phương án được chọn / sửa / loại

Đưa phép tính vào `WaveSystem` — nơi duy nhất biết kế hoạch spawn — dưới dạng `RemainingEnemyCount`:

```
đang chạy:  (chưa spawn) + (đang sống) = currentPlan.Count − nextSpawnIndex + LivingCount
chuẩn bị:   tổng roster của wave sắp tới
```

### Lý do

Cả bốn yêu cầu rơi thẳng ra từ công thức, không cần case riêng nào — kể cả ngoại lệ summoner: quân
triệu hồi vào qua `SpawnPlannedSummon` của combat timeline, **không** nằm trong wave plan, nên nó cộng
vào "sống" mà chưa từng bị trừ khỏi hàng chờ.

### Kết quả triển khai / kiểm tra

Rủi ro duy nhất là số nhảy đúng lúc bấm START nếu `plan.Count ≠ tổng roster`. Kiểm **93 wave của cả 10
level**: 0 sai lệch. Nhịp cập nhật đã có sẵn (`EnemySpawned` / `EnemyKilled` / `EnemyLeaked`).

## Entry 14 — Nút pause/play, ẩn cheat, và modal tạm dừng

### Prompt đã dùng

"làm nút pause/play giống vầy, và tàng hình nut skip wave, vẫn có thể bấm nhưng UI ko thấy, bỏ nút
menu, bấm pause thì hiện modal menu…" (qua 4 lượt tinh chỉnh art).

### Phản hồi quan trọng của AI

Render thật lộ ra hai lỗi mà số liệu không cho thấy:

- Nút pause ra **ô xám** dù sprite và `Image.color` đều đúng — thủ phạm là `Button.transition =
  ColorTint` với `normalColor = (0.08, 0.11, 0.10)`, tàn dư của `StyleButton`, nhân một lớp gần đen lên
  sprite.
- **Ô vuông trắng to** dưới plaque: `Slot 0` của Next Wave Grid có Image không sprite nhưng
  `enabled = true` (7 slot kia false) — Image rỗng thì uGUI vẽ hộp trắng.

### Phương án được chọn / sửa / loại

- **Chọn:** Skip Waves alpha 0 chứ **không** `Image.enabled = false` — Graphic tắt thì raycaster không
  có gì để bắn trúng.
- **Chọn:** `Pause HUD` đặt **trong Safe Area, ngay trước Pause Button** — modal phủ HUD, nút pause phủ
  modal. Tránh canvas lồng, thứ đã từng làm mất raycast của Game Start.
- **Chọn:** nền modal alpha 0 nhưng vẫn chặn raycast, khi owner yêu cầu bỏ nền tối — xoá hẳn thì tap
  xuyên xuống bàn chơi lúc game đang dừng.
- **Sửa theo owner:** hiểu nhầm một lượt — nền NEXT WAVE là cho **nút pause**, không phải modal.
- **Loại theo owner:** nút NEXT LEVEL trong modal (hỏi trước khi bỏ, vì lượt trước owner đã yêu cầu nó).

### Kết quả triển khai / kiểm tra

Modal cuối dùng `NextWavePlaque` 9-slice (mult 1.539), ba nút từ `buttons.png`, icon từ `playpause.png`,
hai dải sao hai bên chữ PAUSED. Lỗi tự gây và tự bắt: lần render đầu ra **ba ảnh trống** vì `Render` dời
panel theo vị trí trụ sau khi camera đã canh.

## Entry 15 — Nút SELL bấm không ăn: bốn lượt chẩn đoán sai

### Vấn đề đang gặp

Bấm SELL không có gì xảy ra; UNLINK thì ăn. Ban đầu owner chỉ báo trụ Sink không bán được.

### Prompt đã dùng

"trụ sink vẫn ko bán được" → "vì nút sell khi bấm ko bán?" → "bấm sell các trụ khác không ăn luôn, chỉ
unlink được, có vẻ do UI?" → "vẫn ko sell được? bấm rồi và ko có gì xảy ra".

### Phản hồi quan trọng của AI

Ba giả thuyết liên tiếp **đều sai**, và mỗi lần đều bị chính dữ liệu của owner phủ định:

1. "Cờ `sellable` sai" — đo bộ nhớ lúc game đang chạy: `sellable=True`, catalog giữ đúng object đó.
2. "`Tower Actions` bị `Start Wave`/`Next Wave Toggle` nuốt raycast" — nếu vậy **cả hai** nút phải
   chết, không thể UNLINK ăn còn SELL không.
3. "Logic từ chối" — dựng lại đúng đường chạy bằng API công khai và chạy: `TrySellSelected -> True`,
   "Sold Generator for 154 gold", vàng 1000 → 1154.

Nguyên nhân thật nằm ở tầng input: `GameplayInputSource.IsPointerOverUi` gọi
`EventSystem.IsPointerOverGameObject(pointerId)`, hàm này trả kết quả EventSystem giải được ở **frame
trước**. Một cú chạm vừa bắt đầu **không có frame trước**, nên nó trả `false` đúng vào frame nhấn →
`TowerInteractionSystem.BeginPointer` chạy → `TryPickTower` trong bán kính 96px không thấy trụ →
`ClearSelection()` → nhả tay, `onClick` mới bắn → `selectedTower == null` → từ chối im lặng.

UNLINK sống sót vì nó là nút **dưới**, gần trụ hơn, vẫn nằm trong bán kính 96px nên `BeginPointer` chọn
lại đúng trụ đó. Đúng một khoảng cách nút.

### Phương án được chọn / sửa / loại

- **Chọn:** thay bằng `EventSystem.RaycastAll` tại đúng toạ độ chạm — chính xác theo frame, đúng cả với
  touch mới sinh. Dùng `PointerEventData` và list tái sử dụng nên không sinh rác mỗi frame.
- **Chọn:** sửa ở `GameplayInputSource` chứ không vá trong `TowerInteractionSystem`, vì cùng cờ đó còn
  được `GridPlacementSystem` dùng.
- **Chọn:** presenter thôi nuốt lỗi bằng `out _`, log lý do từ chối ra console — chính cái `out _` đã
  khiến nút trông như hỏng thay vì như bị từ chối, và làm AI mò mấy lượt.

### Kết quả triển khai / kiểm tra

Bắn `EventSystem.RaycastAll` vào tâm nút trong phiên chơi sống: cả hai nút đều `topmost IS the button`
— loại dứt giả thuyết bị che. Gọi `sellButton.onClick.Invoke()` thì **bán thật** (2 trụ → 1; lần đếm
đầu vẫn thấy 2 vì `Destroy` hoãn tới cuối frame). Console in đúng
`Sell refused: Select a tower before selling.`

## Entry 16 — Chỉ chọn trụ trong phase chuẩn bị

### Prompt đã dùng

"sửa sao cho khi vào gameplay, start wave rồi thì ko click được trụ để show sell hay unlink."

### Phương án được chọn / sửa / loại

Chặn ở `TowerNetworkSystem.Select` — đặt ở luật miền, không ở view, nên chặn mọi đường gọi. Giữa wave,
tap vào trụ sẽ **xoá** selection thay vì tạo mới.

### Lý do

Trước đó panel vẫn hiện giữa wave, chỉ là hai nút bị xám. Một panel nổi với hai nút chết thì vô nghĩa.

### Kết quả triển khai / kiểm tra

Thêm `SoulNexus_CanBeSelectedAndSold` và `SelectingATower_IsRefusedWhileTheWaveRuns`. Cũng chuyển
`Tower Actions` xuống vẽ trên cùng: popover không nên bị UI tĩnh vẽ đè.

## Entry 17 — Nâng cấp trụ, chỉ số đọc từ SO

### Prompt đã dùng

"dùng icon coin và code, để sửa UI khi click vào tower thành, có nâng câp, sell, và unlink, nâng cấp
tạm tăng stats thôi (sửa từ SO datadriven)" → "2 level thôi, nâng cấp được lần 1, và lần 2 là hết" →
"khogo nân cấp được thì xóa cái icon tiền luôn đi".

### Phản hồi quan trọng của AI

Có sẵn `ElementUpgradeCostProfile` và `LinearUpgradeCost` trong SO nhưng **chưa có cơ chế nâng cấp nào
được cài**, và trụ chưa có trạng thái cấp. Đây là tính năng thật, không phải chỉnh UI.

### Phương án được chọn / sửa / loại

- **Chọn:** `TowerUpgradeProfile` mới (`maxLevel`, `costPerLevel`, `damageBonusPerLevel`), giá tuyến
  tính theo cấp.
- **Chọn:** cấp **nhân vào payload** chứ không đụng chu kỳ — nhờ vậy mô phỏng không cần biết "cấp" là
  gì, nó vẫn đọc đúng payload như cũ. `TryUpgradeTower` dựng lại spec của node ở cấp mới.
- **Chọn:** sát thương thiêu nhân theo nhưng **giữ nguyên thời lượng và nhịp** — nâng cấp nên tăng sát
  thương, không âm thầm viết lại các mốc thời gian mà luật phản ứng nguyên tố đang cân bằng theo.
- **Chọn:** vàng chỉ bị trừ **sau** khi mạng chấp nhận, để một lần từ chối không mất tiền mà không được
  cấp.
- **Chọn:** `UpgradeShowsPrice` trong state thay vì để view đoán từ chuỗi — `MAX` là một *trạng thái*,
  không phải *giá*, đặt đồng xu cạnh nó cũng sai như đặt cạnh ô trống.
- **Chọn:** SoulNexus `maxLevel = 0` — nó không gây damage nên một cấp chẳng mua được gì.

### Kết quả triển khai / kiểm tra

Sau khi owner chốt 2 cấp: Generator 110/220, nguyên tố 100/200, Hero 150/300, damage ×1.35 → ×1.70.
Đọc ngược qua runtime API để xác nhận. Lỗi tự bắt khi render: hộp icon vuông 38×38 với `preserveAspect`
khiến icon RESUME/play (tỉ lệ 0.88) cao đủ 38 còn restart/home (1.10) chỉ cao 34 — đổi hộp thành 60×42
để chiều cao luôn là cạnh quyết định. Khoảng hở xu↔số ban đầu **âm 1px** (chồng nhau), không phải "hơi
gần" như owner mô tả.

## Entry 18 — Vào level không tự cầm trụ, ghost chỉ còn footprint

### Prompt đã dùng

"sửa việc mới vào level ko click trụ nào thì đặt trụ mặc định, xóa vụ đó" + "ghost grid của trụ ko show
chiều cao, chỉ show foot prints."

### Phản hồi quan trọng của AI

Thủ phạm là `GridPlacementPresenter.Bind`: có một field `initialTower` được gán trong **cả 10 scene**,
nên vừa vào là bàn cờ đã ở chế độ đặt trụ và cú chạm đầu tiên xây luôn thứ chưa hề được chọn.

### Phương án được chọn / sửa / loại

Bỏ cả field lẫn đoạn gọi. `GridPlacementView` vẽ hai mesh — lưới footprint phẳng và khối cao bằng trụ —
tắt hẳn `ghostRenderer`, giữ lại mesh vì đây là quyết định trình bày chứ không phải tính năng bị bỏ.

### Kết quả triển khai / kiểm tra

Hai PlayMode test assert `SelectedTower != null` ngay sau `Bind` dựa đúng vào hành vi vừa xoá — đổi
thành assert `Is.Null` (chốt luật mới) rồi tự chọn một trụ như nút build vẫn làm. Xác nhận
`initialTower` đã biến mất khỏi cả type lẫn instance trong scene.

## Entry 19 — Bẫy Unity không biên dịch khi chạy nền

### Vấn đề đang gặp

Ba lượt liên tiếp AI báo "console 0 error" trong khi code chưa hề được biên dịch.

### Phản hồi quan trọng của AI

`TowerDefense3D.System.Runtime.dll` đứng ở 18:21 trong khi source đã sửa lúc 18:43.
`CompilationPipeline.RequestScriptCompilation()` không ăn khi editor chạy nền. Hệ quả: **mọi lần "console
0 error" trong giai đoạn đó đều vô nghĩa** — không có gì để mà báo lỗi.

### Phương án được chọn / sửa / loại

Từ đó luôn đối chiếu mốc thời gian DLL với mốc source **trước** khi kết luận, và nhờ owner đưa focus
vào Unity khi cần biên dịch.

### Kết quả triển khai / kiểm tra

Lỗi `NodeState` ở `TowerNetworkManager.ProjectilePlanning.cs:63` chỉ lộ ra sau khi biên dịch thật —
`Unity_ValidateScript` không bắt được vì nó chỉ soi từng file một. Rà thêm và tự tìm ra một lỗi cùng
loại chưa nổ: `TowerNetworkHudViewStub` implement `ITowerNetworkHudView` vừa được thêm
`UpgradeRequested`.

## Entry 20 — Chia commit theo feature

### Prompt đã dùng

"commit theo feature, ngắn gọn ko coauthor, đọc rule commit ở readme root."

### Phương án được chọn / sửa / loại

5 commit theo convention `README.md` (prefix, tiếng Việt, chỉ hoa chữ đầu, một dòng, không body, không
trailer): icon enemy / dựng lại HUD / nâng cấp trụ / fix input / fix placement.

### Kết quả triển khai / kiểm tra

Cố ý **để lại chưa commit**: lightmap và baking data (Unity bake lại khi AI mở Level 1), 3 atlas font
TMP, và vài file có sẵn từ trước — không gói lén rác tái bake vào commit feature. Nói rõ một hạn chế:
`GameplayUI.prefab` là một file duy nhất chứa cả HUD nên nằm trọn trong một commit, khiến **từng commit
không tự đứng vững hoàn toàn**.

## Entry 21 — Dọn `Co-Authored-By` trong lịch sử

### Prompt đã dùng

"kiểm tra tất cả commit local của tôi xem có commit nào co author ko" → "sửa commit đó đi, ko được thêm
co author by."

### Phản hồi quan trọng của AI

Quét 217 commit: đúng **1** commit dính (`88e8e274`, 31/08). Nó **đã được push** — có trên
`github/main`, `github/level-design`, `github/stat-balance` — nên viết lại sẽ đổi hash của nó và 67
commit sau nó.

### Phương án được chọn / sửa / loại

- **Chọn:** viết lại **chỉ ở local** bằng `git filter-branch --msg-filter`, tạo nhánh
  `backup/before-coauthor-strip` trước.
- **Loại:** force-push. Đó là hành động ra bên ngoài, để owner quyết.
- **Loại:** xoá body bullet list dù nó vi phạm luật "một dòng subject" — body giải thích vì sao từng
  file bị un-ignore, xoá đi là mất thông tin thật.

### Kết quả triển khai / kiểm tra

`git diff backup/before-coauthor-strip main` **rỗng** — nội dung file không đổi một byte; 217 commit;
author/committer giữ nguyên tên lẫn ngày gốc. Có hai commit trùng tiêu đề nên đối chiếu cả hai để chắc
không xoá nhầm body của commit còn lại. Nêu rõ: commit cũ vẫn nằm trên hai nhánh remote kia.

## Entry 22 — Reset tiến trình mở khoá level

### Prompt đã dùng

"clear player pref hay save để tôi có thể reset toàn bộ level đã mở khóa."

### Phản hồi quan trọng của AI

**Không phải PlayerPrefs.** Tiến trình nằm ở file JSON dưới `persistentDataPath`; đã kiểm PlayerPrefs,
không có key nào liên quan, nên `DeleteAll()` sẽ chẳng làm gì.

### Phương án được chọn / sửa / loại

Xoá qua `LocalSaveRepository.DeleteOwnedAutosave()` chứ không `rm` tay, để dọn luôn file backup và các
file `.tmp` ghi dở — xoá tay chỉ `autosave.json` thì lần load sau sẽ khôi phục từ backup. Thêm menu
`Tools ▸ Tower Defense ▸ Reset Save Progress`, có liệt kê file sắp xoá trước khi hỏi xác nhận.

### Kết quả triển khai / kiểm tra

Save cũ mở khoá và clear cả 10 level; sau khi xoá, thư mục rỗng và `UnlockProgress` mới có 1 level mở
khoá, 0 level clear. Menu cảnh báo nếu đang Play Mode — session đang chạy vẫn giữ progress trong bộ nhớ
và sẽ ghi đè lại.

## Entry 23 — Số level bị khoá chồng lên ổ khoá

### Vấn đề đang gặp

Node level bị khoá không đọc được số.

### Prompt đã dùng

"không hiện số khi bị lock level."

### Phản hồi quan trọng của AI

Chẩn đoán khác với mô tả: số **không hề bị ẩn** — nó vẫn được vẽ, chồng ngay lên ổ khoá. `Label` là con
cuối của node nên vẽ trên cùng và luôn `active`. Render riêng một node khoá ở cỡ lớn thì thấy "02" nằm
chồng lên cái khoá, hai thứ cùng màu kem, cùng chính giữa. Ở cỡ nhỏ trong game nó nhoè thành một vệt.

### Phương án được chọn / sửa / loại

Ẩn `Label` khi node bị khoá, cùng chỗ đang bật/tắt các biến thể node. Text vẫn được gán như cũ nên khi
level mở khoá thì số hiện lại ngay, không cần bind lại.

### Kết quả triển khai / kiểm tra

Render 4 trạng thái: đã qua `01`, mở khoá `02`, đang chọn `03`, khoá → không số.

## Entry 24 — Ghi log

### Prompt đã dùng

"Thêm vào ai collab log ở documents tất cả chats trong chat session này theo ngày và format có sẵn."

### Phương án được chọn / sửa / loại

Chia ngày bằng **mốc giờ thật của từng tin nhắn**, không gộp cả phiên vào một ngày: transcript ghi
giờ UTC nên biên ngày local (UTC+7) rơi vào 17:00 UTC. Yêu cầu “UI nextwave lên góc trái trên” lúc
17:29 UTC đã là 00:29 ngày 05/09.

- Phần 04/09 còn thiếu (sau khi `AI_Collaboration_Log_ApplicationUI_04_09.md` chốt ở `f5a8180b`,
  16:25) → file mới `AI_Collaboration_Log_GameplayUI_04_09.md`. Không viết chèn vào log 04/09 cũ vì
  nó đã đóng bằng mục “Open items”; cùng một ngày có hai log là tiền lệ đã có.
- Phần từ 00:29 trở đi → file này, theo format tiếng Việt của các log 12/09–13/09.

## Ghi chú trung thực về mức kiểm chứng

- Entry 1–7 có chạy thật qua Bootstrap → Level 1. **Từ Entry 8 trở đi không chạy Play Mode lần nào.**
  Xác minh khi đó dựa vào: đọc lại prefab/scene đã lưu, dựng lại đường chạy bằng API công khai,
  và **render thật bằng Unity** (preview scene + camera + RenderTexture) — không phải composite
  tự dựng, trừ lần kiểm thanh máu ở Entry 9 có nói rõ.
- **Không chạy được test** qua MCP. Các test được thêm/sửa trong phiên (`TowerNetworkSystemTests`,
  `TowerNetworkHudViewTests`, `WaveHudViewTests`, `LevelSkipCheatTests`, `GameFlowPlayModeTests`,
  `GridPlacementSceneInputTests`, `GameplayUISystemTests`) mới chỉ được validate cú pháp và biên dịch.
- Ba giả thuyết sai liên tiếp ở Entry 15 và ba lượt "console 0 error" vô nghĩa ở Entry 19 là hai chỗ tốn
  nhiều lượt nhất của phiên.
