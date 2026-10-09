using UnityEngine;

/// <summary>
/// Camera theo dõi chú ếch mượt mà theo chiều dọc (Vertical Smooth Follow):
/// 1. Tự động lia theo chú ếch khi nhảy lên các tầng cao hơn của căn bếp.
/// 2. Khóa hoặc giới hạn trục X để luôn nằm gọn trong chiều ngang căn bếp.
/// 3. Offset nhìn lên trên (Look-ahead) để người chơi dễ quan sát các kệ bếp phía trên để tính góc nhảy.
/// 4. Theo kịp khi rơi xuống để người chơi thấy rõ cú "ngã đau đớn" chuẩn Jump King.
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    [Header("=== Mục tiêu theo dõi ===")]
    [Tooltip("Kéo chú ếch vào đây")]
    public Transform target;

    [Header("=== Tinh chỉnh di chuyển ===")]
    [Tooltip("Thời gian làm mượt chuyển động (càng nhỏ bám càng sát)")]
    public float smoothTime = 0.22f;

    [Tooltip("Độ lệch vị trí so với ếch (thường nâng cao trục Y để thấy các bậc nhảy bên trên)")]
    public Vector2 offset = new Vector2(0f, 1.2f);

    [Header("=== Giới hạn khung hình phòng khách ===")]
    [Tooltip("Cố định trục X ở giữa phòng")]
    public bool lockXAxis = false;
    public float fixedXPosition = 0f;

    [Tooltip("Nếu không khóa trục X, giới hạn mép trái và mép phải của căn phòng")]
    public float minX = -12f;
    public float maxX = 12f;

    [Tooltip("Giới hạn đáy sàn nhà (không cho camera tụt xuống dưới sàn)")]
    public float minY = 1.0f;

    // Vận tốc phục vụ hàm SmoothDamp của Unity
    private Vector3 currentVelocity = Vector3.zero;

    private void Start()
    {
        if (target == null)
        {
            FrogController frog = FindAnyObjectByType<FrogController>();
            if (frog != null) target = frog.transform;
        }

        // Đặt vị trí ban đầu nhìn ngay vào chú ếch
        if (target != null)
        {
            float startX = lockXAxis ? fixedXPosition : Mathf.Clamp(target.position.x + offset.x, minX, maxX);
            float startY = Mathf.Max(target.position.y + offset.y, minY);
            transform.position = new Vector3(startX, startY, -10f);
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Tính toán vị trí mong muốn (Target Position)
        float targetX;
        if (lockXAxis)
        {
            targetX = fixedXPosition;
        }
        else
        {
            targetX = Mathf.Clamp(target.position.x + offset.x, minX, maxX);
        }

        // Giới hạn không cho camera chìm xuống dưới sàn nhà bếp
        float targetY = Mathf.Max(target.position.y + offset.y, minY);

        Vector3 desiredPosition = new Vector3(targetX, targetY, transform.position.z);

        // Di chuyển mượt mà tới vị trí mục tiêu
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);
    }

    /// <summary>
    /// Đưa camera về khung hình nhìn ngay vào mục tiêu mà không qua SmoothDamp (khi respawn/cutscene)
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        float targetX = lockXAxis ? fixedXPosition : Mathf.Clamp(target.position.x + offset.x, minX, maxX);
        float targetY = Mathf.Max(target.position.y + offset.y, minY);
        transform.position = new Vector3(targetX, targetY, transform.position.z);
        currentVelocity = Vector3.zero;
    }
}
