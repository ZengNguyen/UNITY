using System.Collections;
using UnityEngine;

/// <summary>
/// Đồ đạc dễ vỡ trong phòng khách (Lọ hoa trên bàn trà, tách sứ, đĩa gốm):
/// - Khi chú ếch chạm nhẹ hoặc nhảy lên đứng: KHÔNG BỊ VỠ (hoạt động như một bậc nhảy bình thường).
/// - Ếch có thể đẩy/xô lọ hoa di chuyển hoặc làm nghiêng.
/// - CHỈ VỠ khi lọ hoa bị rơi đổ xuống sàn nhà (Floor) hoặc va đập mạnh xuống đất:
///   + Đồ vật vỡ tan tành ("XOẢNG!").
///   + Gửi tín hiệu tiếng ồn lớn đến HumanAlertManager làm tăng mức báo động của con người!
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BreakableObject : MonoBehaviour
{
    [Header("=== Thông số vỡ ===")]
    [Tooltip("Lực va chạm tối thiểu để làm vỡ đồ vật khi đập xuống sàn")]
    public float breakForceThreshold = 3.5f;

    [Tooltip("Mức độ ồn làm tăng cảnh báo con người (0 - 100%)")]
    public float alertNoiseAmount = 35.0f;

    [Tooltip("Tên đồ vật (Lọ hoa, Ly trà, Đĩa sứ)")]
    public string objectName = "Lọ hoa cổ phòng khách";

    [Header("=== Tự Động Hồi Sinh (Respawn) ===")]
    [Tooltip("Tự động spawn lại sau khi bị vỡ")]
    public bool autoRespawn = true;

    [Tooltip("Thời gian chờ trước khi hồi sinh lại đúng chỗ cũ (giây)")]
    public float respawnDelay = 3.5f;

    private bool isBroken = false;
    private Rigidbody2D rb;
    private Collider2D objCollider;
    private SpriteRenderer objRenderer;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    public bool IsBroken => isBroken;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        objCollider = GetComponent<Collider2D>();
        objRenderer = GetComponent<SpriteRenderer>();

        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isBroken) return;

        if (collision.gameObject.GetComponent<FrogController>() != null)
        {
            return;
        }

        float impactForce = collision.relativeVelocity.magnitude;

        bool isFloorOrGround = collision.gameObject.name.Contains("Floor") || 
                                collision.gameObject.name.Contains("Ground") ||
                                collision.gameObject.layer == LayerMask.NameToLayer("Default");

        if (isFloorOrGround && impactForce >= breakForceThreshold)
        {
            Shatter();
        }
    }

    private void Update()
    {
        if (isBroken) return;

        float tiltAngle = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, 0f));
        if (tiltAngle > 55f && transform.position.y < 0.6f && rb.linearVelocity.magnitude < 0.3f)
        {
            Shatter();
        }
    }

    public void Shatter()
    {
        if (isBroken) return;
        isBroken = true;

        Debug.LogWarning($"💥 [BreakableObject] {objectName} ĐÃ BỊ RƠI ĐỔ VỠ TOANG! +{alertNoiseAmount}% Cảnh Báo Con Người!");

        if (HumanAlertManager.Instance != null)
        {
            HumanAlertManager.Instance.AddNoise(alertNoiseAmount, transform.position);
        }

        SpawnShards();

        if (autoRespawn)
        {
            StartCoroutine(RespawnRoutine(respawnDelay));
        }
        else
        {
            Destroy(gameObject, 0.05f);
        }
    }

    private IEnumerator RespawnRoutine(float delay)
    {
        if (objRenderer != null) objRenderer.enabled = false;
        if (objCollider != null) objCollider.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        yield return new WaitForSeconds(delay);

        transform.position = initialPosition;
        transform.rotation = initialRotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }

        if (objRenderer != null)
        {
            objRenderer.enabled = true;
        }
        if (objCollider != null)
        {
            objCollider.enabled = true;
        }

        isBroken = false;
        Debug.Log($"✨ [BreakableObject] {objectName} đã spawn lại nguyên vẹn tại chính xác chỗ ban đầu: {initialPosition}!");
    }

    private void SpawnShards()
    {
        Sprite white = LivingRoomAutoBuilder.GetWhiteSprite();
        Material mat = LivingRoomAutoBuilder.GetSpriteMaterial();
        Color vaseColor = GetComponent<SpriteRenderer>() != null ? GetComponent<SpriteRenderer>().color : new Color(0.2f, 0.75f, 0.85f);

        for (int i = 0; i < 5; i++)
        {
            GameObject shard = new GameObject($"Shard_{i}");
            shard.transform.position = transform.position + (Vector3)(Random.insideUnitCircle * 0.25f);
            shard.transform.localScale = new Vector3(Random.Range(0.12f, 0.25f), Random.Range(0.12f, 0.25f), 1f);

            var rend = shard.AddComponent<SpriteRenderer>();
            rend.sprite = white;
            rend.color = vaseColor;
            if (mat != null) rend.sharedMaterial = mat;
            rend.sortingOrder = 5;

            var col = shard.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;

            var shardRb = shard.AddComponent<Rigidbody2D>();
            shardRb.mass = 0.1f;
            Vector2 randomDir = (Random.insideUnitCircle + Vector2.up * 1.2f).normalized;
            shardRb.linearVelocity = randomDir * Random.Range(3.5f, 7.0f);
            shardRb.angularVelocity = Random.Range(-360f, 360f);

            Destroy(shard, 2.5f);
        }
    }
}
