using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý giao diện HUD tối giản cho màn chơi Phòng Khách (Living Room):
/// 1. Thanh Cảnh Báo Người Nuôi: Chỉ hiển thị khi có tiếng ồn (> 0%), tự động ẩn khi yên tĩnh.
/// 2. Toàn bộ các UI phụ (Cân nặng, độ no, hướng dẫn điều khiển, túi ngậm đồ, trạng thái mắt) đều được ẩn để giữ màn hình sạch đẹp chuẩn Jump King.
/// 3. Hiệu ứng Cutscene đen màn hình khi bị người nuôi bắt lại vào hộp.
/// </summary>
public class LivingRoomUIManager : MonoBehaviour
{
    public static LivingRoomUIManager Instance { get; private set; }

    [Header("=== Tham chiếu hệ thống ===")]
    public FrogController frog;
    public FrogHungerWeight hungerWeight;
    public FrogMouthInventory mouthInventory;

    [Header("=== Khung Cảnh Báo Con Người (Chủ Nuôi) ===")]
    public GameObject alertPanel;
    public Slider alertSlider;
    public Image alertFillImage;
    public Text alertText;

    [Header("=== Các UI Phụ (Tùy chọn ẩn) ===")]
    public Slider hungerSlider;
    public Text weightText;
    public Text weightWarningText;
    public Text slot1Text;
    public Text slot2Text;
    public Text eyeStatusText;
    public GameObject controlsPanel;

    [Header("=== Màn hình Cutscene Đen (Fade Transition) ===")]
    public Image fadeOverlayImage;
    public Text cutsceneText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (frog == null) frog = FindAnyObjectByType<FrogController>();
        if (hungerWeight == null && frog != null) hungerWeight = frog.GetComponent<FrogHungerWeight>();
        if (mouthInventory == null && frog != null) mouthInventory = frog.GetComponent<FrogMouthInventory>();

        // Ẩn tất cả UI phụ theo yêu cầu
        if (hungerSlider != null) hungerSlider.gameObject.SetActive(false);
        if (weightText != null) weightText.gameObject.SetActive(false);
        if (weightWarningText != null) weightWarningText.gameObject.SetActive(false);
        if (slot1Text != null) slot1Text.gameObject.SetActive(false);
        if (slot2Text != null) slot2Text.gameObject.SetActive(false);
        if (eyeStatusText != null) eyeStatusText.gameObject.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);

        // Ban đầu chưa có tiếng ồn -> Ẩn thanh cảnh báo
        if (alertPanel != null) alertPanel.SetActive(false);
        else if (alertSlider != null)
        {
            alertSlider.gameObject.SetActive(false);
            if (alertText != null) alertText.gameObject.SetActive(false);
        }

        if (fadeOverlayImage != null)
        {
            fadeOverlayImage.color = new Color(0, 0, 0, 0);
            fadeOverlayImage.gameObject.SetActive(false);
        }
        if (cutsceneText != null) cutsceneText.gameObject.SetActive(false);
    }

    private void Update()
    {
        UpdateHumanAlert();
    }

    private void UpdateHumanAlert()
    {
        if (HumanAlertManager.Instance != null)
        {
            float alert = HumanAlertManager.Instance.currentAlert;
            bool shouldShow = alert > 0.05f;

            if (alertPanel != null)
            {
                alertPanel.SetActive(shouldShow);
            }
            else
            {
                if (alertSlider != null) alertSlider.gameObject.SetActive(shouldShow);
                if (alertText != null) alertText.gameObject.SetActive(shouldShow);
            }

            if (shouldShow)
            {
                if (alertSlider != null)
                {
                    alertSlider.value = alert / 100f;
                }

                if (alertText != null)
                {
                    alertText.text = $"Cảnh Báo Chủ Nuôi: {alert:F0}%";
                }

                if (alertFillImage != null)
                {
                    if (alert < 40f)
                        alertFillImage.color = new Color(0.2f, 0.9f, 0.3f);
                    else if (alert < 75f)
                        alertFillImage.color = new Color(1.0f, 0.85f, 0.1f);
                    else
                        alertFillImage.color = new Color(1.0f, 0.25f, 0.2f);
                }
            }
        }
    }

    /// <summary>
    /// Kích hoạt đoạn Cutscene: Đen màn hình -> Đặt ếch lại vào hộp
    /// </summary>
    public void PlayCapturedCutscene(string dialogue, System.Action onBlackScreen)
    {
        StartCoroutine(CapturedCutsceneRoutine(dialogue, onBlackScreen));
    }

    private IEnumerator CapturedCutsceneRoutine(string dialogue, System.Action onBlackScreen)
    {
        if (fadeOverlayImage == null)
        {
            onBlackScreen?.Invoke();
            yield break;
        }

        fadeOverlayImage.gameObject.SetActive(true);
        if (cutsceneText != null)
        {
            cutsceneText.gameObject.SetActive(true);
            cutsceneText.text = dialogue;
        }

        // 1. Dần đen màn hình (Fade out)
        float elapsed = 0f;
        float fadeDuration = 0.45f;
        while (elapsed < fadeDuration)
        {
            float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            fadeOverlayImage.color = new Color(0, 0, 0, alpha);
            if (cutsceneText != null) cutsceneText.color = new Color(1, 1, 1, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }
        fadeOverlayImage.color = Color.black;

        // 2. Khi màn hình đen hoàn toàn -> Đặt ếch lại vào hộp
        onBlackScreen?.Invoke();

        // 3. Giữ màn hình đen một nhịp (cảm giác tua nhanh thời gian / người bế ếch)
        yield return new WaitForSeconds(1.2f);

        // 4. Mở sáng màn hình trở lại (Fade in)
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            fadeOverlayImage.color = new Color(0, 0, 0, alpha);
            if (cutsceneText != null) cutsceneText.color = new Color(1, 1, 1, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        fadeOverlayImage.color = new Color(0, 0, 0, 0);
        fadeOverlayImage.gameObject.SetActive(false);
        if (cutsceneText != null) cutsceneText.gameObject.SetActive(false);
    }
}
