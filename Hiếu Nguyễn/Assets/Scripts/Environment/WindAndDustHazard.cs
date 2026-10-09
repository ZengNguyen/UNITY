using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý cơ chế Gió & Bụi lùa (Quạt máy phòng khách):
/// - Chu kỳ chuẩn: Cứ sau đúng 10 GIÂY NGHỈ YÊN TĨNH sẽ thổi bụi 1 LẦN (trong 2.5 giây).
/// - Trong 10 giây nghỉ: Quạt tắt hoàn toàn, không có gió, không có bụi, các hạt bụi ẩn đi.
/// - Trong 2.5 giây thổi: Cánh quạt quay vù vù, luồng gió mạnh bạt góc nhảy của ếch và luồng bụi vàng cát bay dạt làm cay mắt.
/// </summary>
public class WindAndDustHazard : MonoBehaviour
{
    [Header("=== Chu kỳ Quạt gió (Fan Cycle) ===")]
    [Tooltip("Thời gian quạt nghỉ giữa mỗi lần thổi (giây) - Mặc định 10 giây")]
    public float restCooldown = 10.0f;

    [Tooltip("Thời gian mỗi đợt quạt thổi gió và bụi (giây)")]
    public float blowDuration = 2.5f;

    [Header("=== Lực Gió & Thời gian Choáng ===")]
    [Tooltip("Bật/tắt hiệu ứng gió thổi bạt")]
    public bool enableWind = true;

    [Tooltip("Bật/tắt hiệu ứng bụi làm cay mắt")]
    public bool hasDustInArea = true;

    [Tooltip("Hướng và cường độ gió khi quạt bật")]
    public Vector2 windForce = new Vector2(-8.0f, 0f);

    [Tooltip("Thời gian chú ếch đứng yên dụi mắt khi dính bụi (giây)")]
    public float eyeRubDuration = 1.2f;

    [Header("=== Trạng thái hiện tại ===")]
    public bool isBlowing = false;

    private BoxCollider2D areaCollider;
    private List<Transform> dustParticles = new List<Transform>();
    private List<SpriteRenderer> dustRenderers = new List<SpriteRenderer>();
    private Transform fanBlades;

    private void Awake()
    {
        areaCollider = GetComponent<BoxCollider2D>();
    }

    private void Start()
    {
        // Tìm cánh quạt để xoay khi thổi
        if (transform.parent != null)
        {
            Transform blades = transform.parent.Find("Fan_Head/Fan_Blades");
            if (blades == null) blades = transform.parent.Find("Fan_Blades");
            fanBlades = blades;
        }

        SpawnVisualDustField();
        StartCoroutine(Fan10SecondCycleRoutine());
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
            rend.color = new Color(0.92f, 0.85f, 0.65f, 0f); // Ban đầu tàng hình vì quạt đang nghỉ
            if (mat != null) rend.sharedMaterial = mat;
            rend.sortingOrder = 8;

            dustParticles.Add(p.transform);
            dustRenderers.Add(rend);
        }
    }

    private IEnumerator Fan10SecondCycleRoutine()
    {
        while (true)
        {
            // 1. GIAI ĐOẠN NGHỈ (10 GIÂY YÊN TĨNH): Người chơi thoải mái nhảy qua an toàn
            isBlowing = false;
            yield return new WaitForSeconds(restCooldown);

            // 2. GIAI ĐOẠN THỔI BỤI (2.5 GIÂY): Quạt quay mạnh và phụt bụi
            isBlowing = true;
            Debug.Log("💨 [Quạt Máy] VÙ VÙ! Quạt đang thổi gió và phụt bụi mù mịt!");
            yield return new WaitForSeconds(blowDuration);
        }
    }

    private void Update()
    {
        // 1. Xoay cánh quạt khi đang trong đợt thổi
        if (fanBlades != null && isBlowing)
        {
            fanBlades.Rotate(0f, 0f, -720f * Time.deltaTime);
        }

        // 2. Di chuyển và làm hiện các hạt bụi khi quạt bật, làm mờ biến mất khi quạt tắt
        if (areaCollider == null || dustParticles.Count == 0) return;

        Bounds bounds = areaCollider.bounds;
        float speed = Mathf.Abs(windForce.x) * 1.3f;
        float dir = Mathf.Sign(windForce.x);
        float targetAlpha = isBlowing ? 0.65f : 0f;

        for (int i = 0; i < dustParticles.Count; i++)
        {
            Transform p = dustParticles[i];
            if (p == null) continue;

            if (isBlowing)
            {
                // Di chuyển hạt bụi dạt theo luồng gió
                p.position += new Vector3(dir * speed * Time.deltaTime, Mathf.Sin(Time.time * 6f + i) * 0.4f * Time.deltaTime, 0f);
                p.Rotate(0f, 0f, dir * 60f * Time.deltaTime);

                // Quấn vòng khi ra khỏi vùng gió
                if (dir < 0 && p.position.x < bounds.min.x)
                {
                    p.position = new Vector3(bounds.max.x, Random.Range(bounds.min.y, bounds.max.y), 0f);
                }
                else if (dir > 0 && p.position.x > bounds.max.x)
                {
                    p.position = new Vector3(bounds.min.x, Random.Range(bounds.min.y, bounds.max.y), 0f);
                }
            }

            // Làm mượt độ mờ hạt bụi
            if (dustRenderers[i] != null)
            {
                Color c = dustRenderers[i].color;
                dustRenderers[i].color = new Color(c.r, c.g, c.b, Mathf.MoveTowards(c.a, targetAlpha, Time.deltaTime * 3.5f));
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // CHỈ TÁC ĐỘNG KHI QUẠT ĐANG TRONG 2.5 GIÂY THỔI
        if (!isBlowing) return;

        if (other.TryGetComponent<FrogController>(out var frog))
        {
            // 1. Tác động của gió (thổi bạt ếch đang bay trên không)
            if (enableWind && (frog.CurrentState == FrogController.FrogState.InAir || frog.CurrentState == FrogController.FrogState.Bounced))
            {
                frog.ApplyWindForce(windForce);
            }

            // 2. Tác động của bụi (làm cay mắt, dừng lại dụi mắt tại chỗ)
            if (hasDustInArea && frog.CurrentState != FrogController.FrogState.Stunned)
            {
                frog.ApplyDustInEyes(eyeRubDuration);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isBlowing ? new Color(1f, 0.8f, 0.2f, 0.45f) : new Color(0.3f, 0.8f, 1f, 0.15f);
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(col.bounds.center, (Vector3)windForce.normalized * 2.0f);
        }
    }
}
