# AI Collaboration Log — Prefab dùng chung cho HUD gameplay, dọn selection strip — 04/09/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Agent:** Claude Code (`claude-opus-5`)
- **Session ID:** `6d136104-c04a-47a1-ac72-24c24033521d`
- **Local date:** 04/09/2026 (`Asia/Saigon`, UTC+7)
- **Coverage:** 5 yêu cầu, 16:38 – 22:35. Nối tiếp `AI_Collaboration_Log_ApplicationUI_04_09.md`
  (log đó chốt ở commit `f5a8180b`, 16:25); phần sau nửa đêm nằm ở
  `AI_Collaboration_Log_Dev_05_09.md`.
- **Prefab chạm vào:** `Assets/Resources/Prefabs/GameplayUI.prefab`,
  `Assets/Resources/Prefabs/TowerBuildButton.prefab` (mới)
- **Scene chạm vào:** `Assets/Scenes/Levels/Level_001.unity`
- **Script đã xoá:** `PlacementHudView.cs` và interface của nó

## Entry 1 — Sáu nút build trụ về chung một prefab

### Vấn đề đang gặp

Sáu nút chọn trụ là sáu object rời, sửa một cái phải sửa sáu lần.

### Prompt đã dùng

"cho những thứ này trong UI gameplay thành prefabs."

### Phản hồi quan trọng của AI

Cả sáu giống hệt nhau về cấu trúc (`Image + Button + TowerPlacementDragButtonView`, ba con
`Name/Coin/Cost`), chỉ khác **màu nền** và **definition**. Rect do `HorizontalLayoutGroup` điều khiển
nên không có hand-tuning nào bị mất khi gom.

### Phương án được chọn / sửa / loại

- Cắt prefab từ chính nút `Generator` đang có để giữ nguyên styling, nhưng **xoá `definition` về
  null** — prefab dùng chung không được mang dữ liệu của một trụ cụ thể.
- Ghi asset ở một lượt, instance hoá ở lượt sau — đúng bài học từ cặp loadout button hôm trước, khi
  làm gộp một lượt thì link không bám.
- Sửa luôn `GameplayHudPrototypeLayout` để lần rebuild sau vẫn dùng prefab, nếu không thì một lần
  rebuild là mất sạch.

### Kết quả triển khai / kiểm tra

Đọc lại từ file đã lưu (không tin log của lệnh vừa chạy): 6/6 là instance, đúng thứ tự catalog, mỗi
cái giữ definition và màu riêng; `towerDragButtons` nối đủ 6, 0 null. Rủi ro đã chủ động chặn:
`nameText`/`costText` trỏ `fileID: 0` thì mọi nút sẽ **im lặng** mất chữ — đã kiểm.

## Entry 2 — Level 1 văng `MissingReferenceException`

### Vấn đề đang gặp

Vào level 1 là lỗi ngay; owner gửi stack trace.

### Prompt đã dùng

"level 1 lỗi này, sửa" → "nhìn các level khác và làm theo".

### Phản hồi quan trọng của AI

Prefab hoàn toàn lành — `PlacementHudView.root` trỏ tới `Selected Panel`, object tồn tại và active.
Lỗi nằm ở **instance trong scene**: `Level_001.unity` là level **duy nhất** có override
`m_RemovedGameObjects` xoá ba object `Network Feedback`, `Selected Status`, `Selected Panel`. Không
chỉ hỏng một reference mà **bốn** (`selectedText`/`chainText`/`queueText` nằm trong `Selected Status`,
`feedbackText` trong `Network Feedback`) — panel lỗi chỉ báo cái đầu tiên gặp phải.

Và phải nói rõ: khối xoá đó **không phải do thay đổi của AI**, nó không có trong HEAD, tức đến từ
session Editor của owner.

### Phương án được chọn / sửa / loại

Gỡ đúng khối `m_RemovedGameObjects` (4 dòng → 1), sau khi xác nhận Level_001 không đang mở trong
Editor. So cả 10 level để chắc đây là tai nạn riêng lẻ, không phải thiết kế.

### Kết quả triển khai / kiểm tra

Diff báo **324 insertions / 0 deletions** — không khớp với việc thay 4 dòng, nên dừng lại kiểm ngay
thay vì báo xong: Unity đã tự ghi lại scene giữa chừng. Mở scene thật để xác nhận: 0 broken serialized
field, `towerDragButtons` 6/6. Chạy thật từ Bootstrap → level 1: **0 console error**.

Một lần suýt báo sai: console có entry lỗi nhưng đó là **log cũ từ lần chạy của owner** — clear console
rồi chạy lại mới kết luận.

## Entry 3 — Xoá thanh đen selection strip

### Prompt đã dùng

"tôi muốn xóa bar đen?"

### Phản hồi quan trọng của AI

Không tắt object được: `PlacementHudView.Show()` gọi `root.SetActive(true)` nên tắt `m_IsActive` sẽ bị
bật lại ngay. Và hai dòng chữ là object **riêng**, không phải con của tấm nền — chỉ bỏ nền thì chúng
trôi lơ lửng trên nền game. Hỏi owner trước thay vì tự chọn.

### Phương án được chọn / sửa / loại

- **Chọn (owner chốt):** xoá hẳn cả ba object và toàn bộ chuỗi code, kể cả `PlacementHudView` — toàn
  bộ việc của nó là bật/tắt tấm nền vừa xoá.
- **Giữ lại:** `SelectedText`/`FeedbackText` trong state + presenter. Xoá phần *hiển thị* không có
  nghĩa là xoá *thông tin*, và cắt nó sẽ giết luôn một test hành vi thật.
- **Thứ tự:** gỡ component khỏi prefab **trước** khi xoá file script, nếu không prefab root ôm một
  missing script.

### Kết quả triển khai / kiểm tra

`missing scripts = 0`. Chạy thật Bootstrap → level 1: 0 error, `Tower Network HUD` còn 9 con (trước
12). Lỗi tự gây trong lúc kiểm: lần chạy đầu đo thấy cả title lẫn menu đều không active suốt 33.7s —
hoá ra **sai scene**, Unity restart và mở lại scene level nên Play chạy từ level chứ không từ Bootstrap.

## Entry 4 — Xoá nút CANCEL và cho bán trụ Sink

### Prompt đã dùng

"xóa nút cancel cho tôi, và tôi muốn được phép bán trụ Sink (logic hiện tại ko cho bán)."

### Phản hồi quan trọng của AI

Việc Sink **không phải logic mà là dữ liệu**: `sellable: 0` trong `SoulNexus.asset`. `sellEnabled`
trong presenter chỉ kiểm `selectedTower != null && CanEditTopology`, không đụng tới `Sellable`. Đổi
đúng một dòng dữ liệu.

Với nút Cancel: trước khi cắt, xác nhận vẫn còn nhiều đường huỷ đặt trụ khác (`TowerInteractionSystem`,
`GridPlacementSystem`, drag-cancel, `TryStartSimulation`) nên xoá nút không làm mất khả năng huỷ.

### Phương án được chọn / sửa / loại

Cắt cả chuỗi: field, event `CancelPlacementRequested`, tham số `CancelPlacementEnabled` của state
(ctor 10 → 9), subscribe/handler trong presenter.

### Kết quả triển khai / kiểm tra

Bắt được một lỗi bỏ sót từ lượt trước: `TowerNetworkHudViewTests` vẫn assert
`panel.Find("Selected Status")` — object đã bị xoá. Đây là **test failure chứ không phải compile
error**, nên "console 0 error" không bao giờ bắt được.

**Tự đính chính một tuyên bố sai:** lượt trước mở đầu bằng "chạy thật để xác nhận cả hai việc — vào
level 1 rồi bán Soul Nexus" nhưng thực tế **chỉ kiểm tĩnh** trên asset và prefab. Sau khi nhận ra, nêu
rõ mức tin cậy từng việc: nút Cancel đã verify chắc (object không còn, serialized field đã mất, compile
0 error, có lần chạy thật trước đó); bán Soul Nexus **chỉ verify tĩnh**. Rủi ro còn lại —
`TrySellSelected` gọi `TryUnlinkAll` trước, nếu hàm đó chặn riêng role Sink thì cờ dữ liệu không đủ —
được loại bằng cách đọc code (`TryUnlinkAll` không có nhánh nào theo `NetworkRole`), và nói rõ là loại
**bằng đọc chứ không bằng chạy**.

> Ghi chú: ngay hôm sau, nút SELL vẫn không ăn — nguyên nhân thật nằm ở tầng input, không phải ở
> cờ `sellable` này. Xem Entry 15 của `AI_Collaboration_Log_Dev_05_09.md`.

## Ghi chú trung thực về mức kiểm chứng

- Entry 1–3 **có chạy thật** qua Bootstrap → Level 1 và đọc lại trạng thái runtime. Entry 4 **không**.
- Hai lần trong ngày, việc đọc lại số liệu đã cứu một báo cáo sai: diff 324 dòng ở Entry 2, và entry
  console cũ bị tưởng là lỗi mới.
- Không chạy test suite nào — cả EditMode lẫn PlayMode.
