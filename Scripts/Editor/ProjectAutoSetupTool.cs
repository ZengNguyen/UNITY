#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// ProjectAutoSetupTool: TỰ ĐỘNG THIẾT LẬP TOÀN BỘ 6 SCENE TRỰC TIẾP VÀO UNITY:
/// 1. Tự động dò tìm đúng 6 Scene có sẵn trong dự án:
///    - Main Menu
///    - Setting
///    - LV1 (Phòng Khách + Hộp Tutorial)
///    - LV 2 (Nhà Bếp)
///    - LV 3 (Sân Vườn)
///    - Ingame UI
/// 2. Tự động thêm toàn bộ 6 Scene vào Unity Build Settings.
/// 3. Dựng trực tiếp UI cho từng scene:
///    - Main Menu: Nút Play (mở chọn 3 màn LV1, LV 2, LV 3), nút Setting (vào scene Setting).
///    - Setting: Sliders âm lượng, nút Quay Lại (Back).
///    - LV1: Hộp Tutorial với nền đất trốn phím S, 2 bục tập nhảy, thành hộp cao, miễn nhiễm tiếng ồn,
///           viên sỏi, mèo rình, camera lock theo ếch, Ingame UI góc trái trên.
///    - LV 2 & LV 3: Bối cảnh, camera lock theo ếch, nút Setting góc trái trên, độ nhạy cảnh báo tăng dần.
///    - Ingame UI: Nút Setting góc trái trên neo chuẩn Anchor (0, 1).
/// </summary>
[InitializeOnLoad]
public class ProjectAutoSetupTool
{
    [InitializeOnLoadMethod]
    private static void OnProjectCompiled()
    {
        EditorApplication.delayCall += () =>
        {
            if (!SessionState.GetBool("JumpKingFrog_AutoSetupRun", false))
            {
                SessionState.SetBool("JumpKingFrog_AutoSetupRun", true);
                Debug.Log("🚀 [ProjectAutoSetupTool] Tự động kích hoạt thiết lập 6 Scene vào Unity...");
                SetupAll6Scenes(false);
            }
        };
    }

    [MenuItem("Tools/Jump King Frog/CAI DAT VA DUNG TAT CA 6 SCENE (One-Click Setup All Scenes)", false, 0)]
    public static void RunFullSetupFromMenu()
    {
        SetupAll6Scenes(true);
    }

    public static void SetupAll6Scenes(bool showDialog)
    {
        // Lưu scene hiện tại nếu có chỉnh sửa
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        // 1. Dò tìm đúng 6 Scene (hỗ trợ cả có dấu cách và không có dấu cách: "Main Menu", "Setting", "LV1", "LV 2", "LV 3", "Ingame UI")
        string mainMenuPath = FindOrCreateScene("Main Menu", "mainmenu", "menu");
        string settingPath = FindOrCreateScene("Setting", "setting");
        string lv1Path = FindOrCreateScene("LV1", "lv1", "level1");
        string lv2Path = FindOrCreateScene("LV 2", "lv2", "level2", "lv 2");
        string lv3Path = FindOrCreateScene("LV 3", "lv3", "level3", "lv 3");
        string ingameUIPath = FindOrCreateScene("Ingame UI", "ingame", "ingameui", "ui");

        // 2. Thêm vào Build Settings
        UpdateBuildSettings(mainMenuPath, settingPath, lv1Path, lv2Path, lv3Path, ingameUIPath);

        // 3. Dựng từng Scene
        SetupMainMenuScene(mainMenuPath, lv1Path, lv2Path, lv3Path, settingPath);
        SetupSettingScene(settingPath, mainMenuPath);
        SetupLevel1Scene(lv1Path, settingPath);
        SetupLevel2Scene(lv2Path, settingPath);
        SetupLevel3Scene(lv3Path, settingPath);
        SetupIngameUIScene(ingameUIPath, settingPath);

        // 4. Mở lại Scene LV1 để sẵn sàng Play test
        EditorSceneManager.OpenScene(lv1Path, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("🎉🎉🎉 HOÀN TẤT THIẾT LẬP TOÀN BỘ 6 SCENE TRỰC TIẾP TRONG UNITY! 🎉🎉🎉");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Jump King Frog - Setup Thành Công!",
                "Đã tự động cài đặt và hoàn thiện trọn vẹn 6 Scene:\n" +
                "1. Main Menu: Nút Play (chọn 3 màn) & Setting\n" +
                "2. Setting: Điều chỉnh âm lượng & nút Quay Lại\n" +
                "3. LV1: Hộp Tutorial (Đất trốn, 2 bục nhảy, miễn tiếng ồn), Camera lock theo ếch, Nút Setting góc trái trên\n" +
                "4. LV 2: Nhà bếp + Ingame UI\n" +
                "5. LV 3: Sân vườn + Ingame UI\n" +
                "6. Ingame UI: Nút Setting góc trái trên\n\n" +
                "Tất cả scene đã được add vào Build Settings. Bạn có thể bấm Play (▶️) để chơi ngay!", "Tuyệt vời!");
        }
    }

    private static string FindOrCreateScene(string defaultName, params string[] searchKeywords)
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string rawFileName = Path.GetFileNameWithoutExtension(path).ToLower();
            string cleanFileName = rawFileName.Replace(" ", "").Replace("_", "");

            foreach (string kw in searchKeywords)
            {
                string cleanKw = kw.ToLower().Replace(" ", "").Replace("_", "");
                if (cleanFileName == cleanKw || cleanFileName.Contains(cleanKw))
                {
                    Debug.Log($"[SetupTool] Đã tìm thấy scene có sẵn: {path}");
                    return path;
                }
            }
        }

        // Nếu chưa có, tạo thư mục Scenes và tạo scene mới
        if (!Directory.Exists("Assets/Scenes"))
        {
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
        }

        string newScenePath = $"Assets/Scenes/{defaultName}.unity";
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(newScene, newScenePath);
        Debug.Log($"[SetupTool] Đã tạo mới scene: {newScenePath}");
        return newScenePath;
    }

    private static void UpdateBuildSettings(params string[] scenePaths)
    {
        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
        foreach (string p in scenePaths)
        {
            if (!string.IsNullOrEmpty(p))
            {
                buildScenes.Add(new EditorBuildSettingsScene(p, true));
            }
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();
        Debug.Log($"[SetupTool] Đã cập nhật {buildScenes.Count} scene vào Unity Build Settings.");
    }

    #region 1. SETUP MAIN MENU
    private static void SetupMainMenuScene(string scenePath, string lv1Path, string lv2Path, string lv3Path, string settingPath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        SetupCamera2D(Vector3.zero, 6f, new Color(0.08f, 0.1f, 0.15f));

        var existingCanvas = Object.FindAnyObjectByType<Canvas>();
        if (existingCanvas != null) Object.DestroyImmediate(existingCanvas.gameObject);

        var controller = Object.FindAnyObjectByType<MainMenuController>();
        if (controller == null)
        {
            GameObject ctrlObj = new GameObject("MainMenu_Controller");
            controller = ctrlObj.AddComponent<MainMenuController>();
        }

        controller.level1SceneName = Path.GetFileNameWithoutExtension(lv1Path);
        controller.level2SceneName = Path.GetFileNameWithoutExtension(lv2Path);
        controller.level3SceneName = Path.GetFileNameWithoutExtension(lv3Path);
        controller.settingSceneName = Path.GetFileNameWithoutExtension(settingPath);

        controller.BuildDefaultMenuUI();

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SetupTool] Đã thiết lập xong Main Menu tại: {scenePath}");
    }
    #endregion

    #region 2. SETUP SETTING SCENE
    private static void SetupSettingScene(string scenePath, string returnScenePath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        SetupCamera2D(Vector3.zero, 6f, new Color(0.1f, 0.12f, 0.18f));

        var existingCanvas = Object.FindAnyObjectByType<Canvas>();
        if (existingCanvas != null) Object.DestroyImmediate(existingCanvas.gameObject);

        var controller = Object.FindAnyObjectByType<SettingSceneController>();
        if (controller == null)
        {
            GameObject ctrlObj = new GameObject("SettingScene_Controller");
            controller = ctrlObj.AddComponent<SettingSceneController>();
        }

        controller.defaultReturnScene = Path.GetFileNameWithoutExtension(returnScenePath);
        controller.BuildDefaultSettingUI();

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SetupTool] Đã thiết lập xong Setting Scene tại: {scenePath}");
    }
    #endregion

    #region 3. SETUP LEVEL 1 SCENE (PHÒNG KHÁCH + HỘP TUTORIAL)
    private static void SetupLevel1Scene(string scenePath, string settingPath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Gọi dựng phòng khách toàn diện (Hộp tutorial + 2 bục + đất trốn + sỏi + mèo + camera lock + Ingame UI)
        LivingRoomSetupTool.SetupLivingRoomScene();

        // Đảm bảo nút Setting trỏ đúng scene setting
        var ingameUI = Object.FindAnyObjectByType<IngameUIController>();
        if (ingameUI != null)
        {
            ingameUI.settingSceneName = Path.GetFileNameWithoutExtension(settingPath);
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SetupTool] Đã thiết lập xong Level 1 (Tutorial Box + Living Room) tại: {scenePath}");
    }
    #endregion

    #region 4. SETUP LEVEL 2 SCENE (NHÀ BẾP)
    private static void SetupLevel2Scene(string scenePath, string settingPath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        if (GameObject.Find("Frog_Player") == null)
        {
            SetupGenericLevel(scene, "MÀN 2: NHÀ BẾP (KITCHEN)", 1.4f, new Color(0.18f, 0.22f, 0.2f), settingPath);
        }
        else
        {
            EnsureIngameUIInScene(settingPath, "MÀN 2: NHÀ BẾP (KITCHEN)");
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SetupTool] Đã thiết lập xong Level 2 tại: {scenePath}");
    }
    #endregion

    #region 5. SETUP LEVEL 3 SCENE (SÂN VƯỜN)
    private static void SetupLevel3Scene(string scenePath, string settingPath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        if (GameObject.Find("Frog_Player") == null)
        {
            SetupGenericLevel(scene, "MÀN 3: SÂN VƯỜN (GARDEN ESCAPE)", 1.8f, new Color(0.12f, 0.22f, 0.15f), settingPath);
        }
        else
        {
            EnsureIngameUIInScene(settingPath, "MÀN 3: SÂN VƯỜN (GARDEN ESCAPE)");
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SetupTool] Đã thiết lập xong Level 3 tại: {scenePath}");
    }
    #endregion

    #region 6. SETUP INGAME UI SCENE
    private static void SetupIngameUIScene(string scenePath, string settingPath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        SetupCamera2D(Vector3.zero, 6f, new Color(0.1f, 0.1f, 0.1f, 0f));

        var existingCanvas = Object.FindAnyObjectByType<Canvas>();
        if (existingCanvas != null) Object.DestroyImmediate(existingCanvas.gameObject);

        GameObject uiObj = new GameObject("IngameUI_Controller");
        var ingameUI = uiObj.AddComponent<IngameUIController>();
        ingameUI.settingSceneName = Path.GetFileNameWithoutExtension(settingPath);
        ingameUI.BuildDefaultIngameUI();

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SetupTool] Đã thiết lập xong Ingame UI Scene tại: {scenePath}");
    }
    #endregion

    #region HELPERS FOR GENERIC LEVEL (LV2 & LV3)
    private static void SetupGenericLevel(Scene scene, string title, float alertMultiplier, Color bgColor, string settingPath)
    {
        GameObject envParent = new GameObject("Environment");

        // Sàn và tường
        CreateBox("Floor", new Vector2(0f, -0.5f), new Vector2(24f, 1f), new Color(0.25f, 0.2f, 0.15f), envParent.transform);
        CreateBox("Wall_L", new Vector2(-11.5f, 8f), new Vector2(1f, 18f), Color.gray, envParent.transform);
        CreateBox("Wall_R", new Vector2(11.5f, 8f), new Vector2(1f, 18f), Color.gray, envParent.transform);

        // Vài bục nhảy thử thách
        CreateBox("Platform_1", new Vector2(-5f, 2.0f), new Vector2(3f, 0.4f), new Color(0.6f, 0.45f, 0.3f), envParent.transform);
        CreateBox("Platform_2", new Vector2(0f, 4.5f), new Vector2(3f, 0.4f), new Color(0.6f, 0.45f, 0.3f), envParent.transform);
        CreateBox("Platform_3", new Vector2(5f, 7.5f), new Vector2(3f, 0.4f), new Color(0.6f, 0.45f, 0.3f), envParent.transform);
        CreateBox("Platform_Goal", new Vector2(-2f, 11f), new Vector2(4f, 0.5f), new Color(0.9f, 0.75f, 0.2f), envParent.transform);

        // Ếch
        GameObject frog = CreateBox("Frog_Player", new Vector2(-8f, 0.6f), new Vector2(0.85f, 0.85f), new Color(0.15f, 0.85f, 0.25f), null);
        var rb = frog.AddComponent<Rigidbody2D>();
        rb.mass = 1f;
        rb.gravityScale = 4f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var frogCtrl = frog.AddComponent<FrogController>();
        frogCtrl.visualTransform = frog.transform;
        frogCtrl.spriteRenderer = frog.GetComponent<SpriteRenderer>();

        GameObject groundPoint = new GameObject("GroundCheckPoint");
        groundPoint.transform.parent = frog.transform;
        groundPoint.transform.localPosition = new Vector3(0f, -0.45f, 0f);
        frogCtrl.groundCheckPoint = groundPoint.transform;
        frogCtrl.groundLayer = LayerMask.GetMask("Default");

        frog.AddComponent<FrogTongue>();
        frog.AddComponent<FrogHungerWeight>();
        frog.AddComponent<FrogMouthInventory>();

        // Alert Manager
        GameObject alertObj = new GameObject("HumanAlertManager");
        var alertMgr = alertObj.AddComponent<HumanAlertManager>();
        alertMgr.sensitivityMultiplier = alertMultiplier;

        // Camera Lock theo ếch
        Camera cam = SetupCamera2D(new Vector3(-8f, 1.8f, -10f), 6f, bgColor);
        var follow = cam.gameObject.GetComponent<CameraFollow2D>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow2D>();
        follow.target = frog.transform;
        follow.lockToFrog = true;
        follow.moveOnlyWhenFrogMoves = true;
        follow.smoothTime = 0.08f;
        follow.minY = 1.0f;
        follow.minX = -12f;
        follow.maxX = 12f;
        follow.offset = new Vector2(0f, 1.2f);

        // Ingame UI
        EnsureIngameUIInScene(settingPath, title);
    }

    private static void EnsureIngameUIInScene(string settingPath, string title)
    {
        var existingCanvas = Object.FindAnyObjectByType<Canvas>();
        GameObject canvasObj = existingCanvas != null ? existingCanvas.gameObject : new GameObject("Ingame_Canvas");
        if (existingCanvas == null)
        {
            var c = canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        var ingameUI = canvasObj.GetComponent<IngameUIController>();
        if (ingameUI == null) ingameUI = canvasObj.AddComponent<IngameUIController>();
        ingameUI.settingSceneName = Path.GetFileNameWithoutExtension(settingPath);
        ingameUI.BuildDefaultIngameUI();
        if (ingameUI.levelTitleText != null) ingameUI.levelTitleText.text = title;
    }

    private static Camera SetupCamera2D(Vector3 pos, float size, Color bgColor)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }
        cam.orthographic = true;
        cam.orthographicSize = size;
        cam.backgroundColor = bgColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(pos.x, pos.y, -10f);
        return cam;
    }

    private static GameObject CreateBox(string name, Vector2 pos, Vector2 size, Color color, Transform parent)
    {
        GameObject obj = new GameObject(name);
        if (parent != null) obj.transform.parent = parent;
        obj.transform.position = new Vector3(pos.x, pos.y, 0f);
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);

        var rend = obj.AddComponent<SpriteRenderer>();
        rend.sprite = LivingRoomAutoBuilder.GetWhiteSprite();
        rend.color = color;
        var mat = LivingRoomAutoBuilder.GetSpriteMaterial();
        if (mat != null) rend.sharedMaterial = mat;

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        return obj;
    }
    #endregion
}
#endif
