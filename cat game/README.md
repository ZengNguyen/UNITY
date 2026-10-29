# Mèo AI (Cat Enemy)

Con mèo kẻ địch cho game 2D (Unity 6). Mèo tuần tra, **nghe tiếng động** để đi kiểm tra, có **thanh chú ý** trên đầu để người chơi biết đường tránh, và đuổi theo vồ người chơi khi thanh chú ý đầy.

Người làm: *(điền tên bạn)*

## Chức năng

- Tuần tra qua lại giữa **Point A** và **Point B**, dừng nghỉ ở mỗi điểm.
- **Nghe tiếng động** (kính vỡ, vật rơi, tiếng người chơi...) trong bán kính cho trước và chạy tới chỗ phát ra tiếng.
- **Thanh chú ý** nổi trên đầu mèo, từ 0 đến 1, đổi màu xanh → vàng → đỏ:
  - Mèo nhìn thấy người chơi: thanh tăng dần, càng gần càng nhanh.
  - Mèo nghe tiếng động: thanh nhảy lên một mức, tiếng càng to thì càng cao.
  - Mèo không thấy gì: thanh từ từ tụt về 0 rồi ẩn đi.
  - Vạch trắng nhỏ trên thanh là mức "nghi ngờ": vượt qua mức này mèo dừng lại nhìn về phía người chơi.
- Thanh đầy thì mèo **đuổi theo và vồ**. Vồ trúng thì gọi sự kiện `On Caught Player`.
- Mất dấu người chơi thì ra chỗ cuối cùng thấy để tìm, hết thời gian thì quay về tuần tra.

## Các trạng thái của mèo

| Trạng thái | Mô tả |
|---|---|
| Patrol | Đi qua lại giữa A và B |
| Alert | Dừng lại, quay đầu về phía tiếng động hoặc người chơi đáng ngờ |
| Investigate | Chạy tới chỗ phát ra tiếng động |
| Search | Đứng nhìn quanh một lúc rồi bỏ cuộc |
| Chase | Đuổi theo người chơi |
| Attack | Vồ, trúng thì gọi `On Caught Player` |

## Các file

| File | Tác dụng |
|---|---|
| `CatPatrolAI.cs` | Bộ não của mèo (di chuyển, nghe, nhìn, đuổi, vồ, thanh chú ý) |
| `CatAttentionBar.cs` | Vẽ thanh chú ý trên đầu mèo, tự tạo bằng code, không cần asset |
| `NoiseSystem.cs` | Hệ thống tiếng động dùng chung. Bất kỳ script nào cũng gọi được |
| `CatEnemyController.controller` | Animator Controller của mèo |
| `Prefabs/Cat.prefab` | Con mèo đã gắn sẵn mọi thứ, kéo thả là dùng |

> Nhớ giữ cả file `.meta` đi kèm mỗi file khi chép vào project.

## Cài đặt vào project

1. Chép cả thư mục này vào `Assets` của project (giữ nguyên các file `.meta`).
2. Import bộ **Pet Cats pack** vào project. Mèo dùng sprite và animation từ bộ này, thiếu bộ này mèo sẽ hiện trống hoặc đứng im.
3. Chờ Unity compile xong, kiểm tra Console không có lỗi đỏ. Nếu project đã có script trùng tên (`NoiseSystem`, `CatPatrolAI`...) thì đổi tên script cũ để tránh trùng class.

## Cách dùng

### 1. Đặt mèo vào scene

1. Kéo `Prefabs/Cat.prefab` vào scene.
2. Đặt mèo đứng đúng trên mặt sàn. Mèo chỉ di chuyển theo trục **X**, độ cao Y giữ nguyên như lúc đặt.
3. Di chuyển `PatrolPointA` và `PatrolPointB` tới hai đầu đoạn đường mèo sẽ đi. Hai điểm này chỉ cần đúng tọa độ X.

Cấu trúc prefab:

```
Cat
├── Cat_Enemy        (Sprite Renderer, Animator, Cat Patrol AI, Cat Attention Bar)
├── PatrolPointA
└── PatrolPointB
```

> **Lưu ý:** mèo không có va chạm vật lý nên đi xuyên tường. Hãy đặt A và B trong đoạn đường mèo thật sự đi được.

### 2. Nối với người chơi

Ô **Target** của `Cat Patrol AI` là đối tượng mèo theo dõi. Chọn một trong hai cách:

- Đặt **Tag** của người chơi là `Player`. Mèo sẽ tự tìm khi game bắt đầu.
- Hoặc kéo người chơi vào ô **Target**.

Nếu người chơi được tạo lúc đang chạy game (Instantiate), hãy gán bằng code ngay sau khi tạo:

```csharp
FindFirstObjectByType<CatPatrolAI>().target = player.transform;
```

### 3. Tạo tiếng động

Bất kỳ script nào cũng gọi được, với bán kính là độ to của tiếng:

```csharp
NoiseSystem.Emit(transform.position, 6f);
```

Gợi ý bán kính:

| Loại tiếng | Bán kính |
|---|---|
| Bước chân nhỏ | 3 |
| Nhảy, hạ cánh | 4 |
| Vật rơi, đụng đồ | 6 đến 8 |
| Kính vỡ, tiếng nổ | 10 đến 12 |

Ví dụ từ người chơi:

```csharp
// Khi nhảy
NoiseSystem.Emit(transform.position, 4f);

// Khi chạy nhanh, phát tiếng mỗi 0.4 giây
if (isSprinting && Time.time >= nextStepNoise)
{
    NoiseSystem.Emit(transform.position, 5f);
    nextStepNoise = Time.time + 0.4f;
}
```

**Vật tạo tiếng khi va chạm** (hộp rơi, chai vỡ, cửa đóng sầm...): tạo thêm script `NoiseOnImpact` rồi gắn vào vật. Vật cần có `Collider2D` và va chạm với vật có `Rigidbody2D`.

```csharp
using UnityEngine;

public class NoiseOnImpact : MonoBehaviour
{
    public float minImpactSpeed = 2f;    // va chạm yếu hơn mức này thì im lặng
    public float baseRadius = 4f;
    public float radiusPerSpeed = 1f;    // va chạm càng mạnh, tiếng càng xa
    public float maxRadius = 12f;
    public float cooldown = 0.3f;        // tránh phát liên tục

    private float lastTime = -10f;

    private void OnCollisionEnter2D(Collision2D c)
    {
        float speed = c.relativeVelocity.magnitude;
        if (speed < minImpactSpeed) return;
        if (Time.time - lastTime < cooldown) return;
        lastTime = Time.time;

        float radius = Mathf.Min(maxRadius, baseRadius + speed * radiusPerSpeed);
        NoiseSystem.Emit(transform.position, radius);
    }
}
```

Khi chạy game trong Editor, mỗi tiếng động sẽ vẽ một vòng tròn vàng cho thấy nó lan tới đâu, rất tiện để chỉnh bán kính.

### 4. Xử lý khi mèo bắt được người chơi

Khi vồ trúng, mèo gọi sự kiện **On Caught Player**. Nối vào hàm của người chơi bằng một trong hai cách:

- **Inspector:** ở `Cat Patrol AI`, bấm dấu **+** trong mục On Caught Player, kéo người chơi vào, chọn một hàm `public void` không có tham số (ví dụ `Die()`, `TakeDamage()`).
- **Code:**
  ```csharp
  FindFirstObjectByType<CatPatrolAI>().onCaughtPlayer.AddListener(OnCaught);
  ```

Nếu người chơi bị đẩy lùi rồi chơi tiếp, nên cho một khoảng bất tử ngắn để không bị bắt liên tiếp.

### 5. Điền tên animation

Trong Inspector của `Cat Patrol AI`, mục **Hình ảnh / Animation**, điền đúng tên các State trong Animator:

| Ô | Mặc định | Ghi chú |
|---|---|---|
| Idle State | `CatIdle` | Đứng nghỉ |
| Walk State | `CatWalk` | Đi tuần tra |
| Alert State | `CatAlert` | Dỏng tai (tùy chọn) |
| Run State | `CatRun` | Chạy, đuổi (tùy chọn) |
| Attack State | `CatAttack` | Vồ (tùy chọn) |

State nào chưa có thì mèo tự dùng Idle hoặc Walk thay thế. Mèo tự điều khiển Animator bằng code nên **không cần tạo Parameters hay Transition**.

## Các thông số chính (Cat Patrol AI)

| Nhóm | Thông số | Ý nghĩa |
|---|---|---|
| Tuần tra | `Speed`, `Wait Time` | Tốc độ đi, thời gian nghỉ ở mỗi điểm |
| Nghe | `Hearing Multiplier` | 1 = nghe đúng bán kính, nhỏ hơn 1 = nghe kém |
| Nghe | `Alert Duration`, `Search Duration` | Thời gian dỏng tai, thời gian tìm kiếm |
| Nhìn | `Vision Range` | Mèo nhìn xa bao nhiêu |
| Nhìn | `Vertical Tolerance` | Chênh lệch độ cao tối đa để còn nhìn thấy |
| Thanh chú ý | `Suspicious Threshold` | Vạch nghi ngờ (mặc định 0.3) |
| Thanh chú ý | `See Rate Far` / `See Rate Near` | Tốc độ tăng thanh khi người chơi ở xa / sát mèo |
| Thanh chú ý | `Decay Rate` | Tốc độ tụt thanh khi mèo không thấy gì |
| Thanh chú ý | `Noise Attention Min` / `Max` | Mức thanh nhảy lên khi nghe tiếng nhỏ / rất to |
| Đuổi & bắt | `Chase Speed`, `Attack Range` | Tốc độ đuổi, tầm vồ |
| Đuổi & bắt | `Attack Hit Time` | Thời điểm tính là trúng (khớp frame vồ trong animation) |
| Đuổi & bắt | `Lose Sight Range`, `Lose Sight Time` | Điều kiện mèo mất dấu |

Thông số của thanh chú ý (`Cat Attention Bar`): `Offset` (vị trí so với mèo), `Width`, `Height`, `Always Visible` (luôn hiện thanh), ba màu thấp, giữa, cao.

## Lỗi thường gặp

| Hiện tượng | Nguyên nhân và cách sửa |
|---|---|
| Mèo đứng im, không có animation | Chưa import Pet Cats pack, hoặc tên State trong Animator không khớp với Inspector |
| Mèo không phản ứng với người chơi | Target trống và người chơi không có Tag `Player` |
| Mèo không nghe tiếng động | Vật chưa gọi `NoiseSystem.Emit`, hoặc bán kính nhỏ hơn khoảng cách tới mèo. Xem vòng tròn vàng trong Game view |
| Mèo đi ngược hướng nhìn | Bỏ tick **Sprite Faces Right** |
| Mèo không thấy người chơi ở tầng khác | Tăng **Vertical Tolerance** |
| Thanh chú ý sai vị trí hoặc quá nhỏ | Chỉnh **Offset** và **Width** trên `Cat Attention Bar` |
| Lỗi trùng tên class | Project đã có script cùng tên. Đổi tên script cũ |
