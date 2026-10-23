using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Quản lý giao diện Màn Hình Chính (Main Menu):
/// 1. Có 2 nút bấm chính: PLAY và SETTING.
/// 2. Bấm SETTING -> Chuyển qua scene Setting.
/// 3. Bấm PLAY -> Hiển thị 3 màn chơi: Level 1 (lv1), Level 2 (lv2), Level 3 (lv3) để người chơi lựa chọn,
///    hoặc chuyển thẳng vào scene lv1 nếu muốn chơi ngay!
/// 4. Hỗ trợ tự động tạo giao diện Canvas nếu scene Main Menu còn trống.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("=== Tên các Scene trong Unity Build Settings ===")]
    [Tooltip("Tên scene Màn 1")]
    public string level1SceneName = "LV1";

    [Tooltip("Tên scene Màn 2")]
    public string level2SceneName = "LV 2";

    [Tooltip("Tên scene Màn 3")]
    public string level3SceneName = "LV 3";

    [Tooltip("Tên scene Cài Đặt")]
    public string settingSceneName = "Setting";

    [Header("=== Các nút bấm Main Menu ===")]
    public Button playButton;
    public Button settingButton;
    public Button quitButton;

    [Header("=== Bảng Chọn Màn (Level Selection Panel) ===")]
    public GameObject levelSelectPanel;
    public Button level1Button;
    public Button level2Button;
    public Button level3Button;
    public Button backFromLevelSelectButton;

    [Tooltip("Nếu tích chọn: Bấm Play sẽ chuyển ngay vào lv1; nếu bỏ tích: Bấm Play sẽ mở bảng chọn 3 màn lv1, lv2, lv3")]
    public bool directPlayLevel1 = false;

    private void Awake()
    {
        // Đảm bảo TimeScale hoạt động bình thường khi về Menu
        Time.timeScale = 1f;

        // Tự động kiểm tra và cấu hình EventSystem tương thích New Input System & Legacy
        EnsureValidEventSystem();

        // Lưu scene hiện tại
        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
    }

    private void Start()
    {
        // Gắn sự kiện cho các nút nếu có sẵn
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayButtonClicked);
        }

        if (settingButton != null)
        {
            settingButton.onClick.RemoveAllListeners();
            settingButton.onClick.AddListener(OnSettingButtonClicked);
        }

        if (level1Button != null)
        {
            level1Button.onClick.RemoveAllListeners();
            level1Button.onClick.AddListener(() => LoadLevel(level1SceneName));
        }

        if (level2Button != null)
        {
            level2Button.onClick.RemoveAllListeners();
            level2Button.onClick.AddListener(() => LoadLevel(level2SceneName));
        }

        if (level3Button != null)
        {
            level3Button.onClick.RemoveAllListeners();
            level3Button.onClick.AddListener(() => LoadLevel(level3SceneName));
        }

        if (backFromLevelSelectButton != null)
        {
            backFromLevelSelectButton.onClick.RemoveAllListeners();
            backFromLevelSelectButton.onClick.AddListener(CloseLevelSelectPanel);
        }

        // Tự động dựng UI Canvas nếu chưa gắn nút nào
        if (playButton == null && settingButton == null)
        {
            BuildDefaultMenuUI();
        }
    }

    /// <summary>
    /// Xử lý khi nhấn nút PLAY:
    /// - Chuyển qua scene lv1 hoặc mở bảng chọn 3 màn chơi lv1, lv2, lv3
    /// </summary>
    public void OnPlayButtonClicked()
    {
        if (directPlayLevel1)
        {
            LoadLevel(level1SceneName);
        }
        else
        {
            OpenLevelSelectPanel();
        }
    }

    /// <summary>
    /// Xử lý khi nhấn nút SETTING: Chuyển qua scene Setting
    /// </summary>
    public void OnSettingButtonClicked()
    {
        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();

        LoadSceneSafely(settingSceneName);
    }

    public void OpenLevelSelectPanel()
    {
        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(true);
        }
        else
        {
            // Nếu không có panel riêng, chuyển trực tiếp vào lv1
            LoadLevel(level1SceneName);
        }
    }

    public void CloseLevelSelectPanel()
    {
        if (levelSelectPanel != null)
        {
            levelSelectPanel.SetActive(false);
        }
    }

    public void LoadLevel(string sceneName)
    {
        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
        PlayerPrefs.SetString("CurrentPlayingLevel", sceneName);
        PlayerPrefs.Save();

        LoadSceneSafely(sceneName);
    }

    private void LoadSceneSafely(string sceneName)
    {
        Debug.Log($"[MainMenu] Đang chuyển tới scene: {sceneName}");
        try
        {
            SceneManager.LoadScene(sceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MainMenu] Không thể tải scene '{sceneName}'. Hãy kiểm tra Build Settings (File > Build Settings)! Lỗi: {e.Message}");
        }
    }

    /// <summary>
    /// Tự động sinh giao diện Canvas Menu chuẩn nếu scene chưa có UI
    /// </summary>
    public void BuildDefaultMenuUI()
    {
        var existingCanvas = FindAnyObjectByType<Canvas>();
        if (existingCanvas != null && playButton != null) return;

        Debug.Log("🎨 [MainMenu] Đang tự động tạo giao diện Menu chuẩn...");

        GameObject canvasObj = new GameObject("MainMenu_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // EventSystem
        EnsureValidEventSystem();

        // Font mặc định
        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. Tiêu đề Game
        GameObject titleObj = new GameObject("Title_Text");
        titleObj.transform.SetParent(canvasObj.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "🐸 CHÚ ẾCH TRỐN KHỎI NHÀ\n(FROG ESCAPE HOUSE)";
        titleText.font = defaultFont;
        titleText.fontSize = 44;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.2f, 0.95f, 0.35f);
        titleText.raycastTarget = false;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0f, 160f);
        titleRect.sizeDelta = new Vector2(800f, 140f);

        // 2. Nút PLAY
        playButton = CreateMenuButton("Btn_Play", "▶ CHƠI GAME (PLAY)", new Vector2(0f, 20f), new Vector2(280f, 65f), new Color(0.2f, 0.75f, 0.3f), canvasObj.transform, defaultFont);
        playButton.onClick.AddListener(OnPlayButtonClicked);

        // 3. Nút SETTING
        settingButton = CreateMenuButton("Btn_Setting", "⚙ CÀI ĐẶT (SETTING)", new Vector2(0f, -60f), new Vector2(280f, 65f), new Color(0.25f, 0.45f, 0.85f), canvasObj.transform, defaultFont);
        settingButton.onClick.AddListener(OnSettingButtonClicked);

        // 4. Panel chọn 3 màn chơi (Level Select Panel)
        levelSelectPanel = new GameObject("Panel_LevelSelect");
        levelSelectPanel.transform.SetParent(canvasObj.transform, false);
        Image panelBg = levelSelectPanel.AddComponent<Image>();
        panelBg.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);
        RectTransform panelRect = levelSelectPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(500f, 360f);
        panelRect.anchoredPosition = Vector2.zero;

        // Tiêu đề chọn màn
        GameObject selectTitle = new GameObject("Text_SelectLevel");
        selectTitle.transform.SetParent(levelSelectPanel.transform, false);
        Text stText = selectTitle.AddComponent<Text>();
        stText.text = "CHỌN MÀN CHƠI (SELECT LEVEL)";
        stText.font = defaultFont;
        stText.fontSize = 24;
        stText.fontStyle = FontStyle.Bold;
        stText.alignment = TextAnchor.MiddleCenter;
        stText.color = Color.white;
        stText.raycastTarget = false;
        RectTransform stRect = selectTitle.GetComponent<RectTransform>();
        stRect.anchoredPosition = new Vector2(0f, 130f);
        stRect.sizeDelta = new Vector2(450f, 50f);

        // 3 nút chọn màn: lv1, lv2, lv3
        level1Button = CreateMenuButton("Btn_Level1", "MÀN 1: PHÒNG KHÁCH (LV1)", new Vector2(0f, 60f), new Vector2(380f, 50f), new Color(0.2f, 0.65f, 0.3f), levelSelectPanel.transform, defaultFont);
        level1Button.onClick.AddListener(() => LoadLevel(level1SceneName));

        level2Button = CreateMenuButton("Btn_Level2", "MÀN 2: NHÀ BẾP (LV 2)", new Vector2(0f, 0f), new Vector2(380f, 50f), new Color(0.85f, 0.55f, 0.15f), levelSelectPanel.transform, defaultFont);
        level2Button.onClick.AddListener(() => LoadLevel(level2SceneName));

        level3Button = CreateMenuButton("Btn_Level3", "MÀN 3: SÂN VƯỜN (LV 3)", new Vector2(0f, -60f), new Vector2(380f, 50f), new Color(0.75f, 0.25f, 0.25f), levelSelectPanel.transform, defaultFont);
        level3Button.onClick.AddListener(() => LoadLevel(level3SceneName));

        // Nút Đóng panel
        backFromLevelSelectButton = CreateMenuButton("Btn_BackSelect", "◀ QUAY LẠI", new Vector2(0f, -125f), new Vector2(180f, 40f), new Color(0.4f, 0.4f, 0.45f), levelSelectPanel.transform, defaultFont);
        backFromLevelSelectButton.onClick.AddListener(CloseLevelSelectPanel);

        // Mặc định ẩn panel chọn màn
        levelSelectPanel.SetActive(false);
    }

    public static void EnsureValidEventSystem()
    {
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            es = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        }

#if ENABLE_INPUT_SYSTEM
        var standalone = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (standalone != null)
        {
            if (Application.isPlaying) Object.Destroy(standalone);
            else Object.DestroyImmediate(standalone);
        }
        if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
        {
            es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
#else
        if (es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
        {
            es.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
#endif
    }

    private Button CreateMenuButton(string name, string text, Vector2 pos, Vector2 size, Color color, Transform parent, Font font)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        Image img = btnObj.AddComponent<Image>();
        img.color = color;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = color * 1.25f;
        cb.pressedColor = color * 0.8f;
        btn.colors = cb;

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        Text t = txtObj.AddComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = (int)(size.y * 0.4f);
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false; // Không cản trở click chuột vào button

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        return btn;
    }
}
