using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý scene Cài Đặt (Setting Scene):
/// - Điều chỉnh âm lượng Master, BGM, SFX (tiếng kêu Croak, đồ vỡ, va đập).
/// - Nút "Quay Lại (Back)": Tự động quay về scene trước đó (Main Menu hoặc màn chơi lv1, lv2, lv3 đang chơi).
/// - Tự động dựng giao diện nếu scene Setting chưa có UI.
/// </summary>
public class SettingSceneController : MonoBehaviour
{
    [Header("=== Nút Quay Lại ===")]
    public Button backButton;

    [Header("=== Sliders Âm Thanh ===")]
    public Slider masterVolumeSlider;
    public Slider bgmVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("=== Tên Scene Mặc Định Nếu Chưa Có Lịch Sử ===")]
    public string defaultReturnScene = "Main Menu";

    private void Awake()
    {
        Time.timeScale = 1f;
        MainMenuController.EnsureValidEventSystem();
    }

    private void Start()
    {
        LoadSettings();

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnBackButtonClicked);
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        // Tự động dựng UI nếu scene trống
        if (backButton == null && masterVolumeSlider == null)
        {
            BuildDefaultSettingUI();
        }
    }

    private void LoadSettings()
    {
        float masterVol = PlayerPrefs.GetFloat("Setting_MasterVolume", 1.0f);
        float bgmVol = PlayerPrefs.GetFloat("Setting_BgmVolume", 0.8f);
        float sfxVol = PlayerPrefs.GetFloat("Setting_SfxVolume", 1.0f);

        if (masterVolumeSlider != null) masterVolumeSlider.value = masterVol;
        if (bgmVolumeSlider != null) bgmVolumeSlider.value = bgmVol;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxVol;

        AudioListener.volume = masterVol;
    }

    public void OnMasterVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("Setting_MasterVolume", value);
        AudioListener.volume = value;
    }

    public void OnBgmVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("Setting_BgmVolume", value);
    }

    public void OnSfxVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("Setting_SfxVolume", value);
    }

    /// <summary>
    /// Xử lý khi nhấn nút QUAY LẠI:
    /// Tự động quay về scene trước đó (Main Menu hoặc Level đang chơi)
    /// </summary>
    public void OnBackButtonClicked()
    {
        PlayerPrefs.Save();

        string returnScene = PlayerPrefs.GetString("PreviousScene", defaultReturnScene);

        // Tránh tình trạng lặp lại chính scene setting
        if (string.IsNullOrEmpty(returnScene) || returnScene == SceneManager.GetActiveScene().name)
        {
            returnScene = defaultReturnScene;
        }

        Debug.Log($"[SettingScene] Quay lại scene: {returnScene}");
        try
        {
            SceneManager.LoadScene(returnScene);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SettingScene] Không thể tải '{returnScene}', chuyển về MainMenu fallback. Lỗi: {e.Message}");
            SceneManager.LoadScene(0);
        }
    }

    /// <summary>
    /// Tự động sinh giao diện Canvas Setting nếu scene chưa có
    /// </summary>
    public void BuildDefaultSettingUI()
    {
        var existingCanvas = FindAnyObjectByType<Canvas>();
        if (existingCanvas != null && backButton != null) return;

        Debug.Log("🎨 [SettingScene] Đang tự động tạo giao diện Setting...");

        GameObject canvasObj = new GameObject("Setting_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        MainMenuController.EnsureValidEventSystem();

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Khung nền
        GameObject panelObj = new GameObject("Setting_Panel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image pImg = panelObj.AddComponent<Image>();
        pImg.color = new Color(0.12f, 0.14f, 0.2f, 0.96f);
        RectTransform pRect = panelObj.GetComponent<RectTransform>();
        pRect.sizeDelta = new Vector2(560f, 440f);
        pRect.anchoredPosition = Vector2.zero;

        // Tiêu đề
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panelObj.transform, false);
        Text title = titleObj.AddComponent<Text>();
        title.text = "⚙ CÀI ĐẶT TRÒ CHƠI\n(SETTINGS)";
        title.font = defaultFont;
        title.fontSize = 28;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color(0.3f, 0.9f, 1f);
        RectTransform tRect = titleObj.GetComponent<RectTransform>();
        tRect.anchoredPosition = new Vector2(0f, 160f);
        tRect.sizeDelta = new Vector2(500f, 70f);

        // Sliders
        masterVolumeSlider = CreateSliderRow("MasterVolume", "Âm lượng tổng (Master):", new Vector2(0f, 70f), panelObj.transform, defaultFont);
        bgmVolumeSlider = CreateSliderRow("BgmVolume", "Nhạc nền (BGM):", new Vector2(0f, 10f), panelObj.transform, defaultFont);
        sfxVolumeSlider = CreateSliderRow("SfxVolume", "Hiệu ứng (SFX / Ếch kêu / Đồ vỡ):", new Vector2(0f, -50f), panelObj.transform, defaultFont);

        LoadSettings();

        // Nút Back
        GameObject btnObj = new GameObject("Btn_Back");
        btnObj.transform.SetParent(panelObj.transform, false);
        Image bImg = btnObj.AddComponent<Image>();
        bImg.color = new Color(0.85f, 0.3f, 0.25f);
        backButton = btnObj.AddComponent<Button>();
        RectTransform bRect = btnObj.GetComponent<RectTransform>();
        bRect.anchoredPosition = new Vector2(0f, -145f);
        bRect.sizeDelta = new Vector2(220f, 50f);

        GameObject bTxt = new GameObject("Text");
        bTxt.transform.SetParent(btnObj.transform, false);
        Text bt = bTxt.AddComponent<Text>();
        bt.text = "◀ QUAY LẠI (BACK)";
        bt.font = defaultFont;
        bt.fontSize = 18;
        bt.fontStyle = FontStyle.Bold;
        bt.alignment = TextAnchor.MiddleCenter;
        bt.color = Color.white;
        RectTransform btRect = bTxt.GetComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.offsetMin = Vector2.zero;
        btRect.offsetMax = Vector2.zero;

        backButton.onClick.AddListener(OnBackButtonClicked);
    }

    private Slider CreateSliderRow(string name, string label, Vector2 pos, Transform parent, Font font)
    {
        GameObject row = new GameObject("Row_" + name);
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.anchoredPosition = pos;
        rowRect.sizeDelta = new Vector2(480f, 40f);

        GameObject lblObj = new GameObject("Label");
        lblObj.transform.SetParent(row.transform, false);
        Text lText = lblObj.AddComponent<Text>();
        lText.text = label;
        lText.font = font;
        lText.fontSize = 15;
        lText.color = Color.white;
        lText.alignment = TextAnchor.MiddleLeft;
        RectTransform lRect = lblObj.GetComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0f, 0.5f);
        lRect.anchorMax = new Vector2(0.5f, 0.5f);
        lRect.sizeDelta = new Vector2(240f, 30f);
        lRect.anchoredPosition = new Vector2(120f, 0f);

        // Slider
        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(row.transform, false);
        RectTransform sRect = sliderObj.AddComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.52f, 0.5f);
        sRect.anchorMax = new Vector2(1.0f, 0.5f);
        sRect.sizeDelta = new Vector2(220f, 20f);
        sRect.anchoredPosition = new Vector2(110f, 0f);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.25f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRect = fillArea.AddComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero;
        faRect.anchorMax = Vector2.one;
        faRect.offsetMin = new Vector2(5f, 0f);
        faRect.offsetMax = new Vector2(-5f, 0f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.8f, 0.4f);
        RectTransform fRect = fill.GetComponent<RectTransform>();
        fRect.anchorMin = Vector2.zero;
        fRect.anchorMax = Vector2.one;
        fRect.offsetMin = Vector2.zero;
        fRect.offsetMax = Vector2.zero;

        slider.fillRect = fRect;
        slider.targetGraphic = fillImg;

        return slider;
    }
}
