# AI_Collaboration_Log_GD

**Dự án:** Hạn Trời (Cóc Kiện Trời) — Projectile Network Tower Defense
**Vai:** Game Designer
**Công cụ AI:** Claude (Opus)
**Kỳ ghi nhận:** 31/08/2026 – 10/09/2026
**Trạng thái GDD:** v1.0 Finalized (10/09/2026)

---

## Cách tôi dùng AI trong dự án này

Tôi không dùng AI để sinh ý tưởng. Cơ chế cốt lõi — trụ không tự bắn, người chơi nối trụ để tạo đường đạn — là quyết định của tôi từ đầu, và AI không được phép đụng vào.

Tôi dùng AI cho ba việc:

1. **Mô phỏng cân bằng bằng script** thay vì chơi thử tay. Mọi con số HP, tiền, DPS trong dự án này đều được tính bằng Python trên dữ liệu wave thật, không ước lượng.
2. **Phản biện thiết kế** — bắt AI tìm chiến thuật phá game của chính tôi, tìm chỗ mâu thuẫn giữa các tài liệu, và chỉ ra cái gì tôi đang giả định mà chưa đo.
3. **Đối chiếu chéo** giữa GDD, doc map, tool Unity và build thật — bốn nguồn hay trôi khỏi nhau.

Tôi giữ nguyên tắc: AI đưa đề xuất, tôi chốt. Mọi con số AI đưa ra đều được gắn nhãn `PROPOSAL` cho tới khi tôi duyệt. Mười một lần AI đưa kết luận sai và tôi bắt được — tất cả đều ghi trong log này, vì chúng dạy tôi nhiều hơn những lần nó đúng. Bảng ở cuối log liệt kê đủ, kèm cái gì bị vô hiệu mỗi lần.

---

## ENTRY 01 — Cắt hệ thống damage xuống một chỉ số duy nhất
**31/08/2026**

**Vấn đề.** Bản thiết kế đầu có hai loại damage (vật lý / phép), giáp theo phần trăm, kháng theo phần trăm. Nhưng game của tôi **không có thanh máu và không nhảy số damage**. Người chơi không có cách nào đọc được "giáp giảm 25%".

**Prompt đã dùng.**
> "tôi muốn chỉ có 1 chỉ số damg duy nhất, máu enemy, tốc bắn và tốc độ di chuyển của enemy, tiền kiếm qua tiêu diệt, qua từng wave, chart spawn, chart độ khó. ko giảm kháng phép, giáp hay phần trăm gì hết. keep it simple"

**Phản hồi quan trọng của AI.** AI chỉ ra hệ quả tôi chưa nghĩ tới: nếu bỏ hết phần trăm thì **đơn vị đọc được duy nhất còn lại là "bao nhiêu phát thì chết"**. Muốn nó đọc được thì máu enemy phải là **bội số chính xác** của damage cơ bản — nếu không, số phát sẽ ra số lẻ và người chơi không đếm được.

**Phương án được chốt.** Bỏ toàn bộ hệ thống hai damage, giáp, kháng phép. Máu enemy đưa về bội số của damage cơ bản.

**Lý do.** Ràng buộc "không UI thanh máu" là quyết định thẩm mỹ của tôi, nhưng nó kéo theo một ràng buộc toán học mà tôi chưa nhìn ra. AI không đề xuất bỏ ràng buộc — nó đề xuất cách làm ràng buộc đó hoạt động được.

**Kết quả.** Bảng máu enemy được thiết kế lại theo bội số. Hệ quả phái sinh: "trụ Lửa giết đúng một nửa số phát so với trụ cơ bản" đúng với **mọi** enemy, không ngoại lệ — người chơi học một lần dùng được cho cả bảng.

---

## ENTRY 02 — Cổng Giáp: từ giảm tỉ lệ sang miễn nhiễm nhị phân
**31/08/2026**

**Vấn đề.** Sau khi bỏ phần trăm, con enemy có giáp không còn cơ chế nào. Tôi muốn nó phải bị counter bằng phản ứng Sốc Nhiệt (Lửa ↔ Nước) nhưng chưa biết thể hiện thế nào.

**Prompt đã dùng.** Tôi đưa bảng enemy trong tool và hỏi con Armored (Armor 300) nên xử lý ra sao khi đã bỏ hệ thống giáp theo tỉ lệ.

**Phản hồi quan trọng của AI.** AI tính ra: Armor 300 với damage hiện tại chỉ tiết kiệm được **3 phát đạn**. Trong một game không có số nhảy, chênh 3 phát là **không thể nhận ra**. Đề xuất: đổi sang **miễn nhiễm hoàn toàn cho tới khi trúng Sốc Nhiệt**.

**Phương án được chốt.** Giáp = miễn nhiễm nhị phân. Chìa khoá là Sốc Nhiệt.

**Lý do.** Cùng một luật đã dùng cho con Tàng hình (miễn nhiễm tới khi trúng Nước). **Một khái niệm, hai con enemy** — người chơi không phải học hai thứ.

**Kết quả.** Bảng enemy gọn đi một cột. Nhưng phát sinh một rủi ro mới mà tôi ghi lại: **cả hai cổng đều cần Trụ Nước**, nên Trụ Nước có nguy cơ thành trụ *bắt buộc* thay vì *lựa chọn*. Chưa giải quyết.

---

## ENTRY 03 — Đóng Trụ Đất làm ĐẦY bảng phản ứng
**01/09/2026**

**Vấn đề.** Tôi có 4 nguyên tố (Lửa, Nước, Gió, Đất) nhưng chỉ đặt tên được 3 phản ứng: Sốc Nhiệt, Lan Lửa, Hất Tung. Ba ô trong bảng 4×4 bị trống và tôi định nghĩ thêm tên.

**Prompt đã dùng.** Tôi báo "trụ đất tạm không mở" như một quyết định cắt scope, và hỏi ảnh hưởng tới bảng phản ứng.

**Phản hồi quan trọng của AI.** 4 nguyên tố = 6 cặp = cần 6 phản ứng. **3 nguyên tố = 3 cặp = đúng 3 phản ứng tôi đã có.** Việc đóng Trụ Đất không phải cắt scope — nó làm bảng phản ứng **đầy hoàn hảo**, không còn ô trống nào.

**Phương án được chốt.** Đóng Trụ Đất vĩnh viễn cho bản demo, và viết vào GDD như một **quyết định thiết kế**, không phải một khoản nợ.

**Lý do.** Tôi vốn định nghĩ thêm 3 cái tên phản ứng để lấp chỗ trống. Đó đúng là thứ đề bài gọi là *novelty theater* — thêm nội dung mà không thêm quyết định nào cho người chơi.

**Kết quả.** Bảng phản ứng 3×3 đầy đủ, không có ô "TBD". Tiết kiệm được cả một nhánh thiết kế.

---

## ENTRY 04 — AI so sai mốc khi tính nhịp triệu hồi Boss, rồi tự phát hiện
**01/09/2026**

**Vấn đề.** Boss đứng yên và triệu hồi quái thường. Tôi cần biết nhịp triệu hồi bao nhiêu thì hợp lý.

**Prompt đã dùng.**
> "ok v kéo cái nhịp triệu hồi ra đi. r tổng hợp, reflect và gửi lại cho tôi"

**Phản hồi quan trọng của AI.** Lần đầu AI trả lời: nhịp 8 giây / 3 con là "+32% so với spawn thường, vùng lành". Sau đó **tự phát hiện mình so sai mốc**: nó đem nhịp Boss so với *tốc độ spawn lúc đỉnh* (0,65 giây một con), trong khi lịch spawn nhả 10 con trong 7 giây rồi **dừng hẳn**, còn Boss thì đẻ **suốt 20 giây** của wave. So theo tổng mỗi wave thì con số thật là **+83% — quá tải**.

**Phương án được chốt.** Hạ xuống **10 giây / 2 con = +44%**.

**Lý do.** Tôi giữ lại cả phần sai trong log này vì nó là bài học về cách đọc output AI: con số "+32%" trông rất thuyết phục và tôi đã suýt duyệt. Cái sai không nằm ở phép tính mà ở **chọn mốc so sánh** — thứ mà đọc lướt không thấy được.

**Kết quả.** Nhịp triệu hồi chốt ở 10s/2 con. Và tôi rút ra: mọi con số so sánh AI đưa ra, tôi phải hỏi lại **"so với cái gì"** trước khi duyệt.

---

## ENTRY 05 — Đường cong độ khó đọc ra từ hình học map, không phải chọn tuỳ ý
**01/09/2026**

**Vấn đề.** Tôi muốn mỗi màn có một đường cong độ khó riêng, không phải cùng một hình dáng nhân hệ số.

**Prompt đã dùng.**
> "tùy theo map tôi muốn độ khó theo đường cong khác theo từng màn."

**Phản hồi quan trọng của AI.** AI đọc lại Map Design Doc của tôi và trích ra chính câu tôi đã viết: *"độ khó map tăng bằng số entry, merge, shortcut và timing pressure"*. Từ đó lập luận: bốn thứ đó **sinh ra** hình dáng đường cong, nên **đường cong không phải thứ được chọn — nó đọc được từ hình học map**.

**Phương án được chốt.** Lập bảng ánh xạ: 1 cổng → dốc đều · 2 cổng luân phiên → răng cưa · 3 cổng đồng thời → leo không nhả · có shortcut → gai đột ngột · hai vùng exposure → hai đỉnh.

**Lý do.** Đây là lần AI hữu ích nhất không phải vì nó nghĩ ra gì mới, mà vì nó **đọc lại tài liệu của tôi và chỉ ra tôi đã tự trả lời câu hỏi này rồi**.

**Kết quả.** 10 màn có 10 đường cong khác nhau, mỗi cái giải thích được bằng hình học map. Phép thử tôi tự đặt: che nhãn 10 biểu đồ, nếu không phân biệt được thì việc gán đường cong đã sai.

---

## ENTRY 06 — AI đưa "phát hiện" sai từ trùng hợp số học
**02/09/2026**

**Vấn đề.** Đối chiếu bảng cân bằng tôi làm với bảng enemy thật trong tool Unity.

**Prompt đã dùng.** Tôi gửi ảnh chụp bảng EnemyCatalog trong tool và yêu cầu đối chiếu.

**Phản hồi quan trọng của AI.** AI báo: *"bảng máu của bạn đã được neo sẵn vào damage 8"* — vì 6 trên 7 giá trị HP chia hết cho 8. Nghe rất thuyết phục.

**Phương án — LOẠI BỎ.** Tôi trả lời rằng damage tôi set là **5 cho trụ cơ bản và 10 cho trụ Lửa**, không phải 8. Lúc đó không giá trị HP nào chia chẵn nữa, và AI phải rút lại: đó chỉ là **trùng hợp của bộ số**, không phải chủ đích thiết kế.

**Lý do.** Đây là lỗi nguy hiểm nhất trong cả dự án: AI tìm ra một *pattern có thật* trong dữ liệu rồi gán cho nó một *ý định không có thật*. Nếu tôi không biết con số damage thật của mình, tôi đã tin.

**Kết quả.** Đề xuất mới: đưa HP về bội số của 10 để chia chẵn cho cả 5 lẫn 10. Và tôi thêm một luật làm việc: **AI không được suy ra ý định thiết kế từ dữ liệu — chỉ được báo cáo dữ liệu.**

---

## ENTRY 07 — Con gà: từ aura sang sự kiện có nhịp
**03/09/2026**

**Vấn đề.** AI mô tả con SpeedSupport như một aura buff tốc liên tục. Đó không phải thứ tôi thiết kế.

**Prompt đã dùng.**
> "speed support là 1 con gà, nó đi trong 1 khoảng thời gian sẽ gáy và buff tốc cho enemy, ko phải chết"

**Phản hồi quan trọng của AI.** AI dựng bảng so sánh hai mô hình và chỉ ra: với aura thì counter là *"giết nó, lúc nào cũng được"*; với gáy theo nhịp thì counter là *"giết nó TRƯỚC lần gáy tiếp theo"* — và điều đó buộc mạng lưới phải phủ **đầu route**, chỗ vắng nhất, trong khi bản năng của mọi người chơi TD là dồn hoả lực vào **choke**, chỗ đông nhất.

**Phương án được chốt.** Giữ mô hình gáy theo nhịp.

**Lý do.** Nó tạo ra hai mục tiêu kéo mạng lưới về hai hướng ngược nhau. Đó là quyết định thật, không phải thêm một chỉ số.

**Kết quả.** Con gà từ "một con quái buff" thành một bài học kill-priority, và nó trở thành nội dung dạy chính của màn 3.

---

## ENTRY 08 — Kiểm chứng bằng mô phỏng: nhịp gáy theo thời gian hay theo quãng đường
**03/09/2026**

**Vấn đề.** Nhịp gáy của con gà nên đếm theo thời gian hay theo quãng đường đã đi? Tôi nghiêng về quãng đường nhưng không có lý do vững.

**Prompt đã dùng.**
> "quãng đường và có ngắt" — rồi sau đó: "nếu theo thời gian"

**Phản hồi quan trọng của AI.** Khi tôi bắt AI tính lại phương án ngược lại, nó **tự đính chính lập luận trước của chính nó**. Ban đầu nó viết "theo thời gian thì làm chậm khiến gà gáy nhiều hơn nên tự hại mình". Chạy mô phỏng ra kết quả khác: gáy nhiều hơn nhưng **buff uptime gần như không đổi** (62–72%), nên làm chậm không phản tác dụng — nó **vô dụng**.

Công thức rút ra: `uptime = (số lần gáy × 4 giây) ÷ (thời gian trên route)`. Theo thời gian, tử số tăng cùng mẫu số nên uptime **bị ghim** — không trụ nào trong game hạ được nó. Theo quãng đường, tử số cố định nên **mọi thứ làm chậm đều hạ uptime tuyến tính**.

**Phương án được chốt.** Nhịp gáy tính theo **quãng đường**, 9,5 m một lần.

**Lý do.** Không phải vì "nghe hợp lý hơn", mà vì mô phỏng cho thấy nó là cách duy nhất khiến **Trụ Nước có việc làm với con gà**. Trụ Nước là trụ 0 damage mà người chơi bắt buộc phải mua — đây là chỗ hiếm hoi nó được *chọn* chứ không bị *ép*.

**Kết quả.** Chốt luật quãng đường. Và một luật dùng chung được: *"damage không ngắt channel, CC ngắt"* — áp cho con gà, cho Kẻ Mở Đường, và sau này cho stun của Hero. Một luật, ba nơi dùng.

---

## ENTRY 09 — Ba map là ba bài học hình học, đo bằng pixel
**05/09/2026**

**Vấn đề.** Ba map đầu đã greybox xong. Tôi cần biết mỗi map dạy được gì, và tutorial nên dạy gì ở đâu.

**Prompt đã dùng.** Tôi gửi ảnh chụp scene của ba map và nói: *"tôi sẽ gửi bạn ảnh 3 map đầu."*

**Phản hồi quan trọng của AI.** AI **đo trực tiếp toạ độ đường đi từ pixel của ảnh** thay vì ước lượng bằng mắt, rồi kết luận ba map không phải ba mức độ khó mà là **ba bài học khác nhau về cách đặt dây**:

- Map 1 (chữ L, không nếp gấp) → chỉ dạy được **dọc hay ngang**
- Map 2 (route gấp dọc) → dây **ngang** ăn nhiều hơn dây dọc, ngược hẳn map 1
- Map 3 (4 hàng song song) → **một đoạn dây cắt cả bốn hàng**

Và chỉ ra map 2 tồn tại để **chặn người chơi biến bài học map 1 thành mê tín** — không có luật "song song là tốt", chỉ có luật "đọc hình route trước khi kéo".

**Phương án được chốt.** Gán ba bài học vào ba wave cụ thể: M1·W2, M2·W2, M3·W1.

**Lý do.** Hai trong ba wave đó trước đây là wave chết không dạy gì. Bài học vốn đã nằm sẵn trong map tôi dựng — tôi chỉ chưa dùng.

**Kết quả.** Số wave "không dạy gì" giảm từ 12 xuống 10 trên tổng 26 wave tutorial. Bảng chỉ dẫn tutorial được viết lại theo ba bài này.

---

## ENTRY 10 — Mô phỏng economy trên dữ liệu wave thật
**06/09/2026**

**Vấn đề.** Đề bài yêu cầu *"tài nguyên phải luôn khan hiếm; nếu đến giữa màn người chơi thừa tiền, economy chưa đạt"*. Tôi không có cách nào biết mình có đạt hay không.

**Prompt đã dùng.** Tôi gửi ảnh chụp bảng wave của 9 màn trong tool và nói: *"đây là stat chính thức"*.

**Phản hồi quan trọng của AI.** AI chép toàn bộ 856 dòng vào script Python và chạy hai phép đo:

1. **Wave nào người chơi dư tiền mua hết trụ** → màn 4,5,6 là wave 4/10; **màn 7 là wave 2/10**
2. **HP mỗi wave ÷ tiền tích luỹ khi vào wave đó** → màn 7 đi 28 → 45 → 45 → 17 → 8 → 13 → 10 → 12 → **7** → 18

Kết luận: **đỉnh khó thật của mọi màn nằm ở wave 2–3, rồi rơi 60–70% và không bao giờ về lại**. Ở màn 7, wave 3 khó gấp **6,4 lần** wave cuối — dù wave cuối có HP cao nhất màn.

**Phương án được chốt.** Chưa sửa. Đang chờ chốt giá nâng cấp — hiện `Upgrade Cost = 0` cho cả sáu trụ nên chưa có chỗ hút tiền.

**Lý do.** Đề bài nói đường cong phải có *"nhịp căng — nghỉ"*. Dữ liệu có nhịp, nhưng **nhịp ngược**: căng ở đầu, nghỉ suốt phần còn lại. Nguyên nhân duy nhất là tiền kill (10–18/con) quá cao so với giá trụ, mà mỗi màn có 100–134 con.

**Kết quả.** `CHƯA TRIỂN KHAI.` Con số cần đạt: thêm khoảng **1.900 gold sink** dạng nâng cấp mỗi màn.

---

## ENTRY 11 — Yêu cầu AI tìm chiến thuật phá game của chính tôi
**06/09/2026**

**Vấn đề.** Đề bài chấm trượt nếu *"người chơi dùng đúng một công thức bố trí cho cả 10 màn mà vẫn thắng"*. Tôi cần biết công thức đó có tồn tại không.

**Prompt đã dùng.** Tôi gửi bảng giá trụ chính thức trong tool và yêu cầu AI phân tích, sau khi trước đó đã yêu cầu nó đóng vai người chơi tìm cách phá game.

**Phản hồi quan trọng của AI.** AI tìm ra một chiến thuật và tính ra con số:

> Nếu damage **cộng dồn** dọc chain, và `Instances Per Level = 0` nghĩa là trụ không giới hạn số lượng, thì chiến thuật tối ưu là **xây chuỗi trụ Lửa dài nhất mà tiền cho phép**. Ngân sách màn 8 (3.242 gold) mua được 15 trụ Lửa → đoạn dây cuối gây 155 damage → 183 DPS chỉ trên một đoạn → giết sạch 2.440 HP của cả màn trong **13 giây**, trong khi màn spawn suốt 76 giây. **Thừa 5,7 lần.** Và không có điểm bão hoà.

**Phương án — chiến thuật này KHÔNG tồn tại.** Tôi trả lời: *"nó thay thế"* — mỗi đoạn dây mang damage của trụ phát ra nó, không cộng dồn.

**Lý do.** Với luật thay thế, mười trụ Lửa vẫn cho mười đoạn **10 damage** mỗi đoạn. Mua thêm trụ = **thêm vùng phủ**, không thêm sức mạnh. Toàn bộ giá trị dồn vào hình học map — đúng như thiết kế.

**Kết quả.** Chiến thuật phá game biến mất. Nhưng bài tập này vẫn có giá trị: nó cho tôi một câu để viết vào GDD — *vì sao game này không thể bị phá bằng tiền*. Và nó lôi ra một lỗi lớn hơn, ghi ở entry tiếp theo.

---

## ENTRY 12 — AI giữ một giả định sai suốt sáu ngày, và tôi bắt được quá muộn
**06/09/2026**

**Vấn đề.** Trong lượt trao đổi đầu tiên tôi mô tả luật damage:
> "đường đạn tiếp theo từ trụ lửa nối bất cứ trụ nào sẽ mang **đặc tính của trụ lửa**. rồi đạn đi từ trụ nối của trụ lửa sẽ mang **đặc tính của trụ đó**."

Câu đó nghĩa là **thay thế**. AI diễn giải thành **cộng dồn** và viết vào file: *"Chain Gen→Lửa = 5+10 = 15/viên"*.

**Prompt đã dùng.** Sau khi AI đưa ra bảng DPS lần thứ ba, tôi hỏi lại và nó tự nêu nghi vấn. Tôi trả lời: *"nó thay thế"*.

**Phản hồi quan trọng của AI.** Khi tính lại với luật đúng, DPS thật của một chain đầy đủ là **20,0** — không phải **29,4** rồi **37,5** như AI đã ghi. Sai **gần gấp đôi**.

**Phương án — LOẠI BỎ toàn bộ.** Những thứ phải làm lại: mốc mua hết 3.900 · mốc 5.200 · đường cong kinh tế 67%→82% · bảng phân tích Hero · phân tích cổ chai Tỏa Hồn · sheet Throughput trong file Excel.

**Lý do.** Con số `5+10=15` nằm trong deliverable nhiều ngày và tôi nhìn thấy nó nhiều lần mà không phản đối, nên AI coi như đã được duyệt. **Im lặng bị hiểu thành đồng ý.**

**Kết quả.** Luật thay thế được chốt và ghi vào tài liệu. Và tôi đổi cách làm việc: **mọi lần AI diễn giải một câu mô tả cơ chế thành công thức số, tôi phải xác nhận công thức đó ngay lượt đó** — không để nó trôi vào bảng tính rồi mới kiểm.

Đây là entry tôi thấy đáng giá nhất trong cả log. Nó không phải chuyện AI sai — mà là chuyện **quy trình duyệt của tôi có lỗ**.

---

## ENTRY 13 — Trụ Gió bị Trụ Lửa lấn át hoàn toàn
**06/09/2026**

**Vấn đề.** Sau khi chốt luật thay thế, cần kiểm lại vai trò từng trụ.

**Prompt đã dùng.** Tôi báo *"hero ko nhận đạn, chỉ là 1 trụ gây stun và damg tách biệt"* và yêu cầu tính lại.

**Phản hồi quan trọng của AI.** Với cùng giá 200 gold:
- `Gen → Lửa → Nước → **Gió**` = **20 DPS**
- `Gen → Lửa → **Lửa** → Nước` = **25 DPS**

Lý do: Gió bắn 1,54 viên/giây, Lửa 1,18 — nhưng **Generator chỉ đẩy ra 1,00**. Không trụ nguyên tố nào từng chạm trần của mình, nên **ưu thế tốc độ của Gió là con số chết**.

**Phương án đề xuất — CHƯA CHỐT.** Nâng Generator lên 1,54 viên/giây. Khi đó Lửa (1,18) thành cổ chai và Gió lấy lại đúng vai "ống rộng" mà tôi thiết kế cho nó.

**Lý do.** Đề bài yêu cầu *"không trụ nào tối ưu ở mọi tình huống"*. Ở đây ngược lại — có một trụ **kém hơn ở mọi tình huống**. Đó là lỗi cùng loại, chỉ đảo dấu.

**Kết quả.** `CHƯA TRIỂN KHAI.` Đang cân nhắc giữa nâng tốc Generator và nâng damage Gió.

---

## ENTRY 14 — Đường cong win rate làm ĐẶC TẢ, không phải mô tả
**06/09/2026**

**Vấn đề.** Bảng wave của tôi được viết mà không có mục tiêu độ khó nào để nhắm vào. Không ai nói được nó đúng hay sai.

**Prompt đã dùng.**
> "chart win rate tôi đang muốn trừ 2 màn tutorial đầu là 100% tôi muốn nó tuân theo 1 chart cụ thể"

**Phản hồi quan trọng của AI.** AI đề xuất đảo ngược quan hệ: **đường cong là yêu cầu, bảng wave là cách thực hiện**. Và quy đường cong ra ràng buộc đo được:

| | |
|---|---|
| Lượt chơi cho 10 màn | 15,6 |
| Thời lượng demo @ 4 phút/màn | 62 phút |
| Thua 3 lần liên tiếp ở màn 8 | 22% |

Hạ đáy màn 8 từ 40% xuống 30% thì demo vượt 70 phút và tỉ lệ bỏ ở màn 8 lên **34%** — một phần ba người chơi không bao giờ thấy con boss.

**Phương án được chốt.** Tutorial (màn 1–2) khoá 100%. Tám màn còn lại theo luật: **dạy thứ mới thì tụt, lặp lại thứ đã học thì hồi**. Đáy ở màn 8 (ba cổng đồng thời lần đầu), không phải màn 10.

**Lý do.** Đề bài đòi *"nhịp căng — nghỉ, không phải đường thẳng dốc đều"*. Luật này sinh ra nhịp đó một cách có thể giải thích được cho từng điểm gãy, thay vì chọn số theo cảm tính.

**Kết quả.** `CHƯA KIỂM CHỨNG.` Kế hoạch: playtest 5 người, chỉ ghi lần đầu mỗi màn thắng hay thua. Màn nào lệch quá 15 điểm thì sửa HP và số lượng của màn đó — **không sửa đường cong**.

---

## ENTRY 15 — Nạp catalog chính thức, và mốc "mua hết" của AI sai gần 6 lần
**07/09/2026**

**Vấn đề.** Tôi đưa ảnh TowerCatalog và LevelCatalog thật từ tool. Trước đó AI đã kết luận "người chơi thừa tiền ở 8 màn, thừa 8 255 vàng" dựa trên mốc mua hết = **650** (một bộ 5 trụ Ngũ Hành giá cũ).

**Phản hồi quan trọng của AI.** Sau khi có giá thật, AI tự tính lại: mốc mua hết không phải 650 mà là **build 1 120 + nâng cấp 2 610 = 3 730**. Tỷ lệ tổng thu / mốc mua hết ra **0.44× – 0.90×**, tức **chưa màn nào mua nổi hết**.

**Phương án được chốt.** Bỏ toàn bộ kết luận "thừa tiền". Giữ start gold 440.

**Lý do.** Con số 440 tôi set trước đó tình cờ gần đúng sàn: chain tối thiểu = KIM 220 + một hành 200 = **420**. Start gold 440 = 420 + 20.

**Kết quả.** Kết luận cũ bị đảo ngược hoàn toàn — từ "thừa tiền nghiêm trọng" thành "khan hiếm đúng ý". Bài học tôi ghi: **AI so sánh với mốc nào thì phải in mốc đó ra**, không được để nó ẩn trong câu kết luận.

---

## ENTRY 16 — Mô hình độ phơi nhiễm: chỉ số dai thật không phải HP
**08/09/2026**

**Vấn đề.** Game này gây damage khi collider quái cắt đoạn dây. Tôi biết `Base Hit Radius` có trong tool nhưng chưa biết nó ảnh hưởng balance thế nào.

**Prompt đã dùng.** Tôi đưa EnemyCatalog đầy đủ và không hỏi gì, chỉ đưa ảnh.

**Phản hồi quan trọng của AI.** AI dựng mô hình: một con bán kính R đi tốc v thì **nằm trên đoạn dây trong 2R/v giây**, nên số phát nó ăn tỉ lệ với **R ÷ v**. Từ đó **độ dai thật = HP ÷ (R ÷ v)**.

Hệ quả đo được, và nó đảo bảng xếp hạng:

| Quái | HP | Độ dai thật so với Thường |
|---|---|---|
| Kháng phép | 30 (= Trâu) | **3.96×** — vì R chỉ 0.371 |
| Trâu | 30 | 1.74× |
| Tàng hình | 20 (= Thường) | 1.64× |
| Boss | 600 (= 30× Thường) | **chỉ 11.14×** |

**Phương án được chốt.** Nhận `phơi nhiễm = R ÷ v` làm chỉ số phái sinh chính của bảng balance. Ghi vào GDD 7.4 làm một trục độ khó riêng.

**Lý do.** Nó giải thích được thứ tôi thấy mà không lý giải được: con Kháng phép "cảm giác dai hơn nhiều so với máu của nó". Không phải cảm giác — nó dai gần 4 lần.

**Kết quả.** Đổi cả hướng cân bằng: THỦY (slow) không còn là utility mà là **hệ số nhân damage cho toàn mạng**, vì giảm v thì tăng phơi nhiễm. `INFERENCE` — chưa xác nhận trong build.

---

## ENTRY 17 — AI giả định nhịp nguồn 1.00 s và giữ nó suốt nhiều ngày
**08/09/2026**

**Vấn đề.** Mọi phân tích throughput của AI đều dựa trên "nguồn 1.00 viên/s". Nó kết luận: "không có gì trong chain nghẹn được, vì mọi trụ nguyên tố đều nhanh hơn nguồn".

**Cái sai.** `Attack Interval` thật của Generator trong ảnh tool là **0.65** → **1.538 viên/s**. Nghĩa là HỎA và THỦY (0.85 → 1.176 viên/s) **đã nghẹn sẵn**, mất **24 % nhịp**.

**Hệ quả.** Ba thứ bị vô hiệu cùng lúc: kết luận "không nghẹn"; đề xuất "nâng nhịp nguồn lên 1.25 s" (vô nghĩa, vì nó đã nhanh hơn thế); và câu tôi trả lời AI trước đó — *"tôi chưa muốn nó nghẹn bây giờ"* — hoá ra là trả lời cho một vấn đề AI tự tạo ra.

**Lý do sai.** AI đọc một bảng cũ có interval 1.00 rồi **không đọc lại khi tôi đưa ảnh mới**.

**Kết quả.** Luật tôi đặt sau đó: **mỗi lần tôi đưa ảnh tool, AI phải in lại toàn bộ giá trị nó đang dùng trước khi kết luận gì.** Đây là entry đắt thứ hai sau Entry 12.

Ghi chú 10/09: **cùng con số này lại xung đột trong GDD final** — 4.1a ghi 0.65 nhưng 4.1b và luật 3.2 #3 ghi 1.00. Xem Entry 24.

---

## ENTRY 18 — Luật thay thế: chuỗi 4 trụ và chuỗi 2 trụ mạnh bằng nhau
**08/09/2026**

**Vấn đề.** Tôi chốt luật "mỗi đoạn dây mang damage của trụ phát ra nó". Tôi chưa đo hệ quả.

**Phản hồi quan trọng của AI.** AI tính ra con số làm tôi phải đọc hai lần: chuỗi **2 trụ** và chuỗi **4 trụ** đều cho **21.2 DPS**. Trụ ở giữa cho **0 DPS** — nó chỉ cho phủ sóng.

**Phương án được chốt.** Nhận nó làm **mâu thuẫn trung tâm** của game (GDD 1.2): PHỦ RỘNG ↔ ĐÁNH MẠNH. Mỗi trụ chỉ làm được một trong hai.

**Lý do.** Đây là lần AI hữu ích nhất trong cả dự án, và nó không sáng tạo gì — nó chỉ **tính hệ quả của luật tôi đã đặt**. Luật là của tôi; con số 21.2 là của nó.

**Kết quả.** Câu mâu thuẫn trung tâm trong GDD được viết từ con số này, không phải từ cảm giác. Và nó cho tôi một câu để nói với người ngoài: *"thêm trụ = thêm phủ sóng, không phải thêm sức mạnh"*.

Kèm một cảnh báo AI nêu mà tôi chưa xử lý: mâu thuẫn này **chỉ đứng nếu có cap số trụ**. `Instances Per Level` đang = 0 → màn giàu nhất kê được **16 trụ** và mâu thuẫn bốc hơi.

---

## ENTRY 19 — AI đề xuất một mâu thuẫn, tôi bắt nó đo, và nó sai
**08/09/2026**

**Vấn đề.** AI đề xuất mâu thuẫn trung tâm là "dây ngắn (rẻ, ít phủ) ↔ dây cắt route nhiều lần".

**Prompt đã dùng.** Tôi không tranh luận, tôi bắt nó đo trên map thật.

**Cái sai.** Đo trên map 3: đoạn **55 px cắt route 4 lần** thắng đoạn **303 px cắt 1 lần**. Ngắn và cắt-nhiều **cùng một hướng**, không đối nghịch. Mâu thuẫn AI đề xuất **không tồn tại**.

**Kết quả.** Bỏ. Dùng mâu thuẫn từ Entry 18 (đo được) thay cho mâu thuẫn nghe hay (không đo được). Bài học: **một mâu thuẫn chưa đo chỉ là một câu văn.**

---

## ENTRY 20 — AI gán cảm xúc của nhân vật cho người chơi
**08/09/2026**

**Vấn đề.** Tôi nhờ AI đề xuất câu feeling mục tiêu cho GDD 1.1.

**Cái sai.** AI đề xuất cảm xúc mục tiêu là **"GAN"** (dám kiện Trời). Tôi chỉ ra: người chơi **không** kiện Trời — người chơi **giúp** kẻ đi kiện. Gan là cảm xúc của **con cóc**, không phải của người chơi.

AI tự phân loại lại lỗi của nó: đây không phải câu *yếu*, đây là câu **sai chủ thể**. Nó lấy cảm xúc nhân vật rồi dán lên người chơi.

**Phương án được chốt.** Câu feeling xoay quanh vai **người mở đường / chiến thuật gia** — người chơi là kẻ căng dây, không phải kẻ đi kiện. Bản cuối trong GDD 1.1 là câu tôi tự viết, AI chỉ soi cấu trúc.

**Lý do.** Tôi cũng tự bắt được một lỗi của mình ở đây: bản đầu tôi viết "người mở đường", nhưng AI chỉ ra **`Kẻ Mở Đường` đã là tên một con enemy** trong doc map. Đổi thành "chiến thuật gia".

**Kết quả.** GDD 1.1 và 1.2 được viết xong. Bài học: **AI không phân biệt được cảm xúc của nhân vật và cảm xúc của người chơi trừ khi tôi nói rõ ai đang cầm máy.**

---

## ENTRY 21 — Buff của con gà đọc lại thành giảm damage 33 %
**09/09/2026**

**Vấn đề.** Con gà buff tốc. Tôi nghĩ về nó như "quái tới sớm hơn".

**Phản hồi quan trọng của AI.** Vì damage của game này là **không gian** (cắt đoạn dây), không phải **thời gian**, tăng tốc **giảm phơi nhiễm**: +50 % tốc → phơi nhiễm **×2/3** → **giảm 33 % damage nhận vào**.

Nghĩa là con gà không phải "thêm thân" — nó là một đòn **giảm damage diện rộng**. Màn 10 wave 8 có **9 con gà**.

**Phương án được chốt.** Nhận đây là lý do THỦY (slow) là counter của gà — slow đẩy phơi nhiễm ngược lại.

**Ghi chú của bản final.** GDD v1.0 chốt nhịp gáy theo **quãng đường 9.5 m** (luật 3.2 #9), không theo thời gian, nên con số uptime 62 % → 34 % thay cho phép tính phơi nhiễm ở trên. Bảng 5.1 vẫn còn chữ "buff 4 s" — **đó là chữ cũ sót lại**, đã ghi vào bảng xung đột.

---

## ENTRY 22 — Thứ tự xử lý: một chi tiết không nhìn ra được từ gameplay
**09/09/2026**

**Vấn đề.** GDD 3.3 trống. Tôi cần thứ tự xử lý trong một tick.

**Phản hồi quan trọng của AI.** Hai điều tôi chưa nghĩ tới:

1. **Vị trí phải xong trước damage.** Damage đo bằng đoạn dây cắt collider, nên phải chốt vị trí trước khi đo giao nhau. Push là **thay đổi vị trí**, nên nó thuộc bước di chuyển, không thuộc bước damage.
2. **Backpressure phải chạy từ Tỏa Hồn về nguồn**, không phải ngược lại. Nếu chạy chiều nguồn → endpoint thì mỗi mắt tốn một tick, tức **chain càng dài càng chậm** — và đó là một bug **không ai nhìn ra được từ gameplay**, chỉ thấy khi đọc thứ tự.

**Phương án được chốt.** 5 bước trong GDD 3.3, đúng thứ tự trên.

**Kết quả.** Đây là loại đóng góp tôi thấy AI mạnh nhất: không phải ý tưởng, mà là **hệ quả kỹ thuật của luật đã có**.

---

## ENTRY 23 — Bậc nâng cấp giữa là bậc tệ nhất
**09/09/2026**

**Vấn đề.** Tôi nạp hiệu ứng tier thật (HỎA T1 mult 1.35, T2 mult 1.70 + burn 4/0.5/3).

**Phản hồi quan trọng của AI.** Tính ra giá trên mỗi điểm damage:

| Bậc | Cost | +Damage | Vàng / +1 damage |
|---|---|---|---|
| HỎA T1 | 280 | +3.5 (18 → 21.5) | **80** |
| HỎA T2 | 420 | **+19.5** (21.5 → 41) | **21.5** |

T2 tốt hơn T1 **3.7 lần** trên mỗi vàng. Đường nâng cấp **dốc ngược** — người chơi sẽ đọc T1 là **thuế để tới T2**, không phải một món mua.

**Phương án được chốt.** `CHƯA CHỐT.` Tôi giữ giá hiện tại và ghi lại vấn đề. Nếu dồn payoff về cuối là chủ ý thì được, nhưng phải biết **bậc T1 hiện không tạo quyết định nào**.

---

## ENTRY 24 — Đưa việc đối chiếu tài liệu cho máy: nó bắt được 9 chỗ GDD tự xung đột
**10/09/2026**

**Vấn đề.** GDD đã Finalized. Tôi cần một file balance dựng **từ GDD**, không từ ghi chú rời.

**Prompt đã dùng.**
> "đây là file final, mọi thứ trong file là final. dựa vào đây, cho tôi full file excel theo đúng đề bài. format theo đúng constant, calc, monitor với đầy đủ data"

**Phản hồi quan trọng của AI.** Thay vì chọn một con số khi gặp mâu thuẫn, AI **encode cả hai** và biến việc đối chiếu thành công thức. Kết quả: nó liệt kê **9 chỗ GDD v1.0 tự nói khác nhau**, trong đó nặng nhất:

- **Nhịp nguồn KIM**: 4.1a ghi 0.65 s (1.538 viên/s) · 4.1b + luật 3.2 #3 ghi 1.00 s. Kiểm chéo: 4.1a nói *"HỎA mất 24 % nhịp"* và 3.5 nói *"2 chain vượt 2.22 viên/s"* — **cả hai chỉ đúng nếu nguồn là 0.65**.
- **Tổng tiền nâng cấp**: bảng 4.2 ra 2 030 (2 tier) · mục 6.2 ghi 2 610 (hàm ý 3 tier).
- **Biên tha thứ màn 1**: bảng ghi 37 % · đoạn văn ngay dưới bảng ghi 60.6 %.
- **Tên hành của Gió**: 4.1a và 4.3 ghi PHONG · 6.2 ghi MỘC.

Và sheet MATRIX so bảng 4.3 với số tính ra, bắt **3 ô GDD sai**:

| Ô | GDD 4.3 | Số tính | Vì sao |
|---|---|---|---|
| KIM × Trâu | X | ✔ | Trâu không có cổng miễn nhiễm nào; KIM giết được trong 6 phát |
| PHONG × Kháng phép | ✔ | X | Cổng đó chỉ mở bằng Sốc Nhiệt (HỎA + THỦY) |
| Hero × Kháng phép | ✔ | X | Hero không phải nguyên tố, không tạo được Sốc Nhiệt |

Cộng hai ô lệch vì **tiêu chí khác**, không phải sai: GDD chấm PHONG ✔✔ ở cột Gà theo *chặn được lần gáy*, còn công thức chấm theo *tốc độ giết*.

**Phương án được chốt.** Nhận cách làm này làm quy trình: **bảng chứng minh nằm trong file balance**, không nằm trong đầu ai. Và **cả 9 xung đột đã được chốt ngày 10/09** — khối 1 của sheet MONITOR giờ là **danh sách sửa GDD**, mỗi dòng ghi rõ bên nào thắng và phải sửa câu nào ở mục nào.

**Lý do.** Trước đó tôi đối chiếu tài liệu bằng cách đọc lại — và đọc lại thì bỏ sót, vì mắt tin cái mình vừa viết. Công thức không tin gì cả.

**Kết quả.** Bảng kiểm chạy **21 dòng, 17 ĐẠT**. Bốn dòng còn lại **không phải lỗi**: ba dòng là quyết định thiết kế được đánh dấu `CHỦ Ý` (không cap trụ · bước dốc màn 3 · hit rate do người chơi), một dòng là `CHỜ SỬA GDD` (4 hàng ma trận 4.3).

---

## ENTRY 25 — Bậc 3: nguyên tắc "không tăng damage mỗi phát"
**10/09/2026**

**Vấn đề.** GDD 4.2 chỉ có 2 tier. Mốc mua hết chỉ còn 3 150, và bảng kiểm phát hiện **màn 8 (1.06×) và màn 10 (1.04×) dư tiền** — nâng cấp hết chỗ tiêu.

**Prompt đã dùng.** *"cho nâng cấp bậc 3."*

**Phản hồi quan trọng của AI.** Nó không đề xuất tăng damage. Lý do nó đưa: HP quái **cố định** (luật 3.2 #10) và HTK là thứ người chơi đọc; HỎA T2 đã **41/phát**, một phát giết mọi con ≤ 30 HP. Tăng nữa là **xoá bảng 5.2**. Nên bậc 3 phải tăng **số mục tiêu** và **độ mạnh hiệu ứng**.

| Hành | T3 | Giá | Hiệu ứng |
|---|---|---|---|
| HỎA | Hoả Tử Liên Xạ | 560 | Burn lan 1 địch chưa cháy khi mục tiêu chết vì burn. Damage **giữ nguyên 41** |
| THỦY | Sa Phất | 490 | **Lần đầu tăng Slow Strength** 0.304 → 0.45 → damage toàn mạng +44 % → **+82 %** |
| PHONG | Nghịch Phong | 420 | Push 0.7 → 0.8 m, và đẩy lùi **hạ được mốc high-water-mark** của Gà |

**Phương án được chốt.** `DECIDED 10/09/2026` — duyệt nguyên bộ. Con số kinh tế đã kiểm: nâng cấp đủ 2 030 → **3 500**, mốc mua hết 3 150 → **4 620**, tỷ lệ mọi màn về **0.36× – 0.73×** → không màn nào mua nổi hết. Lỗi kinh tế mà bảng kiểm phát hiện (màn 8 và màn 10 dư tiền) **được sửa bằng chính bậc 3 này**.

**Ghi chú khi duyệt.** Hiệu ứng "burn lan" và "hạ mốc high-water-mark" là **cơ chế mới**, không phải con số. Chúng vào GDD dưới dạng đặc tả, nhưng vẫn phải prototype để xác nhận cảm giác — con số kinh tế đúng không bảo đảm cơ chế vui.

---

## ENTRY 26 — Ba hạng mục treo được chốt là "chủ ý", không phải được sửa
**10/09/2026**

**Vấn đề.** Bảng kiểm để lại ba dòng KHÔNG ĐẠT. AI trình bày cả ba như lỗi cần sửa.

**Quyết định của tôi.** Cả ba đều là **quyết định thiết kế**, không phải lỗi:

| Hạng mục | Chốt | Lý do |
|---|---|---|
| `Instances Per Level` | **Không cap** | Số trụ bị giới hạn bởi tiền, không cần giới hạn bằng luật |
| Bước dốc màn 2 → 3 (−17.2 điểm %) | **Giữ nguyên, có chủ đích** | Màn 3 là màn thật đầu tiên; cú tụt đó là chỗ game bắt đầu |
| Tỷ lệ đạn trúng thực tế | **Không phải hằng số** | Số phát trúng do người chơi đặt trụ và kéo dây quyết định — đó chính là kỹ năng của game này |

**Phương án được chốt.** Thay vì sửa số, tôi bắt bảng kiểm **phân biệt được lỗi với chủ ý**. Thêm núm `Bước dốc CHỦ Ý?` vào CONSTANTS khối F (cùng kiểu với `Breather?` đã có), và đổi ba dòng kiểm đó từ pass/fail thành **báo hệ quả**.

**Lý do.** Một bảng kiểm báo đỏ ở chỗ mình cố ý làm vậy thì lần sau không ai đọc nó nữa. Ngưỡng phải biết chỗ nào được miễn — và chỗ miễn phải là **một núm vặn có tên**, không phải một ngoại lệ chôn trong công thức.

**Kết quả.** Bảng kiểm còn **21 dòng, 17 ĐẠT**. Bốn dòng còn lại: ba dòng `CHỦ Ý` (in ra hệ quả thay vì phán quyết) và một dòng `CHỜ SỬA GDD`.

Dòng `CHỦ Ý` của việc không cap trụ vẫn in một con số tôi muốn thấy: **màn nghèo nhất mua được 8 trụ**. Đó là chỗ mâu thuẫn PHỦ RỘNG ↔ ĐÁNH MẠNH thật sự ràng buộc. Ở màn giàu nhất là 16 trụ — mâu thuẫn lỏng hơn, và tôi biết điều đó.

---

## Bảng lỗi của AI trong cả dự án

| # | Lỗi | Ngày | Cái gì bị vô hiệu |
|---|---|---|---|
| 1 | So sai mốc nhịp triệu hồi Boss ("+32 %" thật ra +83 %) | 01/09 | Kết luận "vùng lành" |
| 2 | Suy ý định thiết kế từ trùng hợp số học (HP chia hết cho 8) | 02/09 | "Bảng đã neo vào damage 8" |
| 3 | Biến mô tả bằng lời thành `5+10=15` rồi giữ 6 ngày | 06/09 | Toàn bộ bảng cân bằng, mốc 3 900 và 5 200 |
| 4 | Mốc "mua hết" = 650, thật ra 3 730 | 07/09 | Kết luận "thừa tiền 8 màn / 8 255 vàng" |
| 5 | Giả định nhịp nguồn 1.00 s, thật ra 0.65 s | 08/09 | Phân tích "không có gì nghẹn"; đề xuất nâng nguồn lên 1.25 |
| 6 | Route length 39.6 m trình bày như "suy từ tutorial" — thực ra là giả định của chính nó | 08/09 | Con số 24 % ở bản đầu |
| 7 | Đề xuất mâu thuẫn "dây ngắn ↔ cắt nhiều lần", đo ra là sai | 08/09 | Cả mâu thuẫn đó |
| 8 | Gán cảm xúc của con cóc ("GAN") cho người chơi | 08/09 | Câu feeling bản 1 |
| 9 | Ghi mini boss màn 10 là ×1, thật ra ×2 | 08/09 | Bảng wave màn 10 bản đầu |
| 10 | Ghi Wind cap 5, catalog là 3 | 09/09 | Bảng trụ bản đầu |
| 11 | Nói "4 chỗ xung đột" khi thật ra là 9 | 10/09 | Câu tóm tắt đó |

Tôi bắt được cả 11. Lỗi số 3 mất nhiều nhất — sáu ngày. Lỗi số 5 và 11 cùng một dạng: **AI tự tin về con số nó không đọc lại.**

---

## Bảy bài học về cách dùng AI mà tôi rút ra

**1. AI diễn giải mô tả thành công thức, và công thức đó cần được duyệt riêng.**
Entry 12 là ví dụ đắt nhất. Tôi mô tả cơ chế bằng lời, AI biến nó thành `5+10=15`, và tôi không phản đối. Sáu ngày sau phải bỏ toàn bộ bảng cân bằng. **Im lặng không phải là duyệt** — và tôi phải nói rõ điều đó từ đầu.

**2. AI tìm pattern trong dữ liệu rồi gán ý định cho pattern đó.**
Entry 06: AI thấy 6/7 giá trị HP chia hết cho 8 và kết luận "bảng của bạn đã neo vào damage 8". Đó là trùng hợp. Luật tôi đặt sau đó: **AI được báo cáo dữ liệu, không được suy ra ý định thiết kế.**

**3. Con số so sánh phải luôn hỏi lại "so với cái gì".**
Entry 04 và Entry 15 cùng một lỗi. "+32 %, vùng lành" và "thừa 8 255 vàng" đều nghe thuyết phục, đều sai mốc. Luật: **mốc so sánh phải in ra cạnh kết luận**, không được ẩn trong câu.

**4. Mỗi lần đưa dữ liệu mới, bắt AI in lại toàn bộ giá trị nó đang dùng.**
Entry 17: nó giữ interval 1.00 sau khi tôi đã đưa ảnh ghi 0.65. Đọc lướt không thấy, vì phép tính vẫn đúng — chỉ đầu vào sai.

**5. Một mâu thuẫn chưa đo chỉ là một câu văn.**
Entry 19: mâu thuẫn nghe hay nhất trong cả dự án bị chính phép đo giết. Mâu thuẫn được dùng cuối cùng (Entry 18) đến từ một con số: 21.2 DPS cho cả chuỗi 2 trụ và 4 trụ.

**6. Đưa việc đối chiếu tài liệu cho máy, đừng đọc lại bằng mắt.**
Entry 24: khi bảng chứng minh nằm trong file balance dưới dạng công thức, nó bắt được **9 chỗ GDD tự xung đột** và **3 ô ma trận sai** mà tôi đã đọc qua nhiều lần không thấy. Mắt tin cái mình vừa viết; công thức không tin gì cả.

**7. Giá trị lớn nhất của AI ở dự án này không phải sinh ý tưởng, mà là tính hệ quả của luật tôi đã đặt.**
Bốn đóng góp mạnh nhất — đường cong từ hình học map (05), mô phỏng economy từ wave data (10), mô hình phơi nhiễm R÷v (16), và con số 21.2 DPS phá vỡ trực giác "dài hơn = mạnh hơn" (18) — **không có cái nào là ý tưởng mới**. Tất cả đều là hệ quả toán học của luật tôi viết ra. Những lần tôi nhờ AI "nghĩ ý tưởng" (Entry 19, Entry 20) đều cho ra thứ tôi phải bỏ.
