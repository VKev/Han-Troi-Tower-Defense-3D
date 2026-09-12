# AI Collaboration Log — HUD feedback, confetti, tối ưu UI và invalid chain warning — 12/09/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Agent:** Claude Code (`claude-opus-5`)
- **Session ID:** `0c44cb13-bb6e-42c3-ae27-0355645d3cfb`
- **Local date:** 12/09/2026 (`Asia/Saigon`, UTC+7)
- **Coverage:** 45 yêu cầu trong ngày (14:43 – 23:45)

## Entry 1 — Sửa shake/flash của Level Status khi cóc mất máu

### Vấn đề đang gặp

Cóc mất máu thì thanh máu tụt nhưng không có shake và không có flash đỏ; effect tiêu coin cũng không chạy. Owner nghi
VContainer chưa register.

### Prompt đã dùng

"không thấy animation dotween của level Status khi cóc mất máu? (shake, flash đỏ)... có thể chưa register vcontainer hay
gì đó"; sau đó correction quan trọng: "health change vẫn được gọi vì tôi thấy máu có giảm, có rút, nhưng ko thấy effect".

### Phản hồi quan trọng của AI

AI đoán sai vài lần (timeScale, logic `tookDamage`, nhiều instance HUD). Log `[HUD-DEBUG]` của owner cho thấy
`previousHealth=0 hasRendered=False` ở **mọi** lần render, tức state bị reset chứ không phải tween hỏng.

### Phương án được chọn / sửa / loại

- Xác định `WaveThreeDefeatHudPresenter.Refresh()` gọi `view.HideWaveThreeDefeat()` mỗi frame, và hàm này reset
  `hasRenderedHealth`/`previousHealth`.
- Chọn guard `isShowingWaveThreeDefeat` để `HideWaveThreeDefeat` chỉ làm việc khi thật sự đang hiện.
- Loại phương án đụng vào DOTween hoặc registration.

### Lý do

Nguyên nhân là state bị clobber, không phải animation; sửa đúng chỗ giữ nguyên contract của presenter.

### Kết quả triển khai / kiểm tra

Shake và flash chạy lại; thêm `LevelStatusHudDamageTests`. Cường độ shake sau đó giảm một nửa theo yêu cầu.

## Entry 2 — Tower HUD responsive và gating theo tutorial

### Vấn đề đang gặp

Tower HUD bị stretch trong lúc tutorial; sau tutorial unlink ở Level 1, tap vào trụ không hiện nút unlink. Owner cũng yêu
cầu Level 1 chỉ hiện unlink + sell (ẩn upgrade), và trong beat unlink thì hiện sell nhưng chỉ cho bấm unlink.

### Prompt đã dùng

Nhiều prompt kèm ảnh: "Tower HUD chưa được Responsive", "sau tutorial unlink... thì khi người chơi tap vào trụ thì show
icon unlink", "chỉ cho phép bấm unlink... cho tới khi tutorial unlink xong".

### Phản hồi quan trọng của AI

`AreTowerActionsAvailable()` trộn hai câu hỏi khác nhau (tool đã mở khoá vs. hành động khả dụng); layout group bị
`childForceExpandWidth` làm stretch trong beat tutorial.

### Phương án được chọn / sửa / loại

- Xoá `AreTowerActionsAvailable()`, dùng `AreTowerToolsUnlocked()`.
- Thêm `SetUpgradeAvailable` vào `ITowerNetworkHudView`; Level 1 không hiện upgrade.
- Trong beat unlink: sell luôn hiện nhưng `sellEnabled = state.SellEnabled && !tutorialUnlinkOnly`.
- `childForceExpandWidth = false` trong `SetTutorialControlsVisible` thay vì tắt cả layout group.

### Lý do

Giữ layout group (cần cho responsive) nhưng chặn đúng thuộc tính gây stretch.

### Kết quả triển khai / kiểm tra

Owner xác nhận HUD hết stretch khi vào lại Level 1 sau tutorial.

## Entry 3 — Confetti khi thắng

### Vấn đề đang gặp

`Assets/Art/UI/Confetti.png` đã slice sẵn mỗi sprite là một frame, cần bắn từ đáy màn hình lên khi modal chiến thắng bật.

### Prompt đã dùng

"tạo animation confetti và khi chiến thắng modal popup, play anim này để nó bắn từ dưới màn hình lên"; sau đó "làm cho
confetti nằm ở layer trên cùng, ko bị che bởi modal chiến thắng".

### Phản hồi quan trọng của AI

Flipbook 59 frame chạy bằng `DOTween.To` trên unscaled time để không chết khi modal dừng game.

### Phương án được chọn / sửa / loại

- Thêm `ConfettiBurstView` (`framesPerSecond = 30`, `emitterStagger`), `Play()`/`Stop()`.
- Gọi trong `PlayVictoryAnimation`, dừng ở `ResetVictoryAnimation`/`SkipVictoryAnimation`.
- Đưa node Confetti thành sibling cuối của `Outcome Root` thay vì đổi sorting order.

### Lý do

Thứ tự sibling là cách rẻ nhất để nằm trên trong cùng một Canvas.

### Kết quả triển khai / kiểm tra

Confetti bắn đúng lúc và không bị modal che.

## Entry 4 — One-shot wave 3 chỉ còn là một phần của tutorial

### Vấn đề đang gặp

Chơi lại Level 1 (và Level 2) sau khi xong tutorial vẫn bị one-shot ở wave 3 và phải chơi lại wave.

### Prompt đã dùng

"khi xong tutorial rồi thì chơi lại level 1 thì wave 3... mất máu như bình thường, tương tự như level 2, việc oneshot và
chơi lại wave là 1 phần của tutorial thôi".

### Phản hồi quan trọng của AI

`instantDefeatWaveNumber` đang là hằng số cho Level 1 và 2, không hỏi trạng thái đã học.

### Phương án được chọn / sửa / loại

`ResolveInstantDefeatWave` đọc `TutorialProgress.HasCompletedLevelOneTutorial` cho Level 1 và `SaveSystem` `IsCleared`
cho Level 2; trả 0 khi đã học xong.

### Lý do

Nguồn sự thật "đã dạy hay chưa" khác nhau giữa hai level nên phải hỏi đúng nguồn.

### Kết quả triển khai / kiểm tra

Level 1 và 2 chơi lại mất máu bình thường; các test lifecycle được cập nhật theo số tham số mới.

## Entry 5 — Bật lại hai nút cheat

### Vấn đề đang gặp

Hai nút cheat bị code tắt, owner cần dùng chúng.

### Prompt đã dùng

"bỏ đoạn code tắt 2 nút cheat, tôi cần nút cheat"; correction: "ko set alpha gì hết, cứ để nút cheat màu vậy".

### Phương án được chọn / sửa / loại

Xoá `cheatsEnabled` và `ApplyCheatVisibility()`; `Render` chỉ còn `interactable = canSkip`, `Show()` chỉ `SetActive(true)`.

### Kết quả triển khai / kiểm tra

Test `LevelSkipCheatTests` đổi tên theo hành vi mới và bỏ assert `activeSelf == False`.

## Entry 6 — Material property, GPU instancing và phương án A′

### Vấn đề đang gặp

Owner yêu cầu rà soát chỗ dùng material property làm hỏng batching, và liệt kê việc cần làm để giảm draw call.

### Prompt đã dùng

"check lại ở những nơi dùng material propeties, thì nên dùng sao cho GPU instancing hoạt động... list tôi những thứ nên
làm để giảm draw call/ tối ưu cho UI"; sau đó "Cách A đi", rồi "A' đi".

### Phản hồi quan trọng của AI

Cách A (bật instancing cho ToonShader) sẽ đá 91 renderer trên 74 prefab ra khỏi SRP Batcher; chính comment trong shader
ghi lại rằng việc này đã từng thử và bị revert. AI tự sửa lại đề xuất của mình thành A′.

### Phương án được chọn / sửa / loại

- Chọn A′: tách `ToonShaderInstanced.shader` riêng, chỉ 14 material cần `MaterialPropertyBlock` chuyển sang; 34 material
  còn lại giữ SRP Batcher.
- Loại A.

### Lý do

SRP Batcher và GPU instancing loại trừ nhau; tách shader cho phép mỗi nhóm dùng đúng đường nhanh của nó.

## Entry 7 — Đợt tối ưu UI

### Vấn đề đang gặp

UI chưa thống nhất TMP, raycastTarget bật tràn lan, atlas gộp chung, phần đổi mỗi frame nằm chung canvas lớn.

### Prompt đã dùng

"thống nhât TMP, và tắt raycastTartget, giữ thứ tự sibbling hoặc đổi cách gộp sprite atlas, Tách atlas theo màn hình.
Tách thứ đổi mỗi frame ra sub-canvas riêng"; correction: "cân nhắc nó có cần click hay không, nếu cần thì không tắt".

### Phản hồi quan trọng của AI

Bộ lọc raycastTarget đầu tiên sẽ tắt cả backdrop của modal (`Pause Root`, `Outcome Root`, `Loading`, `Blocking Error`),
làm tap xuyên qua modal — phát hiện ở bước dry run.

### Phương án được chọn / sửa / loại

- 56 `Text` → `TMP_Text`, 26 raycastTarget tắt (giữ mọi blocker), 3 sub-canvas cho phần đổi mỗi frame.
- Tách thật atlas thành Gameplay/Menu/Shared (35/7/12 texture).

### Lý do

Tắt raycast phải theo vai trò thực của element, không theo quét máy móc.

### Kết quả triển khai / kiểm tra

Hai lỗi phải sửa lại sau khi chạy: `SerializedProperty` âm thầm từ chối 35 assignment vì chạy trước khi Unity recompile
(gây crash boot `TutorialOverlayView requires fully authored UI references` và NRE `LoadingView.Show`), và
`enableAutoSizing` bật với `fontSizeMin/Max = 0` làm mất toàn bộ chữ tutorial. Cả hai được sửa bằng cách chạy lại rewire
sau compile **có đọc ngược để verify** (35/35) và khôi phục min/max từ `resizeTextMinSize/MaxSize`.
`SpriteAtlasExtensions.Remove/Add` không persist cho `.spriteatlasv2` nên packable GUID được sửa thẳng trong YAML — nếu
không, cả ba atlas vẫn pack toàn bộ thư mục `Production` và gấp ba RAM.

## Entry 8 — Invalid chain: từ dim màu sang warning icon

### Vấn đề đang gặp

Trụ nằm ngoài chain hợp lệ không có chỉ dấu rõ ràng.

### Prompt đã dùng

Ban đầu "trụ ở invalid chain sẽ tối màu"; chỉnh "tối 10%", "tối 20%"; rồi đổi hướng: "các tower ko tối nữa, mà hiện icon
warning tôi vừa thêm vào UI production"; sau đó "icon quá to, nhỏ lại", "giảm x10", "trừ tower crab hero ra".

### Phương án được chọn / sửa / loại

- Thêm `TowerChainStatePresentationSystem` nghe `TowerNetworkSystem.StateChanged`.
- Bỏ hoàn toàn phần dim; `TowerRuntimeView` hiện/ẩn node `Invalid Chain Warning` kèm pop animation.
- Hero (crab) không bao giờ bị đánh dấu.

### Lý do

Hero đánh một mình, không cần chain, nên đánh dấu nó là nói sai về một trụ đang hoạt động tốt.

### Kết quả triển khai / kiểm tra

Chuỗi lỗi phải sửa lần lượt: biển báo tự làm phồng `TryMeasureVisualBounds` (lệch cả anchor link/projectile) → loại trừ;
pop ghi đè scale đã author → cache rest scale; scale gốc của trụ từ 1× đến 183× làm biển khổng lồ → chia `lossyScale`;
factory scale trụ **sau** instantiate nên tính một lần là stale → tính lại trong `LateUpdate`; `drawMode = Sliced` cảnh
báo vì sprite không Full Rect → `Simple`; `sortingOrder = 100` vẽ đè overlay tutorial → `-1`.

## Entry 9 — Tutorial highlight sai trên Sink

### Vấn đề đang gặp

Ở tutorial Level 1, highlight bao sai vùng và icon văng ra ngoài màn hình — chỉ Sink bị, Generator thì đúng.

### Prompt đã dùng

"icon warning hiện trên sink ở level 1 tutorial bị lỗi?", "high light ko đúng, icon hiên ra khỏi màn hình?", "chỉ có
sink bị, generator ko bị và hiện đúng?".

### Phản hồi quan trọng của AI

`GetWorldScreenRect` encapsulate **mọi** renderer kể cả inactive, và chỉ Sink có `Link Slots/Slot 0,1`.

### Phương án được chọn / sửa / loại

Lọc ba nhóm: renderer không được vẽ, renderer không phải mesh, và con của `ResolveTargetLinkSlotsRoot(target)`.

### Kết quả triển khai / kiểm tra

Lần sửa đầu làm hỏng build vì `TowerLinkSlotsView` nằm ở namespace `TowerDefense3D.Towers` chưa import — đã fully
qualify. Kiểm lại 0 component hỏng sau `Refresh(ForceUpdate)`.

## Entry 10 — Trụ đặt sẵn không hiện warning

### Vấn đề đang gặp

Sink đặt từ card hiện warning bình thường, còn Sink đặt sẵn trong scene thì không.

### Prompt đã dùng

"sink đặt trước ko hiện icon warning khi invalid chain, sửa nó".

### Phản hồi quan trọng của AI

`manager.RegisterTower()` publish `StateChanged` **trước** `viewRegistry.Register(nodeId, runtimeView)`, nên listener
duyệt view bỏ qua trụ đó và không có event nào sau đấy.

### Phương án được chọn / sửa / loại

Thêm `PublishStateChanged()` ở cuối `TryRegisterAuthoredTower`, sau khi view đã vào registry. Loại hai giả thuyết sai
trước đó (trụ không phải prefab instance, `Configure` không được gọi).

### Kết quả triển khai / kiểm tra

Trụ đặt sẵn nhận đúng trạng thái chain ngay khi level mở.
