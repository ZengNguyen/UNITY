using System.Collections;
using UnityEngine;

/// <summary>
/// Máu của chú ếch Pacman: nhận sát thương (rơi từ trên cao...), hồi máu (ăn ruồi...)
/// và hiện thanh máu nhỏ phía trên đầu ếch.
/// Script tự được FrogController gắn vào ếch khi chơi, không cần thêm tay trong scene.
/// </summary>
public class FrogHealth : MonoBehaviour
{
    [Header("=== Máu ===")]
    public float maxHealth = 200f;
    [SerializeField] private float currentHealth = 200f;

    [Header("=== Thanh máu trên đầu ===")]
    public bool showHealthBar = true;
    [Tooltip("Thanh máu chỉ hiện khi mất máu hoặc vừa thay đổi (giây hiển thị sau mỗi lần đổi). 0 = luôn hiện")]
    public float barVisibleSeconds = 3f;
    public Vector2 barSize = new Vector2(1.0f, 0.12f);
    public float barOffsetY = 0.85f;

    public float CurrentHealth => currentHealth;
    public float Normalized => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public bool IsDead => currentHealth <= 0f;

    public event System.Action<float, float> OnHealthChanged; // (máu hiện tại, máu tối đa)

    private Transform barRoot;
    private SpriteRenderer barBg;
    private SpriteRenderer barFill;
    private float lastChangeTime = -999f;
    private FrogController frog;

    private void Awake()
    {
        frog = GetComponent<FrogController>();
        currentHealth = maxHealth;
    }

    private void Start()
    {
        if (showHealthBar) CreateBar();
        RefreshBar();
    }

    /// <summary>
    /// Trừ máu. Trả về true nếu ếch chết do lần này.
    /// </summary>
    public bool TakeDamage(float amount, string reason = "")
    {
        if (IsDead || amount <= 0f) return false;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Changed();
        SpawnPopup("-" + Mathf.CeilToInt(amount), new Color(1f, 0.25f, 0.2f));
        Debug.Log($"💔 Ếch mất {amount:F0} máu ({reason}). Còn {currentHealth:F0}/{maxHealth:F0}");
        return IsDead;
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        float before = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        float gained = currentHealth - before;
        if (gained <= 0f) return;

        Changed();
        SpawnPopup("+" + Mathf.CeilToInt(gained), new Color(0.35f, 1f, 0.4f));
        Debug.Log($"💚 Ếch hồi {gained:F0} máu. Còn {currentHealth:F0}/{maxHealth:F0}");
    }

    /// <summary>
    /// Hồi đầy máu (dùng khi hồi sinh).
    /// </summary>
    public void ResetFull()
    {
        currentHealth = maxHealth;
        Changed();
    }

    private void Changed()
    {
        lastChangeTime = Time.time;
        RefreshBar();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    #region THANH MÁU
    private void CreateBar()
    {
        if (barRoot != null) return;

        Sprite white = LivingRoomAutoBuilder.GetWhiteSprite();
        Material mat = LivingRoomAutoBuilder.GetSpriteMaterial();

        barRoot = new GameObject("Health_Bar").transform;
        barRoot.SetParent(transform, false);

        barBg = CreateBarPart("BG", white, mat, new Color(0f, 0f, 0f, 0.65f), 30);
        barFill = CreateBarPart("Fill", white, mat, Color.green, 31);
    }

    private SpriteRenderer CreateBarPart(string partName, Sprite sprite, Material mat, Color color, int order)
    {
        var obj = new GameObject(partName);
        obj.transform.SetParent(barRoot, false);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (mat != null) sr.sharedMaterial = mat;
        return sr;
    }

    private void RefreshBar()
    {
        if (barBg == null || barFill == null) return;

        float pad = 0.02f;
        float fullW = barSize.x - pad * 2f;
        float w = Mathf.Max(0.0001f, fullW * Normalized);

        barBg.transform.localPosition = Vector3.zero;
        barBg.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);

        // Thanh máu canh lề trái
        barFill.transform.localPosition = new Vector3(-fullW * 0.5f + w * 0.5f, 0f, 0f);
        barFill.transform.localScale = new Vector3(w, barSize.y - pad * 2f, 1f);
        barFill.color = Normalized > 0.5f
            ? Color.Lerp(Color.yellow, new Color(0.25f, 0.9f, 0.3f), (Normalized - 0.5f) * 2f)
            : Color.Lerp(new Color(0.95f, 0.15f, 0.15f), Color.yellow, Normalized * 2f);
    }

    private void LateUpdate()
    {
        if (barRoot == null) return;

        bool visible = showHealthBar && !IsDead
                       && (barVisibleSeconds <= 0f || Time.time - lastChangeTime <= barVisibleSeconds || Normalized < 0.999f)
                       && (frog == null || !frog.IsBurrowed);
        if (barRoot.gameObject.activeSelf != visible) barRoot.gameObject.SetActive(visible);
        if (!visible) return;

        // Thanh máu là con của ếch nên bù ngược scale để không bị méo khi ếch co/duỗi
        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Max(Mathf.Abs(ls.x), 0.05f);
        float sy = Mathf.Max(Mathf.Abs(ls.y), 0.05f);
        barRoot.localScale = new Vector3(1f / sx, 1f / sy, 1f);
        barRoot.localPosition = new Vector3(0f, barOffsetY / sy, 0f);
    }
    #endregion

    #region CHỮ NỔI +/- MÁU
    private void SpawnPopup(string text, Color color)
    {
        StartCoroutine(PopupRoutine(text, color));
    }

    private IEnumerator PopupRoutine(string text, Color color)
    {
        var obj = new GameObject("Health_Popup");
        obj.transform.position = transform.position + Vector3.up * 1.1f;
        var tm = obj.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 28;
        tm.characterSize = 0.1f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = color;
        obj.GetComponent<MeshRenderer>().sortingOrder = 40;

        float duration = 0.9f;
        float t = 0f;
        while (t < duration && obj != null)
        {
            t += Time.deltaTime;
            obj.transform.position += Vector3.up * (Time.deltaTime * 1.2f);
            tm.color = new Color(color.r, color.g, color.b, 1f - t / duration);
            yield return null;
        }
        if (obj != null) Destroy(obj);
    }
    #endregion
}