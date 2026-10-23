using UnityEngine;

/// <summary>
/// Cơ chế Viên Sỏi (Pebble Item) - Cơ chế #11 trong Game Design:
/// - "Ngậm sỏi để đứng lên để nhảy chỗ cao hơn"
/// - Ếch có thể ngậm viên sỏi vào miệng (phím E).
/// - Nhè sỏi ra sàn (phím Q): Viên sỏi rơi xuống đất và trở thành một bệ đứng kiên cố.
/// - Chú ếch có thể nhảy lên đứng trên viên sỏi để tăng độ cao ban đầu (+0.5m),
///   giúp nhảy tới các kệ cao, bậc lơ lửng hoặc thoát qua thành hộp tutorial dễ dàng hơn!
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PebbleItem : MonoBehaviour
{
    [Header("=== Thông số viên sỏi ===")]
    public string pebbleName = "Viên sỏi nâng tầm nhảy";
    public float extraHeightBoost = 0.5f;

    private Rigidbody2D rb;
    private Collider2D col;
    private GrabbableItem grabbable;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        grabbable = GetComponent<GrabbableItem>();
        if (grabbable == null)
        {
            grabbable = gameObject.AddComponent<GrabbableItem>();
            grabbable.itemName = pebbleName;
        }

        // Đảm bảo sỏi có vật lý đầm chắc, rơi xuống sàn đứng yên làm bệ đỡ
        rb.mass = 3.0f;
        rb.gravityScale = 4.0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Đặt layer là Default để FrogController GroundCheck có thể đứng lên
        gameObject.layer = LayerMask.NameToLayer("Default");
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Khi sỏi rơi tiếp đất: khóa vận tốc để làm bệ đứng vững chắc
        if (collision.gameObject.layer == LayerMask.NameToLayer("Default"))
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}
