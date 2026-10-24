using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý giao diện Trong Trò Chơi (Ingame UI):
/// - Xuất hiện ở các scene lv1, lv2, lv3.
/// - Có nút SETTING ở góc trái trên (Top-Left):
///   + Khi nhấn vào nút Setting: Mở popup Cài Đặt / Tạm Dừng ngay tại chỗ (Time.timeScale = 0).
///   + KHÔNG reload hay reset màn chơi! Khi bấm TIẾP TỤC (Resume), trò chơi tiếp tục chính xác vị trí cũ!
/// - Hỗ trợ phím ESC hoặc phím P để bật/tắt menu Cài Đặt.
/// - Có nút CHƠI LẠI (Restart) và nút VỀ MENU CHÍNH.
/// - Có thanh chỉnh âm lượng lưu tự động vào PlayerPrefs.
/// </summary>
public class IngameUIController : MonoBehaviour
{
    public static IngameUIController Instance { get; private set; }

    [Header("=== Nút Setting Góc Trái Trên ===")]
    public Button settingButton;
    public string settingSceneName = "Setting";
    public bool loadSettingSceneDirectly = false;

    [Header("=== Thông Tin Màn Chơi ===")]
    public Text levelTitleText;

    [Header("=== Huy Hiệu Vùng An Toàn / Hộp Nuôi ===")]
    public GameObject safeZoneBadge;
    public Text safeZoneText;

    [Header("=== Bảng Popup Cài Đặt Tại Chỗ (Pause/Setting Popup) ===")]
    public GameObject settingPopupPanel;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public Button resumeButton;
    public Button restartButton;
    public Button mainMenuButton;

    private bool isPaused = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        MainMenuController.EnsureValidEventSystem();
    }

    private void Start()
    {
        Time.timeScale = 1.0f; // Đảm bảo thời gian chạy bình thường khi vào màn

        if (settingButton != null)
        {
            settingButton.onClick.RemoveAllListeners();
            settingButton.onClick.AddListener(ToggleSettingPopup);
        }

        UpdateLevelTitle();

        // Tự động dựng UI và Popup nếu chưa có
        if (settingButton == null || settingPopupPanel == null)
        {
            BuildDefaultIngameUI();
        }

        // Gắn lại sự kiện cho các nút trong popup (sự kiện gắn bằng code không được lưu trong scene)
        WireAllButtons();
    }

    /// <summary>
    /// Tìm và gắn lại sự kiện cho các nút/thanh trượt trong popup.
    /// Cần thiết khi scene đã lưu sẵn popup (BuildDefaultIngameUI bị bỏ qua nên nút mất sự kiện).
    /// </summary>
    private void WireAllButtons()
    {
        if (settingPopupPanel != null)
        {
            if (resumeButton == null) resumeButton = FindPopupButton("Btn_Resume");
            if (restartButton == null) restartButton = FindPopupButton("Btn_Restart");
            if (mainMenuButton == null) mainMenuButton = FindPopupButton("Btn_MainMenu");
            if (musicVolumeSlider == null) musicVolumeSlider = settingPopupPanel.GetComponentInChildren<Slider>(true);
        }

        BindButton(settingButton, ToggleSettingPopup);
        BindButton(resumeButton, ResumeGame);
        BindButton(restartButton, RestartCurrentLevel);
        BindButton(mainMenuButton, ReturnToMainMenu);

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            musicVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
    }

    private Button FindPopupButton(string buttonName)
    {
        Transform t = settingPopupPanel.transform.Find("Dialog_Box/" + buttonName);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void Update()
    {
        UpdateSafeZoneStatus();

        // Nhấn phím Escape hoặc P để bật/tắt menu Tạm Dừng/Cài Đặt
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            ToggleSettingPopup();
        }
    }

    // Đã ẩn dòng tên màn chơi
    private void UpdateLevelTitle()
    {
        if (levelTitleText != null) levelTitleText.gameObject.SetActive(false);
    }

    // Đã ẩn dòng huy hiệu vùng an toàn
    private void UpdateSafeZoneStatus()
    {
        if (safeZoneBadge != null) safeZoneBadge.SetActive(false);
    }

    /// <summary>
    /// Bật / Tắt popup Cài Đặt ngay trong màn chơi (KHÔNG reload scene)
    /// </summary>
    public void ToggleSettingPopup()
    {
        if (settingPopupPanel == null)
        {
            BuildDefaultIngameUI();
        }

        isPaused = !isPaused;
        settingPopupPanel.SetActive(isPaused);

        // Tạm dừng trò chơi khi mở bảng cài đặt, tiếp tục khi đóng
        Time.timeScale = isPaused ? 0f : 1f;

        Debug.Log($"[IngameUI] {(isPaused ? "ĐÃ MỞ BẢNG CÀI ĐẶT / TẠM DỪNG (TimeScale = 0)" : "ĐÃ TIẾP TỤC TRÒ CHƠI (TimeScale = 1)")}");
    }

    /// <summary>
    /// Nút TIẾP TỤC: Đóng popup, giữ nguyên trạng thái chơi hiện tại
    /// </summary>
    public void ResumeGame()
    {
        isPaused = false;
        if (settingPopupPanel != null)
        {
            settingPopupPanel.SetActive(false);
        }
        Time.timeScale = 1.0f;
        Debug.Log("[IngameUI] Bấm 'Tiếp tục' -> Trò chơi tiếp tục bình thường, không bị reset màn!");
    }

    /// <summary>
    /// Nút CHƠI LẠI: Tải lại màn chơi hiện tại
    /// </summary>
    public void RestartCurrentLevel()
    {
        Time.timeScale = 1.0f;
        string activeScene = SceneManager.GetActiveScene().name;
        Debug.Log($"[IngameUI] Bấm 'Chơi lại' -> Tải lại màn {activeScene}");
        SceneManager.LoadScene(activeScene);
    }

    /// <summary>
    /// Nút VỀ MENU CHÍNH
    /// </summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1.0f;
        Debug.Log("[IngameUI] Bấm 'Về Menu Chính' -> Chuyển sang scene Main Menu");
        SceneManager.LoadScene("Main Menu");
    }

    private void OnVolumeChanged(float val)
    {
        AudioListener.volume = val;
        PlayerPrefs.SetFloat("MusicVolume", val);
        PlayerPrefs.SetFloat("SFXVolume", val);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Tự động sinh nút Setting góc trái trên và Popup cài đặt tại chỗ
    /// </summary>
    public void BuildDefaultIngameUI()
    {
        var existingCanvas = FindAnyObjectByType<Canvas>();
        GameObject canvasObj;
        if (existingCanvas != null)
        {
            canvasObj = existingCanvas.gameObject;
        }
        else
        {
            canvasObj = new GameObject("Ingame_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        MainMenuController.EnsureValidEventSystem();

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. NÚT SETTING Ở GÓC TRÁI TRÊN (Top-Left Setting Button)
        if (settingButton == null)
        {
            GameObject btnObj = new GameObject("Btn_TopLeft_Setting");
            btnObj.transform.SetParent(canvasObj.transform, false);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.18f, 0.22f, 0.32f, 0.9f);

            settingButton = btnObj.AddComponent<Button>();
            ColorBlock cb = settingButton.colors;
            cb.highlightedColor = new Color(0.28f, 0.35f, 0.5f);
            cb.pressedColor = new Color(0.12f, 0.15f, 0.22f);
            settingButton.colors = cb;

            RectTransform bRect = btnObj.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0f, 1f);
            bRect.anchorMax = new Vector2(0f, 1f);
            bRect.pivot = new Vector2(0f, 1f);
            bRect.anchoredPosition = new Vector2(20f, -20f);
            bRect.sizeDelta = new Vector2(140f, 44f);

            GameObject bTxtObj = new GameObject("Text");
            bTxtObj.transform.SetParent(btnObj.transform, false);
            Text bTxt = bTxtObj.AddComponent<Text>();
            bTxt.text = "⚙ SETTING";
            bTxt.font = defaultFont;
            bTxt.fontSize = 17;
            bTxt.fontStyle = FontStyle.Bold;
            bTxt.alignment = TextAnchor.MiddleCenter;
            bTxt.color = Color.white;
            RectTransform btRect = bTxtObj.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;

            settingButton.onClick.AddListener(ToggleSettingPopup);
        }

        // 2. Tiêu đề màn chơi ở giữa trên
        if (levelTitleText == null)
        {
            GameObject titleObj = new GameObject("Text_LevelTitle");
            titleObj.transform.SetParent(canvasObj.transform, false);
            levelTitleText = titleObj.AddComponent<Text>();
            levelTitleText.font = defaultFont;
            levelTitleText.fontSize = 18;
            levelTitleText.fontStyle = FontStyle.Bold;
            levelTitleText.alignment = TextAnchor.UpperCenter;
            levelTitleText.color = new Color(0.9f, 0.9f, 0.95f, 0.85f);
            RectTransform tRect = titleObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 1f);
            tRect.anchorMax = new Vector2(0.5f, 1f);
            tRect.pivot = new Vector2(0.5f, 1f);
            tRect.anchoredPosition = new Vector2(0f, -20f);
            tRect.sizeDelta = new Vector2(500f, 35f);
        }

        // 3. Huy hiệu vùng an toàn trong hộp
        if (safeZoneBadge == null)
        {
            safeZoneBadge = new GameObject("Badge_SafeZone");
            safeZoneBadge.transform.SetParent(canvasObj.transform, false);
            safeZoneText = safeZoneBadge.AddComponent<Text>();
            safeZoneText.font = defaultFont;
            safeZoneText.fontSize = 14;
            safeZoneText.alignment = TextAnchor.UpperCenter;
            RectTransform szRect = safeZoneBadge.GetComponent<RectTransform>();
            szRect.anchorMin = new Vector2(0.5f, 1f);
            szRect.anchorMax = new Vector2(0.5f, 1f);
            szRect.pivot = new Vector2(0.5f, 1f);
            szRect.anchoredPosition = new Vector2(0f, -50f);
            szRect.sizeDelta = new Vector2(600f, 25f);
        }

        // 4. BẢNG POPUP CÀI ĐẶT / TẠM DỪNG TẠI CHỖ (Pause Setting Popup Overlay)
        if (settingPopupPanel == null)
        {
            GameObject overlay = new GameObject("Popup_Setting_Overlay");
            overlay.transform.SetParent(canvasObj.transform, false);
            var oRt = overlay.AddComponent<RectTransform>();
            oRt.anchorMin = Vector2.zero;
            oRt.anchorMax = Vector2.one;
            oRt.sizeDelta = Vector2.zero;
            var oImg = overlay.AddComponent<Image>();
            oImg.color = new Color(0f, 0f, 0f, 0.75f); // Làm tối hậu cảnh

            // Khung hộp thoại chính giữa
            GameObject box = new GameObject("Dialog_Box");
            box.transform.SetParent(overlay.transform, false);
            var bRt = box.AddComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.5f, 0.5f);
            bRt.anchorMax = new Vector2(0.5f, 0.5f);
            bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.sizeDelta = new Vector2(460f, 380f);
            var bImg = box.AddComponent<Image>();
            bImg.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

            // Tiêu đề Popup
            GameObject pTitle = new GameObject("Text_PopupTitle");
            pTitle.transform.SetParent(box.transform, false);
            var ptTxt = pTitle.AddComponent<Text>();
            ptTxt.text = "⚙ CÀI ĐẶT & TẠM DỪNG";
            ptTxt.font = defaultFont;
            ptTxt.fontSize = 22;
            ptTxt.fontStyle = FontStyle.Bold;
            ptTxt.alignment = TextAnchor.MiddleCenter;
            ptTxt.color = new Color(0.95f, 0.85f, 0.3f);
            var ptRt = pTitle.GetComponent<RectTransform>();
            ptRt.anchorMin = new Vector2(0f, 1f);
            ptRt.anchorMax = new Vector2(1f, 1f);
            ptRt.pivot = new Vector2(0.5f, 1f);
            ptRt.anchoredPosition = new Vector2(0f, -25f);
            ptRt.sizeDelta = new Vector2(0f, 40f);

            // Nhãn Âm Lượng
            GameObject volLbl = new GameObject("Text_VolLabel");
            volLbl.transform.SetParent(box.transform, false);
            var vlTxt = volLbl.AddComponent<Text>();
            vlTxt.text = "🔊 Âm lượng trò chơi:";
            vlTxt.font = defaultFont;
            vlTxt.fontSize = 16;
            vlTxt.alignment = TextAnchor.MiddleLeft;
            vlTxt.color = Color.white;
            var vlRt = volLbl.GetComponent<RectTransform>();
            vlRt.anchorMin = new Vector2(0.5f, 0.5f);
            vlRt.anchorMax = new Vector2(0.5f, 0.5f);
            vlRt.anchoredPosition = new Vector2(0f, 75f);
            vlRt.sizeDelta = new Vector2(360f, 30f);

            // Thanh trượt âm lượng
            GameObject sliderObj = new GameObject("Slider_Volume");
            sliderObj.transform.SetParent(box.transform, false);
            var slRt = sliderObj.AddComponent<RectTransform>();
            slRt.anchorMin = new Vector2(0.5f, 0.5f);
            slRt.anchorMax = new Vector2(0.5f, 0.5f);
            slRt.anchoredPosition = new Vector2(0f, 40f);
            slRt.sizeDelta = new Vector2(360f, 22f);

            musicVolumeSlider = sliderObj.AddComponent<Slider>();
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            float savedVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
            musicVolumeSlider.value = savedVol;
            musicVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);

            // NÚT 1: TIẾP TỤC (Resume)
            resumeButton = CreatePopupButton(box.transform, "Btn_Resume", "▶ TIẾP TỤC", new Vector2(0f, -20f), new Color(0.18f, 0.65f, 0.32f), defaultFont);
            resumeButton.onClick.AddListener(ResumeGame);

            // NÚT 2: CHƠI LẠI (Restart Level)
            restartButton = CreatePopupButton(box.transform, "Btn_Restart", "🔄 CHƠI LẠI", new Vector2(0f, -80f), new Color(0.25f, 0.45f, 0.75f), defaultFont);
            restartButton.onClick.AddListener(RestartCurrentLevel);

            // NÚT 3: VỀ MENU CHÍNH
            mainMenuButton = CreatePopupButton(box.transform, "Btn_MainMenu", "🏠 VỀ MENU CHÍNH", new Vector2(0f, -140f), new Color(0.75f, 0.25f, 0.25f), defaultFont);
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);

            settingPopupPanel = overlay;
            settingPopupPanel.SetActive(false); // Mặc định ẩn
        }

        UpdateLevelTitle();
    }

    private Button CreatePopupButton(Transform parent, string name, string label, Vector2 pos, Color btnColor, Font font)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        var rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(320f, 46f);

        var img = btnObj.AddComponent<Image>();
        img.color = btnColor;

        var btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = btnColor * 1.25f;
        cb.pressedColor = btnColor * 0.8f;
        btn.colors = cb;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        var txt = txtObj.AddComponent<Text>();
        txt.text = label;
        txt.font = font;
        txt.fontSize = 18;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        var tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.sizeDelta = Vector2.zero;

        return btn;
    }
}