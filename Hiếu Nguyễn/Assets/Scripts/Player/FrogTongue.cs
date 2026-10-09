using System.Collections;
using UnityEngine;

/// <summary>
/// Cơ chế Thò Lưỡi Kéo Đồ Vật của Chú Ếch (Frog Tongue):
/// - Kích hoạt bằng: Phím E hoặc Click Chuột Trái.
/// - Hướng bắn: Bắn theo vị trí chuột trên màn hình hoặc theo hướng mặt ếch.
/// - Khả năng:
///   1. Lưỡi màu hồng co giãn bắn xa tới 6.5m.
///   2. Dính và KÉO Lọ Hoa (Fragile Vase) hoặc vật thể Rigidbody2D trượt về phía ếch.
///   3. Dính và TÁP Ruồi (Fly) từ xa kéo thẳng vào miệng để ăn.
///   4. Có âm thanh "Thwip! Táp!" tạo bằng code âm thanh chân thực.
/// </summary>
[RequireComponent(typeof(FrogController))]
public class FrogTongue : MonoBehaviour
{
    [Header("=== Thông số lưỡi ếch ===")]
    [Tooltip("Khoảng cách thò lưỡi tối đa (mét)")]
    public float maxDistance = 6.5f;

    [Tooltip("Tốc độ phóng lưỡi")]
    public float shootSpeed = 26f;

    [Tooltip("Tốc độ kéo lưỡi và vật thể về")]
    public float retractSpeed = 18f;

    [Tooltip("Lực kéo giật vật thể về phía ếch")]
    public float pullForce = 12f;

    [Header("=== Màu sắc lưỡi ===")]
    public Color tongueColor = new Color(1.0f, 0.35f, 0.55f);
    public Color tipColor = new Color(0.95f, 0.15f, 0.4f);

    private FrogController frogController;
    private AudioSource audioSource;
    private LineRenderer lineRenderer;
    private GameObject tongueTipObj;
    private SpriteRenderer tipRenderer;
    private bool isTongueActive = false;

    private static AudioClip proceduralTongueClip;

    public bool IsTongueActive => isTongueActive;

    private void Awake()
    {
        frogController = GetComponent<FrogController>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        SetupVisualTongue();
    }

    private void SetupVisualTongue()
    {
        // 1. LineRenderer vẽ sợi lưỡi
        GameObject lineObj = new GameObject("Tongue_Line");
        lineObj.transform.parent = transform;
        lineObj.transform.localPosition = Vector3.zero;

        lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.12f;
        lineRenderer.endWidth = 0.16f;
        lineRenderer.positionCount = 2;
        lineRenderer.material = LivingRoomAutoBuilder.GetSpriteMaterial();
        lineRenderer.startColor = tongueColor;
        lineRenderer.endColor = tongueColor;
        lineRenderer.sortingOrder = 9;
        lineRenderer.enabled = false;

        // 2. Đầu lưỡi dính tròn
        tongueTipObj = new GameObject("Tongue_Tip");
        tongueTipObj.transform.parent = transform;
        tongueTipObj.transform.localScale = new Vector3(0.28f, 0.28f, 1f);

        tipRenderer = tongueTipObj.AddComponent<SpriteRenderer>();
        tipRenderer.sprite = LivingRoomAutoBuilder.GetWhiteSprite();
        tipRenderer.color = tipColor;
        tipRenderer.sharedMaterial = LivingRoomAutoBuilder.GetSpriteMaterial();
        tipRenderer.sortingOrder = 10;
        tongueTipObj.SetActive(false);
    }

    private void Update()
    {
        if (isTongueActive) return;

        // Không cho thò lưỡi khi đang bị choáng dính bụi hoặc đang đào đất
        if (frogController.CurrentState == FrogController.FrogState.Stunned || 
            frogController.CurrentState == FrogController.FrogState.Burrowed)
        {
            return;
        }

        // Bấm Chuột Trái hoặc Phím E để phóng lưỡi
        if (GameInput.GetMouseButtonDown(0) || GameInput.GetKeyDown(KeyCode.E))
        {
            Vector2 aimDirection = GetAimDirection(GameInput.GetMouseButtonDown(0));
            StartCoroutine(ShootTongueRoutine(aimDirection));
        }
    }

    private Vector2 GetAimDirection(bool isMouseClick)
    {
        Camera cam = Camera.main;
        if (cam != null && isMouseClick)
        {
            Vector2 mouseScreen = GameInput.GetMousePosition();
            Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 10f));
            Vector2 dir = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;
            if (dir.sqrMagnitude > 0.01f)
            {
                return dir;
            }
        }

        // Bắn theo hướng nhìn của ếch nếu nhấn phím E
        float facing = frogController != null ? frogController.FacingDirection : 1f;
        return new Vector2(facing, 0.2f).normalized;
    }

    private static AudioClip GetOrCreateTongueClip()
    {
        if (proceduralTongueClip != null) return proceduralTongueClip;

        int sampleRate = 44100;
        float duration = 0.18f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (t / duration); // Giảm dần nhanh
            float freq = Mathf.Lerp(650f, 220f, t / duration); // Tụt tần số tạo tiếng "Thwip!"
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.75f;
        }

        proceduralTongueClip = AudioClip.Create("Frog_Tongue_Sound", sampleCount, 1, sampleRate, false);
        proceduralTongueClip.SetData(samples, 0);
        return proceduralTongueClip;
    }

    private IEnumerator ShootTongueRoutine(Vector2 direction)
    {
        isTongueActive = true;

        // Phát âm thanh phóng lưỡi
        if (audioSource != null)
        {
            audioSource.PlayOneShot(GetOrCreateTongueClip(), 0.9f);
        }

        lineRenderer.enabled = true;
        tongueTipObj.SetActive(true);

        Vector3 mouthPos = transform.position + Vector3.up * 0.1f;
        Vector3 currentTipPos = mouthPos;
        float traveledDistance = 0f;

        Transform hookedTarget = null;
        Rigidbody2D hookedRb = null;
        FlyItem hookedFly = null;

        // --- GIAI ĐOẠN 1: PHÓNG LƯỠI RA (EXTENDING) ---
        while (traveledDistance < maxDistance)
        {
            mouthPos = transform.position + Vector3.up * 0.1f;
            float step = shootSpeed * Time.deltaTime;
            currentTipPos += (Vector3)(direction * step);
            traveledDistance += step;

            lineRenderer.SetPosition(0, mouthPos);
            lineRenderer.SetPosition(1, currentTipPos);
            tongueTipObj.transform.position = currentTipPos;

            // Kiểm tra va chạm dính mục tiêu bằng CircleCast nhỏ
            Collider2D[] hits = Physics2D.OverlapCircleAll(currentTipPos, 0.3f);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject || hit.transform.IsChildOf(transform)) continue;

                // 1. MÈO KHÔNG PHẢI LÀ ĐỐI TƯỢNG ĐỂ ÉCH DÙNG LƯỠI KÉO ĐƯỢC!
                // Nếu thò lưỡi đụng trúng mèo, lưỡi bật dội lùi lại ngay lập tức, tuyệt đối không dính và không kéo mèo
                if (hit.GetComponent<CatEnemyAI>() != null || hit.gameObject.name.Contains("Cat") || hit.CompareTag("Enemy"))
                {
                    Debug.Log("🐱❌ [Lưỡi Ếch] Chạm trúng con mèo! Mèo quá to và nguy hiểm, lưỡi dội ngược về không kéo được mèo!");
                    traveledDistance = maxDistance; // Dừng phóng và thu lưỡi về ngay
                    break;
                }

                // 2. Bắt trúng Ruồi
                var fly = hit.GetComponent<FlyItem>();
                if (fly != null && !fly.IsEaten)
                {
                    hookedFly = fly;
                    hookedTarget = fly.transform;
                    break;
                }

                // 3. Dính trúng Đồ vật có Rigidbody2D (Lọ hoa, thùng, v.v.)
                var rb = hit.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    var breakable = hit.GetComponent<BreakableObject>();
                    if (breakable != null && breakable.IsBroken) continue; // Đang vỡ thì không kéo

                    hookedTarget = hit.transform;
                    hookedRb = rb;
                    break;
                }

                // 4. Chạm tường hoặc bậc cố định -> dừng phóng
                if (hit.gameObject.name.Contains("Wall") || hit.gameObject.name.Contains("Floor") || hit.gameObject.name.Contains("Shelf"))
                {
                    traveledDistance = maxDistance; // Dừng lại để kéo về
                    break;
                }
            }

            if (hookedTarget != null)
            {
                Debug.Log($"👅 [Lưỡi Ếch] ĐÃ DÍNH TRÚNG: {hookedTarget.name}! Bắt đầu kéo lại gần!");
                break;
            }

            yield return null;
        }

        // --- GIAI ĐOẠN 2: THU LƯỠI & KÉO VẬT THỂ VỀ (RETRACTING) ---
        while (Vector3.Distance(currentTipPos, transform.position) > 0.6f)
        {
            mouthPos = transform.position + Vector3.up * 0.1f;
            currentTipPos = Vector3.MoveTowards(currentTipPos, mouthPos, retractSpeed * Time.deltaTime);

            lineRenderer.SetPosition(0, mouthPos);
            lineRenderer.SetPosition(1, currentTipPos);
            tongueTipObj.transform.position = currentTipPos;

            // Kéo đồ vật theo đầu lưỡi
            if (hookedTarget != null)
            {
                var breakable = hookedTarget.GetComponent<BreakableObject>();
                if (breakable != null && breakable.IsBroken)
                {
                    hookedTarget = null;
                    hookedRb = null;
                }
                else if (hookedFly != null)
                {
                    hookedTarget.position = currentTipPos;
                }
                else if (hookedRb != null)
                {
                    // Tạo lực kéo vật thể trượt mượt mà về phía chú ếch
                    Vector2 pullDir = ((Vector2)mouthPos - (Vector2)hookedTarget.position).normalized;
                    hookedRb.linearVelocity = pullDir * pullForce;
                }
            }

            yield return null;
        }

        // Khi vật thể về tới miệng ếch
        if (hookedFly != null && !hookedFly.IsEaten)
        {
            hookedFly.OnCaughtByFrog();
            var hunger = GetComponent<FrogHungerWeight>();
            if (hunger != null) hunger.DigestFly(35f);
            frogController.PulseVisualWhenFed();
            Debug.Log("😋 Ếch: 'Ngon tuyệt!' Đã nuốt chửng con ruồi vừa kéo về!");
        }

        lineRenderer.enabled = false;
        tongueTipObj.SetActive(false);
        isTongueActive = false;
    }
}
