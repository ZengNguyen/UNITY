using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý hiển thị thanh đo lực nhảy (Jump Charge Bar) cho chú ếch.
/// - Hỗ trợ cả 2 cách làm:
///   1. Dùng SpriteRenderer (đơn giản nhất cho người mới, không cần tạo Canvas).
///   2. Dùng UI Slider (nếu bạn tạo Canvas).
/// - Đổi màu theo lực: Xanh lá (nhẹ) -> Vàng (vừa) -> Đỏ rực (tối đa).
/// - Tự động ẩn khi không gồng và hiện lên ngay khi bắt đầu đè phím Space.
/// </summary>
public class JumpChargeBar : MonoBehaviour
{
    [Header("=== Tham chiếu ===")]
    [Tooltip("Kéo chú ếch có gắn FrogController vào đây")]
    public FrogController frog;

    [Header("=== Cách 1: Hiển thị bằng Sprite đơn giản ===")]
    [Tooltip("Sprite thanh lực (phần ruột co giãn theo chiều X)")]
    public SpriteRenderer fillSprite;
    [Tooltip("Sprite khung viền nền đen phía sau (tự ẩn/hiện cùng thanh lực)")]
    public SpriteRenderer backgroundSprite;

    [Header("=== Cách 2: Hiển thị bằng UI Slider (Tùy chọn) ===")]
    public Slider uiSlider;
    public Image uiSliderFillImage;

    [Header("=== Màu sắc cảnh báo lực ===")]
    public Color minChargeColor = new Color(0.2f, 0.9f, 0.2f); // Xanh lá
    public Color midChargeColor = new Color(0.95f, 0.85f, 0.1f); // Vàng
    public Color maxChargeColor = new Color(1.0f, 0.15f, 0.15f); // Đỏ rực

    [Header("=== Vị trí đi theo ===")]
    [Tooltip("Độ lệch vị trí so với chú ếch (hiển thị trên đầu)")]
    public Vector3 offset = new Vector3(0f, 0.8f, 0f);

    private Vector3 initialFillScale = Vector3.one;

    private void Start()
    {
        if (frog == null)
        {
            frog = FindAnyObjectByType<FrogController>();
        }

        if (fillSprite != null)
        {
            initialFillScale = fillSprite.transform.localScale;
        }

        // Ban đầu ẩn thanh đi
        SetBarVisible(false);
    }

    private void LateUpdate()
    {
        if (frog == null) return;

        // Cho thanh lực bám theo đỉnh đầu chú ếch
        transform.position = frog.transform.position + offset;

        // Chỉ hiện thanh khi ếch đang ở trạng thái gồng (Charging)
        if (frog.IsCharging)
        {
            SetBarVisible(true);

            float percent = frog.ChargePercentage;
            Color currentColor = EvaluateColor(percent);

            // Cập nhật cho Sprite Fill
            if (fillSprite != null)
            {
                fillSprite.transform.localScale = new Vector3(initialFillScale.x * percent, initialFillScale.y, initialFillScale.z);
                fillSprite.color = currentColor;
            }

            // Cập nhật cho UI Slider (nếu dùng)
            if (uiSlider != null)
            {
                uiSlider.value = percent;
            }
            if (uiSliderFillImage != null)
            {
                uiSliderFillImage.color = currentColor;
            }
        }
        else
        {
            SetBarVisible(false);
        }
    }

    private Color EvaluateColor(float percent)
    {
        if (percent < 0.5f)
        {
            return Color.Lerp(minChargeColor, midChargeColor, percent * 2f);
        }
        else
        {
            return Color.Lerp(midChargeColor, maxChargeColor, (percent - 0.5f) * 2f);
        }
    }

    private void SetBarVisible(bool visible)
    {
        if (fillSprite != null) fillSprite.enabled = visible;
        if (backgroundSprite != null) backgroundSprite.enabled = visible;
        if (uiSlider != null) uiSlider.gameObject.SetActive(visible);
    }
}
