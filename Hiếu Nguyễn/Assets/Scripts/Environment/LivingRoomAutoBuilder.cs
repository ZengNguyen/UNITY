using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tự động dựng toàn bộ màn chơi Phòng Khách (Living Room) khi vào game:
/// - Màn hình UI tối giản: Chỉ hiển thị thanh cảnh báo khi có tiếng ồn (> 0%), ẩn toàn bộ UI rườm rà.
/// - Chú ếch xanh trong hộp nuôi ban đầu, mèo cam rình rập, bàn trà, lọ hoa vỡ, ghế sofa nảy,
///   quạt gió kèm luồng bụi bay trực quan, chậu cây đào đất, tường gel bám dính, ruồi bay.
/// - Tự động căn chỉnh Camera theo sát chú ếch cả trong Edit Mode và Play Mode.
/// </summary>
[ExecuteAlways]
public class LivingRoomAutoBuilder : MonoBehaviour
{
    private static Sprite whiteSprite;
    private static Material defaultSpriteMaterial;

    public static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;

#if UNITY_EDITOR
        whiteSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/square.png");
        if (whiteSprite != null) return whiteSprite;
#endif

        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] cols = new Color[32 * 32];
        for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
        tex.SetPixels(cols);
        tex.Apply();
        tex.hideFlags = HideFlags.DontUnloadUnusedAsset | HideFlags.HideAndDontSave;

        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        whiteSprite.hideFlags = HideFlags.DontUnloadUnusedAsset | HideFlags.HideAndDontSave;
        return whiteSprite;
    }

    public static Material GetSpriteMaterial()
    {
        if (defaultSpriteMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                defaultSpriteMaterial = new Material(shader);
                defaultSpriteMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset | HideFlags.HideAndDontSave;
            }
        }
        return defaultSpriteMaterial;
    }

    private void Awake()
    {
        BuildRoomIfNeeded();
    }

    private void Start()
    {
        BuildRoomIfNeeded();
        AlignCameraToFrog();
    }

    private void Update()
    {
        AlignCameraToFrog();
        EnsureSpritesValid();
    }

    private void AlignCameraToFrog()
    {
        Camera cam = Camera.main;
        GameObject frog = GameObject.Find("Frog_Player");
        if (cam != null && frog != null)
        {
            var follow = cam.GetComponent<CameraFollow2D>();
            if (follow != null)
            {
                follow.target = frog.transform;
                follow.minY = 1.0f;
                follow.minX = -12f;
                follow.maxX = 12f;
                follow.offset = new Vector2(0f, 1.2f);
            }

            if (!Application.isPlaying)
            {
                cam.orthographic = true;
                cam.orthographicSize = 6.0f;
                cam.backgroundColor = new Color(0.12f, 0.12f, 0.16f);
                cam.transform.position = new Vector3(-8.0f, 1.8f, -10f);
            }
        }
    }

    /// <summary>
    /// Đảm bảo tất cả SpriteRenderer không bao giờ bị mất sprite do garbage collection
    /// </summary>
    private void EnsureSpritesValid()
    {
        Sprite s = GetWhiteSprite();
        if (s == null) return;
        Material mat = GetSpriteMaterial();

        var renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude);
        foreach (var r in renderers)
        {
            if (r != null && r.sprite == null)
            {
                r.sprite = s;
                if (mat != null && r.sharedMaterial == null) r.sharedMaterial = mat;
            }
        }
    }

    [ContextMenu("Xoa va Dung Lai Tu Dau (Rebuild Room)")]
    public void RebuildRoom()
    {
        CleanExisting();
        BuildRoom();
    }

    public void CleanExisting()
    {
        GameObject env = GameObject.Find("Environment_LivingRoom");
        if (env != null) DestroyImmediate(env);
        GameObject frog = GameObject.Find("Frog_Player");
        if (frog != null) DestroyImmediate(frog);
        GameObject cat = GameObject.Find("Cat_Enemy");
        if (cat != null) DestroyImmediate(cat);
        GameObject canvas = GameObject.Find("LivingRoom_Canvas");
        if (canvas != null) DestroyImmediate(canvas);
        GameObject manager = GameObject.Find("HumanAlertManager");
        if (manager != null) DestroyImmediate(manager);
    }

    [ContextMenu("Dung Phong Khach Ngay")]
    public void BuildRoomIfNeeded()
    {
        if (GameObject.Find("Environment_LivingRoom") != null && GameObject.Find("Frog_Player") != null)
        {
            EnsureSpritesValid();
            var existingFrog = GameObject.Find("Frog_Player");
            if (existingFrog != null && existingFrog.GetComponent<FrogTongue>() == null)
            {
                existingFrog.AddComponent<FrogTongue>();
            }
            var hazard = Object.FindAnyObjectByType<WindAndDustHazard>();
            if (hazard != null)
            {
                hazard.restCooldown = 10.0f;
                hazard.blowDuration = 2.5f;
            }
            var alertMgr = Object.FindAnyObjectByType<HumanAlertManager>();
            if (alertMgr != null)
            {
                alertMgr.defaultBoxPosition = new Vector3(-8.0f, 0.6f, 0f);
                GameObject petBox = GameObject.Find("Pet_Terrarium_Box");
                if (petBox != null) alertMgr.startingBoxTransform = petBox.transform;
            }
            var breakables = Object.FindObjectsByType<BreakableObject>(FindObjectsInactive.Exclude);
            foreach (var b in breakables)
            {
                b.autoRespawn = true;
                b.respawnDelay = 3.5f;
            }
            return;
        }

        CleanExisting();
        BuildRoom();
    }

    private void BuildRoom()
    {
        Debug.Log("🚀 LivingRoomAutoBuilder: Đang tự động dựng màn chơi Phòng Khách...");

        // 1. Tạo Environment Parent
        GameObject envParent = new GameObject("Environment_LivingRoom");

        // --- SÀN NHÀ (Floor) ---
        GameObject floor = Create2DBox("Floor_Ground", new Vector2(0f, -0.5f), new Vector2(24f, 1f), new Color(0.25f, 0.18f, 0.12f), envParent.transform);
        floor.layer = LayerMask.NameToLayer("Default");

        // --- TƯỜNG TRÁI & TƯỜNG PHẢI (Jump King Boundary Walls) ---
        Create2DBox("Wall_Left", new Vector2(-11.5f, 10f), new Vector2(1f, 22f), new Color(0.82f, 0.78f, 0.7f), envParent.transform);
        Create2DBox("Wall_Right", new Vector2(11.5f, 10f), new Vector2(1f, 22f), new Color(0.82f, 0.78f, 0.7f), envParent.transform);

        // --- HỘP NUÔI ẾCH BAN ĐẦU Ở GÓC PHÒNG (Starting Terrarium / Pet Box) ---
        GameObject petBox = new GameObject("Pet_Terrarium_Box");
        petBox.transform.parent = envParent.transform;
        petBox.transform.position = new Vector3(-8.0f, 0.4f, 0f);

        Create2DBox("Box_Bottom", new Vector2(-8.0f, 0.1f), new Vector2(2.4f, 0.2f), new Color(0.4f, 0.7f, 0.9f, 0.7f), petBox.transform);
        Create2DBox("Box_Wall_L", new Vector2(-9.1f, 0.65f), new Vector2(0.2f, 1.1f), new Color(0.4f, 0.7f, 0.9f, 0.6f), petBox.transform);
        Create2DBox("Box_Wall_R", new Vector2(-6.9f, 0.55f), new Vector2(0.2f, 0.9f), new Color(0.4f, 0.7f, 0.9f, 0.6f), petBox.transform);

        // --- BÀN TRÀ PHÒNG KHÁCH (Tea Table) ---
        GameObject table = Create2DBox("Table_Tea", new Vector2(-3.5f, 1.2f), new Vector2(4.5f, 0.4f), new Color(0.55f, 0.35f, 0.2f), envParent.transform);
        Create2DBox("Table_Leg_L", new Vector2(-5.3f, 0.5f), new Vector2(0.4f, 1.0f), new Color(0.45f, 0.28f, 0.15f), envParent.transform);
        Create2DBox("Table_Leg_R", new Vector2(-1.7f, 0.5f), new Vector2(0.4f, 1.0f), new Color(0.45f, 0.28f, 0.15f), envParent.transform);

        // --- ĐỒ VẬT DỄ VỠ TRÊN BÀN (Lọ hoa sứ - Chỉ vỡ khi rơi đổ xuống sàn) ---
        GameObject vase = Create2DBox("FragileVase", new Vector2(-3.5f, 1.95f), new Vector2(0.6f, 1.1f), new Color(0.2f, 0.75f, 0.85f), envParent.transform);
        var vaseRb = vase.AddComponent<Rigidbody2D>();
        vaseRb.mass = 0.6f;
        var breakable = vase.AddComponent<BreakableObject>();
        breakable.objectName = "Lọ hoa pha lê phòng khách";
        breakable.breakForceThreshold = 3.5f;
        breakable.alertNoiseAmount = 35.0f;
        breakable.autoRespawn = true;
        breakable.respawnDelay = 3.5f;

        // --- GHẾ SOFA PHÒNG KHÁCH (Bậc platform đầm chắc ổn định, KHÔNG tự động nảy) ---
        GameObject sofa = Create2DBox("Sofa_Base", new Vector2(4.5f, 1.5f), new Vector2(5.5f, 1.0f), new Color(0.32f, 0.46f, 0.62f), envParent.transform);
        Create2DBox("Sofa_Backrest", new Vector2(7.0f, 3.2f), new Vector2(1.2f, 3.5f), new Color(0.25f, 0.38f, 0.52f), sofa.transform);

        // --- MẢNG TƯỜNG CÓ GEL BÁM TƯỜNG (Sticky Gel Wall) ---
        GameObject gelWall = Create2DBox("StickyGel_Wall", new Vector2(-10.8f, 6.5f), new Vector2(0.45f, 5.0f), new Color(0.95f, 0.9f, 0.2f, 0.85f), envParent.transform);
        var stickyGel = gelWall.AddComponent<StickyGelSurface>();
        stickyGel.maxHoldingWeight = 1.6f;
        stickyGel.slipSlideSpeed = 2.5f;

        // --- CHẬU CÂY CẢNH ĐỂ ĐÀO ĐẤT TRỐN (Planter Box with Diggable Soil) ---
        GameObject planter = Create2DBox("PlanterBox", new Vector2(9.2f, 0.6f), new Vector2(2.5f, 1.2f), new Color(0.7f, 0.35f, 0.15f), envParent.transform);
        GameObject soilTrigger = new GameObject("Soil_Trigger");
        soilTrigger.transform.parent = planter.transform;
        soilTrigger.transform.position = new Vector3(9.2f, 1.3f, 0f);
        var soilCol = soilTrigger.AddComponent<BoxCollider2D>();
        soilCol.isTrigger = true;
        soilCol.size = new Vector2(2.2f, 0.6f);
        var diggable = soilTrigger.AddComponent<DiggableGround>();
        diggable.soilName = "Đất mềm chậu cây cảnh";

        // --- KỆ SÁCH CAO TRÊN TƯỜNG (High Bookshelf Goal) ---
        Create2DBox("Shelf_Mid", new Vector2(-1.5f, 5.5f), new Vector2(3.5f, 0.4f), new Color(0.6f, 0.4f, 0.25f), envParent.transform);
        Create2DBox("Shelf_High", new Vector2(3.5f, 9.5f), new Vector2(4.0f, 0.4f), new Color(0.6f, 0.4f, 0.25f), envParent.transform);
        Create2DBox("Shelf_Goal_Escape", new Vector2(-5.5f, 13.5f), new Vector2(5.0f, 0.5f), new Color(0.85f, 0.65f, 0.2f), envParent.transform);

        // --- QUẠT MÁY THỔI GIÓ & BỤI LÙA (Fan Hazard với luồng bụi trực quan) ---
        GameObject fanObj = Create2DBox("Standing_Fan", new Vector2(10.5f, 5.0f), new Vector2(0.8f, 2.5f), new Color(0.4f, 0.4f, 0.45f), envParent.transform);
        GameObject fanHead = Create2DBox("Fan_Head", new Vector2(10.5f, 6.5f), new Vector2(1.8f, 1.8f), new Color(0.35f, 0.5f, 0.65f), fanObj.transform);
        Create2DBox("Fan_Blades", new Vector2(10.5f, 6.5f), new Vector2(1.4f, 0.25f), new Color(0.85f, 0.85f, 0.9f), fanHead.transform, false, false);

        GameObject windZone = new GameObject("WindAndDust_Zone");
        windZone.transform.parent = fanObj.transform;
        windZone.transform.position = new Vector3(4.5f, 6.0f, 0f);
        var windCol = windZone.AddComponent<BoxCollider2D>();
        windCol.isTrigger = true;
        windCol.size = new Vector2(10.0f, 3.5f);
        var hazard = windZone.AddComponent<WindAndDustHazard>();
        hazard.enableWind = true;
        hazard.windForce = new Vector2(-7.5f, 0f);
        hazard.hasDustInArea = true;
        hazard.eyeRubDuration = 1.2f;
        hazard.restCooldown = 10.0f;
        hazard.blowDuration = 2.5f;

        // --- RUỒI BAY LƯỢN (Flies) ---
        CreateFly("Fly_1", new Vector3(-3.5f, 3.5f, 0f), envParent.transform);
        CreateFly("Fly_2", new Vector3(0.5f, 4.0f, 0f), envParent.transform);
        CreateFly("Fly_3", new Vector3(4.5f, 7.5f, 0f), envParent.transform);

        // --- TẠO CON MÈO TUẦN TRA (Cat Enemy) ---
        GameObject cat = Create2DBox("Cat_Enemy", new Vector2(0.5f, 0.5f), new Vector2(1.6f, 0.9f), new Color(0.95f, 0.55f, 0.15f), null);
        var catRb = cat.AddComponent<Rigidbody2D>();
        catRb.freezeRotation = true;
        catRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var catAI = cat.AddComponent<CatEnemyAI>();
        catAI.patrolSpeed = 2.2f;
        catAI.visionRadius = 7.5f;
        catAI.maxStillTimeBeforePounce = 2.0f;
        catAI.obstacleLayer = LayerMask.GetMask("Default");

        GameObject pA = new GameObject("Cat_Patrol_A");
        pA.transform.position = new Vector3(-5f, 0.5f, 0f);
        pA.transform.parent = envParent.transform;
        GameObject pB = new GameObject("Cat_Patrol_B");
        pB.transform.position = new Vector3(7f, 0.5f, 0f);
        pB.transform.parent = envParent.transform;
        catAI.patrolPointA = pA.transform;
        catAI.patrolPointB = pB.transform;

        // Mắt mèo
        Create2DBox("Cat_Eye", new Vector2(0.35f, 0.15f), new Vector2(0.18f, 0.18f), Color.white, cat.transform, true, false);

        // --- TẠO CHÚ ẾCH (CỤC VUÔNG XANH LÁ TRONG HỘP BẮT ĐẦU) ---
        GameObject frog = Create2DBox("Frog_Player", new Vector2(-8.0f, 0.6f), new Vector2(0.85f, 0.85f), new Color(0.15f, 0.85f, 0.25f), null);
        var frogRb = frog.AddComponent<Rigidbody2D>();
        frogRb.mass = 1.0f;
        frogRb.gravityScale = 4.0f;
        frogRb.freezeRotation = true;
        frogRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        frogRb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var frogCtrl = frog.AddComponent<FrogController>();
        frogCtrl.visualTransform = frog.transform;
        frogCtrl.spriteRenderer = frog.GetComponent<SpriteRenderer>();

        var frogAudio = frog.AddComponent<AudioSource>();
        frogAudio.playOnAwake = false;

        frog.AddComponent<FrogTongue>(); // Cơ chế thò lưỡi kéo đồ vật

        GameObject groundPoint = new GameObject("GroundCheckPoint");
        groundPoint.transform.parent = frog.transform;
        groundPoint.transform.localPosition = new Vector3(0f, -0.45f, 0f);
        frogCtrl.groundCheckPoint = groundPoint.transform;
        frogCtrl.groundLayer = LayerMask.GetMask("Default");

        // Đôi mắt chú ếch
        Create2DBox("Eye_Left", new Vector2(-0.2f, 0.25f), new Vector2(0.24f, 0.24f), Color.white, frog.transform, true, false);
        Create2DBox("Eye_Right", new Vector2(0.2f, 0.25f), new Vector2(0.24f, 0.24f), Color.white, frog.transform, true, false);
        Create2DBox("Pupil_Left", new Vector2(-0.16f, 0.25f), new Vector2(0.1f, 0.1f), Color.black, frog.transform, true, false);
        Create2DBox("Pupil_Right", new Vector2(0.24f, 0.25f), new Vector2(0.1f, 0.1f), Color.black, frog.transform, true, false);

        var hunger = frog.AddComponent<FrogHungerWeight>();
        hunger.fullness = 25f;

        var mouth = frog.AddComponent<FrogMouthInventory>();
        mouth.mouthTransform = frog.transform;

        // Thanh lực Jump Charge Bar trên đầu
        GameObject chargeBarObj = new GameObject("JumpChargeBar");
        chargeBarObj.transform.parent = frog.transform;
        chargeBarObj.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        var chargeBar = chargeBarObj.AddComponent<JumpChargeBar>();
        chargeBar.frog = frogCtrl;
        chargeBar.offset = new Vector3(0f, 0.9f, 0f);

        GameObject barBg = Create2DBox("Bar_BG", Vector2.zero, new Vector2(1.2f, 0.18f), Color.black, chargeBarObj.transform, true, false);
        GameObject barFill = Create2DBox("Bar_Fill", Vector2.zero, new Vector2(1.15f, 0.14f), Color.green, chargeBarObj.transform, true, false);
        chargeBar.backgroundSprite = barBg.GetComponent<SpriteRenderer>();
        chargeBar.fillSprite = barFill.GetComponent<SpriteRenderer>();

        // --- HUMAN ALERT MANAGER (NGƯỜI NUÔI ẾCH) ---
        GameObject alertMgrObj = new GameObject("HumanAlertManager");
        var alertMgr = alertMgrObj.AddComponent<HumanAlertManager>();
        alertMgr.sensitivityMultiplier = 1.3f;
        alertMgr.startingBoxTransform = petBox.transform;
        alertMgr.defaultBoxPosition = new Vector3(-8.0f, 0.6f, 0f);

        // --- UI CANVAS & HUD (TỐI GIẢN - CHỈ HIỆN CẢNH BÁO KHI CÓ TIẾNG ỒN) ---
        CreateHUD(frogCtrl, hunger, mouth);

        // --- SETUP CAMERA ---
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 6.0f;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.16f);
            var follow = cam.gameObject.GetComponent<CameraFollow2D>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow2D>();
            follow.target = frog.transform;
            follow.lockXAxis = false;
            follow.minX = -12f;
            follow.maxX = 12f;
            follow.minY = 1.0f;
            follow.offset = new Vector2(0f, 1.2f);
            cam.transform.position = new Vector3(-8.0f, 1.8f, -10f);
        }

        Debug.Log("🎉 LivingRoomAutoBuilder: Đã dựng xong toàn bộ phòng khách!");
    }

    private static GameObject Create2DBox(string name, Vector2 pos, Vector2 size, Color color, Transform parent, bool isLocal = false, bool addCollider = true)
    {
        GameObject obj = new GameObject(name);
        if (parent != null) obj.transform.parent = parent;
        if (isLocal)
        {
            obj.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        }
        else
        {
            obj.transform.position = new Vector3(pos.x, pos.y, 0f);
        }
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);

        var rend = obj.AddComponent<SpriteRenderer>();
        rend.sprite = GetWhiteSprite();
        rend.color = color;
        var mat = GetSpriteMaterial();
        if (mat != null) rend.sharedMaterial = mat;

        if (addCollider)
        {
            var col = obj.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }

        return obj;
    }

    private static void CreateFly(string name, Vector3 pos, Transform parent)
    {
        GameObject fly = new GameObject(name);
        fly.transform.parent = parent;
        fly.transform.position = pos;
        fly.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

        var rend = fly.AddComponent<SpriteRenderer>();
        rend.sprite = GetWhiteSprite();
        rend.color = new Color(0.15f, 0.15f, 0.2f);
        var mat = GetSpriteMaterial();
        if (mat != null) rend.sharedMaterial = mat;

        var col = fly.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.6f;

        fly.AddComponent<FlyItem>();
    }

    private static void CreateHUD(FrogController frog, FrogHungerWeight hunger, FrogMouthInventory mouth)
    {
        GameObject canvasObj = new GameObject("LivingRoom_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        var uiMgr = canvasObj.AddComponent<LivingRoomUIManager>();
        uiMgr.frog = frog;
        uiMgr.hungerWeight = hunger;
        uiMgr.mouthInventory = mouth;

        // BẢNG CẢNH BÁO CON NGƯỜI (CĂN GIỮA PHÍA TRÊN, MẶC ĐỊNH ẨN)
        GameObject alertPanel = CreateUIPanel("Panel_Alert", canvasObj.transform, new Vector2(0f, -15f), new Vector2(360f, 54f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        uiMgr.alertPanel = alertPanel;

        uiMgr.alertText = CreateUIText("Text_Alert", alertPanel.transform, new Vector2(0f, -6f), "Cảnh Báo Chủ Nuôi: 0%", 16, Color.yellow, TextAnchor.MiddleCenter);
        uiMgr.alertSlider = CreateUISlider("Slider_Alert", alertPanel.transform, new Vector2(20f, -30f), new Vector2(320f, 16f), out uiMgr.alertFillImage);

        // Ban đầu chưa có tiếng ồn -> Ẩn thanh cảnh báo
        alertPanel.SetActive(false);

        // MÀN HÌNH FADE ĐEN CUTSCENE (KHI BỊ NGƯỜI NUÔI BẮT)
        GameObject fadeObj = new GameObject("Image_FadeOverlay");
        fadeObj.transform.SetParent(canvasObj.transform, false);
        var fadeRt = fadeObj.AddComponent<RectTransform>();
        fadeRt.anchorMin = Vector2.zero;
        fadeRt.anchorMax = Vector2.one;
        fadeRt.sizeDelta = Vector2.zero;
        var fadeImg = fadeObj.AddComponent<Image>();
        fadeImg.color = new Color(0, 0, 0, 0);
        fadeObj.SetActive(false);
        uiMgr.fadeOverlayImage = fadeImg;

        GameObject cutsceneTextObj = new GameObject("Text_CutsceneDialogue");
        cutsceneTextObj.transform.SetParent(fadeObj.transform, false);
        var csRt = cutsceneTextObj.AddComponent<RectTransform>();
        csRt.anchorMin = new Vector2(0.1f, 0.35f);
        csRt.anchorMax = new Vector2(0.9f, 0.65f);
        csRt.sizeDelta = Vector2.zero;
        var csTxt = cutsceneTextObj.AddComponent<Text>();
        csTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (csTxt.font == null) csTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        csTxt.alignment = TextAnchor.MiddleCenter;
        csTxt.fontSize = 22;
        csTxt.lineSpacing = 1.3f;
        csTxt.color = Color.white;
        csTxt.text = "";
        cutsceneTextObj.SetActive(false);
        uiMgr.cutsceneText = csTxt;
    }

    private static GameObject CreateUIPanel(string name, Transform parent, Vector2 anchoredPos, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = panel.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);
        return panel;
    }

    private static Text CreateUIText(string name, Transform parent, Vector2 anchoredPos, string textContent, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        var rt = textObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(0, 26);

        var txt = textObj.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.text = textContent;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = alignment;
        return txt;
    }

    private static Slider CreateUISlider(string name, Transform parent, Vector2 anchoredPos, Vector2 size, out Image fillImage)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent, false);
        var rt = sliderObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var slider = sliderObj.AddComponent<Slider>();

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        var bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.25f);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        var fillAreaRt = fillArea.AddComponent<RectTransform>();
        fillAreaRt.anchorMin = Vector2.zero;
        fillAreaRt.anchorMax = Vector2.one;
        fillAreaRt.sizeDelta = Vector2.zero;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        var fillRt = fillObj.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;
        fillImage = fillObj.AddComponent<Image>();
        fillImage.color = Color.green;

        slider.fillRect = fillRt;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;

        return slider;
    }
}
