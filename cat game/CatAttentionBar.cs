using UnityEngine;

/// <summary>
/// Thanh chú ý nổi trên đầu mèo. Gắn vào Cat_Enemy (cùng chỗ với CatPatrolAI).
/// Thanh là object riêng, không phải con của mèo nên không bị ảnh hưởng bởi Scale lớn của mèo.
/// </summary>
[RequireComponent(typeof(CatPatrolAI))]
public class CatAttentionBar : MonoBehaviour
{
    [Header("Vị trí & kích thước")]
    public Vector2 offset = new Vector2(0f, 1.4f);
    public float width = 1.6f;
    public float height = 0.18f;
    public int sortingOrder = 50;

    [Header("Hiển thị")]
    [Tooltip("Tắt: thanh tự ẩn khi mèo hoàn toàn không chú ý. Bật: luôn hiện")]
    public bool alwaysVisible = false;
    public Color lowColor = new Color(0.3f, 0.9f, 0.3f);
    public Color midColor = new Color(1f, 0.85f, 0.2f);
    public Color highColor = new Color(1f, 0.2f, 0.2f);

    private CatPatrolAI cat;
    private GameObject root;
    private Transform fill;
    private SpriteRenderer fillSr;
    private Texture2D tex;
    private Sprite spriteCenter;
    private Sprite spriteLeft;

    private void Awake()
    {
        cat = GetComponent<CatPatrolAI>();

        tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        // 4 pixel = 1 đơn vị -> sprite rộng 1x1
        spriteCenter = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        spriteLeft = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);

        Build();
    }

    private void Build()
    {
        root = new GameObject("CatAttentionBar");

        // Nền tối
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(root.transform, false);
        bg.transform.localScale = new Vector3(width + 0.08f, height + 0.08f, 1f);
        SpriteRenderer bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = spriteCenter;
        bgSr.color = new Color(0f, 0f, 0f, 0.7f);
        bgSr.sortingOrder = sortingOrder;

        // Phần tô màu (neo ở mép trái)
        GameObject f = new GameObject("Fill");
        f.transform.SetParent(root.transform, false);
        f.transform.localPosition = new Vector3(-width * 0.5f, 0f, 0f);
        f.transform.localScale = new Vector3(0f, height, 1f);
        fillSr = f.AddComponent<SpriteRenderer>();
        fillSr.sprite = spriteLeft;
        fillSr.sortingOrder = sortingOrder + 1;
        fill = f.transform;

        // Vạch nghi ngờ (vạch trắng nhỏ)
        GameObject tick = new GameObject("SuspiciousTick");
        tick.transform.SetParent(root.transform, false);
        float tickX = -width * 0.5f + width * cat.suspiciousThreshold;
        tick.transform.localPosition = new Vector3(tickX, 0f, 0f);
        tick.transform.localScale = new Vector3(0.03f, height + 0.08f, 1f);
        SpriteRenderer tickSr = tick.AddComponent<SpriteRenderer>();
        tickSr.sprite = spriteCenter;
        tickSr.color = Color.white;
        tickSr.sortingOrder = sortingOrder + 2;

        root.SetActive(alwaysVisible);
    }

    private void LateUpdate()
    {
        if (root == null) return;

        float a = cat.Attention;
        bool visible = alwaysVisible || a > 0.01f;
        if (root.activeSelf != visible) root.SetActive(visible);
        if (!visible) return;

        root.transform.position = transform.position + (Vector3)offset;
        fill.localScale = new Vector3(width * a, height, 1f);

        Color c = a < 0.5f
            ? Color.Lerp(lowColor, midColor, a / 0.5f)
            : Color.Lerp(midColor, highColor, (a - 0.5f) / 0.5f);
        fillSr.color = c;
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root);
        if (spriteCenter != null) Destroy(spriteCenter);
        if (spriteLeft != null) Destroy(spriteLeft);
        if (tex != null) Destroy(tex);
    }
}