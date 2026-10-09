using System.Collections;
using UnityEngine;

/// <summary>
/// Bề mặt Gel dính bám tường / sàn:
/// - Khi chú ếch chạm vào, Gel sẽ giữ chân chú ếch lại trên tường (không bị rơi).
/// - Ếch có thể tích lực để nhảy vọt ra khỏi tường (Wall Jump).
/// - Bị ảnh hưởng bởi TRỌNG LƯỢNG cơ thể:
///   + Nếu ếch quá nặng (do ăn no bụng/ruồi) > maxHoldingWeight:
///   + Gel không giữ nổi, ếch sẽ bị tuột từ từ xuống hoặc bong khỏi tường rơi tự do!
/// </summary>
public class StickyGelSurface : MonoBehaviour
{
    [Header("=== Khả năng chịu tải của Gel ===")]
    [Tooltip("Trọng lượng tối đa mà mảng Gel này có thể giữ vững (kg)")]
    public float maxHoldingWeight = 1.6f;

    [Tooltip("Tốc độ tuột xuống nếu quá tải trọng lượng")]
    public float slipSlideSpeed = 2.5f;

    [Tooltip("Màu sắc của Gel (thường là vàng mật ong hoặc xanh nhớt)")]
    public Color gelColor = new Color(0.85f, 0.95f, 0.25f, 0.9f);

    private void Start()
    {
        // Tự đổi màu Renderer nếu có
        var rend = GetComponent<SpriteRenderer>();
        if (rend != null) rend.color = gelColor;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryAttachFrog(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryAttachFrog(collision);
    }

    private void TryAttachFrog(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<FrogController>(out var frog))
        {
            // Chỉ bám khi đang bay trên không hoặc vừa đập tường
            if (frog.CurrentState == FrogController.FrogState.InAir || frog.CurrentState == FrogController.FrogState.Bounced)
            {
                Vector2 wallNormal = collision.contacts[0].normal;

                // Kiểm tra trọng lượng ếch
                float currentWeight = frog.CurrentWeight;
                bool isOverweight = currentWeight > maxHoldingWeight;

                frog.AttachToGelWall(this, wallNormal, isOverweight);
            }
        }
    }
}
