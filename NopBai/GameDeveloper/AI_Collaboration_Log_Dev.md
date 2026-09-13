# AI Collaboration Log — Hạn Trời

Tổng hợp các trao đổi quan trọng với AI trong quá trình làm game, từ **13/08/2026** đến **13/09/2026**.

---

## 0. Mục lục

1. [Concept và prototype](#1-concept-và-prototype)
   - [1.1 Chọn cơ chế lõi: RELAY](#11-chọn-cơ-chế-lõi-relay)
   - [1.2 Bỏ xoay trụ, thay bằng link tường minh](#12-bỏ-xoay-trụ-thay-bằng-link-tường-minh)
2. [Kiến trúc](#2-kiến-trúc)
   - [2.1 Thiết kế grid placement ưu tiên Android](#21-thiết-kế-grid-placement-ưu-tiên-android)
   - [2.2 Gom cả dự án về một entry point VContainer](#22-gom-cả-dự-án-về-một-entry-point-vcontainer)
   - [2.3 Một vòng simulation fixed-step cho wave, enemy, tower và đạn](#23-một-vòng-simulation-fixed-step-cho-wave-enemy-tower-và-đạn)
   - [2.4 Một thực thể phải sống ngoài mô phỏng tính trước](#24-một-thực-thể-phải-sống-ngoài-mô-phỏng-tính-trước)
3. [Những lỗi khó và cách tìm ra](#3-những-lỗi-khó-và-cách-tìm-ra)
   - [3.1 Lift vĩnh viễn làm nút Start Wave chết](#31-lift-vĩnh-viễn-làm-nút-start-wave-chết)
   - [3.2 AI tự sửa sai một lần sửa sai](#32-ai-tự-sửa-sai-một-lần-sửa-sai)
   - [3.3 Bản đồ hành trình được author ở hai nơi](#33-bản-đồ-hành-trình-được-author-ở-hai-nơi)
   - [3.4 Một fixture test làm hỏng 15 test không liên quan](#34-một-fixture-test-làm-hỏng-15-test-không-liên-quan)
   - [3.5 Nút SELL bấm không ăn: bốn lượt chẩn đoán sai](#35-nút-sell-bấm-không-ăn-bốn-lượt-chẩn-đoán-sai)
   - [3.6 Override trong scene âm thầm thắng prefab](#36-override-trong-scene-âm-thầm-thắng-prefab)
   - [3.7 Ba lần "console 0 error" không có nghĩa gì](#37-ba-lần-console-0-error-không-có-nghĩa-gì)
4. [Hiệu năng](#4-hiệu-năng)
   - [4.1 Outline pass tốn draw call ở Stroke Width 0](#41-outline-pass-tốn-draw-call-ở-stroke-width-0)
   - [4.2 Probe: vì sao APV đang là chi phí không đem lại gì](#42-probe-vì-sao-apv-đang-là-chi-phí-không-đem-lại-gì)
   - [4.3 SRP Batcher và GPU instancing loại trừ nhau: phương án A′](#43-srp-batcher-và-gpu-instancing-loại-trừ-nhau-phương-án-a)
   - [4.4 Prewarm: một hiệu ứng chỉ được "hâm nóng" khi nó thật sự được vẽ](#44-prewarm-một-hiệu-ứng-chỉ-được-hâm-nóng-khi-nó-thật-sự-được-vẽ)
5. [Gameplay và UI](#5-gameplay-và-ui)
   - [5.1 Tách "đã qua màn" khỏi "đã mở khoá"](#51-tách-đã-qua-màn-khỏi-đã-mở-khoá)
   - [5.2 Hiệu ứng mất máu không chạy: state bị xoá, không phải tween hỏng](#52-hiệu-ứng-mất-máu-không-chạy-state-bị-xoá-không-phải-tween-hỏng)
   - [5.3 Đợt tối ưu UI và hai cái bẫy của nó](#53-đợt-tối-ưu-ui-và-hai-cái-bẫy-của-nó)
   - [5.4 Trụ đặt sẵn bị lún: một luật đã có nhưng không dùng chung](#54-trụ-đặt-sẵn-bị-lún-một-luật-đã-có-nhưng-không-dùng-chung)
   - [5.5 Giới hạn trụ Hero: từ hardcode sang dữ liệu đã author](#55-giới-hạn-trụ-hero-từ-hardcode-sang-dữ-liệu-đã-author)
   - [5.6 Stun của cua, đặt trong một mô phỏng tính trước](#56-stun-của-cua-đặt-trong-một-mô-phỏng-tính-trước)
   - [5.7 Thanh máu cóc: sai hình học, không phải sai vài pixel](#57-thanh-máu-cóc-sai-hình-học-không-phải-sai-vài-pixel)
   - [5.8 Trạng thái khoá của thẻ trụ không thể sống trong code](#58-trạng-thái-khoá-của-thẻ-trụ-không-thể-sống-trong-code)
   - [5.9 Số quái còn lại: một công thức, đặt ở nơi duy nhất biết kế hoạch](#59-số-quái-còn-lại-một-công-thức-đặt-ở-nơi-duy-nhất-biết-kế-hoạch)
   - [5.10 Nâng cấp trụ: nhân vào payload, không đụng nhịp](#510-nâng-cấp-trụ-nhân-vào-payload-không-đụng-nhịp)
   - [5.11 Một câu hỏi, ba hệ thống trả lời](#511-một-câu-hỏi-ba-hệ-thống-trả-lời)
6. [Quy trình](#6-quy-trình)
   - [6.1 Chia lại một commit khổng lồ theo feature](#61-chia-lại-một-commit-khổng-lồ-theo-feature)
   - [6.2 Một camera debug bị lưu nhầm vào scene](#62-một-camera-debug-bị-lưu-nhầm-vào-scene)
   - [6.3 Dọn `Co-Authored-By` khỏi lịch sử mà không đụng remote](#63-dọn-co-authored-by-khỏi-lịch-sử-mà-không-đụng-remote)
   - [6.4 Reset tiến trình: không phải PlayerPrefs](#64-reset-tiến-trình-không-phải-playerprefs)
7. [Tổng kết cách làm việc với AI](#7-tổng-kết-cách-làm-việc-với-ai)
   - [7.1 Bằng chứng thắng suy đoán](#71-bằng-chứng-thắng-suy-đoán)
   - [7.2 AI phải được phép rút lại kết luận của chính mình](#72-ai-phải-được-phép-rút-lại-kết-luận-của-chính-mình)
   - [7.3 Ranh giới quyết định](#73-ranh-giới-quyết-định)
   - [7.4 Verify theo kiểu Unity](#74-verify-theo-kiểu-unity)
   - [7.5 Một luật, một chỗ](#75-một-luật-một-chỗ)
   - [7.6 Nói rõ thứ chưa kiểm được](#76-nói-rõ-thứ-chưa-kiểm-được)

---

## 1. Concept và prototype

### 1.1 Chọn cơ chế lõi: RELAY

#### Vấn đề đang gặp

Game cần một cơ chế lõi nói được trong một câu và thay đổi *việc người chơi lặp đi lặp lại*,
chứ không phải thêm một dàn trụ hay một hệ buff thụ động nữa.

#### Prompt đã dùng

Yêu cầu AI đưa ra các từ khoá cơ chế thủ thành lạ, rõ ràng ngang tầm "merge" hay "random",
rồi đòi thêm ý tưởng có tham chiếu game khác.

#### Phản hồi quan trọng của AI

AI so sánh một loạt: Orbit, Relay, Echo, Magnet, Swap, Core, Stack, String,
Traffic, Record, Fuse, Catch. Quan trọng hơn danh sách là **tiêu chí** nó đặt ra cho một cơ chế lõi tốt:
giải thích được trong một câu, dùng liên tục, áp được lên toàn bộ dàn trụ, và **đẻ ra quyết định chứ
không phải trang trí**. `Relay` được mô tả là trụ chuyền đạn qua nhau, thứ tự trụ làm đổi viên đạn.

#### Phương án chọn / sửa / loại

- **Chọn:** `RELAY` — bản thân mạng lưới trụ là vũ khí.
- **Chọn:** đoạn nối thẳng, một chuỗi liên tục hoặc zigzag, đặt route sao cho cắt ngang nhiều quái nhất.
- **Loại:** đề xuất riêng của AI là `ORBIT` — nó chỉ dừng ở mức nghiên cứu, không thành hướng dự án.

#### Lý do

Relay làm cho thứ tự, góc và hình học trở thành quyết định chiến thuật, lại nhìn thấy được
ngay trong một ảnh chụp màn hình — điều kiện sống còn của một game mobile.

#### Kết quả sau khi triển khai

Chat này chỉ sinh ra nghiên cứu và định hướng, không sinh code. Kết luận Relay về sau thành
`RawConcept_2.md` và toàn bộ prototype routing. Cơ chế **NỐI** trong bản game cuối là hậu duệ trực tiếp
của entry này.

---

### 1.2 Bỏ xoay trụ, thay bằng link tường minh

#### Vấn đề đang gặp

Prototype ban đầu cho xoay trụ liên tục để chỉnh hướng bắn. Trên màn cảm ứng, việc này khiến
người chơi gần như không dựng nổi một mạng đạn chính xác.

#### Prompt đã dùng

Chủ dự án yêu cầu bỏ xoay, thay bằng hành động Link; highlight trụ hợp lệ trong tầm; từ chối
link hai chiều trực tiếp; tăng tầm nối; rút ngắn route tutorial cho thẳng theo lưới; và làm rõ: *trụ buff
ở cuối chuỗi mà chỉ có link vào thì không được làm gì cho tới khi có link ra*.

#### Phản hồi quan trọng của AI

AI mô hình hoá mọi kết nối thành **cạnh có hướng** từ một trụ tới đúng một trụ
đích. Mỗi trụ giữ một cạnh ra, nhiều trụ được phép cùng đổ vào một trụ nhận, chỉ chặn đúng cạnh ngược
trực tiếp A→B rồi B→A. Sát thương chỉ tồn tại trên đoạn đã nối.

#### Phương án chọn / sửa / loại

- **Chọn:** chọn trụ nguồn → vào Link mode → chạm một trụ đích đang được highlight.
- **Chọn:** tăng 50% mọi tầm nối khác 0; giữ tường, luật độ cao và kiểm tra tầm là nguồn phán quyết.
- **Loại:** tự động nối ngầm, bắn tự do theo hướng, điều khiển xoay, và route tutorial dài không theo lưới.

#### Lý do

Link tường minh làm trạng thái mạng lưới **kiểm tra được và tất định** trên cả chuột lẫn cảm
ứng. Route ngắn theo lưới cho tutorial dạy được cơ chế mà không vô tình giải hộ người chơi.

#### Kết quả sau khi triển khai

Bộ test link đầy đủ: 37 test pass, 7 skip cố ý do khác viewport, 0 fail trên cả desktop và
mobile. Bản deploy production chạy thật không lỗi console. Đây là cơ chế được giữ nguyên vào bản Unity.

---

## 2. Kiến trúc

### 2.1 Thiết kế grid placement ưu tiên Android

#### Vấn đề đang gặp

Cần một thiết kế đủ chi tiết để code ngay: đặt trụ trên lưới XZ với các tầng Y rời rạc,
footprint 3 chiều cấu hình được, chống chồng lấn, và phản hồi chỉ hiện ở ô ứng viên — cho game mobile
landscape.

#### Prompt đã dùng

Chủ dự án yêu cầu một kế hoạch hoàn chỉnh, làm rõ *width/depth là ngang, height là dọc*, đổi
xác nhận từ nút bấm sang **thả tay là đặt**, chốt Android trước, đặt application ID, fullscreen landscape,
nhắm 60 FPS trên máy tầm trung.

#### Phản hồi quan trọng của AI

AI đề xuất tách phần logic thành **C# thuần, tất định** (mapping, validation,
occupancy, reservation) nằm sau các component Unity mỏng chỉ lo input và hiển thị. Dữ liệu board và trụ
là ScriptableObject. Occupancy phủ toàn khối 3 chiều. Preview dùng **một khối mờ xanh/đỏ gộp** thay vì vẽ
cả lưới.

#### Phương án chọn / sửa / loại

- **Chọn:** kéo-thả cảm ứng, thả tay là đặt, giữ lại ứng viên không hợp lệ để người chơi thấy vì sao sai,
  Safe Area Cancel, nút Back Android để huỷ.
- **Chọn:** reserve → spawn → commit → rollback nguyên tử.
- **Sửa:** xác nhận bằng nút → revalidate và đặt ngay khi thả tay.
- **Loại:** vẽ toàn lưới, renderer preview cho từng ô, xếp chồng, xoay, bán, di chuyển, và iOS.

#### Lý do

Giữ luật miền tất định và test được, đồng thời hạn chế allocation, draw call và sự mập mờ khi
chạm trên mobile.

#### Kết quả sau khi triển khai

Hợp đồng và đồ thị phụ thuộc B1–B10 được ghi vào `README.md`. Bản cài đặt cuối cùng: Editor
idle, 0 lỗi 0 warning console, 6 prefab không thiếu script, có Edit Mode và Play Mode test.

---

### 2.2 Gom cả dự án về một entry point VContainer

#### Vấn đề đang gặp

Dự án đã có một `IStartable` điều phối ứng dụng, nhưng phần lớn công việc hệ thống vẫn nằm rải
rác trong hàng loạt callback `MonoBehaviour`, adapter, presenter, UI manager. Thứ tự vòng đời, tuổi thọ
của state có thể đổi, và **lý do một class phải kế thừa `MonoBehaviour`** đều khó thấy.

#### Prompt đã dùng

Chủ dự án yêu cầu xem lại **toàn bộ** dự án từ góc nhìn một entry point duy nhất, dùng hết các
lifecycle mà VContainer sở hữu được, còn callback riêng của object Unity thì để tại chỗ.

#### Phản hồi quan trọng của AI

`ApplicationEntryPoint` là **type duy nhất** implement `IAsyncStartable`,
`ITickable`, `ILateTickable`, `IDisposable`. Nó dispatch tường minh một `ApplicationSystemGroup` và một
`LevelSystemGroup`. Mỗi level additive sở hữu `LevelLifetimeScope` con: **toàn bộ state của placement,
tower, simulation, input, HUD chết cùng scope đó**. Callback thuộc về object — enable/disable, pointer,
collision, destroy — vẫn ở lại local vì Unity mới là chủ của những sự kiện này.

#### Phương án chọn / sửa / loại

- **Chọn:** cây thư mục `Application`, `System`, `Components`, `Editor`, `Tests` + 6 assembly phi chu trình.
- **Chọn:** thứ tự tick tường minh: input → placement → tower interaction → simulation → HUD → link →
  projectile → camera.
- **Sửa:** **không** tạo folder `Core` đầu cơ; chỉ tách primitive dùng chung khi có hệ thống **thứ hai**
  thật sự cần.
- **Loại:** event bus toàn cục, tick interface generic, tự enumerate container, code PlayerLoop tuỳ biến,
  và một commit migration khổng lồ.

#### Lý do

Entry point làm thứ tự hệ thống hiện rõ mà **không giả vờ** rằng sự kiện object của Unity thuộc
về một vòng lặp toàn cục. Scope con cho state của level một tuổi thọ do container ép buộc. Tách `System`
khỏi `Components` phơi bày đúng lý do một class cần Unity.

#### Kết quả sau khi triển khai

Migration hoàn tất qua 9 commit theo feature, mỗi commit vẫn build được. Đây là kiến trúc
đang chạy trong bản nộp.

---

### 2.3 Một vòng simulation fixed-step cho wave, enemy, tower và đạn

#### Vấn đề đang gặp

Action mô phỏng trụ cũ không phải chỗ hợp lý để bắt đầu và kết thúc wave. Quái cần đi trên
đường đã author mà không dùng collider, dùng chung tick với đạn, và quay về pool khi chết hoặc lọt.

#### Prompt đã dùng

Yêu cầu một hệ Wave đầy đủ (Preparation, Start Wave, spawn, hoàn thành, preview, Victory), quái
và đạn dùng chung tick `0.05` giây. Cơ chế Core dùng chung **chỉ được phép** khi nhiều hệ thống thật sự
cần — State Machine, Event Bus, Pool Manager là ví dụ chứ không phải bắt buộc.

#### Phản hồi quan trọng của AI

Một `GameplaySimulationSystem` duy nhất. Mỗi bước cố định chạy đúng thứ tự: lịch
spawn → mô phỏng trụ → quái đi đường → xử va chạm đạn/quái → xét hoàn thành wave. Chỉ **`FixedStepClock`**
được tách ra làm hạ tầng dùng chung, vì nó đã chứng minh được là dùng chung thật.

#### Phương án chọn / sửa / loại

- **Chọn:** một aggregate simulation ở cấp level, catch-up `0.05` giây tường minh.
- **Loại:** event bus toàn cục, pool manager generic, framework state machine generic, `Update` cho từng
  quái, di chuyển bằng physics, và một entry point thứ hai.

#### Lý do

Thứ tự bước tường minh cho kết quả tất định và làm điều kiện thắng wave phụ thuộc **cả** vào
"hết lệnh spawn" **lẫn** "không còn quái sống". Class chạy thẳng dễ debug hơn framework tổng quát dựng ra
trước khi có ca dùng thứ hai.

#### Kết quả sau khi triển khai

Wave chỉ bắt đầu được khi có chuỗi trụ hợp lệ; mô phỏng trụ dừng giữa các wave; reset đưa
level về đúng trạng thái Preparation ban đầu. Chính kiến trúc "tính trước cả wave" này là nền cho mục 3.1
và mục 5.6.

---

### 2.4 Một thực thể phải sống ngoài mô phỏng tính trước

#### Vấn đề đang gặp

Level 10 cần một con boss **đứng sẵn trên đường**: xuất hiện từ wave 6, dùng skill triệu hồi quái cho tới
wave 9, bất khả xâm phạm trong suốt thời gian đó, và chỉ tới wave 10 mới thật sự bước vào trận với máu đầy.

#### Prompt đã dùng

"Boss ở level 10, lúc vào wave 1 sẽ đứng ở nơi tôi khoanh tròn trên map… cho phép game balancer tool chỉnh
boss spawn quái từ wave 1 tới wave kế cuối"; rồi ba câu trả lời thiết kế: "(a) boss nằm trên đường (b) máu
boss ko cộng dồn, bất khả xâm phạm tới wave cuối, wave cuối nó vào trận với máu đầy (c) boss ko chặn đường,
và ko có va chạm, chỉ đứng trang trí và dùng skill spawn"; và "trình bày cách làm để tôi duyệt".

#### Phản hồi quan trọng của AI

Boss đứng **không thể nằm trong wave plan**, và lý do nằm đúng ở kiến trúc mục 2.3: `CombatTimelinePlanner`
chạy shadow sim rồi bake mọi tick, và nó chỉ thoát khi mọi quái đã chết hoặc đã lọt. Một con boss bất tử làm
`enemies.Count` không bao giờ về 0 ⇒ planner ném đúng cái exception "exceeded its deterministic horizon" của
mục 3.1. Vậy boss phải là một **fixture ngoài plan** (`EnemySystem.EnsureStandingBoss`), do
`StationaryBossPlan` điều khiển để designer chỉnh theo wave.

#### Phương án chọn / sửa / loại

- **Chọn:** marker trong scene (`StandingBossAnchorView`) là **nguồn vị trí duy nhất**, đo lại mỗi lần bắt
  đầu wave; hướng mặt lấy từ rotation của marker, vì hướng bình thường suy ra từ chuyển động mà boss đứng thì
  không có chuyển động.
- **Loại (theo owner):** công cụ "Write Stand Distance From Marker" — "nó cứ wire sai vị trí tôi muốn, tôi kéo
  marker thôi được ko"; công cụ bị xoá.
- **Chọn:** ở wave cuối **giữ nguyên instance** thay vì spawn con mới: order được đánh dấu
  `AdoptsExistingEnemy`, boss được re-key sang id của plan (`AdoptStandingBossAs` + `EnemyRekeyed` +
  `IEnemyViewPool.Rekey`).
- **Sửa (chốt cuối ngày):** boss dời từ wave 1 sang wave 6, nên hai nhánh "standing / không standing" không
  đủ — phải tách thành ba trạng thái `IsStandingOnWave` / `IsFightingOnWave` / `IsPresentOnWave`.

#### Lý do

Owner nói rõ: "tôi cần instance diễn như là boss". Người chơi nhìn con boss đứng suốt bốn wave thì phải chính
con đó bước đi ở wave cuối — đánh tráo bằng một instance mới là phá vỡ điều duy nhất làm con boss đáng nhớ.
Còn việc để nó ngoài plan không phải lách luật: plan là *mô phỏng tính trước của một trận đánh có kết thúc*,
mà boss đứng theo định nghĩa thì chưa tham chiến.

#### Kết quả sau khi triển khai

Chuỗi lỗi runtime được sửa lần lượt, và **phần lớn đều là cùng một luật bị chép ở hai nơi**:

- `ArgumentOutOfRangeException: enemyId` — truyền `0L` vào `MeasureRoadDistance` trong khi id bắt đầu từ 1.
- Boss không bao giờ dùng skill: `StepStandingBoss` nằm trong `EnemySystem.Step`, mà hàm đó **không hề được
  gọi trong game** (grep ra chỉ có test gọi). Chuyển sang `WaveSystem.StepSpawning`.
- `ArgumentException: same key '1'` và chuột nhấp nháy: `Reset()` xoá model trong im lặng rồi tua
  `nextEnemyId` về 1, làm view mồ côi.
- `KeyNotFoundException: key '15'`: tái dùng id thấp của boss phá vỡ bất biến id (planner đánh số summon từ
  `max(plan ids)+1`) — sửa bằng cách **đảo chiều**: order giữ id bình thường, boss re-key sang id đó.

Probe chạy thật `CreatePlan` trên asset: 10 wave, 0 lỗi validation, chỉ wave 6 spawn fixture, wave 10 có đúng
1 order takeover, bất biến id đúng ở cả 10 wave. Khi owner vặn lại "sao lại ko khớp tick, vì hệ thống đã
precompute trước nên phải khớp chứ", AI kiểm tra lại và **tự đính chính**: `RecordFrames` ghi mọi tick và
`ApplyPlannedFrame` ghi đè, nên plan mới là nguồn sự thật — chỉ jitter giữa hai tick là còn rủi ro thật.

---

## 3. Những lỗi khó và cách tìm ra

### 3.1 Lift vĩnh viễn làm nút Start Wave chết

#### Vấn đề đang gặp

Ở Level 1 wave 4, bấm START WAVE không có gì xảy ra và ném
`InvalidOperationException: Combat planning exceeded its deterministic horizon of 1756 ticks`. HUD báo
"Tower simulation started." nhưng wave vẫn READY TO START — bàn chơi **kẹt cứng**, không sửa trụ được nữa.

#### Prompt đã dùng

"Cannot click start wave, clicking does not start the wave, is something wrong?" kèm exception
và ảnh chụp wave 04/08.

#### Phản hồi quan trọng của AI

AI đọc ra từ **chính các con số đã author**, không cần chạy lại: planner mô phỏng
cả wave trước khi bắt đầu và bỏ cuộc sau `lastMovementTick * 4 + 200` tick; nó chỉ thoát sớm khi mọi quái
đã chết hoặc lọt. `WaterWind_Lift` nhấc quái **1.5 giây** và gây **0 sát thương**, trong khi mọi trụ nguyên
tố có `cycleIntervalSeconds = 0.85`. Chuỗi Water→Wind vì thế tái áp lift mỗi ~0.85 giây lên trên một lift
1.5 giây ⇒ quái **treo vĩnh viễn**: không tiến, không lọt, không chết. Tệ hơn, đứng yên trong tầm trụ lại
bảo đảm nó tiếp tục bị nhấc.

AI còn tách ra **lỗi thứ hai, độc lập**: `TryStartWave` gọi `TryStartSimulation` *trước* khi phát
`WavePlanCreated`, và exception thoát ra giữa hai bước đó — nên simulation đã start còn wave thì không bao
giờ sang Running. Đó mới là thứ biến một lỗi hồi phục được thành một phiên chơi hỏng.

#### Phương án chọn / sửa / loại

- **Chọn:** thêm `liftImmunitySeconds` vào asset reaction (mặc định 1.5); planner từ chối lift mới cho tới
  khi lift cũ **cộng** cửa sổ miễn nhiễm trôi qua.
- **Chọn:** validation từ chối một lift không có cửa sổ miễn nhiễm, để không ai cấu hình lại được cái khoá.
- **Chọn:** bắt lỗi planning ngay trong `TryStartWave`, dừng simulation, xoá plan, trả lỗi qua
  `out string error` sẵn có.
- **Loại:** nâng horizon — chỉ trì hoãn một vòng lặp vô hạn chứ không kết thúc nó.
- **Loại:** coi quái bị kẹt là "đã lọt" — che một lỗ hổng thiết kế bằng một luật im lặng.
- **Loại:** bỏ lift hoặc đặt thời gian bằng 0 — đó là quyết định cân bằng, thuộc về designer.

#### Lý do

Lift là hiệu ứng khống chế duy nhất **vừa** chặn di chuyển **vừa** không gây sát thương, nên nó
là thứ duy nhất đẻ ra được một con quái không bao giờ kết thúc. Cửa sổ miễn nhiễm là câu trả lời chuẩn
(diminishing returns) và bảo đảm tiến triển với **mọi** giá trị dương — đó là thứ làm planning dừng được.
Để con số trong asset là giữ quyền cân bằng cho designer.

#### Kết quả sau khi triển khai

Lift uptime từ 100% xuống ~50%. Thêm `ElementReactionAssetTests`. 189/189 EditMode test pass.
**Ghi rõ giới hạn:** bản sửa suy ra từ số liệu author và luật di chuyển của planner, *chưa* tái hiện trong
Play Mode — wave 4 với chuỗi Water→Wind vẫn cần một lượt test tay.

---

### 3.2 AI tự sửa sai một lần sửa sai

#### Vấn đề đang gặp

`Chicken.fbx` nặng 4.91 MB và `Gecko.fbx` 4.60 MB cho những mesh rất ít poly.

#### Prompt đã dùng

"sửa luôn, dọn luôn".

#### Phản hồi quan trọng của AI

*Entry này ghi lại một lỗi của AI.* AI nói nguyên nhân là texture nhúng trong FBX
→ rồi **tự đảo ngược** kết luận đó, vì `AssetDatabase.LoadAllAssetsAtPath` của Unity báo `embedded
textures = 0` cho cả hai file. Một lượt audit bằng Blender chứng minh **câu đầu mới đúng, câu đảo ngược
sai**: Chicken mang một base colour 2048² nhúng nặng 2.61 MB cộng một normal 1.70 MB; Gecko 2.11 MB +
1.49 MB. **Unity không phơi texture nhúng trong FBX ra thành sub-asset, nên con số của nó không phải bằng
chứng cho sự vắng mặt.** Stone Sentinel — 0 ảnh nhưng 83.913 keyframe, nhiều hơn cả hai file kia — là đối
chứng loại trừ giả thuyết "do dữ liệu animation".

#### Phương án chọn / sửa / loại

- **Chọn:** coi audit Blender là nguồn phán quyết, coi số sub-asset của Unity là **không đáng tin** cho câu
  hỏi này.
- **Chọn:** backup cả hai FBX trước khi round-trip, và chuẩn hoá tên action để lần export sau không cộng
  dồn thêm một prefix `Armature|`.
- **Loại:** tin một báo cáo của một công cụ thay vì đo trực tiếp.

#### Lý do

Cú đảo ngược xảy ra vì chấp nhận một tín hiệu tiện tay mà không kiểm tra xem nó **đo cái gì**.
Bài học được ghi lại: báo cáo texture nhúng của Unity đang trả lời một câu hỏi khác với câu đang hỏi.

#### Kết quả sau khi triển khai

Chicken 4.91 → 0.60 MB (−87,8%); Gecko 4.60 → 1.00 MB (−78,3%); tổng FBX quái 11,89 → 3,97 MB.
Rủi ro lo ngại không xảy ra: Unity giữ nguyên fileID sub-asset theo tên, nên `overrideController`, prefab
Stealth và clip Crow đều sống sót, scale `1.31` và yaw `184.7` không đổi.

---

### 3.3 Bản đồ hành trình được author ở hai nơi

#### Vấn đề đang gặp

Sau khi sửa anchor, ảnh chụp của chủ dự án **không đổi gì**, và game ném
`NullReferenceException` tại `LevelMenuView.UnbindButtons` ngay lúc khởi động.

#### Prompt đã dùng

Chủ dự án dán exception cùng một loạt lỗi Inspector (`SerializedObjectNotCreatableException`,
`MissingReferenceException`) và ảnh cho thấy foreground vẫn nằm chỗ cũ.

#### Phản hồi quan trọng của AI

Bản đồ được author **hai lần**: trong `ApplicationUI.prefab`, *và* trực tiếp trong
`Bootstrap.unity` dưới dạng object chỉ-có-trong-scene. **Bản trong scene mới là bản chạy**, nên mọi sửa đổi
lên prefab cả phiên đó đều vô hình với game — các bản sửa đều đúng, chỉ là **áp vào sai chỗ**. Crash đến từ
cùng một chỗ rẽ: `levelButtons` là một override trong scene giữ 10 phần tử **đều null**, vì đích của
override là các node đã bị thay trong prefab.

#### Phương án chọn / sửa / loại

- **Chọn:** nối `levelButtons` vào đúng 10 node của scene — chúng chỉ tồn tại trong scene, mảng của prefab
  không bao giờ ánh xạ tới được.
- **Thử và loại:** `RevertPropertyOverride` trên mảng — gỡ override xong vẫn còn nguyên 10 null, vì trong
  prefab **không có gì** để một object chỉ-có-trong-scene tương ứng. Ghi lại để không thử lại lần nữa.
- **Thử và thất bại:** `RevertObjectOverride` trên `Layer4` ném `ArgumentException` — bản thân nó chính là
  bằng chứng object đó chỉ thuộc scene.

#### Lý do

Object chỉ tồn tại trong scene thì mảng của prefab **không bao giờ** ánh xạ tới được — đó là lý do
mọi thao tác revert đều vô nghĩa và bản sửa bắt buộc phải nằm ở chỗ bản đang chạy. Rộng hơn: khi một
nội dung được author ở hai nơi, mọi bản sửa đều có 50% cơ hội rơi vào bản không ai chạy.

#### Kết quả sau khi triển khai

`levelButtons`: 10 phần tử, **0 null**. **Một hành động lẽ ra phải hỏi trước:** AI đã lưu
`Bootstrap.unity`; file phình từ ~1,9k lên 15.882 dòng vì thao tác lưu ghi lại cấu trúc node mới của prefab
thành override. Nội dung của chủ dự án còn nguyên, nhưng **trạng thái trước khi lưu không khôi phục được** —
đã kiểm `git stash`, `git fsck --lost-found`, thư mục backup và `Temp/` của Unity, đều trống.

**Vấn đề còn mở được nêu ra chứ không tự quyết:** bản đồ cần **một** nguồn sự thật; phương án gom về prefab
đã được đề xuất và để chủ dự án chọn.

---

### 3.4 Một fixture test làm hỏng 15 test không liên quan

#### Vấn đề đang gặp

Thêm `JourneyForegroundLayerTests` làm suite EditMode từ 277/277 tụt xuống 262 pass / 15 fail.
Toàn bộ 15 fail nằm ở `BoardSceneAuthoringTests` và `BoardGridPlaceableAuthoringTests` — chẳng liên quan gì
tới parallax.

#### Prompt đã dùng

Không có prompt riêng cho lỗi này — nó lộ ra khi chạy lại suite sau khi thêm fixture mới.
Chủ dự án chỉ hỏi thêm về việc Unity liên tục bật hộp thoại Save / Don’t Save trong lúc chạy test.

#### Phản hồi quan trọng của AI

Các fixture board chạy riêng thì pass 16/16, nên đây là **phụ thuộc thứ tự**, không
phải lỗi logic. Bỏ fixture mới ra thì về lại 277/277, xác định được thủ phạm. Nguyên nhân: một component
`Image` thừa trên các object của rig — **uGUI graphic sống sót trong scene Edit Mode dùng chung chính là
cách một fixture bắt đầu phá fixture kế tiếp**. Rig chưa bao giờ cần `Image`.

#### Phương án chọn / sửa / loại

- **Chọn:** bỏ `Image` khỏi rig → lỗi board về 0 ngay lập tức.
- **Chọn:** dựng rig trong `EditorSceneManager.NewPreviewScene()` và đóng ở `TearDown`, để fixture không
  thể làm bẩn scene đang mở.
- **Chọn:** cho rig một `Canvas` cha — Unity chỉ tôn trọng `overrideSorting` trên canvas **lồng** trong một
  canvas khác và **âm thầm xoá** nó trên canvas gốc.

#### Lý do

Một fixture Edit Mode dùng chung một scene với mọi fixture khác trong lượt chạy. Thứ nó để lại là
lỗi **của nó**, không phải của thứ fail sau đó.

#### Kết quả sau khi triển khai

EditMode 282/282 pass. Ghi nhận thêm: 11 fixture khác trong repo cũng tạo GameObject thẳng vào
scene đang mở — mới chỉ chuyển được một cái sang preview scene.

---

### 3.5 Nút SELL bấm không ăn: bốn lượt chẩn đoán sai

#### Vấn đề đang gặp

Bấm SELL không có gì xảy ra, trong khi UNLINK — nút ngay bên dưới — thì ăn bình thường. Ban đầu owner còn
tưởng chỉ trụ Sink mới không bán được.

#### Prompt đã dùng

"trụ sink vẫn ko bán được" → "vì nút sell khi bấm ko bán?" → "bấm sell các trụ khác không ăn luôn, chỉ unlink
được, có vẻ do UI?" → "vẫn ko sell được? bấm rồi và ko có gì xảy ra".

#### Phản hồi quan trọng của AI

Ba giả thuyết liên tiếp **đều sai**, và mỗi lần đều bị chính dữ liệu phủ định:

1. "Cờ `sellable` sai" — đo bộ nhớ lúc game đang chạy: `sellable=True`.
2. "`Tower Actions` bị `Start Wave` nuốt raycast" — nếu vậy **cả hai** nút phải chết, không thể UNLINK ăn còn
   SELL không.
3. "Logic từ chối" — dựng lại đúng đường chạy bằng API công khai: `TrySellSelected -> True`, "Sold Generator
   for 154 gold", vàng 1000 → 1154.

Nguyên nhân thật nằm ở **tầng input**: `GameplayInputSource.IsPointerOverUi` gọi
`EventSystem.IsPointerOverGameObject(pointerId)`, hàm này trả kết quả EventSystem giải được ở **frame trước**.
Một cú chạm vừa bắt đầu **không có frame trước**, nên nó trả `false` ngay tại frame nhấn ⇒
`TowerInteractionSystem.BeginPointer` chạy → `TryPickTower` trong bán kính 96px không thấy trụ →
`ClearSelection()` → tới lúc nhả tay `onClick` mới bắn thì `selectedTower == null` → **từ chối trong im lặng**.

UNLINK sống sót chỉ vì nó là nút **dưới**, gần trụ hơn, vẫn nằm trong bán kính 96px nên `BeginPointer` chọn
lại đúng trụ đó. Khác biệt giữa "hỏng" và "chạy" đúng bằng **một khoảng cách nút**.

#### Phương án chọn / sửa / loại

- **Chọn:** thay bằng `EventSystem.RaycastAll` tại đúng toạ độ chạm — chính xác theo frame, đúng cả với touch
  vừa sinh; dùng `PointerEventData` và list tái sử dụng nên không sinh rác mỗi frame.
- **Chọn:** sửa ở `GameplayInputSource` chứ không vá trong `TowerInteractionSystem`, vì cùng cờ đó còn được
  `GridPlacementSystem` dùng.
- **Chọn:** presenter thôi nuốt lỗi bằng `out _`, log lý do từ chối ra console.

#### Lý do

Chính cái `out _` là thứ biến "bị từ chối" thành "trông như hỏng", và là lý do AI mò tới bốn lượt: không có
thông điệp nào để đọc thì mọi giả thuyết đều nghe hợp lý như nhau. Còn bản sửa phải nằm ở nguồn cờ, vì hai hệ
thống đang đọc chung nó.

#### Kết quả sau khi triển khai

Bắn `EventSystem.RaycastAll` vào tâm nút trong phiên chơi sống: cả hai nút đều `topmost IS the button` — loại
dứt giả thuyết bị che. Gọi `sellButton.onClick.Invoke()` thì **bán thật** (2 trụ → 1; lần đếm đầu vẫn thấy 2
vì `Destroy` hoãn tới cuối frame). Console in đúng `Sell refused: Select a tower before selling.`

---

### 3.6 Override trong scene âm thầm thắng prefab

#### Vấn đề đang gặp

Cùng một triệu chứng lặp lại ba lần trong một tuần: HUD sửa trong prefab nhưng **chỉ một level** hiển thị
sai. Level 1 văng `MissingReferenceException` ngay khi vào; sau đó plaque NEXT WAVE ở Level 1 hiện hẹp, chữ
và icon tràn ra ngoài khung.

#### Prompt đã dùng

"level 1 lỗi này, sửa" → "nhìn các level khác và làm theo"; rồi "lỗi rồi đây là những gì tôi thấy khi vào
level 1, ko đúng như ảnh 2".

#### Phản hồi quan trọng của AI

Prefab hoàn toàn lành trong cả hai lần. Lỗi nằm ở **instance trong scene**:

- `Level_001.unity` là level **duy nhất** có `m_RemovedGameObjects` xoá ba object `Network Feedback`,
  `Selected Status`, `Selected Panel`. Không chỉ hỏng một reference mà **bốn** — panel lỗi chỉ báo cái đầu
  tiên nó gặp. Và phải nói rõ: khối xoá đó **không phải do AI**, nó không có trong `HEAD`.
- Lần sau, cùng instance đó giữ 8 property override (`m_SizeDelta = 257.5×70.4`,
  `m_PixelsPerUnitMultiplier = 8.49`, `Chevron.m_AnchoredPosition.x = -24`), nên plaque bị ép hẹp trong khi
  các phần tử con vẫn ở toạ độ của prefab.

Cơ chế chung: mỗi lần kéo một rect trong Scene view, Unity ghi một override, và từ đó scene ấy **âm thầm phớt
lờ** mọi thay đổi prefab của đúng property đó.

#### Phương án chọn / sửa / loại

- **Chọn:** gỡ đúng khối `m_RemovedGameObjects` (4 dòng → 1) và đúng 8 override, rồi **quét cả 10 level** để
  xác định đây là tai nạn riêng lẻ chứ không phải thiết kế.
- **Chọn:** mọi sửa đổi prefab sau đó đi qua `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset` thay vì
  ghi thẳng YAML, vì Unity từng ghi đè ngược lại thay đổi YAML (Canvas `renderMode` tự quay về giá trị cũ).
- **Loại:** sửa từng scene — HUD là prefab dùng chung, sửa 10 chỗ là tạo ra 10 đường trôi khác nhau.

#### Lý do

Đây là mục 3.3 lặp lại ở tầng nhỏ hơn: khi cùng một giá trị được author ở hai nơi, bản sửa có 50% cơ hội rơi
vào bản không ai chạy. Khác biệt là lần này **cái sai chỉ lộ ra ở đúng một trong mười level**, nên nó gần như
vô hình nếu không quét cả bộ.

#### Kết quả sau khi triển khai

Đọc lại từ file đã lưu: 10/10 level đều `550×92, Sliced, mult 6.01, chevron −28`; Level 9 chỉ có
`m_StaticEditorFlags` vô hại. Lần trước đó, diff báo **324 insertions / 0 deletions** cho một thao tác thay 4
dòng — con số không khớp nên dừng lại kiểm ngay thay vì báo xong, và đúng là Unity đã tự ghi lại scene giữa
chừng. Cùng loại bệnh: `GridPlacementPresenter` có field `initialTower` được gán trong **cả 10 scene**, khiến
vừa vào level là bàn cờ đã ở chế độ đặt trụ và cú chạm đầu tiên xây luôn thứ chưa hề được chọn.

---

### 3.7 Ba lần "console 0 error" không có nghĩa gì

#### Vấn đề đang gặp

Ba lượt liên tiếp AI báo "console 0 error" trong khi code **chưa hề được biên dịch**.

#### Prompt đã dùng

Không có prompt riêng — nó lộ ra khi đối chiếu mốc thời gian file sau vài lượt sửa mà không lỗi nào xuất hiện.

#### Phản hồi quan trọng của AI

`TowerDefense3D.System.Runtime.dll` đứng ở 18:21 trong khi source đã sửa lúc 18:43.
`CompilationPipeline.RequestScriptCompilation()` **không ăn khi editor chạy nền**. Hệ quả thẳng thừng: mọi
lần "console 0 error" trong giai đoạn đó đều vô nghĩa — không có gì để mà báo lỗi.

#### Phương án chọn / sửa / loại

- **Chọn:** luôn đối chiếu mốc thời gian DLL với mốc source **trước** khi kết luận, cộng một type probe.
- **Chọn:** nhờ owner đưa focus vào Unity khi cần biên dịch thật.
- **Loại:** coi `Unity_ValidateScript` là bằng chứng biên dịch — nó chỉ soi từng file một.

#### Lý do

Một tín hiệu "sạch" lấy từ hệ thống chưa chạy thì không phải bằng chứng, nó là **sự vắng mặt của bằng chứng**
— đúng cùng một cái bẫy với báo cáo texture nhúng ở mục 3.2.

#### Kết quả sau khi triển khai

Lỗi `NodeState` ở `TowerNetworkManager.ProjectilePlanning.cs:63` chỉ lộ ra sau khi biên dịch thật, và rà thêm
thì tự tìm ra một lỗi cùng loại chưa kịp nổ: `TowerNetworkHudViewStub` chưa implement `UpgradeRequested` vừa
được thêm vào `ITowerNetworkHudView`. Một biến thể khác của cùng bài học được ghi lại trước đó:
`TowerNetworkHudViewTests` vẫn assert một object đã bị xoá — đó là **test failure chứ không phải compile
error**, nên "console 0 error" không bao giờ bắt được.

---

## 4. Hiệu năng

### 4.1 Outline pass tốn draw call ở Stroke Width 0

#### Vấn đề đang gặp

Toon shader của vendor vẽ outline bằng một `Pass` thứ hai **vô điều kiện**. Ở `_StrokeWidth = 0`
hình học outline trùng vị trí mesh gốc nên lẽ ra vô hình, nhưng vẫn tốn draw call mỗi frame — và trên
`SpikeBush` còn **thấy rõ viền trắng** do lá mỏng hai mặt làm z-fight. Cả 11 material visual của dự án đều
đang ở Stroke Width 0.

#### Prompt đã dùng

Chủ dự án yêu cầu outline **ẩn hoàn toàn và không tốn draw call** khi Stroke bằng 0. Sau bản sửa
đầu tiên, họ gửi ảnh chụp chứng minh viền trắng **vẫn hiện** quanh `SpikeBush` dù Inspector báo Stroke
Width 0. Sau khi bản sửa thứ hai xử lý được, họ yêu cầu đi xa hơn: bỏ hẳn outline khỏi shader mà mọi
visual đang dùng, và giữ một bản sao có outline đầy đủ cho sau này.

#### Phản hồi quan trọng của AI

Lần sửa đầu dùng `Material.SetShaderPassEnabled("OutlinePass", false)` — API chính
thức của Unity — và đã verify cờ ghi đúng vào `disabledShaderPasses` của từng `.mat`. **Ảnh chụp của chủ dự
án chứng minh outline vẫn vẽ.** Nguyên nhân: pass gắn tag `LightMode="SRPDefaultUnlit"`, và URP coi tag này
là đường fallback tự động, đường đó **không tôn trọng cờ bật/tắt pass của material**. Điều này được xác nhận
bằng thực nghiệm chứ không phải suy đoán: đọc lại `GetShaderPassEnabled` trên material sống thì nó đúng là
`false`, trong khi Scene View vẫn hiện viền trắng.

Bản sửa thật nằm **trong shader**: vertex shader của outline pass set `o.pos = float4(0,0,0,0)` khi
`_StrokeWidth <= 0.0001`, clip tam giác về một điểm trước khi rasterize ⇒ **0 fragment**, bất kể SRP có tôn
trọng cờ hay không.

#### Phương án chọn / sửa / loại

- **Chọn:** clip ở tầng vertex làm cơ chế bảo đảm đúng.
- **Loại:** coi `SetShaderPassEnabled` là bản sửa chính — đã chứng minh bằng thực nghiệm là không đủ.
- **Chọn:** tách hai shader, **giữ nguyên tên và file gốc** cho bản không outline ⇒ 11 material của dự án
  không phải sửa gì.

#### Lý do

Một bản sửa ở tầng shader không phụ thuộc vào việc SRP hiện tại có tôn trọng một API cụ thể hay
không, nên nó đúng trên cả Scene View, Game View lẫn build.

#### Kết quả sau khi triển khai

Audit toàn bộ material xác nhận phân bổ đúng như ý định: 11 material dự án + 2 material demo ở
Stroke 0 giữ shader cũ; 8 material demo có Stroke khác 0 chuyển sang `ToonShader Outline` và **giữ nguyên
giá trị Stroke**. Ảnh chụp 4 góc trước/sau xác nhận viền trắng biến mất. **Giới hạn được ghi rõ:** chưa
profile để chứng minh giảm số SetPass call ở phía CPU.

---

### 4.2 Probe: vì sao APV đang là chi phí không đem lại gì

#### Vấn đề đang gặp

Chủ dự án hỏi cách đặt probe cho object động trên một scene đã bake, và đưa một cấu hình
Adaptive Probe Volumes nhờ review.

#### Prompt đã dùng

Chủ dự án yêu cầu chỉ cách đặt probe cho object động trên một scene **đã bake**, không dùng công cụ
ngoài mà chỉ dùng hỗ trợ sẵn của Unity; sau đó đưa cấu hình Adaptive Probe Volumes đang có và hỏi
nó đã đặt đúng chỗ chưa.

#### Phản hồi quan trọng của AI

Một phân biệt quyết định toàn bộ câu trả lời: **Light Probe Group đổ dữ liệu vào
hằng số `unity_SH*` của từng renderer — đó chính là thứ một lệnh `SampleSH(N)` đọc. Adaptive Probe Volumes
thì không**: APV bắt shader phải lấy mẫu APV một cách tường minh và không đi qua `unity_SH*`. Shader viết
theo `SampleSH` vì thế **không nhận được gì từ APV**, trong khi volume vẫn ăn trọn ngân sách bộ nhớ.

Số đo kèm theo: 56 renderer đọc `SampleSH` (41 MeshRenderer + 15 SkinnedMeshRenderer, toàn bộ ToonShader),
tất cả đặt `BlendProbes`, dữ liệu Light Probe Group = **0 probe**, APV giữ chỗ 512 + 256. Hệ quả: cả 56
renderer rơi về **một màu ambient phẳng của skybox** — nhân vật bước vào bóng râm vẫn sáng y như đứng ngoài
nắng.

#### Phương án chọn / sửa / loại

 Ba lựa chọn được đặt lên bàn và **AI không tự áp cái nào**: (A) quay lại Light Probe Group,
đặt probe group, đổi 183 renderer sang Receive GI = Light Probes rồi bake lại; (B) bỏ bake hoàn toàn; (C)
dạy `RoadShadowOverlay` lấy mẫu shadowmask — công sức lớn nhất nhưng lợi nhất, vì có thể cắt pass 45 caster
xuống chỉ còn nhân vật động.

#### Lý do

Đây là quyết định đánh đổi chất lượng hình ảnh với ngân sách mobile — thuộc về chủ dự án, không
thuộc về AI.

#### Kết quả sau khi triển khai

Entry này kết thúc bằng **một quyết định để ngỏ có chủ ý**, kèm đủ số đo để chọn. Nó cũng chỉ
ra "khuyết điểm hình ảnh rõ nhất của dự án" một cách có bằng chứng chứ không phải cảm tính.

---

### 4.3 SRP Batcher và GPU instancing loại trừ nhau: phương án A′

#### Vấn đề đang gặp

Chủ dự án yêu cầu rà soát những chỗ dùng material property làm hỏng batching, và liệt kê việc
cần làm để giảm draw call.

#### Prompt đã dùng

"check lại ở những nơi dùng material propeties, thì nên dùng sao cho GPU instancing hoạt động…"
→ sau đó "Cách A đi" → rồi "A' đi".

#### Phản hồi quan trọng của AI

Cách A (bật instancing thẳng trên ToonShader) sẽ **đá 91 renderer trên 74 prefab ra
khỏi SRP Batcher**, vì hai cơ chế này loại trừ nhau. Chứng cứ nằm ngay trong comment của chính shader đó:
việc này **đã từng được thử và đã bị revert**. AI **tự sửa lại đề xuất của mình** thành A′ sau khi đọc được
dấu vết đó.

#### Phương án chọn / sửa / loại

- **Chọn:** A′ — tách một `ToonShaderInstanced.shader` riêng; chỉ 14 material thật sự cần
  `MaterialPropertyBlock` chuyển sang; 34 material còn lại **giữ nguyên SRP Batcher**.
- **Loại:** A, dù chính AI đã đề xuất ở lượt trước và chủ dự án đã duyệt.

#### Lý do

Mỗi nhóm material đi đúng con đường nhanh của nó, thay vì ép cả dự án vào một cơ chế.

#### Kết quả sau khi triển khai

14 material chuyển, 34 material giữ SRP Batcher. Bài học đắt nhất của entry này không phải kỹ
thuật: **một đề xuất đã được duyệt vẫn phải bị rút lại nếu tìm ra bằng chứng nó sai.**

---

### 4.4 Prewarm: một hiệu ứng chỉ được "hâm nóng" khi nó thật sự được vẽ

#### Vấn đề đang gặp

Effect lần đầu play gây khựng, và shader hiện màu xanh vì chưa kịp compile ở lần render đầu tiên.

#### Prompt đã dùng

"tôi cần khi vào level, cache trước tất cả các effect có thể xuất hiện trong game bằng cách play nó trước
camera để nó render" → "cách A, tôi muốn cách A vào lúc vừa vào Game luôn được không, hay buộc phải vào level
mới làm được" → "ok hybrid, make sure camera ở boot phải giống setting với level".

#### Phản hồi quan trọng của AI

Warm PSO của Vulkan cần **một draw call thật sự được submit**: nằm trong frustum, sống qua một frame thật, có
particle đang chạy. Instantiate rồi huỷ ngay **không warm được gì**. Và vì format render target cùng MSAA là
một phần của **định danh PSO**, camera lúc boot phải trùng setting với camera level — nếu không thì thứ được
warm là một PSO khác, và khựng vẫn xảy ra nguyên vẹn.

#### Phương án chọn / sửa / loại

- **Chọn:** hybrid — warm ở boot, bổ sung ở level.
- **Chọn:** `RuntimeWarmupView` vẽ 18 VFX vào một RenderTexture ngoài màn hình, rải vị trí để không bị depth
  loại bỏ; warm thêm 7 prefab enemy theo cùng đường, với `SilenceScripts` tắt MonoBehaviour để chúng chỉ được
  *vẽ* chứ không chạy logic thật.
- **Chọn:** `WarmupCameraSync` (menu `Tools/Tower Defense/Sync Effect Warmup Camera`) cộng test
  `WarmupCameraMatchesLevelsTests`, để camera boot không âm thầm trôi khác camera level về sau.
- **Sửa theo owner:** tách `SyncFromMenu()` (có hỏi) khỏi `Sync()` (chạy được từ script) sau khi owner nhắc
  "i hit dont save for you".

#### Lý do

Một bản warm không kèm test là một bản warm sẽ hỏng trong im lặng ở lần ai đó chỉnh camera tiếp theo — và nó
hỏng theo kiểu không có triệu chứng nào ngoài "game hơi giật lúc đầu", tức gần như không bao giờ bị bắt.

#### Kết quả sau khi triển khai

Chính test mới phát hiện **Level_001 bật FXAA còn Level_002–010 để None** — một lệch cấu hình không ai biết;
owner chốt "Bật FXAA cho cả 10". Hai lỗi tự gây phải sửa: assembly Editor âm thầm giữ DLL cũ do thiếu
reference URP/core-RP, và `MissingReferenceException` do đọc camera sau `DestroyImmediate`. Cách xác minh "DLL
có tồn tại" bị thay bằng so mtime DLL với source mới nhất — đúng bài học mục 3.7.

---

## 5. Gameplay và UI

### 5.1 Tách "đã qua màn" khỏi "đã mở khoá"

#### Vấn đề đang gặp

Trụ Hero phải khoá cho tới khi người chơi qua Level 7, nhưng `UnlockProgress` chỉ theo dõi level
đã mở, mà `LevelMenuFlow` lại mở khoá một level **ngay khi người chơi chạm nút** của nó. Không có gì trong
dự án ghi lại rằng một màn đã **thắng** thật.

#### Prompt đã dùng

Chủ dự án yêu cầu trụ Hero bị khoá lúc đầu và **mở khoá khi người chơi qua được Level 7**.

#### Phản hồi quan trọng của AI

Đọc "đã mở level 8" như một proxy cho "đã qua level 7" sẽ **vỡ ở màn cuối cùng** và
sẽ sai ngay khi luật mở khoá thay đổi. Một tập "đã qua" riêng mới là bản ghi trung thực.

#### Phương án chọn / sửa / loại

- **Chọn:** thêm tập cleared vào `UnlockProgress`; qua màn cũng đồng thời mở khoá màn đó.
- **Chọn:** thêm `clearedLevelNumbers` vào `SaveSnapshot` như **field cộng thêm** và **giữ `schemaVersion`
  = 1**, để save cũ deserialize ra tập rỗng thay vì fail validation và **xoá sạch tiến trình người chơi**.
- **Loại:** nâng schema version.

#### Lý do

Menu cố tình mở khoá theo yêu cầu, nên "mở khoá" không thể mang nghĩa "tiến trình". Giữ nguyên
schema quan trọng hơn sự gọn gàng của field, **vì save là thứ duy nhất ghi lại tiến trình của người chơi.**

#### Kết quả sau khi triển khai

`TryMarkClearedAndSave` chỉ ghi khi level chưa từng cleared, nên chơi lại không tốn lượt ghi.
227 EditMode test, gồm cả round-trip save và khôi phục save hỏng, pass nguyên.

---

### 5.2 Hiệu ứng mất máu không chạy: state bị xoá, không phải tween hỏng

#### Vấn đề đang gặp

Cóc mất máu thì thanh máu tụt nhưng **không** shake, **không** flash đỏ.

#### Prompt đã dùng

"không thấy animation dotween của level Status khi cóc mất máu?… có thể chưa register vcontainer
hay gì đó". Sau vài lần AI đoán sai, chủ dự án đưa ra chỉnh sửa quyết định: *"health change vẫn được gọi vì
tôi thấy máu có giảm, có rút, nhưng ko thấy effect"*.

#### Phản hồi quan trọng của AI

AI đoán sai vài lần (timeScale, logic `tookDamage`, nhiều instance HUD). Thứ phá
được thế bế tắc là **log `[HUD-DEBUG]` do chủ dự án chạy**: `previousHealth=0 hasRendered=False` ở **mọi**
lần render. Máu vẫn về đúng, nghĩa là state so sánh bị **reset mỗi frame** — không phải animation hỏng.
Thủ phạm: `WaveThreeDefeatHudPresenter.Refresh()` gọi `view.HideWaveThreeDefeat()` mỗi frame, và hàm đó reset
`hasRenderedHealth`/`previousHealth`.

#### Phương án chọn / sửa / loại

- **Chọn:** guard `isShowingWaveThreeDefeat` — `HideWaveThreeDefeat` chỉ làm việc khi thật sự đang hiện.
- **Loại:** mọi hướng đụng vào DOTween hoặc VContainer registration (giả thuyết ban đầu của *cả hai bên*).

#### Lý do

Sửa đúng nguyên nhân (state bị clobber) giữ nguyên contract của presenter; sửa ở tầng animation
sẽ chỉ che triệu chứng.

#### Kết quả sau khi triển khai

Shake và flash chạy lại; thêm `LevelStatusHudDamageTests`. Entry này là ví dụ rõ nhất trong dự
án về việc **một dòng log của người dùng có giá trị hơn ba vòng suy đoán của AI**.

---

### 5.3 Đợt tối ưu UI và hai cái bẫy của nó

#### Vấn đề đang gặp

UI chưa thống nhất TMP, `raycastTarget` bật tràn lan, atlas gộp chung một cục, phần đổi mỗi
frame nằm chung canvas lớn.

#### Prompt đã dùng

"thống nhât TMP, và tắt raycastTartget, giữ thứ tự sibbling hoặc đổi cách gộp sprite atlas, Tách
atlas theo màn hình. Tách thứ đổi mỗi frame ra sub-canvas riêng" — sau đó chỉnh: *"cân nhắc nó có cần click
hay không, nếu cần thì không tắt"*.

#### Phản hồi quan trọng của AI

Bộ lọc `raycastTarget` đầu tiên **sẽ tắt cả backdrop của modal** (`Pause Root`,
`Outcome Root`, `Loading`, `Blocking Error`) ⇒ tap xuyên qua modal xuống gameplay. Phát hiện ở bước **dry
run**, trước khi ghi.

#### Phương án chọn / sửa / loại

 56 `Text` → `TMP_Text`; 26 `raycastTarget` tắt nhưng **giữ mọi blocker**; 3 sub-canvas cho
phần đổi mỗi frame; tách thật atlas thành Gameplay/Menu/Shared (35/7/12 texture).

#### Lý do

Tắt raycast phải theo **vai trò thật** của từng element chứ không theo một lượt quét máy móc — một
backdrop của modal trông y hệt một ảnh trang trí cho tới lúc tap xuyên qua nó. Và mọi thao tác ghi
hàng loạt lên asset Unity phải đọc ngược lại để xác nhận, vì API Editor thất bại trong im lặng.

#### Kết quả sau khi triển khai

1. `SerializedProperty` **âm thầm từ chối** 35 phép gán vì script chạy trước khi Unity recompile xong phần
   đổi kiểu field — con số "rewired=33" chỉ là số lần gọi hàm, không phải số lần gán thành công. Hậu quả:
   crash lúc boot (`TutorialOverlayView requires fully authored UI references`, NRE `LoadingView.Show`).
   Sửa bằng cách chạy lại sau compile **có đọc ngược để verify** (35/35).
2. `enableAutoSizing` bật kèm `fontSizeMin/Max = 0` làm **mất toàn bộ chữ tutorial**, do không mang theo
   `resizeTextMinSize/MaxSize` khi chuyển từ `Text`.
3. `SpriteAtlasExtensions.Remove/Add` **không persist** cho `.spriteatlasv2`; phải sửa packable GUID thẳng
   trong YAML — nếu không, cả ba atlas vẫn pack toàn bộ thư mục và **gấp ba RAM** thay vì tiết kiệm.

Bài học: mỗi lần AI "đã làm xong" trên asset Unity đều phải **đọc ngược lại giá trị đã ghi**, vì API của
Editor thất bại trong im lặng.

---

### 5.4 Trụ đặt sẵn bị lún: một luật đã có nhưng không dùng chung

#### Vấn đề đang gặp

Trụ nước đặt sẵn bằng Board Painter ở Level 8 lún khoảng 1.0 đơn vị xuống dưới mặt ô.

#### Prompt đã dùng

"Trụ nước đặt sẵn ở level 8 bị lún xuống đất (đặt ở board painter)" — kèm ảnh chụp.

#### Phản hồi quan trọng của AI

Hai đường đặt trụ **không giống nhau**: `TowerInstanceFactory` (đặt từ card, runtime)
nâng instance cho **đáy renderer** chạm mặt ô, còn `BoardSceneSynchronizer` (board painter, authoring) chỉ
đặt **pivot** lên mặt ô. Mesh trụ được author quanh pivot nên lún đúng nửa chiều cao.

Điều đáng nói: **luật này đã tồn tại từ 25/08** (xem lịch sử: Water cần nâng `1.015`, Earth `0.852`, các trụ
khác `0.000` — đúng những con số đo lại được hôm nay). Nó chỉ chưa bao giờ được đường authoring dùng chung.

#### Phương án chọn / sửa / loại

- **Chọn:** tách `TowerSurfaceAlignment` dùng chung cho **cả hai** đường, đo đáy phần *thực sự được vẽ* (bỏ
  renderer tắt và object inactive, chạy được cả trên prefab asset).
- **Chọn:** cộng lift trong `BoardGeometryPlanner` chứ **không** lúc tạo instance — để
  `HasMatchingAuthoredTowers` vẫn nhận instance là up-to-date, tránh sync lại mỗi lượt.

#### Lý do

Một luật seating duy nhất cho cả hai đường thì trụ đặt sẵn đứng y như trụ người chơi xây. Cộng lift
ở planner thay vì lúc tạo instance là để phần so khớp "instance có còn đúng không" vẫn đọc được cùng
một con số, nếu không mỗi lần sync sẽ phá và dựng lại toàn bộ trụ.

#### Kết quả sau khi triển khai

Sau sync: pivot `-2.215`, đáy mesh `-3.23` = đúng mặt ô, khớp chính xác số đo của 25/08. Quét cả
10 level: chỉ Level 8 dính. Phát hiện kèm: helper đo silhouette trong test **vẫn đếm biển cảnh báo đang
inactive** (thấp hơn mesh ~5 mm) và chính nó đang quyết định cao độ đặt trụ — đã lọc theo `activeInHierarchy`.

---

### 5.5 Giới hạn trụ Hero: từ hardcode sang dữ liệu đã author

#### Vấn đề đang gặp

Cần "mỗi màn chỉ đặt được 1 trụ cua, bán rồi đặt lại được".

#### Prompt đã dùng

"trụ crab/hero là mỗi level chỉ được đặt 1 trụ, có thể bán và đặt lại, max là 1 trụ". Sau khi AI
nêu ra field dữ liệu bị bỏ quên, chủ dự án chốt: "CrabHero: 1 dùng, còn soulnexus instance set 0,
tức ko limit, vẫn dùng."

#### Phản hồi quan trọng của AI

Nhịp một, AI cài luật bằng hằng số `HeroTowerLimit = 1` gắn với family
Hero. Nhịp hai, khi đọc asset cho một việc khác, AI phát hiện `TowerEconomyProfile.MaxInstancesPerLevel`
**đã được author sẵn** (`CrabHero: 1`, `SoulNexus: 1`, còn lại 0) nhưng **chưa từng được dùng** ở đâu ngoài
validation. AI **không tự ý chuyển**, vì chuyển sang đọc dữ liệu sẽ đồng thời giới hạn Soul Nexus còn 1 trụ
mỗi màn — một thay đổi gameplay chủ dự án không yêu cầu. Nó nêu vấn đề và hỏi.

#### Phương án chọn / sửa / loại

- **Chọn (sau khi chủ dự án quyết):** đọc `MaxInstancesPerLevel`, `0` = không giới hạn; đếm theo
  **definition** chứ không theo family; `SoulNexus` set về 0.
- **Loại:** âm thầm áp cap cho Soul Nexus.

#### Lý do

Một hằng số trong code nằm cạnh một field dữ liệu bị bỏ quên là mầm mống của lỗi sau này; nhưng
việc "dùng dữ liệu" lại có hệ quả gameplay, nên nó là quyết định của chủ dự án chứ không phải của AI.

#### Kết quả sau khi triển khai

Không còn chỗ nào hardcode "hero" hay số 1. Test assert **dữ liệu nói 1** trước khi assert luật
thực thi 1; thêm chốt chặn đặt 2 Soul Nexus và assert không bị chặn — nếu sau này ai set lại cap cho sink,
test đỏ ngay.

**Lỗi thật lộ ra khi viết test:** `TrySellSelected` **chưa bao giờ gỡ node khỏi network** — node chỉ rời đi
khi `OnDestroy` của view chạy, tức **cuối frame** ở play mode. Trụ vừa bán vẫn là node sống hết frame đó, và
với hero thì **một con cua vô hình vẫn tiếp tục tấn công**. Đã thêm `TowerRuntimeViewRegistry.Unregister` và
gỡ ngay tại chỗ bán.

---

### 5.6 Stun của cua, đặt trong một mô phỏng tính trước

#### Vấn đề đang gặp

Cần cua gây stun 2.5s lên quái trúng đòn, sau khi hết stun phải 1s sau mới stun lại được; đang
stun thì gió không đẩy được; boss miễn nhiễm, mini-boss chỉ 1.25s.

#### Prompt đã dùng

Ba lượt yêu cầu nối tiếp nhau: "cua khi tấn công thì gây stun các kẻ địch dính đòn, stun 2.5s, sau
khi bị stun xong thì 1s sau mới có thể trúng stun tiếp"; rồi "trong lúc bị stun thì enemy không thể
nhận đẩy lùi của gió"; rồi "boss ko thể bị stun, mini boss bị stun 1.25s".

#### Phản hồi quan trọng của AI

Combat của game là **timeline tính trước** (`CombatTimelinePlanner` chạy shadow
simulation rồi bake ra frame), nên stun **phải sống trong shadow sim** — nếu cài như một hiệu ứng ở view thì
timeline sẽ lệch với thứ người chơi thấy. AI mượn đúng khuôn của hiệu ứng **WaterLift** đã có (xem mục 3.1):
`EndTick` + `ImmuneUntilTick`, cùng lý do tồn tại của cửa sổ miễn nhiễm.

#### Phương án chọn / sửa / loại

- **Chọn:** số liệu (`2.5` / `1`) nằm ở `HeroTowerDefinition` để designer chỉnh, không hardcode.
- **Chọn:** guard chặn đẩy lùi đặt **trong** `ApplyPush` chứ không ở call site, để mọi nguồn đẩy sau này tự
  động tuân theo.
- **Chọn:** thêm cờ `IsStunned` chạy tới view — không có nó, quái bị giữ vẫn **chạy animation đi bộ tại
  chỗ**, nhìn như lỗi.
- **Chọn:** validation từ chối stun không có cửa sổ miễn nhiễm — cua đánh mỗi 2s mà giữ 2.5s, thiếu khoảng
  nghỉ là tái tạo đúng cái khoá vĩnh viễn của mục 3.1.

#### Lý do

Boss là trận đánh mà cả màn được dựng quanh nó; một con boss bị ghim tại chỗ thì không còn là
trận đánh. Mini-boss giảm nửa, đúng như knockback đã làm với chúng từ trước.

#### Kết quả sau khi triển khai

355/355 EditMode test pass. Ba test mới: quái thường được thả **đúng lúc** hết 2.5s *dù cú đánh
thứ hai đã rơi vào giữa lúc bị giữ* (đây mới là thứ chứng minh khoảng nghỉ hoạt động); mini-boss được thả
đúng mốc nửa; boss **không một tick nào** bị giữ nhưng máu vẫn tụt — để test không thể pass chỉ vì cua đánh
trượt.

---

### 5.7 Thanh máu cóc: sai hình học, không phải sai vài pixel

#### Vấn đề đang gặp

Phần xanh của thanh máu không lấp kín lòng máng, lộ nền nâu quanh mép. Sau đó, khi owner tự thêm border để
9-slice cho hai đầu bo tròn khỏi bị kéo giãn, nó "không hoạt động".

#### Prompt đã dùng

"sửa thanh máu cóc sao cho phần xanh nằm trong background của nó mà ko hở như hình 1, hình 2 đang hở"; rồi
"thanh máu đang stretch nên nhìn nó kỳ, tôi đã thêm border để stretch ra thì vẫn giữ nguyên 2 đầu, nhưng ko
hoạt động?"

#### Phản hồi quan trọng của AI

Hai chẩn đoán, cùng một gốc là **hình học chứ không phải số**:

- Đo pixel thẳng từ atlas: lòng máng là một **viên nhộng bo tròn** (x 13..585, y 12..80), còn sprite xanh là
  **hình chữ nhật đặc** 648×158. Một hình chữ nhật không bao giờ vừa kín một lòng bo tròn — nới cho hết hở ở
  giữa thì bốn góc đè lên viền vàng, thu lại cho gọn góc thì hở giữa. **Cả hai lỗi đang xảy ra cùng lúc**: hở
  9px trên/dưới, góc vượt ra 5px ở hai đầu.
- `Image.Type.Filled` **bỏ qua sprite border**, nên chừng nào còn dùng `fillAmount` thì 9-slice không thể có
  tác dụng — border owner thêm vào là đúng, chỉ là không ai đọc nó.

#### Phương án chọn / sửa / loại

- **Chọn:** cho phần xanh **mượn hình dáng của khung** — thêm `Health Cavity` làm `Mask` dùng chính sprite
  lòng máng, `Health Fill` thành con của nó và kéo đầy.
- **Chọn:** bỏ `fillAmount`, chỉ dịch `anchorMax.x` theo tỉ lệ máu (3 dòng trong `LevelStatusHudView`), giữ
  `anchorMin` ở 0 ⇒ hai đầu bo tròn giữ đúng bán kính đã author.
- **Loại:** chỉnh số rect cho vừa — đã tính và **mọi** cấu hình chữ nhật đều để lại hở hoặc tràn.

#### Lý do

Khi một bài toán không có nghiệm trong không gian đang tìm (chữ nhật trong hình bo tròn), thêm một vòng chỉnh
số chỉ là đổi chỗ lỗi. Phải đổi loại hình, không phải đổi kích thước.

#### Kết quả sau khi triển khai

`healthFill` vẫn trỏ đúng component sau khi đổi cha (kiểm bằng `SerializedObject`), và script authoring không
wire trường này nên một lần rebuild không phá cấu trúc mới. Đo lại khoang chứa còn chỉnh được một con số sai
từ trước: vùng trong của art là 201×17.5 đơn vị chứ không phải 209.5×24.5 như cấu hình cũ — chính chỗ đó làm
phần xanh tràn ra ngoài viền vàng.

---

### 5.8 Trạng thái khoá của thẻ trụ không thể sống trong code

#### Vấn đề đang gặp

Thẻ trụ bị khoá hiển thị thừa (đồng xu, chữ LOCK, placeholder) và màu do script đặt thì **không bao giờ ăn**.

#### Prompt đã dùng

"when lock, just gray the icon, and dont show coin, dont show lock…" → "bỏ icon sau luôn, khóa bây h là icon,
còn màu nên vẫn giữ màu sắc, chỉ là nó tối hơn thôi".

#### Phản hồi quan trọng của AI

`Button.transition = ColorTint` **ghi đè `targetGraphic.color`** mỗi lần trạng thái đổi, nên mọi màu set bằng
script đều bị xoá ngay sau đó. Trạng thái khoá vì thế phải sống trong `colors.disabledColor` — tức trong dữ
liệu của Button — chứ không phải trong code tô màu.

#### Phương án chọn / sửa / loại

- **Chọn:** để `disabledColor` quyết định độ tối; gỡ `LockedTint`, `LockedIconTint`, `CacheUnlockedColors` và
  toàn bộ field cache màu; `ApplyLockedVisibility` chỉ còn bật/tắt `.enabled`.
- **Loại:** mọi cách tô màu bằng script — đã chứng minh là luôn bị `ColorTint` ghi đè.

#### Lý do

Đây là cùng một họ với lỗi nút pause ra "ô xám": `normalColor = (0.08, 0.11, 0.10)` sót lại từ `StyleButton`
nhân một lớp gần đen lên sprite. Khi Unity đã sở hữu một property, việc ghi đè nó từ bên ngoài không sai ở lần
chạy đầu — nó chỉ sai ở mọi lần sau.

#### Kết quả sau khi triển khai

Thẻ khoá từ "tan nhợt" (do `disabledColor` alpha 0.502) về `(0.44, 0.44, 0.44, 1)`. Và một chẩn đoán sai
được ghi lại đầy đủ: việc **mất card background** ban đầu bị đổ oan cho sprite atlas (đã nâng lên 4096, không
đổi gì, phải revert); nguyên nhân thật là `CardBackground.png` bị cap `maxTextureSize` 512 ở Android nên chỉ
còn 35% kích thước. Sau hai lần thử 9-slice hỏng, AI **tự đính chính** lần nữa: vòng ring nằm cách mép 10–20px,
tức nằm trong border 42, nên slice vẫn đúng — thứ tưởng là artifact thật ra là ring render đúng.

---

### 5.9 Số quái còn lại: một công thức, đặt ở nơi duy nhất biết kế hoạch

#### Vấn đề đang gặp

Ô số cạnh icon nhóm quái luôn hiện `00` lúc chuẩn bị, và owner muốn nó không được reset về 0 rồi đếm lên khi
spawn.

#### Prompt đã dùng

"sao lại 00, tôi muốn nó hiện số enemy sẽ xuất hiện trong wave tiếp theo" → "ko đợi spawn ra mới hiển thị,
phải hiển thị trước luôn, và khi bấm start wave, số đó ko được reset về 0 rồi tăng lên khi spawn, chỉ giảm
khi enemy chết thôi, trừ TH enemy là sommoner boss thì mới tăng."

#### Phương án chọn / sửa / loại

Đưa phép tính vào `WaveSystem` — **nơi duy nhất biết kế hoạch spawn** — dưới dạng `RemainingEnemyCount`:

```
đang chạy:  (chưa spawn) + (đang sống) = currentPlan.Count − nextSpawnIndex + LivingCount
chuẩn bị:   tổng roster của wave sắp tới
```

#### Lý do

Cả bốn yêu cầu rơi thẳng ra từ công thức, **không cần một case riêng nào** — kể cả ngoại lệ summoner: quân
triệu hồi vào qua `SpawnPlannedSummon` của combat timeline nên không nằm trong wave plan, vì thế nó cộng vào
"đang sống" mà chưa từng bị trừ khỏi hàng chờ. Một yêu cầu nghe như bốn luật hoá ra là một định nghĩa.

#### Kết quả sau khi triển khai

Rủi ro duy nhất là số nhảy đúng lúc bấm START nếu `plan.Count ≠ tổng roster` — kiểm **93 wave của cả 10
level**: 0 sai lệch. Nhịp cập nhật thì đã có sẵn (`EnemySpawned` / `EnemyKilled` / `EnemyLeaked`), không phải
thêm gì.

---

### 5.10 Nâng cấp trụ: nhân vào payload, không đụng nhịp

#### Vấn đề đang gặp

Cần nâng cấp trụ bằng vàng, tăng chỉ số, và trụ chưa hề có khái niệm "cấp".

#### Prompt đã dùng

"dùng icon coin và code, để sửa UI khi click vào tower thành, có nâng cấp, sell, và unlink, nâng cấp tạm tăng
stats thôi (sửa từ SO datadriven)" → "2 level thôi, nâng cấp được lần 1, và lần 2 là hết" → "không nâng cấp
được thì xóa cái icon tiền luôn đi".

#### Phản hồi quan trọng của AI

`ElementUpgradeCostProfile` và `LinearUpgradeCost` đã nằm sẵn trong SO nhưng **chưa có cơ chế nâng cấp nào
được cài** — giống hệt tình huống `MaxInstancesPerLevel` ở mục 5.5. Đây là một tính năng thật, không phải một
lượt chỉnh UI.

#### Phương án chọn / sửa / loại

- **Chọn:** `TowerUpgradeProfile` mới (`maxLevel`, `costPerLevel`, `damageBonusPerLevel`).
- **Chọn:** cấp **nhân vào payload**, không đụng chu kỳ — nhờ vậy mô phỏng không cần biết "cấp" là gì, nó vẫn
  đọc đúng payload như cũ.
- **Chọn:** sát thương thiêu nhân theo nhưng **giữ nguyên thời lượng và nhịp** — nâng cấp nên tăng sát thương,
  không âm thầm viết lại các mốc thời gian mà luật phản ứng nguyên tố đang cân bằng theo.
- **Chọn:** trừ vàng **sau** khi mạng chấp nhận, để một lần từ chối không lấy mất tiền mà không trả cấp.
- **Chọn:** `UpgradeShowsPrice` nằm trong state thay vì để view đoán từ chuỗi — `MAX` là một *trạng thái*,
  không phải một *giá*; đặt đồng xu cạnh nó cũng sai như đặt cạnh ô trống.
- **Chọn:** SoulNexus `maxLevel = 0` — nó không gây damage nên một cấp chẳng mua được gì.

#### Lý do

Mọi thứ chạm vào nhịp đều chạm vào cân bằng phản ứng nguyên tố (mục 3.1 và 5.6 đã trả giá cho điều đó). Nhân
vào payload là cách duy nhất tăng sức mạnh mà không phải cân bằng lại toàn bộ timeline.

#### Kết quả sau khi triển khai

Sau khi owner chốt 2 cấp: Generator 110/220, nguyên tố 100/200, Hero 150/300, damage ×1.35 → ×1.70, đọc ngược
qua runtime API để xác nhận. Lỗi tự bắt khi render: hộp icon vuông 38×38 với `preserveAspect` khiến icon
RESUME (tỉ lệ 0.88) cao đủ 38 còn restart/home (1.10) chỉ cao 34 — đổi hộp thành 60×42 để chiều cao luôn là
cạnh quyết định. Khoảng hở xu↔số ban đầu là **âm 1px** (chồng nhau), không phải "hơi gần" như mô tả.

---

### 5.11 Một câu hỏi, ba hệ thống trả lời

#### Vấn đề đang gặp

Kéo link vào một trụ **ngoài tầm** vẫn hiện màu xanh; chỉ tới lúc thả mới im lặng không nối được.

#### Prompt đã dùng

"khi kéo vào 1 trụ out range vẫn xanh link nhưng khi thả ra thì ko attach link (tôi muốn out range thì đỏ
link luôn)"; rồi "ko ý tôi là chặn kéo thả link trong lúc wave luôn, h vẫn kéo thả link được, chỉ là link ko
ăn thôi"; rồi "khi select hoặc kéo thả crab nó đang hiện tầm link, tôi muốn tầm link của crab là tầm đánh 4m".

#### Phản hồi quan trọng của AI

Preview và lúc thả **đang hỏi hai luật khác nhau**, nên chúng có quyền bất đồng — và chúng bất đồng thật.
Cổng kiểm tra được rút về một chỗ dùng chung `TryOpenLinkGate`, để `CanLink` (preview) và `TryRewire` (thả)
không thể trả lời khác nhau nữa. Vòng range lúc kéo còn đang hardcode 12m, thay bằng `DescribeRangeMeters`
để cả hai đường hiển thị cùng một con số.

#### Phương án chọn / sửa / loại

- **Chọn:** rút luật chung, thay vì copy điều kiện tầm sang chỗ preview.
- **Chọn:** chặn tại `CanStartLinkFrom` **lúc bắt đầu cử chỉ** chứ không lúc thả — đúng yêu cầu "chặn kéo thả
  link trong lúc wave luôn"; kéo được rồi mới báo hỏng vẫn là một trải nghiệm hỏng.
- **Chọn:** `DescribeRangeMeters` trả `hero.AttackRangeMeters` cho Hero, nên cả ba yêu cầu về cua rơi vào
  đúng chỗ vừa gom.

#### Lý do

"Một câu hỏi, ba hệ thống trả lời" là kiểu lỗi lặp lại nhiều nhất trong dự án — mục 5.4 ở tầng đặt trụ, mục
3.3 và 3.6 ở tầng asset, và ở đây là tầng luật. Gom về một nguồn là cách duy nhất để chúng không trôi khác
nhau theo thời gian.

#### Kết quả sau khi triển khai

Sau khi gom, ba yêu cầu tiếp theo của owner về trụ cua (tầm hiển thị, cấm link vào/ra, cấm kéo khi wave chạy)
được xử lý **trong cùng một chỗ** chứ không phải ba chỗ — đó là bằng chứng thực tế rằng lần gom này đúng.

---

## 6. Quy trình

### 6.1 Chia lại một commit khổng lồ theo feature

#### Vấn đề đang gặp

Một commit gom tutorial, HUD, retry, save và asset vào cùng một chỗ; working tree sau đó lại chứa
nhiều feature chưa commit.

#### Prompt đã dùng

Chủ dự án yêu cầu commit toàn bộ **theo feature**, subject ngắn theo quy ước trong `README.md`,
không có dòng attribution AI, đồng thời chia lại commit lớn đã lỡ gom nhiều thứ và push đè lên đúng
remote được chỉ định.

#### Phản hồi quan trọng của AI

Trước khi rewrite, AI kiểm tra branch, remote URL, commit graph và toàn bộ trạng thái
tracked/untracked, sau đó **tạo backup ref local**. Commit cũ được dựng lại thành nhiều commit feature **có
cùng tree hash cuối cùng**, rồi replay các commit sau lên lịch sử mới.

#### Phương án chọn / sửa / loại

 Rebuild từ đúng parent của commit lớn ⇒ so được tree hash trước/sau; `--force-with-lease` tới
đúng remote được chỉ định, **không đụng** remote còn lại.

#### Lý do

So tree hash là cách duy nhất chứng minh một lần viết lại lịch sử **không làm mất nội dung**.

#### Kết quả sau khi triển khai

Lịch sử được chia theo feature, nội dung cuối cùng không đổi, có đường lùi local nếu push hỏng.
Quy ước commit (prefix conventional, subject tiếng Việt một dòng, không trailer AI) được ghi vào `README.md`
và áp dụng cho toàn bộ các commit sau đó.

---

### 6.2 Một camera debug bị lưu nhầm vào scene

#### Vấn đề đang gặp

Chủ dự án báo "camera level 8 có vẻ bị lỗi".

#### Prompt đã dùng

"và camera level 8 có vẻ bị lỗi".

#### Phản hồi quan trọng của AI

AI tra ra **đây là lỗi do chính nó gây ra**: một `__InspectionCamera` tạo tạm để
chụp ảnh kiểm tra trụ nước (mục 5.4) đã bị lưu vào `Level_008.unity`, khiến scene có **hai camera enabled**.

#### Phương án chọn / sửa / loại

 Mở Level 8 **additive** để xoá camera thừa rồi lưu — không đụng scene Bootstrap mà chủ dự án
đang mở và **chưa save**.

#### Lý do

Sửa lỗi của mình không được phép tạo ra lỗi thứ hai cho người khác.

#### Kết quả sau khi triển khai

Scene còn đúng một `Main Camera`; transform của nó so với `HEAD` **giống hệt**, chứng minh camera
của level không hề bị đụng tới. Đối chiếu với mục 3.3, đây là cùng một bài học được áp dụng sớm hơn: **thao
tác ghi vào tài sản của người khác phải thu hẹp phạm vi tối đa.**

---

### 6.3 Dọn `Co-Authored-By` khỏi lịch sử mà không đụng remote

#### Vấn đề đang gặp

Quy ước commit của dự án cấm trailer attribution AI, nhưng không ai biết lịch sử có sạch hay không.

#### Prompt đã dùng

"kiểm tra tất cả commit local của tôi xem có commit nào co author ko" → "sửa commit đó đi, ko được thêm co
author by."

#### Phản hồi quan trọng của AI

Quét 217 commit: đúng **1** commit dính (`88e8e274`, 31/08). Nhưng nó **đã được push** — có trên
`github/main`, `github/level-design`, `github/stat-balance` — nên viết lại sẽ đổi hash của nó **và 67 commit
sau nó**.

#### Phương án chọn / sửa / loại

- **Chọn:** viết lại **chỉ ở local** bằng `git filter-branch --msg-filter`, tạo nhánh
  `backup/before-coauthor-strip` trước.
- **Loại:** force-push. Đó là một hành động ra bên ngoài, thuộc quyền quyết định của owner.
- **Loại:** xoá body bullet list dù nó vi phạm luật "một dòng subject" — body giải thích vì sao từng file bị
  un-ignore, xoá đi là mất thông tin thật.

#### Lý do

Sửa một vi phạm hình thức không đáng để đánh đổi bằng một lần rewrite lịch sử đã chia sẻ, và càng không đáng
để mất nội dung giải thích.

#### Kết quả sau khi triển khai

`git diff backup/before-coauthor-strip main` **rỗng** — nội dung file không đổi một byte; 217 commit;
author/committer giữ nguyên tên lẫn ngày gốc. Có hai commit trùng tiêu đề nên phải đối chiếu cả hai để chắc
không xoá nhầm body của commit còn lại. Nêu rõ phần chưa giải quyết: commit cũ vẫn nằm trên hai nhánh remote
kia. (Một lần khác cùng chủ đề, AI chẩn đoán sai một vòng vì so `main` local đã bị viết lại với một
`github/stat-balance` đã cũ và báo "49 ahead" — chỉ sau khi owner chỉ đúng remote và chạy `git fetch github`
thì hai commit thật mới hiện ra.)

---

### 6.4 Reset tiến trình: không phải PlayerPrefs

#### Vấn đề đang gặp

Cần xoá toàn bộ tiến trình mở khoá để test lại từ đầu.

#### Prompt đã dùng

"clear player pref hay save để tôi có thể reset toàn bộ level đã mở khóa."

#### Phản hồi quan trọng của AI

**Không phải PlayerPrefs.** Tiến trình nằm trong file JSON dưới `persistentDataPath`; đã kiểm PlayerPrefs và
không có key nào liên quan, nên `DeleteAll()` sẽ chẳng làm gì — và chẳng làm gì là kiểu thất bại tệ nhất, vì
nó trông y như thành công.

#### Phương án chọn / sửa / loại

- **Chọn:** xoá qua `LocalSaveRepository.DeleteOwnedAutosave()` chứ không `rm` tay, để dọn luôn file backup và
  các file `.tmp` ghi dở — xoá tay mỗi `autosave.json` thì lần load sau sẽ **khôi phục từ backup**.
- **Chọn:** thêm menu `Tools ▸ Tower Defense ▸ Reset Save Progress`, liệt kê file sắp xoá **trước** khi hỏi
  xác nhận.

#### Lý do

Save là thứ duy nhất ghi lại tiến trình người chơi (mục 5.1), nên mọi thao tác xoá phải đi qua chính lớp đang
sở hữu nó và phải cho người dùng nhìn thấy thứ sắp mất.

#### Kết quả sau khi triển khai

Save cũ mở khoá và clear cả 10 level; sau khi xoá, thư mục rỗng và `UnlockProgress` mới có 1 level mở khoá, 0
level clear. Menu cảnh báo nếu đang Play Mode — session đang chạy vẫn giữ progress trong bộ nhớ và sẽ ghi đè
lại ngay sau đó.

---

## 7. Tổng kết cách làm việc với AI

Rút ra từ toàn bộ entry gốc, những gì thực sự tạo khác biệt:

### 7.1 Bằng chứng thắng suy đoán

mục 5.2 (log của người dùng chặn đứng ba vòng đoán sai), mục 3.2 (audit
Blender lật ngược báo cáo của Unity), mục 4.1 (ảnh chụp lật ngược một API chính thức), mục 3.5 (ba giả thuyết
liên tiếp bị chính số đo runtime phủ định). Mọi lần AI đi nhanh nhất đều là lúc có số đo, không phải lúc nó
tự tin nhất.

### 7.2 AI phải được phép rút lại kết luận của chính mình

Mục 4.3 rút lại một phương án **đã được duyệt** sau
khi tìm thấy bằng chứng nó từng bị revert. Mục 3.2 là một cú tự sửa sai *sai*, và việc ghi lại nó có giá trị
hơn việc giấu đi. Mục 5.8 đổ oan cho sprite atlas rồi phải revert; mục 2.4 tự đính chính ngay khi owner vặn
lại một câu về tick.

### 7.3 Ranh giới quyết định

Cân bằng gameplay, đánh đổi chất lượng hình ảnh, và bất cứ thay đổi nào chạm tới
tiến trình người chơi là của chủ dự án — mục 3.1 (không tự đổi số lift), mục 4.2 (ba phương án, không tự
chọn), mục 5.5 (không tự áp cap cho Soul Nexus), mục 6.3 (viết lại lịch sử ở local nhưng **không** force-push).
AI đưa số liệu, người quyết.

### 7.4 Verify theo kiểu Unity

API Editor thất bại trong im lặng: `SerializedProperty` từ chối gán,
`SpriteAtlasExtensions` không persist, `SetShaderPassEnabled` không được SRP tôn trọng, `LoadAllAssetsAtPath`
trả lời một câu hỏi khác, `IsPointerOverGameObject` trả lời về **frame trước** (mục 3.5), và
`RequestScriptCompilation` không ăn khi editor chạy nền — khiến ba lượt "console 0 error" thành vô nghĩa
(mục 3.7). Mọi "đã xong" đều phải **đọc ngược lại**.

### 7.5 Một luật, một chỗ

Mục 5.4 là cái giá của việc để cùng một luật (đặt trụ chạm mặt đất) tồn tại ở một
đường mà không ở đường kia — lỗi quay lại sau gần một tháng. Mục 3.3 và 3.6 là cùng một bệnh ở tầng asset
(override trong scene âm thầm thắng prefab, và chỉ lộ ra ở đúng một trong mười level); mục 5.11 ở tầng luật
(preview và lúc thả hỏi hai câu khác nhau); mục 2.4 ở tầng runtime, nơi cùng một luật bị chép ở hai nơi đẻ ra
bốn exception liên tiếp. Đây là kiểu lỗi tốn thời gian nhất của cả dự án.

### 7.6 Nói rõ thứ chưa kiểm được

Một kết quả kèm giới hạn có ích hơn một kết quả nghe gọn: mục 3.1 (bản
sửa suy ra từ số liệu, chưa tái hiện Play Mode), mục 4.1 (chưa profile SetPass call). Trong các đợt làm UI,
việc này thành thói quen: ghi rõ entry nào có chạy thật qua Bootstrap và entry nào chỉ đọc lại prefab đã lưu;
ghi rõ một prefab VFX **không xác minh được bằng ảnh** vì `ParticleSystem.Simulate` ở Edit Mode không sinh
trail mesh (render chính prefab gốc chưa sửa gì cũng ra ảnh trống), nên phần nhìn được bàn giao cho owner.
Ngược lại, mỗi lần bỏ qua điều này đều phải trả giá: một lượt mở đầu bằng "đã chạy thật để xác nhận" trong
khi thực tế chỉ kiểm tĩnh trên asset, và phải tự đính chính ngay sau đó.
