## Chức năng đã hoàn thành
- Tuần tra qua lại giữa Point A và Point B, dừng nghỉ ở mỗi điểm
- Nghe tiếng động (kính vỡ, tiếng kêu) và chạy tới kiểm tra
- Thanh chú ý hiển thị trên đầu mèo: tăng khi thấy ếch, tụt khi mất dấu
- Đuổi theo và vồ ếch khi thanh chú ý đầy, gọi sự kiện On Caught Player

## Cách dùng
1. Gắn CatPatrolAI và CatAttentionBar vào object mèo (có Animator và Sprite Renderer)
2. Gán Point A, Point B (đặt NGOÀI mèo) và Target (con ếch, Tag "Player")
3. Tạo tiếng động từ script khác: NoiseSystem.Emit(transform.position, bánKính);
4. Nối sự kiện On Caught Player vào hàm xử lý khi ếch bị bắt
5. Điền tên các State trong Animator: Idle, Walk, Alert, Run, Attack

## Thông số chính (Inspector)
- Hearing Multiplier, Vision Range: mèo nghe và nhìn xa cỡ nào
- See Rate Far / Near, Decay Rate: tốc độ thanh chú ý tăng và tụt
- Chase Speed, Attack Range: tốc độ đuổi và tầm vồ