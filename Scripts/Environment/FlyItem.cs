using UnityEngine;

/// <summary>
/// Ruồi bay lượn lờ trong phòng:
/// - Hình ảnh to rõ, thân đen óng, có đôi cánh trắng lấp lánh đập phành phạch sinh động, dễ nhìn từ xa.
/// - Bay lượn tự do quanh vị trí ban đầu (dạng sóng hình sin).
/// - Chú ếch có thể dùng lưỡi táp hoặc đến gần để ăn.
/// </summary>
public class FlyItem : MonoBehaviour
{
    [Header("=== Hành vi bay lượn ===")]
    public float flySpeed = 1.6f;
    public float hoverRadius = 1.3f;
    public float flutterFrequency = 6f;

    private Vector3 spawnPosition;
    private float randomOffset;
    private bool isEaten = false;

    private Transform leftWing;
    private Transform rightWing;
    private SpriteRenderer bodyRenderer;

    public bool IsEaten => isEaten;

    private void Start()
    {
        spawnPosition = transform.position;
        randomOffset = Random.Range(0f, 10f);

        SetupDistinctVisuals();
    }

    private void SetupDistinctVisuals()
    {
        // 1. Chỉnh kích thước thân ruồi to rõ ràng
        transform.localScale = new Vector3(0.55f, 0.45f, 1f);

        bodyRenderer = GetComponent<SpriteRenderer>();
        if (bodyRenderer == null) bodyRenderer = gameObject.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = LivingRoomAutoBuilder.GetWhiteSprite();
        bodyRenderer.color = new Color(0.12f, 0.12f, 0.18f); // Đen đậm nổi bật
        bodyRenderer.sharedMaterial = LivingRoomAutoBuilder.GetSpriteMaterial();
        bodyRenderer.sortingOrder = 15; // Đặt lớp vẽ cao hơn để không bị chìm nền

        // 2. Tạo quầng sáng hào quang (Glow Aura) nhẹ giúp ruồi nổi bật trên mọi hậu cảnh
        if (transform.Find("Fly_Glow") == null)
        {
            GameObject glow = new GameObject("Fly_Glow");
            glow.transform.parent = transform;
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localScale = new Vector3(1.6f, 1.4f, 1f);
            var gRend = glow.AddComponent<SpriteRenderer>();
            gRend.sprite = LivingRoomAutoBuilder.GetWhiteSprite();
            gRend.color = new Color(1.0f, 0.95f, 0.3f, 0.28f); // Hào quang vàng nhạt nổi bật
            gRend.sharedMaterial = LivingRoomAutoBuilder.GetSpriteMaterial();
            gRend.sortingOrder = 14;
        }

        // 3. Tạo đôi cánh trắng lấp lánh đập cánh liên tục
        if (transform.Find("Wing_Left") == null)
        {
            GameObject wingL = new GameObject("Wing_Left");
            wingL.transform.parent = transform;
            wingL.transform.localPosition = new Vector3(-0.25f, 0.4f, 0f);
            wingL.transform.localScale = new Vector3(0.7f, 0.4f, 1f);
            var rendL = wingL.AddComponent<SpriteRenderer>();
            rendL.sprite = LivingRoomAutoBuilder.GetWhiteSprite();
            rendL.color = new Color(0.85f, 0.95f, 1.0f, 0.85f); // Trắng xanh lấp lánh
            rendL.sharedMaterial = LivingRoomAutoBuilder.GetSpriteMaterial();
            rendL.sortingOrder = 16;
            leftWing = wingL.transform;
        }

        if (transform.Find("Wing_Right") == null)
        {
            GameObject wingR = new GameObject("Wing_Right");
            wingR.transform.parent = transform;
            wingR.transform.localPosition = new Vector3(0.25f, 0.4f, 0f);
            wingR.transform.localScale = new Vector3(0.7f, 0.4f, 1f);
            var rendR = wingR.AddComponent<SpriteRenderer>();
            rendR.sprite = LivingRoomAutoBuilder.GetWhiteSprite();
            rendR.color = new Color(0.85f, 0.95f, 1.0f, 0.85f);
            rendR.sharedMaterial = LivingRoomAutoBuilder.GetSpriteMaterial();
            rendR.sortingOrder = 16;
            rightWing = wingR.transform;
        }
    }

    private void Update()
    {
        if (isEaten) return;

        // 1. Bay lượn hình sin
        float time = Time.time * flySpeed + randomOffset;
        float x = Mathf.Sin(time) * hoverRadius;
        float y = Mathf.Sin(time * flutterFrequency) * (hoverRadius * 0.45f);
        transform.position = spawnPosition + new Vector3(x, y, 0f);

        // 2. Đập cánh phành phạch nhanh cực sinh động
        float wingFlap = Mathf.Sin(Time.time * 30f) * 0.25f;
        if (leftWing != null) leftWing.localPosition = new Vector3(-0.25f, 0.35f + wingFlap, 0f);
        if (rightWing != null) rightWing.localPosition = new Vector3(0.25f, 0.35f - wingFlap, 0f);
    }

    public void OnCaughtByFrog()
    {
        isEaten = true;
        gameObject.SetActive(false);
    }

    public void Respawn(Vector3 pos)
    {
        transform.position = pos;
        spawnPosition = pos;
        isEaten = false;
        gameObject.SetActive(true);
    }
}

/// <summary>
/// Ruồi bị nhè ra nằm trên sàn
/// </summary>
public class FlyBait : MonoBehaviour
{
    [Tooltip("Thời gian tồn tại của mồi ruồi trước khi biến mất")]
    public float lifetime = 15f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}

/// <summary>
/// Đồ vật có thể ngậm vào miệng
/// </summary>
public class GrabbableItem : MonoBehaviour
{
    public string itemName = "Vật thể";
}
