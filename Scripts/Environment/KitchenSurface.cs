using UnityEngine;

/// <summary>
/// Các bề mặt địa hình đặc trưng trong căn bếp:
/// 1. BouncySponge (Miếng mút rửa bát): Bật nảy cực mạnh khi ếch đáp xuống.
/// 2. GreasyPan (Chảo dầu mỡ/Thớt mỡ): Siêu trơn trượt, giảm ma sát làm ếch trượt mép.
/// 3. HotStove (Bếp lửa/Bếp từ nóng): Bỏng đít! Đẩy văng chú ếch lên trời khi chạm vào.
/// </summary>
public class KitchenSurface : MonoBehaviour
{
    public enum SurfaceType
    {
        Normal,         // Bề mặt gỗ/gạch men bình thường
        BouncySponge,   // Miếng mút bọt biển đàn hồi cao
        GreasyOil,      // Dầu mỡ trơn trượt
        HotStove        // Bếp nóng giật mình
    }

    [Header("=== Loại bề mặt nhà bếp ===")]
    public SurfaceType surfaceType = SurfaceType.Normal;

    [Header("=== Thông số hiệu ứng ===")]
    [Tooltip("Lực nảy thêm nếu là miếng mút xốp (BouncySponge)")]
    public float spongeBounceForce = 18f;

    [Tooltip("Lực giật nảy bỏng khi đạp vào bếp nóng (HotStove)")]
    public float stoveBurnLaunchForce = 14f;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<FrogController>(out var frog))
        {
            Rigidbody2D rb = frog.GetComponent<Rigidbody2D>();
            if (rb == null) return;

            switch (surfaceType)
            {
                case SurfaceType.BouncySponge:
                    // Chỉ nảy khi ếch đáp từ trên xuống
                    if (collision.relativeVelocity.y <= 0f)
                    {
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.8f, spongeBounceForce);
                    }
                    break;

                case SurfaceType.HotStove:
                    // Chạm bếp nóng -> giật nảy văng ngược lên
                    Vector2 launchDir = (collision.contacts[0].normal + Vector2.up).normalized;
                    rb.linearVelocity = launchDir * stoveBurnLaunchForce;
                    break;

                case SurfaceType.GreasyOil:
                    // Tạo lực trượt quán tính
                    rb.AddForce(new Vector2(Mathf.Sign(rb.linearVelocity.x) * 5f, 0f), ForceMode2D.Impulse);
                    break;
            }
        }
    }
}
