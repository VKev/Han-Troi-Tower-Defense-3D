# AI Collaboration Log — Development — 13/08/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Responsible Codex session ID:** `019ff86e-fd4b-7f03-adae-89a3908ac6f5`
- **Session responsibility:** Set up and verify the portable Unity/Codex toolchain, resolve user-controlled setup decisions, validate the installed AI tools, and establish this first AI collaboration record.
- **Tracking issue:** `TowerDefense3D-769`
- **Security note:** A Voyage API key was pasted into the chat. Its value is intentionally omitted from this document; the exposed key was rejected and rotation was required.

This file summarizes consequential decisions from the responsible session. It is not a verbatim transcript.

## Entry 1 — Khởi động quy trình setup bằng setup-agents

### Vấn đề đang gặp

Project cần được chuẩn bị đầy đủ cho workflow Unity/Codex nhưng phải giữ nguyên công việc hiện có, không commit/push và không tự ý điều khiển vòng đời Unity Editor.

### Prompt đã dùng

`[@setup-agents] do it`

### Phản hồi quan trọng của AI

AI chuyển công việc cho specialist `setup-agents`, thực hiện preflight trên đúng checkout, xác nhận Unity `6000.3.21f1`, Git/Beads, ignore policy và các checkpoint cần người dùng xác nhận.

### Phương án được chọn / sửa / loại

- **Chọn:** setup idempotent theo từng phase với checkpoint Editor đóng/mở rõ ràng.
- **Chọn:** prefix Beads xác định từ project là `TowerDefense3D`.
- **Loại:** tự động launch, close hoặc restart Unity; tự động commit/push.

### Lý do

Các thao tác này bảo toàn trạng thái project, tránh import/package race và giữ quyền kiểm soát các quyết định quan trọng cho người dùng.

### Kết quả sau khi triển khai hoặc kiểm thử

Git được khởi tạo không có `HEAD`, không staging/commit/push. `.gitignore`, Beads, Serena, Video Analyzer, package manifest, archive preparation và các checkpoint setup được tạo hoặc xác minh thành công.

## Entry 2 — Bảo vệ Voyage credential và chọn embedding model

### Vấn đề đang gặp

CocoIndex Code cần Voyage credential; một key đã bị dán trực tiếp vào chat và model cần ưu tiên cho code search.

### Prompt đã dùng

- Người dùng cung cấp Voyage key trong chat; giá trị được lược bỏ khỏi log.
- `use voyage code 4 then fallback if not available`
- Sau đó người dùng xác nhận giữ nguyên cấu hình CocoIndex đã đúng.

### Phản hồi quan trọng của AI

AI cảnh báo key trong chat phải được xem là đã lộ, không được ghi vào project hoặc chuyển qua command/log. AI yêu cầu rotate key, lưu key mới ở user-level settings và chỉ xác nhận bằng `key stored`. Model được kiểm tra theo chuỗi fallback đã phê duyệt.

### Phương án được chọn / sửa / loại

- **Chọn:** lưu credential mới chỉ ở user-level CocoIndex settings.
- **Chọn:** `voyage/voyage-code-4` cho CocoIndex Code.
- **Sửa:** bỏ ý tưởng gán Voyage model riêng cho Docling; Docling chỉ là document converter trong setup này.
- **Loại:** sử dụng hoặc lưu lại key đã xuất hiện trong chat.

### Lý do

Credential đã xuất hiện trong transcript không còn an toàn. `voyage-code-4` phù hợp code retrieval và đã vượt qua cả indexing/query compatibility gates.

### Kết quả sau khi triển khai hoặc kiểm thử

`voyage/voyage-code-4` vượt qua isolated doctor, real-config doctor và semantic query ở dimension `1024`; không cần fallback. CocoIndex hoàn tất với 7 files, 40 chunks và 0 indexing errors. Docling `2.119.0` được xác minh riêng cho PDF, DOCX, XLSX, PPTX và Markdown output.

## Entry 3 — Kích hoạt và xác minh Unity MCP sau Codex restart

### Vấn đề đang gặp

User-level relay configuration đã đúng nhưng Codex session cũ chưa expose Unity MCP tools, nên Phase B không thể tiếp tục an toàn.

### Prompt đã dùng

- `Unity is idle; continue setup`
- Sau khi được yêu cầu restart Codex: `continue setup`

### Phản hồi quan trọng của AI

AI yêu cầu giữ Unity Editor mở, restart Codex một lần và resume đúng task. Sau restart, specialist xác minh project root, Unity PID/version, Editor idle và live registry.

### Phương án được chọn / sửa / loại

- **Chọn:** restart Codex, không restart Unity.
- **Chọn:** chỉ tiếp tục Phase B sau khi MCP xác minh đúng project và trạng thái idle.
- **Loại:** giả định cấu hình tồn tại đồng nghĩa runtime MCP đã sẵn sàng.

### Lý do

MCP registration chỉ có hiệu lực trong Codex process mới; giữ Unity mở tránh lặp package resolution/import không cần thiết.

### Kết quả sau khi triển khai hoặc kiểm thử

Codex nhìn thấy đủ `54` Unity MCP tools; registry báo `54 registered = 54 enabled = 54 advertised`, đúng project và không cần restart thêm.

## Entry 4 — Sửa exporter để đối chiếu chính xác 54 MCP schemas

### Vấn đề đang gặp

Registry exporter ban đầu ghi một số adapted schemas dưới dạng C# debug text, khiến strict JSON/schema equality gate không thể qua.

### Prompt đã dùng

- `authorize exporter repair`
- `authorize test stub`
- `authorize bridge schema repair`

### Phản hồi quan trọng của AI

Hai hướng serialize trực tiếp thất bại do giới hạn dynamic compiler: namespace/assembly không được phép hoặc không được reference. AI đề xuất dùng public `UnityMCPBridge.PrintToolSchemas()`, tạm snapshot/restore clipboard và logger, rồi merge evidence theo exact tool name.

### Phương án được chọn / sửa / loại

- **Chọn:** bridge-based schema capture và ba file setup-tool repair được người dùng phê duyệt.
- **Sửa:** test harness để mô phỏng public bridge/clipboard/logger surface.
- **Loại:** reflection, `System.ComponentModel`, và direct Newtonsoft call trong dynamic `RunCommand`.

### Lý do

Bridge public API dùng đúng runtime serialization surface của Unity Assistant mà không thêm package hoặc sửa product/game code.

### Kết quả sau khi triển khai hoặc kiểm thử

Exporter tests đạt `4/4`, catalog tests đạt `3/3`. Live export và genuine relay client capture cùng trả `54` tools, không duplicate/disabled/mismatch; clipboard và logger được restore. Catalog equality kết thúc với `complete/exact=true`.

## Entry 5 — Cài package, import asset và xử lý Script Updating Consent

### Vấn đề đang gặp

Project cần cài ZLinq/VContainer và import tuần tự 10 asset packages. Unity API Updater nhiều lần hiện consent dialog cho các file Legs Animator đã được compatibility-patch và hash-verified.

### Prompt đã dùng

- `clicked No; continue setup`
- `clicked No again; continue setup`
- `dont use computer use`

### Phản hồi quan trọng của AI

AI yêu cầu chọn **No** để tránh Unity tự sửa vendor source và phá registered hashes. Sau yêu cầu không dùng Computer Use, mọi kiểm tra tiếp theo dùng CLI/Unity MCP; các dialog mới phải do người dùng xử lý thủ công.

### Phương án được chọn / sửa / loại

- **Chọn:** import serial và verify hash/compile/Console sau mỗi package.
- **Chọn:** người dùng tự chọn **No** trên consent dialogs.
- **Loại:** chọn một trong các lựa chọn **Yes**, tự động script update, reimport không có bằng chứng, hoặc UI automation sau khi người dùng từ chối.

### Lý do

Compatibility outputs đã được chuẩn bị và content-addressed; API Updater có thể tạo mutation ngoài profile đã được kiểm thử.

### Kết quả sau khi triển khai hoặc kiểm thử

NuGetForUnity `4.5.0`, ZLinq/ZLinq.Unity `1.5.6`, VContainer `1.19.0` và pinned UPM graph được xác minh. Cả 10 asset packages được import/verify; DOTween setup deterministic đạt yêu cầu. Legs/Retarget hashes không đổi sau khi dialog bị từ chối.

## Entry 6 — Acceptance và kiểm thử từng AI context tool

### Vấn đề đang gặp

Cần phân biệt việc 54 Unity MCP tools xuất hiện đúng schema với việc từng context/index tool thực sự trả kết quả project-owned.

### Prompt đã dùng

- `did anytool not work, have you test all tools`
- `test them 1 please if you havent test`

### Phản hồi quan trọng của AI

AI giải thích không nên chạy end-to-end tất cả mutation/generation/profiler tools chỉ để smoke test. Sau đó AI thực hiện một functional check riêng cho Beads, Better Context, CodeGraph, CocoIndex và Serena.

### Phương án được chọn / sửa / loại

- **Chọn:** read-only functional checks trên cùng khu vực `Readme` để so sánh kết quả.
- **Chọn:** CocoIndex search với `refresh_index=false`.
- **Loại:** destructive tool calls, cloud generation, profiler queries thiếu runtime data và Serena onboarding memories chưa được phê duyệt.

### Lý do

Cách này chứng minh integration hoạt động mà không tạo game-code mutation, tiêu tốn credit hoặc yêu cầu dữ liệu Play Mode không tồn tại.

### Kết quả sau khi triển khai hoặc kiểm thử

- Beads: `bd prime` và `bd ready --json` thành công; queue rỗng hợp lệ.
- Better Context: state fresh; file query nhận diện `Readme` và nested `Section`. Query đầu timeout 34 giây, bounded retry thành công trong 2.1 giây.
- CodeGraph: tìm 14 symbols trong 2 files và đúng dependent relationship.
- CocoIndex: semantic MCP search trả 3 project-owned hits.
- Serena: live symbol overview trả `Readme`, bốn fields và nested `Section`; activation tạo metadata `.serena/` đã được ignore, onboarding memories chưa được tạo.

## Entry 7 — Chuẩn hóa tài liệu và AI collaboration traceability

### Vấn đề đang gặp

Project chưa có root README hoặc cấu trúc tài liệu chuẩn để lưu GDD, tech spec, approved plan và lịch sử quyết định với AI.

### Prompt đã dùng

Người dùng yêu cầu tạo root `README.md`, `Documents/AICollaboration/`, log theo ngày và bắt buộc mỗi entry ghi vấn đề, prompt, phản hồi AI, phương án, lý do, kết quả; đồng thời lưu session ID chịu trách nhiệm.

### Phản hồi quan trọng của AI

AI đề xuất dùng `Documents/` làm canonical documentation root, `Documents/AICollaboration/` cho decision-focused AI logs, lưu session ID thay vì copy raw transcript và luôn redact secrets.

### Phương án được chọn / sửa / loại

- **Chọn:** `README.md` ở root và `Documents/AICollaboration/AI_Collaboration_Log_Dev_13_08.md`.
- **Chọn:** session ID `019ff86e-fd4b-7f03-adae-89a3908ac6f5` chịu trách nhiệm cho log đầu tiên.
- **Loại:** commit raw Codex JSONL hoặc machine-specific transcript path vào project documentation.

### Lý do

Decision log ngắn gọn dễ review và có traceability, trong khi raw transcript chứa tool noise, machine-local paths và từng chứa credential nhạy cảm.

### Kết quả sau khi triển khai hoặc kiểm thử

Root documentation guide và AI collaboration log đầu tiên được tạo theo đúng cấu trúc, có session responsibility, security policy và required entry template. Markdown/link/secret validation được thực hiện trước handoff.

