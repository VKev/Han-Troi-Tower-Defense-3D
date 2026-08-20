# AI Collaboration Log — Browser Prototype — 17/08/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Area:** `Documents/Prototype/Projectile-Network-TD`
- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-w33`
- **Cóc Kiện Trời prototype:** [`Documents/Prototype/Projectile-Network-TD/`](../Prototype/Projectile-Network-TD/) — [production](https://projectile-network-td-soul.vercel.app)

This file records consequential prototype decisions from the responsible session. It summarizes decisions and verification evidence rather than reproducing the raw transcript.

## Entry 47 — Teach a functional high-ground network before Level 2 support towers

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-w33`
- **Date:** `2026-08-17`

### Problem being addressed

Level 2 introduced flying enemies in Wave 3 but taught Hỗ Trợ first. The player was not explicitly taught to place a same-height projectile network on the raised terrain before airborne enemies arrived, so the level could punish a rule it had not demonstrated.

### Prompt used

The owner requested that Level 2 teach the player to place towers on high ground to defend against flying enemies before teaching Hỗ Trợ and Xung Hồn. When asked to choose the lesson cadence, the owner approved the one-lesson-per-wave option: high ground on Wave 3, Hỗ Trợ on Wave 4, and Xung Hồn on Wave 5. The owner also requested immediate deployment after completion.

### Important AI response

The AI proposed a complete free anti-air branch rather than one decorative raised tower because the network rules require a source, two processors at Wave 3 depth, and a valid Tỏa Hồn endpoint before any projectile can travel. It selected a dynamic three-slot high-ground plan whose internal links are legal, unobstructed, visible, and physically cross the Layer 1 path. Existing card highlight, drag hand, grid preview, live link beam, and Start gating were reused so the lesson remained text-free.

### Option selected, revised, or rejected

- **Selected:** Wave 3 grants and teaches `Giếng Hồn -> Hỏa -> Băng -> Tỏa Hồn` on high terrain before the first flying wave.
- **Selected:** Wave 4 teaches Hỗ Trợ and Wave 5 teaches Xung Hồn, preserving one new concept per preparation phase.
- **Selected:** all five mandatory lesson towers cost and refund `0`, retain zero invested value, and do not increase the paid-tower price multiplier.
- **Selected:** block Wave Start until the current lesson network is active, and prove the high-ground branch through real Layer 1 damage rather than placement diagnostics alone.
- **Revised:** keep Hỗ Trợ/Xung Hồn insertion planning on the ground branch after the new second high-ground input revealed that the old first-terminal lookup could select an elevated endpoint.
- **Rejected:** teaching only one isolated high tower, combining high ground and Hỗ Trợ in Wave 3, or allowing the flying wave to begin before the anti-air chain was connected.

### Rationale

A single raised tower cannot fire under the Soul network contract and would teach the wrong mental model. A complete three-tower branch makes altitude, routing, elemental order, and Tỏa Hồn's second input visible in one executable example. Separating Hỗ Trợ and Xung Hồn into later waves prevents tutorial overload, while free grants avoid economy deadlocks and preserve the intended paid-build balance.

### Implementation or verification result

The runtime now selects and caches three legal high-tier slots, guides three free placements and three link drags, and requires the branch to become active before Wave 3 starts. The verified plan used slots `59 -> 63 -> 53`, with an internal projectile segment crossing the flying route at XZ distance `0`. Real desktop and mobile interaction tests recorded Layer 1 hits, then completed free Wave 4 Hỗ Trợ and Wave 5 Xung Hồn insertions without changing Gold or paid-tower price growth. The full Playwright matrix passed `51` tests with `7` intentional viewport skips. Public hardware-backed Intel D3D11 probes were nonblank and error-free at `58` desktop / `48` mobile draw calls. Production implementation deployment `dpl_ACLFVUw54J9m68RRPi9Ui7ZumiJA` reached `READY`; the stable URL and all three level routes returned HTTP 200 with hashed `index-jzhkAzlG.js` and `index-C4hKGpph.css` assets.

## Entry 48 — Retheme the complete Link prototype as Cóc kiện trời on a drought battlefield

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-rjb`
- **Date:** `2026-08-17`

### Problem being addressed

The independent Projectile Network TD prototype had reached a mechanically complete Soul presentation, but the intended product identity changed to the Vietnamese folktale **Cóc kiện trời**. The owner needed a complete theme conversion rather than a palette swap: Cóc as the protected endpoint, a drought-stricken world, a rain-calling terminal and skill, culturally coherent UI, and ground-only Levels 2 and 3. The existing Link mechanics, tutorial practice cadence, mobile support, and tuned difficulty still had to remain authoritative.

### Prompt used

The owner requested that the full prototype become a stylized, low-detail drought/Cóc kiện trời game with orange-yellow dry soil, rocks, dead trees, withered grass, elemental towers, and matching UI/UX. The owner approved three important clarifications: only `Documents/Prototype/Projectile-Network-TD/` would change; the fixed path endpoint would become a pale light-blue frog while the movable two-input terminal became **Trống Gọi Mưa**; and Levels 2/3 would remove all flying enemies and raised terrain while Level 2 taught Hỗ Trợ in Wave 3 and Special in Wave 4. The owner also explicitly allowed Unity MCP, but the work remained isolated to the Three.js prototype and did not require Unity changes.

### Important AI response

The AI wrote and approved `Documents/TechnicalSpec/CocKienTroiDroughtTheme_Technical_Specification.md` before source changes, then preserved stable internal enemy identifiers and every wave order while converting every enemy to Layer 0. This avoided silently weakening the campaign when altitude was removed. It built the new presentation from procedural Three.js geometry and deterministic CanvasTextures because Tripo, Gemini, and ElevenLabs credentials were unavailable. The implementation retained the Link graph, projectile values, rising price curve, status/reaction feedback, tutorial checkpoint, contextual link visibility, mobile controls, and x1/x2/x3 speed controls.

### Option selected, revised, or rejected

- **Selected:** pale light-blue procedural Cóc as the fixed leak-damage endpoint.
- **Selected:** horizontal terracotta-and-bronze Trống Gọi Mưa as the free movable two-input/no-output terminal.
- **Selected:** deterministic cracked-earth and dusty-road textures, dry procedural props, hot-sun lighting, and terracotta/parchment/rain-blue UI.
- **Selected:** convert former `wisp` and `skyWarder` entries into full-detail Layer 0 ground threats while retaining their stats and exact wave positions.
- **Selected:** free Hỗ Trợ placement/linking in Level 2 Wave 3 and free Trụ Sấm placement/linking in Wave 4, each with zero investment and no paid-price inflation.
- **Selected:** preserve all Link-derived projectile, cadence, speed, HP, density, reward, resistance, immunity, reaction-barrier, and purchase-growth values.
- **Rejected:** changing Arcane-Arsenal Link/Rotation, deleting difficult enemy orders, retaining decorative raised terrain, introducing enemy LOD, or importing unverified external assets.

### Rationale

Separating Cóc from Trống Gọi Mưa keeps the gameplay distinction between the fixed failure endpoint and the player's movable network terminal. Converting air archetypes rather than deleting them preserves the proven pressure curve and reaction requirements. The warm matte environment creates a clear drought identity, while rain blue and saturated elemental colors protect combat readability. Keeping the existing layout and interaction geometry limits mobile regression risk and ensures that the retheme changes product identity without invalidating the established control model.

### Implementation or verification result

The runtime now contains the procedural frog, rain drum, drought materials and textures, dry props, rethemed enemies/towers/copy, ground-only Levels 2/3, Wave 3 Hỗ Trợ and Wave 4 Trụ Sấm lessons, and a blue persistent Mưa Rào AOE. The production build passed. The complete Playwright matrix passed `51` tests with `7` intentional project-specific skips, including real desktop/mobile lesson interaction and fourteen regenerated visual baselines. Canvas inspection was nonblank and error-free on desktop/mobile across all three levels; the largest measured Level 3 state used `164` desktop / `142` mobile draw calls and remained inside the approved budgets. Vercel deployment `dpl_5ku8sRnKLgxQx9nYEEPhNWtHjS5S` reached `READY` and was aliased to [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app). The root, all three level routes, `index-BqzUCFYK.js`, and `index-P8TvCm8-.css` returned HTTP 200.

## Entry 49 — Hoàn thiện đường mòn hạn hán, Mưa Rào và hành trình thắng màn của Cóc

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issues:** `TowerDefense3D-c4c`, `TowerDefense3D-8dm`
- **Date:** `2026-08-17`

### 1. Bối cảnh và vấn đề

Ở lần tải web đầu tiên, bàn tay tutorial có một khung hình trỏ sai rồi mới về đúng ô. Bốn trụ nguyên tố vẫn dùng silhouette quá giống nhau, đường quân đi là một mặt phẳng màu đơn, và vùng Mưa Rào giống một cột sáng UFO hơn là mưa. Sau khi hoàn thành màn, Cóc chưa thể hiện hành trình đi ngược đường quân để tiến sang chiến trường kế tiếp.

### 2. Quyết định/hướng tiếp cận

Giữ nguyên toàn bộ tọa độ đường, grid, balance và gameplay Link; chỉ nâng presentation và thêm phase chuyển màn. Sửa nguyên nhân khung hình sai bằng cách cập nhật camera world matrix trước phép chiếu tutorial, không che lỗi bằng loading delay. Dùng hình học và CanvasTexture procedural nhẹ: bốn silhouette nguyên tố riêng, đường đất ba lớp có texture, mưa instanced, và Cóc tách khỏi bệ để nhảy theo chính polyline của địch theo chiều ngược lại.

### 3. Thay đổi đã thực hiện

Hỏa trở thành lò than đất nung, Băng thành điện pha lê, Gió thành chong chóng gió và Đất thành thạch trụ bậc. Đường quân có vai đường, mép nổi, mặt đất nén với hai vệt mòn, dấu chân, sỏi và đất vỡ ở cạnh. Mưa Rào có 32 hạt rơi, vệt ướt và ba vòng gợn nhưng không còn cột sáng đục. Khi thắng, Cóc rời bệ, nhảy từ Nexus tới cuối đường rồi đi ngược về cửa quân; màn 1 chuyển sang 2, màn 2 sang 3, còn màn 3 hiện kết quả sau khi hành trình kết thúc.

### 4. Kiểm chứng

`npm run build` đạt. Toàn bộ Playwright đạt `67` ca với `9` ca bỏ qua có chủ đích theo viewport; các ca mới kiểm tra cold-load tutorial hand, model nguyên tố, đường mòn không đổi dữ liệu, mưa AOE, hop/reset, chuyển màn 1→2/2→3 và kết thúc màn 3. Hai mươi hai baseline desktop/mobile đạt. Canvas inspection dùng Intel D3D11 thật, không có console/page error và mọi trạng thái đều trong budget; riêng hoạt cảnh Cóc dùng `38/31` draw calls trên desktop/mobile.

### 5. Kết quả

Prototype hiện đọc rõ là một chiến trường hạn hán: đường đi có độ dày và chi tiết đất mòn, tower nguyên tố phân biệt được bằng hình khối, Mưa Rào thể hiện đúng hạt mưa và vùng sát thương, bàn tay tutorial không còn nhảy vị trí khi tải lạnh, và thắng màn có chuyển cảnh kể chuyện bằng hành động của Cóc. Deployment `dpl_7Eub542kho8yoFt3cijKUgC7yZpg` ở trạng thái `READY` tại [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app); root, ba level và asset băm đều trả HTTP `200`.

### 6. Bài học/việc còn lại

Projection UI trước lượt render đầu phải chủ động cập nhật camera matrix; loading giả không giải quyết ownership của trạng thái. Với game low-poly, chiều sâu đọc tốt hơn khi ghép silhouette, layer hình học và texture có hướng thay vì tăng số polygon. Phạm vi còn lại là kiểm tra cảm giác trên thiết bị di động vật lý và thay procedural art/audio bằng production assets nếu dự án chuyển từ prototype sang bản phát hành.

## Entry 50 — Trang trí và gộp draw call cho toàn bộ đất hạn quanh ba level

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-3pk`
- **Date:** `2026-08-17`

### 1. Bối cảnh và vấn đề

Phần đất nâu tối bao quanh bàn chơi còn là một mặt phẳng trống, đặc biệt rõ ở góc camera mà chủ dự án đánh dấu. Yêu cầu mới là bổ sung nhiều cỏ úa, đá, cành gãy và cây khô cho cả ba level, đồng thời mọi prop tĩnh phải được gộp để tránh tăng draw call và gây lag trên mobile.

### 2. Quyết định/hướng tiếp cận

Giữ nguyên map, đường địch, grid, tutorial và gameplay. Sinh các cụm prop low-poly theo công thức deterministic trong vành đai nằm ngoài biên bàn, tăng mật độ theo kích thước level. Sau khi đo bản instanced đầu tiên vượt budget Level 3 mobile, chuyển toàn bộ prop tĩnh sang geometry batching: một mesh vertex-color cho prop trong bàn và một mesh cho toàn bộ vành đai ngoài, không dùng LOD hoặc ẩn prop theo viewport.

### 3. Thay đổi đã thực hiện

Level 1 có 34 đá, 42 cụm cỏ, 24 cành và 9 cây; Level 2 có 48/60/34/13; Level 3 có 62/78/44/17. Mỗi cụm cỏ gồm bốn lá tam giác, cành có nhánh chạc và cây có thân cùng ba nhánh lệch hướng. Các source geometry được chuyển non-indexed, áp transform, ghi vertex color theo vật liệu rồi merge; geometry tạm được dispose sau khi tạo mesh cuối. Diagnostics mới công bố mật độ, số source part và số mesh gộp cho cả hai lớp trang trí.

### 4. Kiểm chứng

Build production đạt. Full Playwright đạt `67` ca và bỏ qua `9` ca theo viewport; visual regression đạt `20/20`; test chuyên biệt xác nhận mật độ và hai mesh gộp. Canvas inspection desktop/mobile của cả ba level không có console hoặc page error. Level 1 preparation dùng `79/79` draw call; Level 2 combat dùng `111/93`; Level 3 production combat dùng `151/133`, tất cả trong budget.

### 5. Kết quả

Đất tối quanh bàn giờ có silhouette và nhịp điệu môi trường rõ ràng thay vì một mảng màu trống, nhưng không che đường, Cóc, tower, tutorial hay HUD. Level 3 mobile từng đạt `154` draw call khi còn sáu nhóm instanced và đã giảm xuống `133` sau khi gộp toàn bộ prop tĩnh. Deployment `dpl_ABQ68TEJGVzEtN51p98Q6mCm4Js5` ở trạng thái `READY` tại [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app); root, ba level, `index-V1fOoIeQ.js` và `index-CoNmGYYT.css` đều trả HTTP `200`.

### 6. Bài học/việc còn lại

Instancing phù hợp khi từng family cần material riêng hoặc còn chuyển động; với hàng trăm prop hoàn toàn tĩnh, gộp geometry có vertex color cho kết quả draw-call tốt hơn mà vẫn giữ biến thể màu. Rủi ro còn lại chỉ là cảm nhận FPS trên thiết bị mobile vật lý; kiểm tra WebGL phần mềm và ảnh portrait hiện không cho thấy lỗi hoặc vượt budget.

## Entry 51 — Increase late-campaign pressure and replace projectile meshes with glow trails

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-3mq`
- **Date:** `2026-08-17`

### Problem being addressed

Levels 2 and 3 were too forgiving, and Rain Drum endpoints charged the active skill quickly enough to permit repeated casts. Projectiles also still read as small three-dimensional objects instead of fast elemental energy with a clear travel path. The denser drought scenery had to remain readable and inexpensive on mobile.

### Prompt used

The owner requested more decoration on the drought ground, substantially more enemy HP in Levels 2 and 3, lower skill charge from Rain Drums in those levels, and projectile visuals based on glow and trails rather than a 3D bullet model. The owner approved the proposed HP curves and charge multipliers.

### Important AI response

The AI retained every route, enemy order, movement speed, projectile speed, collider size, link rule, and tutorial contract. It isolated difficulty changes to authored wave HP multipliers and per-stage skill-charge gain, then replaced each projectile mesh with a billboard glow core and a short additive trail. Static environment pieces continued to use the merged vertex-color batches established for mobile draw-call control.

### Option selected, revised, or rejected

- **Selected:** Level 2 HP multipliers `[1.8, 2.4, 3.5, 5, 7.5, 10.5]` and Level 3 multipliers `[2, 2.8, 4, 5.8, 8, 11, 14.5, 19, 24.5, 31]`.
- **Selected:** skill-charge multipliers `1.0`, `0.5`, and `0.35` for Levels 1, 2, and 3 respectively.
- **Selected:** a camera-facing glow sprite with nine additive trail samples for every projectile, while preserving the established speed, scale, and hitbox values.
- **Selected:** keep the denser decorative props inside the existing merged battlefield and perimeter meshes.
- **Rejected:** weakening enemy composition, slowing projectiles, shrinking hitboxes, or restoring per-prop draw calls.

### Rationale

The steeper HP curve forces longer network planning and elemental reactions without changing the player's learned timing. Reduced charge gain prevents the active skill from replacing tower strategy. Billboard cores and additive trails communicate speed, element, and trajectory more clearly than a rotating low-poly bullet and remain suitable for mobile.

### Implementation or verification result

Definitions, diagnostics, tutorial expectations, visual baselines, and deterministic balance tests now expose the approved curves and projectile presentation. The complete Playwright matrix passed 70 tests with 10 intentional viewport-specific skips. Hardware-backed production probes remained within budget in the final skill state, and the combined release was deployed successfully under `dpl_GmRXx1DM6Ahdf38Lgz56pfVhkm38`.

## Entry 52 — Replace Rain Shower with the frog's visible tongue strike

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-1iw`
- **Date:** `2026-08-17`

### Problem being addressed

The former Rain Shower was a persistent field detached from the main Cóc character and did not deliver the desired act of catching prey. The owner wanted the charged skill to originate from the frog, use a visible stretching tongue, deal a strong one-time impact, and teach the real drag gesture without text.

### Prompt used

The owner approved a meter above Cóc that is dragged to a smaller target area. The tongue had to launch visibly from the frog, damage enemies along its route for 20% of the main hit, deal a large one-time AOE impact, shake the camera, attach enemies killed at the target to the tongue, and pull them into the frog's mouth. The owner then clarified that the tongue must visibly stretch from Cóc rather than appear as a completed beam.

### Important AI response

The AI specified a phase-based attack with a named mouth anchor and a continuously scaled tongue mesh: 0.16 seconds to extend, 0.08 seconds at full reach, and 0.28 seconds to retract. Target-area enemies are excluded from corridor damage so the two damage paths never stack. Captured enemies are reparented to the tongue tip for the return animation. The tutorial hand uses the actual projected meter position and releases on the densest living cluster.

### Option selected, revised, or rejected

- **Selected:** base radius `2.7` with `220 + min(18% target max HP, 500)` magic damage.
- **Selected:** disjoint 20% corridor damage, one-time target damage, 0.18-second camera shake, and target kills carried to the frog mouth.
- **Selected:** `Quét Rộng` at radius `3.1` with lower damage and `Đớp Mạnh` at radius `2.5` with higher damage.
- **Selected:** a pink glow core, bright tip, open-mouth feedback, concentric impact rings, and a translucent corridor preview while dragging.
- **Selected:** tutorial completion only after a valid player cast while living enemies are present.
- **Rejected:** a laser-like instant line, a tongue spawned already at full length, lingering damage ticks, slow fields, projectile amplification fields, or a text tutorial.

### Rationale

Continuous extension from the mouth makes the cause-and-effect readable in a single glance and ties the active ability directly to Cóc. The short phase timings keep the action responsive, while the carried kills make the successful catch legible even during dense waves. A real drag tutorial verifies the same interaction used in normal play instead of simulating completion.

### Implementation or verification result

The ready meter now follows Cóc on desktop and mobile. Dragging previews both the tongue corridor and impact radius; release opens the frog mouth, stretches the tongue from the exact mouth anchor, resolves non-stacking corridor/AOE damage, produces a strong impact, and retracts captured kills. The full build and 70-test matrix passed. Live Intel D3D11 probes on the production URL were nonblank with no console or page errors; desktop and mobile each used 104 draw calls, 10,252 triangles, 104 geometries, and 9 textures in the deterministic impact state. Deployment `dpl_GmRXx1DM6Ahdf38Lgz56pfVhkm38` reached `READY`; the stable root and all three level routes returned HTTP 200 at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app).

## Entry 53 — Make the frog tongue a solid model and move impact feedback into the soil

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-6uq`
- **Date:** `2026-08-17`

### Problem being addressed

The first tongue implementation was readable but still resembled a glowing projectile line. Its rounded endpoint was too easy to lose behind captured enemies, and the saturated AOE field covered the tongue at the moment the skill should showcase it.

### Prompt used

The owner supplied a stylized frog reference and requested a real tongue model that visibly stretches from the frog and ends in a large round bulb. The owner then clarified that the tongue must not glow like a projectile, that the AOE ground color should be faint, and that the impact should instead throw soil particles upward.

### Important AI response

The AI identified the presentation hierarchy as model first, captured prey second, and AOE indicator third. It proposed an opaque lit tapered body, a larger spherical endpoint, reduced captured-enemy scale and radial offsets, no glow mesh or emissive material, a 0.06-opacity ground disc, and a deterministic ballistic burst of drought-colored low-poly soil.

### Option selected, revised, or rejected

- **Selected:** a tapered 16-sided three-dimensional body using `MeshStandardMaterial`, with radii `0.20` at the mouth and `0.34` at the target end.
- **Selected:** a radius-`0.68` spherical tongue bulb with a small lit highlight.
- **Selected:** scale captured enemies to `0.62` and distribute them around the bulb so the endpoint remains visible.
- **Selected:** remove the additive halo and all tongue emissive values; diagnostics require `usesGlow: false` and zero `tongueGlow` meshes.
- **Selected:** keep the AOE disc at opacity `0.06` and launch fourteen deterministic brown/orange tetrahedral soil particles with velocity, spin, gravity, and fade.
- **Rejected:** retaining a projectile-like glow, using a large saturated impact field, or hiding the endpoint behind full-size captured enemies.

### Rationale

Lit opaque geometry gives the tongue volume under the existing drought lighting and makes it visually distinct from ammunition. The oversized bulb matches the supplied reference and preserves the catch fantasy. Ballistic earth clods communicate force and ground contact without obscuring the tongue or the enemy route.

### Implementation or verification result

The production code and diagnostics now expose the solid model, rounded tip, zero glow meshes, subtle AOE opacity, and fourteen live dirt particles. The complete Playwright matrix passed 70 tests with 10 intentional viewport skips, including refreshed desktop/mobile tongue baselines. Live hardware-backed Intel D3D11 probes were nonblank and reported no console or page errors; both viewports remained within budget at 109 draw calls, 10,740 triangles, 110 geometries, and 9 textures. Deployment `dpl_3Ncv1T1t2gVEU8XYy2HWykvig1u4` reached `READY`, and the stable root plus all three level routes returned HTTP 200 at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app).

## Entry 54 — Differentiate tower silhouettes and repair endpoint onboarding and multi-route transport

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-53b`
- **Date:** `2026-08-17`

### Problem being addressed

The generator and elemental processors shared too much of the same shrine-like silhouette, making them difficult to distinguish during fast play and on mobile. The icon-only tutorial did not explicitly establish which tower starts a projectile route and which terminal ends it. The elemental-reaction explanation could reopen after it had already been acknowledged. A second generator connected through a new complete route could remain inactive because network validation contained a hidden minimum-processor gate. The initial Level 1 camera also did not match the approved high-oblique composition.

### Prompt used

The owner supplied a gameplay screenshot and requested visibly different tower models, a tutorial focus on Lò Đạn as the source and Trống Gọi Mưa as the endpoint, a one-time reaction popup, a repair for the second-generator route, and the screenshot's camera angle as the default page-entry view.

### Important AI response

The AI proposed differentiating towers through broad primary forms rather than color alone, adding two simultaneous text-free endpoint beacons during the existing first-link lesson, storing reaction acknowledgement for the active browser session, and removing the unintended processor-count condition from route validity while keeping the authored tutorial restrictions. It also derived and verified a numeric camera contract rather than relying only on a screenshot comparison.

### Option selected, revised, or rejected

- **Selected:** mechanical foundry for Lò Đạn; wide brazier for Hỏa; asymmetric crystal crown for Băng; broad four-vane rotor for Phong; squat stepped monolith for Địa.
- **Selected:** warm-gold source and rain-blue terminal beacons with distinct floating glyphs, visible only during the endpoint-link lesson.
- **Selected:** browser-session acknowledgement for the reaction explanation, including reset and checkpoint restore.
- **Selected:** any complete, directed, acyclic Lò Đạn-to-Trống Gọi Mưa route transports ammunition; processor count does not determine route activity.
- **Selected:** Level 1 camera yaw `1.9712`, pitch `0.7844`, distance `32.9841`, target `[-1, 1.2, -0.4]`.
- **Rejected:** recoloring the old shared tower body, permanently visible endpoint markers, and retaining the hidden minimum-processor validation rule.

### Rationale

Large silhouette differences remain legible when hue is compressed by status VFX, distance, or a small screen. The two beacons explain the network's source-to-terminal grammar without adding text or another modal. Session acknowledgement respects the lesson while preventing repeated interruption. Route validity now matches the visible link graph and terminal input contract, so a player's second legal network behaves predictably. A numeric camera contract makes the approved entry composition deterministic across reloads.

### Implementation or verification result

The production runtime now exposes unique tower model profiles and named structural parts, shows exactly two endpoint highlights during the first link lesson, and dismisses them when the route is complete. The reaction explanation cannot reopen after acknowledgement in the same browser session. A deterministic two-route state proves that two independent generators both launch into the two-input terminal. The production build and full Playwright matrix passed `76` tests with `10` intentional viewport skips, including `24` desktop/mobile visual baselines. Hardware-backed Intel D3D11 probes were nonblank and error-free; the changed states stayed inside budget at `67` draw calls, `10,362–15,286` triangles, `69–70` geometries, and `5` textures. Vercel deployment `dpl_JDBX1pCCkhRSdS4K2nKyjQpqnSX2` reached `READY`; the stable root and all three level routes returned HTTP `200` at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app).

## Entry 55 — Keep network endpoints visible and prevent reaction lock loops

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-2k4`
- **Date:** `2026-08-17`

### Problem being addressed

The source and terminal beacons disappeared immediately after the first link even though the owner wanted the network grammar reinforced throughout the complete tutorial. Their actual link attachment points were not emphasized. Ordinary Wind ammunition repeatedly rewound path progress, so dense fire could effectively hold an enemy in place. Repeated maximum-HP reaction procs also scaled too aggressively when several projectiles carried the same reaction.

### Prompt used

The owner requested persistent endpoint illumination through the tutorial, visible `ĐẦU` and `CUỐI` labels on the two towers, brighter link endpoints at both towers, removal of Wind's movement lock, and a balanced cooldown before the same reaction can trigger again.

### Important AI response

The AI extended the existing world-space beacon rather than adding a modal or HUD panel. It used camera-facing Vietnamese labels plus distinct gold/rain-blue sockets positioned at the exact projectile link anchors. For combat balance, it separated ordinary Wind status from Cuồng Phong control and chose a per-enemy, per-reaction cooldown so one reaction cannot be spammed while mixed-reaction networks remain tactically valuable.

### Option selected, revised, or rejected

- **Selected:** retain both endpoint markers through all six Level 1 waves, including the post-reaction mastery checkpoint and retry.
- **Selected:** `ĐẦU` for Lò Đạn and `CUỐI` for Trống Gọi Mưa, with matching glowing outgoing/incoming sockets.
- **Selected:** remove all progress rewind from ordinary Wind ammunition.
- **Selected:** reduce Cuồng Phong displacement from `1.5/0.75` to `0.8/0.35` path units for regular/boss enemies.
- **Selected:** `2.25s` cooldown for each reaction key on each enemy; different reactions do not share the timer.
- **Rejected:** a global reaction cooldown, removing all Wind identity, or globally reducing the existing `6%` maximum-HP reaction damage ratio.

### Rationale

Persistent world-space labels teach the route grammar at the same place where the player acts, and the sockets connect the abstract labels to the physical projectile path. Removing per-hit rewind prevents high-cadence Wind chains from creating a permanent movement lock. A keyed enemy-local cooldown caps duplicate reaction throughput without punishing players who invest in varied elemental ordering.

### Implementation or verification result

The runtime now keeps both labeled endpoint beacons and exact link sockets visible across guided and mastery waves. Ordinary Wind leaves progress unchanged; Cuồng Phong uses the reduced one-time displacement. Deterministic tests prove immediate duplicate suppression, independent different reactions, and cooldown recovery after `2.25s`. The production build and complete Playwright matrix passed `80` tests with `10` intentional viewport skips; the established tutorial balance still requires an expanded reaction network to win the final mastery wave. Hardware-backed Intel D3D11 production probes were nonblank and error-free on desktop/mobile at `75` draw calls, `15,966` triangles, `76` geometries, and `7` textures. Deployment `dpl_7NdSE8pk6bu641E3FbAZKAm4omXL` reached `READY`, and the stable root plus all three level routes returned HTTP `200` at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app).

## Entry 56 — Highlight tutorial endpoint link halves and neutralize completed-link colors

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-wdx1`
- **Date:** `2026-08-17`

### Problem being addressed

The persistent `ĐẦU` and `CUỐI` sockets identified the route endpoints, but they did not visually explain which direction each endpoint owns along a completed link. Completed beams also inherited tower colors, so longer networks became a collection of unrelated colored segments instead of one readable projectile route.

### Prompt used

The owner confirmed that the link leaving `ĐẦU` should glow only to its midpoint and that the link entering `CUỐI` should glow only from its midpoint to the terminal. The owner then required every remaining completed-link span to be translucent white instead of using a separate color for each tower.

### Important AI response

The AI preserved link validation, projectile transport, selected-network visibility, and all balance values. It proposed a neutral white base beam for every completed link, then added tutorial-only directional overlays with explicit normalized coverage: warm gold `[0, 0.5]` from the source and rain blue `[0.5, 1]` into the terminal. Endpoint-adjacent tutorial links remain visible without a selection; other links retain the existing network-selection rule.

### Option selected, revised, or rejected

- **Selected:** white `MeshBasicMaterial` for every completed beam and arrow, with active opacity `0.42` and inactive opacity `0.18`.
- **Selected:** a gold core/glow/highlight overlay on the first half of the guided source link.
- **Selected:** a rain-blue core/glow/highlight overlay on the terminal half of every Rain Drum input.
- **Selected:** preserve both overlays on a direct source-to-terminal link so the complete segment is split into two complementary halves.
- **Selected:** keep only endpoint-adjacent tutorial links visible without selection; middle and non-tutorial links retain selected-network visibility.
- **Rejected:** per-tower completed-link colors, coloring the full route gold or blue, and globally forcing every completed link to remain visible.

### Rationale

A shared white base makes the graph read as one system and prevents elemental tower colors from competing with projectile and status feedback. Limiting color to the source and terminal halves teaches route direction at the exact points where ammunition enters and leaves the network. The tutorial-only visibility exception reinforces the lesson without cluttering Levels 2 and 3.

### Implementation or verification result

Runtime diagnostics now read the actual white beam material and expose every tutorial overlay's role, node pair, color, normalized coverage, and visibility. Regression tests prove the direct split, the chained source/terminal halves, the Levels 2–3 neutral style, and the selected-network tutorial exception. The production build passed, the full Playwright matrix passed `82` tests with `10` intentional viewport skips, and all `24` desktop/mobile visual baselines passed. Hardware-backed Intel D3D11 live probes were nonblank and error-free at `102` draw calls, `17,064` triangles, `103` geometries, and `7` textures, within desktop and mobile budgets. Vercel deployment `dpl_EEaYaRDznmvPzxG4L5JdSM9gYfMS` reached `READY`; the stable root and all three level routes returned HTTP `200` at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app).

## Entry 57 — Simplify endpoint teaching and acknowledge completed chains with two LED passes

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-pefb`
- **Date:** `2026-08-17`

### Problem being addressed

Persistent endpoint rings, sockets, and colored half-link overlays had become more visual instruction than the owner wanted. The tutorial still needed to explain the source-to-terminal grammar, and a newly completed route needed an unmistakable but temporary success acknowledgement. The red enemy-direction arrow also added unnecessary clutter to the authored dirt trail.

### Prompt used

The owner requested plain `ĐẦU` and `CUỐI` labels without endpoint highlights, matching parenthetical roles in the Level 1 build dock, and a short reminder below the top bar to connect the start and end of the chain. After a chain becomes complete, its links should brighten and an LED-like colored packet should run exactly twice. The owner then requested removal of the enemy-direction arrow from every level.

### Important AI response

The AI separated persistent graph presentation from the one-shot completion acknowledgement. It retained translucent white links and selected-network visibility, detected newly complete directed generator-to-Rain-Drum routes by comparing the graph immediately before and after a successful link, and created a disposable ordered-path effect. It also removed the spawn marker without touching path geometry, spawning, waypoint traversal, or victory travel.

### Option selected, revised, or rejected

- **Selected:** independent camera-facing `ĐẦU` and `CUỐI` labels with no rings, glyphs, halos, sockets, or half-link colors.
- **Selected:** `Lò Đạn (ĐẦU)` and `Trống Mưa (CUỐI)` only in the Level 1 dock.
- **Selected:** the Vietnamese chain reminder only during guided link objectives.
- **Selected:** a temporary full-route brightening plus nine alternating gold/rain-blue lights traversing the ordered route exactly twice.
- **Selected:** allow a genuinely disconnected or replaced route to acknowledge a later new completion, while preventing continuous retriggers on an unchanged complete graph.
- **Selected:** remove the red spawn-direction arrow from Levels 1–3 with no replacement marker.
- **Rejected:** persistent endpoint illumination, persistent colored route segments, repeated LED looping, gameplay effects from the notification, and changes to enemy path logic.

### Rationale

Plain labels teach the two semantic roles without competing with tower silhouettes, enemies, or projectiles. A short two-pass packet gives immediate causal feedback at the exact moment the route becomes valid, while automatic cleanup preserves the low-clutter selected-network rule. Removing the separate enemy arrow lets the raised, textured trail carry the environment composition on its own.

### Implementation or verification result

The runtime now exposes endpoint-label, reminder, route-order, pass-count, progress, active-light, cleanup, and spawn-marker diagnostics. The full Playwright matrix passed `88` tests with `10` intentional viewport skips; all `26` desktop/mobile visual baselines passed. Hardware-backed Intel D3D11 probes of the deployed notification were nonblank and error-free at `90` draw calls, `14,416` triangles, `93` geometries, and `7` textures. Deployment `dpl_EURa3uUXziKMSGK51CQ9hfU6bZ3q` reached `READY`, and the stable root plus all three level routes returned HTTP `200` at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app).

## Entry 58 — Remove completion-beam glow and bind endpoint color to full-route validity

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-y3c5`
- **Date:** `2026-08-17`

### Problem being addressed

The completed-chain acknowledgement still brightened the route beneath its moving LED packet. In addition, endpoint labels could remain green from local source and terminal connections even after a middle segment broke, which falsely communicated that the whole projectile route was operational.

### Prompt used

The owner requested no chain-line glow, only the moving LED notification, and green `ĐẦU`/`CUỐI` labels only while those endpoints are connected by one complete valid chain. The owner clarified that if the chain breaks in the middle, both labels must lose their green color even when the source and terminal individually remain linked to neighboring towers.

### Important AI response

The AI removed completion-notice ownership of link visibility and all temporary bright beam geometry. It changed endpoint state from a local-link check to a directed graph-path check against the authored tutorial source and Rain Drum, then added a deterministic broken-middle-segment scenario that preserves links at both ends.

### Option selected, revised, or rejected

- **Selected:** retain only the existing nine-light gold/rain-blue packet for exactly two completion passes.
- **Selected:** preserve ordinary selected-network visibility and translucent white link materials during the notice.
- **Selected:** color both endpoint labels green only when the full authored source-to-terminal route is valid.
- **Selected:** return both labels to neutral immediately after any intermediate disconnection or redirection.
- **Selected:** allow a genuinely repaired route to turn both labels green and acknowledge completion again.
- **Rejected:** bright route overlays, forced route reveal, endpoint-degree heuristics, or independent green states for source and terminal.

### Rationale

The LED packet is sufficient one-shot causal feedback without making the whole network flash. End-to-end path validity is the semantic state the tutorial is teaching; using local endpoint connections would claim success for a network that cannot transport ammunition from its source to its terminal.

### Implementation or verification result

Runtime diagnostics now expose each endpoint label's connected state and color plus zero completion bright segments and beam overlays. The deterministic complete-to-broken test preserves the generator's outgoing link and the Rain Drum's incoming link while severing the middle segment, and proves both labels change from green `#65f7a4` to neutral `#fff4c9`. The production build passed and the complete Playwright matrix passed `90` tests with `10` intentional viewport skips. Hardware-backed Intel D3D11 production probes were nonblank and error-free on desktop and mobile at `87` draw calls, `14,320` triangles, `90` geometries, and `7` textures. Live broken-route probes retained source output `3` and terminal input `4`, reported zero active complete routes, and rendered both endpoint labels neutral without console or page errors. Deployment `dpl_BSm3t5my8bsedrpMKyBY7g8jyd7X` reached `READY`; the stable root and all three level routes returned HTTP `200` at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app).

## Entry 59 — Pulse endpoint color changes and start links on the first drag

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issues:** `TowerDefense3D-6tfk`, `TowerDefense3D-jwau`
- **Date:** `2026-08-17`

### Problem being addressed

Although `ĐẦU` and `CUỐI` correctly changed color with full-route validity, the color change could be missed during play. Creating a link also still required selecting its source tower before performing the actual drag, adding an unnecessary interaction step.

### Prompt used

The owner requested a scale-up/scale-down animation whenever the endpoint words change color so players notice the state transition. The owner then requested direct tower-to-target drag linking without clicking the source tower first.

### Important AI response

The AI tied one short scale pulse to the existing end-to-end validity transition instead of animating on every render or selection refresh. It also moved link-drag startup to the source tower's first pointer press while preserving short-tap inspection and the established empty-ground and multi-pointer camera gestures.

### Option selected, revised, or rejected

- **Selected:** one `0.46s` sine pulse from `1x` to `1.34x` and back to `1x` for both completion and breakage.
- **Selected:** synchronize `ĐẦU` and `CUỐI` because they represent one end-to-end route state.
- **Selected:** suppress overshoot in reduced-motion and deterministic screenshot modes.
- **Selected:** first press on a non-terminal tower both selects it and starts the live link preview.
- **Selected:** releasing without a valid target keeps the source selected for inspection.
- **Rejected:** looping label animation, pulsing from unrelated UI refreshes, requiring a separate link button, or sacrificing background pan/orbit gestures.

### Rationale

A single overshoot makes the semantic color transition noticeable without competing with projectiles or the two-pass LED route acknowledgement. Starting the link on the first press removes a redundant gesture and makes the tutorial hand's existing continuous drag animation match the real control exactly.

### Implementation or verification result

Runtime diagnostics now expose pulse activity, direction, transition count, duration, peak/current scale, and route state. Tutorial and Level 2 interaction tests use direct source-to-target drag on desktop and mobile; existing short-click inspection and camera regression tests remain green. The production build passed and the complete Playwright matrix passed `92` tests with `10` intentional viewport skips. Live desktop/mobile production probes measured a `1.3368x` maximum scale followed by a stable `1x`, confirmed link drag was active on the first press and ended cleanly on release, and reported no console or page errors. Hardware-backed Intel D3D11 canvas probes remained nonblank and within budget at `87` draw calls, `14,320` triangles, `90` geometries, and `7` textures. Deployment `dpl_3pgMx6HuHiNodaW1H8UH9wAjk32Q` reached `READY`; the stable root and all three level routes returned HTTP `200` at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app).

## Entry 60 — Move the Ice lesson below the ammunition source

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-g2rj`
- **Date:** `2026-08-17`

### Problem being addressed

The authored Level 1 Băng placement appeared horizontally beside Lò Đạn in the approved camera. As a result, `Lò Đạn → Hỏa` and `Hỏa → Băng` visually overlapped and read like one line doubling back through Hỏa, which made the first multi-segment network harder for new players to understand.

### Prompt used

The owner requested that the tutorial place Băng below Lò Đạn rather than beside it so the two adjacent lesson links are visibly separate and onboarding is easier to read.

### Important AI response

The AI evaluated valid neighboring grid cells in both screen space and deterministic combat. The most legible cell, `(-1, -6)`, projects nearly directly below Lò Đạn and creates a meaningful turn at Hỏa. Testing also exposed a timing edge case: the repositioned reaction segment could defeat the final enemy before the approved delayed reaction lesson appeared, so the wave needed to remain active through that short delay.

### Option selected, revised, or rejected

- **Selected:** move only the authored Level 1 Băng cell from `(-5, -6)` to `(-1, -6)`.
- **Selected:** preserve Lò Đạn, Hỏa, Trống Gọi Mưa, enemy path, grid, footprints, range validation, obstacles, and global combat values.
- **Selected:** hold a cleared tutorial wave until the pending first-reaction explanation opens, then let dismissal complete the already-cleared wave normally.
- **Selected:** keep the mastery pressure contract by validating an expanded reaction network together with the Cóc skill taught immediately before mastery.
- **Rejected:** the adjacent fallback cell `(-3, -6)` because combat remained viable but the projected route angle was only about `14°` and still read too close to the original collinear composition.
- **Rejected:** changing projectile speed, hit radius, enemy health, path geometry, or general wave balance to compensate for a tutorial-only placement change.

### Rationale

The selected cell solves the actual visual problem instead of merely changing logical coordinates: on production desktop it places Băng `116.49px` below Lò Đạn with `28.63px` horizontal offset, and on mobile `102.41px` below with `25.17px` offset. The `28.29°` turn at Hỏa is clear on both viewports while every link remains valid. Keeping the delayed reaction lesson alive fixes a real onboarding race without weakening combat.

### Implementation or verification result

The production build passed. The complete Playwright matrix passed `92` tests with `10` intentional viewport skips, including desktop/mobile projected-layout, direct-link, reaction-popup, mastery, camera, and campaign coverage. All `26` visual baselines passed after regenerating only the mobile link-drag image affected by the Băng composition. Live production probes reported three directed links, one active chain, green valid endpoint labels, zero console/page errors, and renderer loads of `94` calls / `12,396` triangles on desktop and `91` calls / `12,256` triangles on mobile. Vercel deployment `dpl_9ngSoLM3MKxtaZrWtiQQjgQL21DF` reached `READY`; the stable root and all three level routes returned HTTP `200` at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app). Evidence is stored in `Documents/Prototype/Projectile-Network-TD/artifacts/deploy-2026-08-17-ice-below/`.
