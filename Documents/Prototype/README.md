# Bản Chơi Thử Trên Trình Duyệt

**Trạng thái:** Chơi được  
**Ý tưởng gốc:** [Tài liệu ý tưởng](../GameDesign/Raw_Game_Concept_The_Toad_Is_Heavens_Uncle.md)

## Cách chạy

Mở [`index.html`](index.html) bằng trình duyệt. Không cần cài đặt hay kết nối mạng.

## Nội dung

- Đặt trụ bằng chạm hoặc chuột.
- 6 đợt khó dần: đông hơn, khỏe hơn, nhanh hơn; địch bay từ đợt 3 và tinh anh ở đợt 6.
- Nước, máu đền, thắng, thua, dừng và chơi lại.
- Gấu, Ong, Cáo, Cua và Trụ Nước.
- Vòng Nghiệp Dương → Âm → Dương.
- Chạm trụ để thấy tầm và các mục tiêu nó có thể tác động.
- Chạm đất để bỏ chọn trụ.
- Địch hiện biểu tượng chậm, tăng tốc, độc, giáp, kháng phép và khiên.
- Mỗi đòn bắn hiện sát thương thực tế sau phòng thủ.
- Giữa các đợt, mọi đồng hồ của trụ đều đứng yên.
- Máu Cóc hiển thị bằng thanh ở bảng trạng thái và ngay trên đền.

## Luật tạm của bản chơi thử

- Hạ địch: mọi trụ chiến đấu đang Dương nhận 5 Nghiệp.
- Nghiệp đầy: trụ vào Âm ngay; xả hết thì về Dương.
- Nghiệp riêng tăng nhanh hơn: Gấu 10/đòn, Ong 15/đòn, Cáo 2/đòn, Cua 12.5/đòn, Trụ Nước 15/lần tạo.
- Cáo Âm đánh chậm hơn nhưng ưu tiên tinh anh và cắn mạnh.
- Cua Âm khiến trụ gần tích Nghiệp nhanh hơn 75% và đánh chậm 20%.
- Trụ Nước Âm tạo nhanh gấp đôi; mỗi 3 lần tạo làm đền mất 1 máu.
- Phép xuyên giáp vật lý; vật lý xuyên kháng phép. Đòn đánh không xóa loại phòng thủ còn lại.
- Tiền hạ địch đã giảm; phần thưởng qua đợt và Trụ Nước quan trọng hơn.
- Quái thường/bay lọt đền gây 2 máu; tinh anh gây 4 máu.
- Mỗi trụ nâng tối đa 3 lần giữa các đợt. Gấu/Ong/Cáo chọn sát thương, tốc đánh hoặc tầm; Cua tăng tầm aura; Trụ Nước tăng lượng sinh.
- Bán trụ hoàn 60%, chỉ dùng giữa các đợt.

Các số trên chỉ dùng để thử lối chơi, chưa phải cân bằng chính thức.

## Điều khiển

Chọn thú → chạm vòng trống → mở đợt. Chạm trụ đã đặt để xem tầm và Nghiệp.
