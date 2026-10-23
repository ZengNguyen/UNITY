using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý cơ chế Quạt máy phòng khách (Hoạt động như ngoài đời thực):
/// 1. Xoay theo quán tính thực tế:
///    - Khi bật: Cánh quạt xoay từ từ rồi mới xoay tít cực mạnh.
///    - Khi tắt: Cánh quạt quay chậm dần chậm dần theo quán tính rồi mới dừng hẳn.
/// 2. Gió đẩy người chơi liên tục:
///    - Cứ khi quạt đang quay có gió là người chơi bị đẩy lùi dạt theo hướng gió (dù có bụi hay không).
///    - Lực đẩy tỉ lệ thuận với tốc độ quay của quạt.
/// 3. Bụi cay mắt (xác suất nhỏ):
///    - Chỉ thỉnh thoảng có xác suất nhỏ (20%) bụi mới bay trúng mắt làm ếch phải dụi mắt 1 giây.
/// </summary>
public class WindAndDustHazard : MonoBehaviour
{
    [Header("=== Chu kỳ Quạt gió (Fan Cycle) ===")]
    [Tooltip("Thời gian quạt nghỉ giữa mỗi lần thổi (giây) - Mặc định 10 giây")]
    public float restCooldown = 10.0f;

    [Tooltip("Thời gian quạt bật thổi gió (giây)")]
    public float blowDuration = 3.5f;

    [Header("=== Tốc độ quạt ngoài đời thực (Inertia Fan Speed) ===")]
    [Tooltip("Tốc độ quay tối đa của cánh quạt (độ/giây)")]
    public float maxFanSpeed = 900f;

    [Tooltip("Gia tốc khi bật quạt (xoay từ từ tăng tốc)")]
    public float acceleration = 380f;

    [Tooltip("Lực hãm khi tắt quạt (quay chậm dần rồi dừng hẳn)")]
    public float deceleration = 220f;

    [Header("=== Lực đẩy của gió ===")]
    [Tooltip("Hướng gió (âm là thổi sang trái, dương là thổi sang phải)")]
    public float windDirection = -1f;

    [Tooltip("Cường độ gió đẩy người chơi trên sàn (m/s)")]
    public float groundPushSpeed = 3.8f;

    [Tooltip("Lực gió bạt khi người chơi đang bay trên không")]
    public float airPushForce = 12.0f;

    [Header("=== Xác Suất Dính Bụi Cay Mắt (Xác Suất Nhỏ) ===")]
    [Tooltip("Xác suất nhỏ bị dính bụi cay mắt khi bị quạt thổi trúng (0.2 = 20%)")]
    [Range(0f, 1f)]
    public float dustCatchChance = 0.2f;

    [Tooltip("Thời gian chú ếch đứng yên dụi mắt khi dính bụi (giây)")]
    public float eyeRubDuration = 1.0f;

    [Header("=== Trạng thái hiện tại ===")]
    public bool isFanPoweredOn = false;
    public float currentFanSpeed = 0f;

    // === TƯƠNG THÍCH NGƯỢC (Backward Compatibility) ===
    public bool enableWind
    {
        get => true;
        set { }
    }
    public Vector2 windForce
    {
        get => new Vector2(windDirection * groundPushSpeed, 0f);
        set => windDirection = value.x < 0 ? -1f : 1f;
    }
    public bool hasDustInArea
    {
        get => true;
        set { }
    }

    private BoxCollider2D areaCollider;
    private List<Transform> dustParticles = new List<Transform>();
    private List<SpriteRenderer> dustRenderers = new List<SpriteRenderer>();
    private Transform fanBlades;

    private float lastDustCatchTime = -10f;
    private bool hasCheckedDustThisBlow = false;

    private void Awake()
    {
        areaCollider = GetComponent<BoxCollider2D>();
    }

    private void Start()
    {
        if (transform.parent != null)
        {
            Transform blades = transform.parent.Find("Fan_Head/Fan_Blades");
            if (blades == null) blades = transform.parent.Find("Fan_Blades");
            fanBlades = blades;
        }

        SpawnVisualDustField();
        StartCoroutine(FanCycleRoutine());
    }

    private void SpawnVisualDustField()
    {
        if (areaCollider == null) return;

        Bounds bounds = areaCollider.bounds;
        Sprite white = LivingRoomAutoBuilder.GetWhiteSprite();
        Material mat = LivingRoomAutoBuilder.GetSpriteMaterial();

        int particleCount = 20;
        for (int i = 0; i < particleCount; i++)
        {
            GameObject p = new GameObject($"Dust_Puff_{i}");
            p.transform.parent = transform;
            float rx = Random.Range(bounds.min.x, bounds.max.x);
            float ry = Random.Range(bounds.min.y, bounds.max.y);
            p.transform.position = new Vector3(rx, ry, 0f);

            float sizeW = Random.Range(0.2f, 0.65f);
            float sizeH = Random.Range(0.08f, 0.22f);
            p.transform.localScale = new Vector3(sizeW, sizeH, 1f);

            var rend = p.AddComponent<SpriteRenderer>();
            rend.sprite = white;
            rend.color = new Color(0.92f, 0.85f, 0.65f, 0f);
            if (mat != null) rend.sharedMaterial = mat;
            rend.sortingOrder = 8;

            dustParticles.Add(p.transform);
            dustRenderers.Add(rend);
        }
    }

    private IEnumerator FanCycleRoutine()
    {
        while (true)
        {
            // 1. GIAI ĐOẠN TẮT: Quạt tắt điện trong 10 giây (cánh quạt sẽ quay chậm dần rồi tắt hẳn)
            isFanPoweredOn = false;
            hasCheckedDustThisBlow = false;
            yield return new WaitForSeconds(restCooldown);

            // 2. GIAI ĐOẠN BẬT: Quạt mở điện trong 3.5 giây (cánh quạt xoay từ từ rồi cực mạnh)
            isFanPoweredOn = true;
            hasCheckedDustThisBlow = false;
            yield return new WaitForSeconds(blowDuration);
        }
    }

    private void Update()
    {
        // 1. CƠ CHẾ QUẠT QUAY THEO QUÁN TÍNH THỰC TẾ
        float targetSpeed = isFanPoweredOn ? maxFanSpeed : 0f;
        float rate = (targetSpeed > currentFanSpeed) ? acceleration : deceleration;
        currentFanSpeed = Mathf.MoveTowards(currentFanSpeed, targetSpeed, rate * Time.deltaTime);

        // Xoay cánh quạt theo tốc độ hiện tại
        if (fanBlades != null && currentFanSpeed > 0.1f)
        {
            fanBlades.Rotate(0f, 0f, -currentFanSpeed * Time.deltaTime);
        }

        // 2. DI CHUYỂN CÁC HẠT BỤI & ĐỘ MỜ TỈ LỆ VỚI TỐC ĐỘ QUẠT
        if (areaCollider == null || dustParticles.Count == 0) return;

        Bounds bounds = areaCollider.bounds;
        float speedRatio = currentFanSpeed / maxFanSpeed;
        float dustFlySpeed = Mathf.Max(1.0f, speedRatio * 8.0f);
        float targetAlpha = speedRatio * 0.65f;

        for (int i = 0; i < dustParticles.Count; i++)
        {
            Transform p = dustParticles[i];
            if (p == null) continue;

            if (speedRatio > 0.05f)
            {
                p.position += new Vector3(windDirection * dustFlySpeed * Time.deltaTime, Mathf.Sin(Time.time * 6f + i) * 0.35f * Time.deltaTime, 0f);
                p.Rotate(0f, 0f, windDirection * 50f * Time.deltaTime);

                if (windDirection < 0 && p.position.x < bounds.min.x)
                {
                    p.position = new Vector3(bounds.max.x, Random.Range(bounds.min.y, bounds.max.y), 0f);
                }
                else if (windDirection > 0 && p.position.x > bounds.max.x)
                {
                    p.position = new Vector3(bounds.min.x, Random.Range(bounds.min.y, bounds.max.y), 0f);
                }
            }

            if (dustRenderers[i] != null)
            {
                Color c = dustRenderers[i].color;
                dustRenderers[i].color = new Color(c.r, c.g, c.b, Mathf.MoveTowards(c.a, targetAlpha, Time.deltaTime * 3.5f));
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Chỉ có gió đẩy khi quạt đang quay (> 10% công suất)
        float speedRatio = currentFanSpeed / maxFanSpeed;
        if (speedRatio < 0.1f) return;

        if (other.TryGetComponent<FrogController>(out var frog))
        {
            if (frog.IsBurrowed) return; // Đang đào đất trốn thì không bị gió thổi

            Rigidbody2D frogRb = frog.GetComponent<Rigidbody2D>();
            if (frogRb == null) return;

            // 1. NGƯỜI CHƠI BỊ ĐẨY LIÊN TỤC TRONG SUỐT THỜI GIAN CÓ GIÓ (DÙ CÓ BỤI HAY KHÔNG)
            float pushAmount = windDirection * groundPushSpeed * speedRatio;

            // Tác động đẩy trực tiếp vào vị trí & vận tốc người chơi
            frogRb.position += new Vector2(pushAmount * Time.deltaTime, 0f);

            if (frog.CurrentState == FrogController.FrogState.InAir || frog.CurrentState == FrogController.FrogState.Bounced)
            {
                frogRb.AddForce(new Vector2(windDirection * airPushForce * speedRatio, 0f), ForceMode2D.Force);
            }

            // 2. XÁC SUẤT NHỎ BỊ DÍNH BỤI CAY MẮT (CHỈ XÉT KHI QUẠT QUAY MẠNH > 60%)
            if (speedRatio > 0.6f && !hasCheckedDustThisBlow && Time.time - lastDustCatchTime > 3.0f)
            {
                hasCheckedDustThisBlow = true;
                if (Random.value < dustCatchChance && frog.CurrentState != FrogController.FrogState.Stunned)
                {
                    lastDustCatchTime = Time.time;
                    frog.ApplyDustInEyes(eyeRubDuration);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isFanPoweredOn ? new Color(1f, 0.8f, 0.2f, 0.45f) : new Color(0.3f, 0.8f, 1f, 0.15f);
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(col.bounds.center, Vector3.right * windDirection * 2.5f);
        }
    }
}
