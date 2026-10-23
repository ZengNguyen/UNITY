using System.Collections;
using UnityEngine;

/// <summary>
/// Script điều khiển chú ếch theo phong cách vật lý Jump King 2D:
/// - Di chuyển đi bộ dưới đất, gồng nhảy parabol, khóa lái trên không, đập tường nảy ra, rơi đau bẹp dí.
/// - TÍCH HỢP CÁC CƠ CHẾ NÂNG CAO:
///   1. Trọng lượng cơ thể: Ảnh hưởng lực nhảy và khả năng bám Gel.
///   2. (Đã gỡ bỏ cơ chế Gel bám tường)
///   3. Gió & Bụi: Gió bạt góc nhảy; Bụi bay vào mắt gây Stun đứng yên dụi mắt tại chỗ (không bị teleport).
///   4. Tiếng kêu Croak (Phím F / Phím C): Phát âm thanh ộp ộp + sóng âm dọa mèo, gây ồn báo động người nuôi.
///   5. Đào đất trốn (Phím S): Xoay 360 độ lặn xuống đất, độ sâu phụ thuộc độ dày vùng đất.
///   6. Khóa phím A và D khi đang nhảy trên không trung (chuẩn Jump King).
///   7. Lưỡi bám dính (Chuột phải): kéo vật thể theo kích thước, hoặc bám bề mặt để đu dây
///      (lăn chuột chỉnh độ dài dây, A/D đu qua lại, chuột phải/Space để nhả).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FrogController : MonoBehaviour
{
    public enum FrogState
    {
        Grounded,   // Đang ở trên mặt đất (có thể đi bộ hoặc chuẩn bị nhảy)
        Charging,   // Đang gồng tích lực nhảy (đứng im)
        InAir,      // Đang bay trên không (khóa hoàn toàn phím A/D)
        Bounced,    // Vừa đập tường nảy ra, đang rơi tự do (khóa A/D)
        Splat,      // Bị choáng/bẹp người khi tiếp đất sau cú ngã đau
        Stunned,    // Bị bụi vào mắt, đứng yên dụi mắt tại chỗ
        WallStuck,  // Đang bám dính trên bề mặt Gel tường
        Burrowed    // Đang chui dưới đất trốn (ẩn nấp)
    }

    [Header("=== Trạng thái hiện tại ===")]
    [SerializeField] private FrogState currentState = FrogState.Grounded;
    public FrogState CurrentState => currentState;

    [Header("=== Điều khiển kiểu Mario (Mario Platformer Movement) ===")]
    [Tooltip("Tốc độ di chuyển đi bộ / chạy mượt mà kiểu Mario")]
    public float walkSpeed = 6.8f;

    [Tooltip("Lực nhảy tức thì kiểu Mario khi bấm Space")]
    public float marioJumpForce = 14.5f;

    [Tooltip("Thời gian Coyote Time (cho phép bấm nhảy sau khi vừa bước hụt mép bục)")]
    public float coyoteTimeDuration = 0.15f;

    [Tooltip("Thời gian Jump Buffer (ghi nhận phím Space bấm trước khi chạm đất)")]
    public float jumpBufferDuration = 0.15f;

    [HideInInspector] public float minJumpForce = 6.0f;
    [HideInInspector] public float maxJumpForce = 15.0f;
    [HideInInspector] public float maxChargeTime = 0.1f;
    [HideInInspector] public float jumpAngle = 90f;

    [Header("=== Bật tường & Rơi ===")]
    [Tooltip("Lực nảy bật ngược lại khi đập đầu/thân vào tường lúc đang bay")]
    public float wallBounceForceX = 3.5f;
    public float wallBounceForceY = 2.0f;

    [Tooltip("Vận tốc rơi tối thiểu để kích hoạt trạng thái dẹp lép (Splat)")]
    public float fallSplatVelocityThreshold = -12f;

    [Tooltip("Thời gian bị choáng nằm bẹp sau cú rơi mạnh (giây)")]
    public float splatDuration = 0.5f;

    [Header("=== Tiếng kêu ếch (Croak - Phím F hoặc Phím C) ===")]
    public KeyCode croakKey = KeyCode.F;
    public float croakScareRadius = 6.0f;
    public float croakNoiseAlert = 20.0f;

    [Header("=== Đào đất trốn ===")]
    [Tooltip("Thời gian xoay 360 độ và lặn xuống (giây)")]
    public float burrowSpinDuration = 0.6f;

    [Tooltip("Nhân với chiều cao vùng đất để ra độ sâu ếch lặn xuống")]
    public float burrowDepthMultiplier = 1.0f;

    public float minBurrowDepth = 0.2f;
    public float maxBurrowDepth = 1.2f;

    [Header("=== Lưỡi bám dính - Điều khiển ===")]
    [Tooltip("0 = chuột trái, 1 = chuột phải, 2 = chuột giữa")]
    public int tongueMouseButton = 1;
    public KeyCode tongueReleaseKey = KeyCode.Space;
    [Tooltip("Bật để đảo chiều lăn chuột (lên = nới dài)")]
    public bool invertScroll = false;

    [Header("=== Lưỡi bám dính - Lưỡi ===")]
    public float maxTongueLength = 9f;
    public float tongueSpeed = 40f;
    public float tongueRetractSpeed = 60f;
    public float tongueWidth = 0.12f;
    public Color tongueColor = new Color(1f, 0.45f, 0.55f);
    public Vector2 mouthOffset = new Vector2(0f, 0.1f);

    [Header("=== Lưỡi bám dính - Kéo vật thể ===")]
    [Tooltip("Tốc độ kéo của vật có kích thước rất nhỏ")]
    public float pullSpeedBase = 14f;
    [Tooltip("Vật càng to càng chậm: tốc độ = base / (1 + kích thước x hệ số này)")]
    public float pullSizePenalty = 1.0f;
    public float minPullSpeed = 2.5f;
    [Tooltip("Vật có cạnh lớn hơn mức này sẽ không kéo được, mà dùng làm điểm bám để đu")]
    public float heavySize = 2.5f;
    public float pullStopDistance = 1.1f;
    public float pullTimeout = 2.5f;

    [Header("=== Lưỡi bám dính - Đu dây ===")]
    public float minRopeLength = 0.8f;
    [Tooltip("Mỗi nấc lăn chuột đổi độ dài dây bao nhiêu")]
    public float scrollStep = 0.6f;
    [Tooltip("Tốc độ tời dây (đơn vị/giây) khi lăn chuột để thu ngắn hoặc nới dài")]
    public float ropeReelSpeed = 8f;
    public float swingForce = 35f;
    public float maxSwingSpeed = 16f;

    [Header("=== Kiểm tra Mặt đất (Ground Check) ===")]
    public Transform groundCheckPoint;
    public Vector2 groundCheckSize = new Vector2(0.35f, 0.08f);
    public LayerMask groundLayer;

    [Header("=== Hiệu ứng Pixel / Squash & Stretch ===")]
    public SpriteRenderer spriteRenderer;
    public Transform visualTransform;

    // Biến nội bộ
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private float currentChargeTime = 0f;
    private float horizontalInput = 0f;
    private int facingDirection = 1; // 1: Phải, -1: Trái
    private int jumpDirection = 0;   // -1: Chéo trái, 0: Thẳng đứng, 1: Chéo phải
    private bool isGrounded = false;
    private Vector3 originalVisualScale = Vector3.one;
    private float highestFallSpeed = 0f;
    private float originalGravityScale = 4.0f;

    // Biến hỗ trợ Mario Movement
    private float coyoteTimer = 0f;
    private float jumpBufferTimer = 0f;

    // Biến đào đất
    private bool canBurrowHere = false;
    private DiggableGround nearbySoil;
    private bool isBurrowAnimating = false;
    private float currentBurrowDepth = 0f;
    private SpriteRenderer[] burrowRenderers;
    private int[] burrowOriginalOrders;

    // Biến lưỡi bám dính
    private enum TongueState { Idle, Extending, Pulling, Swinging, Retracting }
    private TongueState tongueState = TongueState.Idle;
    private LineRenderer tongueLine;
    private Vector2 tongueDir;
    private float tongueTipDist;
    private float tongueTargetDist;
    private bool tongueHasHit;
    private Vector2 tongueHitPoint;
    private Transform tongueHitTf;
    private Collider2D tongueHitCol;
    private Vector2 tongueDrawTip;
    private Rigidbody2D pullBody;
    private Collider2D pullCol;
    private float pullTimer;
    private float pullSize;
    private Transform anchorTf;
    private Vector2 anchorLocal;
    private DistanceJoint2D ropeJoint;
    private float ropeLength;
    private float ropeTarget;
    private float swingInput;
    private bool ExternalControl; // Đang đu dây trên không: không tự đặt vận tốc đi bộ

    // Tham chiếu module hỗ trợ
    private FrogHungerWeight hungerWeight;

    // Âm thanh tiếng ếch kêu tạo bằng code
    private static AudioClip proceduralCroakClip;

    // Thuộc tính công khai
    public float ChargePercentage => Mathf.Clamp01(currentChargeTime / maxChargeTime);
    public bool IsCharging => currentState == FrogState.Charging;
    public int FacingDirection => facingDirection;
    public bool IsBurrowed => currentState == FrogState.Burrowed;
    public bool IsMoving => Mathf.Abs(rb.linearVelocity.x) > 0.1f || Mathf.Abs(rb.linearVelocity.y) > 0.1f;
    public float CurrentWeight => hungerWeight != null ? hungerWeight.CurrentWeight : 1.0f;

    public Vector3 InitialSpawnPosition { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hungerWeight = GetComponent<FrogHungerWeight>();

        InitialSpawnPosition = transform.position;

        if (visualTransform == null) visualTransform = transform;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        originalVisualScale = visualTransform.localScale;

        originalGravityScale = 4.0f;
        rb.gravityScale = originalGravityScale;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.freezeRotation = true;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        CreateTongueLine();
    }

    private void OnDisable()
    {
        ReleaseTongue();
    }

    private void Update()
    {
        CheckGrounded();
        UpdateTongue();

        // Cập nhật Coyote Time & Jump Buffer chuẩn Mario
        if (isGrounded)
        {
            coyoteTimer = coyoteTimeDuration;
            if (currentState == FrogState.InAir || currentState == FrogState.Bounced)
            {
                currentState = FrogState.Grounded;
            }
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }

        if (jumpBufferTimer > 0f) jumpBufferTimer -= Time.deltaTime;

        // Bấm phím F, C hoặc X: Kêu Croak tạo tiếng động dọa mèo
        if (GameInput.GetKeyDown(croakKey) || GameInput.GetKeyDown(KeyCode.C) || GameInput.GetKeyDown(KeyCode.X))
        {
            ExecuteCroak();
        }

        // 1. NHẬN LỆNH DI CHUYỂN TRÁI / PHẢI CẢ KHI TRÊN ĐẤT LẪN TRÊN KHÔNG (Air Control Mario)
        if (currentState != FrogState.Burrowed && currentState != FrogState.Stunned)
        {
            horizontalInput = GameInput.GetHorizontalAxis();

            if (horizontalInput > 0.05f)
            {
                facingDirection = 1;
                SetSpriteFacing(true);
            }
            else if (horizontalInput < -0.05f)
            {
                facingDirection = -1;
                SetSpriteFacing(false);
            }

            // Nhấn S để đào đất trốn khi ở trên chậu cây/bồn đất
            if (isGrounded && canBurrowHere && (GameInput.GetKeyDown(KeyCode.S) || GameInput.GetKeyDown(KeyCode.DownArrow)))
            {
                StartBurrowing();
                return;
            }

            // 2. NHẢY TỨC THỜI KIỂU MARIO KHI BẤM SPACE
            if (GameInput.GetKeyDown(KeyCode.Space))
            {
                jumpBufferTimer = jumpBufferDuration;
            }

            if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                ExecuteMarioJump();
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
            }

            // Nhả Space sớm khi đang bay lên -> giảm độ cao (Variable Jump Height kiểu Mario)
            if (GameInput.GetKeyUp(KeyCode.Space) && rb.linearVelocity.y > 1.0f)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
            }
        }
        else if (currentState == FrogState.Burrowed)
        {
            HandleBurrowedInput();
        }
    }

    private void FixedUpdate()
    {
        // Đang đào đất (Kinematic): vị trí do coroutine điều khiển, không đụng vào vận tốc
        if (currentState == FrogState.Burrowed)
        {
            return;
        }

        FixedUpdateTongue();

        // Đang đu dây trên không: để vật lý điều khiển
        if (ExternalControl)
        {
            return;
        }

        if (currentState == FrogState.Grounded || currentState == FrogState.InAir || currentState == FrogState.Bounced)
        {
            // Di chuyển mượt mà cả dưới đất và trên không (Air Control Mario)
            rb.linearVelocity = new Vector2(horizontalInput * walkSpeed, rb.linearVelocity.y);
        }
        else if (currentState == FrogState.Charging || currentState == FrogState.Splat || currentState == FrogState.Stunned)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    private void ExecuteMarioJump()
    {
        currentState = FrogState.InAir;
        rb.linearVelocity = new Vector2(horizontalInput * walkSpeed, marioJumpForce);

        // Hiệu ứng co duỗi nhẹ khi bật nhảy
        visualTransform.localScale = new Vector3(originalVisualScale.x * 0.85f, originalVisualScale.y * 1.25f, originalVisualScale.z);
        StartCoroutine(ResetVisualScaleRoutine());
    }

    #region DI CHUYỂN DƯỚI ĐẤT & GỒNG NHẢY

    private void HandleGroundedInput()
    {
        horizontalInput = GameInput.GetHorizontalAxis();

        // Xoay mặt
        if (horizontalInput > 0.1f)
        {
            facingDirection = 1;
            SetSpriteFacing(true);
        }
        else if (horizontalInput < -0.1f)
        {
            facingDirection = -1;
            SetSpriteFacing(false);
        }

        // Nhấn phím S để đào đất trốn (nếu đang ở trên chậu cây/đất)
        if (canBurrowHere && (GameInput.GetKeyDown(KeyCode.S) || GameInput.GetKeyDown(KeyCode.DownArrow)))
        {
            StartBurrowing();
            return;
        }

        // Nhấn giữ Space để bắt đầu gồng nhảy
        if (GameInput.GetKeyDown(KeyCode.Space))
        {
            StartCharging();
        }
    }

    private void StartCharging()
    {
        currentState = FrogState.Charging;
        currentChargeTime = 0f;
        rb.linearVelocity = Vector2.zero;
        UpdateJumpDirection();
    }

    private void HandleCharging()
    {
        UpdateJumpDirection();
        currentChargeTime += Time.deltaTime;

        // Hiệu ứng Squash (co người lại)
        float factor = ChargePercentage;
        float squishY = Mathf.Lerp(1.0f, 0.65f, factor);
        float stretchX = Mathf.Lerp(1.0f, 1.3f, factor);
        visualTransform.localScale = new Vector3(originalVisualScale.x * stretchX, originalVisualScale.y * squishY, originalVisualScale.z);

        // Tự động bung nhảy khi max lực
        if (currentChargeTime >= maxChargeTime)
        {
            ExecuteJump();
            return;
        }

        // Thả Space -> Bung nhảy
        if (GameInput.GetKeyUp(KeyCode.Space))
        {
            ExecuteJump();
        }
    }

    private void UpdateJumpDirection()
    {
        float h = GameInput.GetHorizontalAxis();
        if (h > 0.1f)
        {
            jumpDirection = 1;
            facingDirection = 1;
            SetSpriteFacing(true);
        }
        else if (h < -0.1f)
        {
            jumpDirection = -1;
            facingDirection = -1;
            SetSpriteFacing(false);
        }
        else
        {
            jumpDirection = 0;
        }
    }

    private void ExecuteJump()
    {
        float chargeRatio = ChargePercentage;

        // Trọng lượng ảnh hưởng tới lực nhảy: Ếch nặng hơn sẽ nhảy thấp hơn 1 chút
        float weightPenalty = Mathf.Lerp(1.0f, 0.75f, (CurrentWeight - 1.0f) / 1.5f);
        float actualForce = Mathf.Lerp(minJumpForce, maxJumpForce, chargeRatio) * weightPenalty;

        Vector2 jumpVector;
        if (jumpDirection == 0)
        {
            jumpVector = Vector2.up * actualForce;
        }
        else
        {
            float rad = jumpAngle * Mathf.Deg2Rad;
            float vx = Mathf.Cos(rad) * jumpDirection;
            float vy = Mathf.Sin(rad);
            jumpVector = new Vector2(vx, vy).normalized * actualForce;
        }

        visualTransform.localScale = new Vector3(originalVisualScale.x * 0.8f, originalVisualScale.y * 1.25f, originalVisualScale.z);
        rb.gravityScale = originalGravityScale;
        rb.linearVelocity = jumpVector;

        // Khóa phím điều khiển ngay khi bung nhảy
        horizontalInput = 0f;
        currentState = FrogState.InAir;
        highestFallSpeed = 0f;
        currentChargeTime = 0f;

        StartCoroutine(ResetVisualScaleRoutine());
    }

    private IEnumerator ResetVisualScaleRoutine()
    {
        yield return new WaitForSeconds(0.12f);
        visualTransform.localScale = originalVisualScale;
    }

    private void HandleInAir()
    {
        // Khóa hoàn toàn A và D trên không trung
        horizontalInput = 0f;

        // Khi tiếp đất thành công
        if (isGrounded && rb.linearVelocity.y <= 0.05f)
        {
            visualTransform.localScale = originalVisualScale;

            if (highestFallSpeed < fallSplatVelocityThreshold)
            {
                StartCoroutine(SplatRoutine());
            }
            else
            {
                currentState = FrogState.Grounded;
            }

            highestFallSpeed = 0f;
        }
    }

    private IEnumerator SplatRoutine()
    {
        currentState = FrogState.Splat;
        rb.linearVelocity = Vector2.zero;
        visualTransform.localScale = new Vector3(originalVisualScale.x * 1.4f, originalVisualScale.y * 0.4f, originalVisualScale.z);

        // Rơi mạnh gây tiếng động nhẹ làm tăng cảnh báo con người
        if (HumanAlertManager.Instance != null)
        {
            HumanAlertManager.Instance.AddNoise(10f, transform.position);
        }

        yield return new WaitForSeconds(splatDuration);

        visualTransform.localScale = originalVisualScale;
        currentState = FrogState.Grounded;
    }

    #endregion

    #region GEL BÁM TƯỜNG (ĐÃ GỠ BỎ)

    /// <summary>
    /// Cơ chế dính tường đã bị gỡ bỏ. Giữ hàm rỗng này chỉ để các script cũ
    /// (như StickyGelSurface) còn gọi tới không bị lỗi biên dịch. Hàm không làm gì cả.
    /// </summary>
    public void AttachToGelWall(StickyGelSurface gel, Vector2 normal, bool isOverweight)
    {
    }

    #endregion

    #region GIÓ & BỤI LÙA (WIND & DUST)

    /// <summary>
    /// Nhận lực gió tác động bẻ cong đường bay
    /// </summary>
    public void ApplyWindForce(Vector2 wind)
    {
        rb.AddForce(wind, ForceMode2D.Force);
    }

    /// <summary>
    /// Bị bụi bay vào mắt -> Dừng hành động tại chỗ để dụi mắt (Không bị teleport)
    /// </summary>
    public void ApplyDustInEyes(float duration)
    {
        if (currentState == FrogState.Stunned) return;
        StartCoroutine(DustStunRoutine(duration));
    }

    private IEnumerator DustStunRoutine(float duration)
    {
        currentState = FrogState.Stunned;
        // Dừng chuyển động ngang, giữ nguyên vị trí rơi hiện tại, không teleport về (0,0)
        rb.linearVelocity = new Vector2(0f, Mathf.Min(rb.linearVelocity.y, 0f));
        horizontalInput = 0f;

        Debug.LogWarning("👁️ BỤI BAY VÀO MẮT! Ếch đang dừng lại dụi mắt...");

        // Rung nhẹ đôi mắt (Child Transform) thay vì di chuyển cả thân ếch
        Transform eyeL = transform.Find("Eye_Left");
        Transform eyeR = transform.Find("Eye_Right");
        Vector3 origL = eyeL != null ? eyeL.localPosition : new Vector3(-0.2f, 0.25f, 0f);
        Vector3 origR = eyeR != null ? eyeR.localPosition : new Vector3(0.2f, 0.25f, 0f);

        // Tạo chữ thông báo nổi trên đầu
        GameObject dustTextObj = new GameObject("Dust_Stun_Text");
        dustTextObj.transform.position = transform.position + Vector3.up * 0.95f;
        dustTextObj.transform.parent = transform;
        var tm = dustTextObj.AddComponent<TextMesh>();
        tm.text = "👁️💨 CAY MẮT!";
        tm.fontSize = 24;
        tm.characterSize = 0.07f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.85f, 0.3f);

        // Tạo đám bụi nhỏ quay quanh đầu
        GameObject dustPuff = new GameObject("Dust_HeadPuff");
        dustPuff.transform.position = transform.position + Vector3.up * 0.45f;
        dustPuff.transform.parent = transform;
        var puffRend = dustPuff.AddComponent<SpriteRenderer>();
        puffRend.sprite = LivingRoomAutoBuilder.GetWhiteSprite();
        puffRend.sharedMaterial = LivingRoomAutoBuilder.GetSpriteMaterial();
        puffRend.color = new Color(0.85f, 0.75f, 0.4f, 0.6f);
        puffRend.sortingOrder = 12;
        dustPuff.transform.localScale = new Vector3(0.5f, 0.2f, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float shake = Mathf.Sin(elapsed * 32f) * 0.04f;
            if (eyeL != null) eyeL.localPosition = origL + new Vector3(shake, 0f, 0f);
            if (eyeR != null) eyeR.localPosition = origR + new Vector3(shake, 0f, 0f);

            dustPuff.transform.Rotate(0f, 0f, 220f * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (eyeL != null) eyeL.localPosition = origL;
        if (eyeR != null) eyeR.localPosition = origR;
        Destroy(dustTextObj);
        Destroy(dustPuff);

        currentState = isGrounded ? FrogState.Grounded : FrogState.InAir;
    }

    #endregion

    #region TIẾNG KÊU CROAK (PHÍM F / PHÍM C - DỌA MÈO)

    private static AudioClip GetOrCreateCroakClip()
    {
        if (proceduralCroakClip != null) return proceduralCroakClip;

        int sampleRate = 44100;
        float duration = 0.32f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Sin(t / duration * Mathf.PI);
            float f1 = Mathf.Sin(2f * Mathf.PI * 145f * t);
            float f2 = Mathf.Sin(2f * Mathf.PI * 290f * t) * 0.5f;
            float f3 = Mathf.Sin(2f * Mathf.PI * 450f * t) * 0.3f;
            float throatPulse = Mathf.Sin(2f * Mathf.PI * 28f * t);
            samples[i] = (f1 + f2 + f3) * (0.6f + 0.4f * throatPulse) * envelope * 0.85f;
        }

        proceduralCroakClip = AudioClip.Create("Frog_Croak_Sound", sampleCount, 1, sampleRate, false);
        proceduralCroakClip.SetData(samples, 0);
        return proceduralCroakClip;
    }

    private void ExecuteCroak()
    {
        Debug.Log("🐸 'ỘP! ỘP!' (Tiếng ếch kêu vang dội dọa mèo!)");

        if (audioSource != null)
        {
            audioSource.PlayOneShot(GetOrCreateCroakClip(), 1.0f);
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, croakScareRadius);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<CatEnemyAI>(out var cat))
            {
                cat.ScareAway(transform.position);
            }
        }

        if (HumanAlertManager.Instance != null)
        {
            HumanAlertManager.Instance.AddNoise(croakNoiseAlert, transform.position);
        }

        // Đã tắt hiệu ứng hình ảnh: StartCoroutine(CroakWavePulseRoutine());
    }

    private IEnumerator CroakWavePulseRoutine()
    {
        visualTransform.localScale = originalVisualScale * 1.35f;

        GameObject wave = new GameObject("Croak_SoundWave");
        wave.transform.position = transform.position;
        var rend = wave.AddComponent<SpriteRenderer>();
        rend.sprite = LivingRoomAutoBuilder.GetWhiteSprite();
        rend.color = new Color(0.3f, 1f, 0.4f, 0.7f);
        rend.sharedMaterial = LivingRoomAutoBuilder.GetSpriteMaterial();
        rend.sortingOrder = 10;

        GameObject textObj = new GameObject("Croak_FloatingText");
        textObj.transform.position = transform.position + Vector3.up * 0.8f;
        var tm = textObj.AddComponent<TextMesh>();
        tm.text = "📢 ỘP! ỘP!";
        tm.fontSize = 24;
        tm.characterSize = 0.08f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = Color.green;

        float duration = 0.42f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float s = Mathf.Lerp(0.8f, croakScareRadius * 1.6f, t);
            wave.transform.localScale = new Vector3(s, s, 1f);
            rend.color = new Color(0.3f, 1f, 0.4f, (1f - t) * 0.65f);

            textObj.transform.position += Vector3.up * (Time.deltaTime * 1.5f);
            tm.color = new Color(0.2f, 1f, 0.3f, 1f - t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(wave);
        Destroy(textObj);
        visualTransform.localScale = originalVisualScale;
    }

    #endregion

    #region ĐÀO ĐẤT TRỐN (BURROWING)

    public void SetCanBurrow(bool canBurrow, DiggableGround soil)
    {
        canBurrowHere = canBurrow;
        nearbySoil = soil;
    }

    private void StartBurrowing()
    {
        if (isBurrowAnimating) return;
        StartCoroutine(BurrowDownRoutine());
    }

    /// <summary>
    /// Độ sâu lặn = chiều cao collider vùng đất x hệ số, giới hạn trong [min, max]
    /// </summary>
    private float GetSoilDepth()
    {
        Collider2D selfCol = GetComponent<Collider2D>();
        if (selfCol != null)
        {
            float feetY = selfCol.bounds.min.y;
            Vector2 origin = new Vector2(selfCol.bounds.center.x, feetY + 0.05f);

            // Tìm lớp đất đặc ngay dưới chân (bỏ qua trigger và chính ếch)
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 2f);
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.isTrigger) continue;
                if (h.collider.gameObject == gameObject || h.collider.transform.IsChildOf(transform)) continue;

                // Độ sâu tối đa = từ chân ếch xuống đáy lớp đất (không chui xuyên mặt phẳng bên dưới)
                float soilThickness = feetY - h.collider.bounds.min.y - 0.02f;
                float depth = soilThickness * Mathf.Clamp01(burrowDepthMultiplier);
                return Mathf.Clamp(depth, 0.05f, Mathf.Max(0.05f, maxBurrowDepth));
            }
        }

        // Không tìm thấy lớp đất đặc: dùng chiều cao vùng đất, giới hạn nhẹ
        float fallback = 0.3f;
        if (nearbySoil != null)
        {
            Collider2D col = nearbySoil.GetComponent<Collider2D>();
            if (col != null) fallback = col.bounds.size.y;
        }
        return Mathf.Clamp(fallback * 0.5f, 0.05f, maxBurrowDepth);
    }

    private IEnumerator BurrowDownRoutine()
    {
        currentState = FrogState.Burrowed;
        isBurrowAnimating = true;
        horizontalInput = 0f;

        // Chuyển sang Kinematic để tự điều khiển vị trí và góc xoay
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.None;

        currentBurrowDepth = GetSoilDepth();
        SetBurrowSorting(true); // Cho ếch hiện phía sau lớp đất

        Vector2 startPos = rb.position;
        Vector2 endPos = startPos + Vector2.down * currentBurrowDepth;

        float elapsed = 0f;
        while (elapsed < burrowSpinDuration)
        {
            float t = Mathf.Clamp01(elapsed / burrowSpinDuration);
            float eased = t * t * (3f - 2f * t);
            rb.position = Vector2.Lerp(startPos, endPos, eased);
            // Xoáy ngang 360 độ (quanh trục dọc): bề rộng co lại rồi lật qua rồi trở về
            float spinX = Mathf.Cos(eased * 2f * Mathf.PI);
            visualTransform.localScale = new Vector3(originalVisualScale.x * spinX, originalVisualScale.y, originalVisualScale.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.position = endPos;
        visualTransform.localScale = originalVisualScale;
        isBurrowAnimating = false;
        Debug.Log($"[Burrow] Ếch đã lặn xuống {currentBurrowDepth:F2} tại {nearbySoil?.soilName}!");
    }

    private void HandleBurrowedInput()
    {
        if (!isBurrowAnimating && GameInput.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(BurrowUpRoutine());
        }
    }

    private IEnumerator BurrowUpRoutine()
    {
        isBurrowAnimating = true;

        Vector2 startPos = rb.position;
        Vector2 endPos = startPos + Vector2.up * currentBurrowDepth;
        float duration = burrowSpinDuration * 0.6f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            rb.position = Vector2.Lerp(startPos, endPos, eased);
            float spinX = Mathf.Cos(eased * 2f * Mathf.PI);
            visualTransform.localScale = new Vector3(originalVisualScale.x * spinX, originalVisualScale.y, originalVisualScale.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.position = endPos;
        visualTransform.localScale = originalVisualScale;
        RestoreFromBurrow();
        currentState = FrogState.Grounded;
        rb.linearVelocity = Vector2.up * minJumpForce;
        Debug.Log("[Burrow] Ếch nhảy vọt lên khỏi đất!");
    }

    /// <summary>
    /// Trả ếch về trạng thái vật lý bình thường sau khi đào đất
    /// </summary>
    private void RestoreFromBurrow()
    {
        rb.rotation = 0f;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = originalGravityScale;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.freezeRotation = true;
        SetBurrowSorting(false);
        isBurrowAnimating = false;
    }

    private void SetBurrowSorting(bool hidden)
    {
        if (hidden)
        {
            burrowRenderers = GetComponentsInChildren<SpriteRenderer>();
            burrowOriginalOrders = new int[burrowRenderers.Length];
            for (int i = 0; i < burrowRenderers.Length; i++)
            {
                burrowOriginalOrders[i] = burrowRenderers[i].sortingOrder;
                burrowRenderers[i].sortingOrder = -5;
            }
        }
        else if (burrowRenderers != null)
        {
            for (int i = 0; i < burrowRenderers.Length; i++)
            {
                if (burrowRenderers[i] != null) burrowRenderers[i].sortingOrder = burrowOriginalOrders[i];
            }
            burrowRenderers = null;
        }
    }

    #endregion

    #region LƯỠI BÁM DÍNH (GRAPPLE TONGUE)

    private void CreateTongueLine()
    {
        // Dùng object con riêng để không đụng tới các LineRenderer của script khác
        GameObject lineObj = new GameObject("Grapple_TongueLine");
        lineObj.transform.SetParent(transform, false);
        tongueLine = lineObj.AddComponent<LineRenderer>();
        tongueLine.useWorldSpace = true;
        tongueLine.positionCount = 2;
        tongueLine.startWidth = tongueWidth;
        tongueLine.endWidth = tongueWidth;
        tongueLine.startColor = tongueColor;
        tongueLine.endColor = tongueColor;
        tongueLine.numCapVertices = 4;
        tongueLine.sortingOrder = 9;
        Material mat = LivingRoomAutoBuilder.GetSpriteMaterial();
        if (mat != null) tongueLine.sharedMaterial = mat;
        tongueLine.enabled = false;
    }

    private void UpdateTongue()
    {
        if (Time.timeScale == 0f) return;

        // Đang chui đất thì không dùng lưỡi
        if (currentState == FrogState.Burrowed)
        {
            if (tongueState != TongueState.Idle) ReleaseTongue();
            return;
        }

        Vector2 mouth = (Vector2)transform.position + mouthOffset;
        bool pressed = Input.GetMouseButtonDown(tongueMouseButton);

        switch (tongueState)
        {
            case TongueState.Idle:
                if (pressed) FireTongue(mouth);
                break;
            case TongueState.Extending:
                UpdateTongueExtending(mouth);
                break;
            case TongueState.Pulling:
                UpdateTonguePulling(mouth, pressed);
                break;
            case TongueState.Swinging:
                UpdateTongueSwinging(mouth, pressed);
                break;
            case TongueState.Retracting:
                UpdateTongueRetracting(mouth);
                break;
        }

        // Chỉ khóa điều khiển đi bộ khi ếch đang lơ lửng trên dây
        ExternalControl = tongueState == TongueState.Swinging && !isGrounded;

        UpdateTongueLine(mouth);
    }

    private void FixedUpdateTongue()
    {
        if (tongueState == TongueState.Pulling && pullBody != null)
        {
            Vector2 toFrog = (Vector2)transform.position - pullBody.position;
            if (toFrog.sqrMagnitude > 0.0001f)
            {
                float speed = Mathf.Max(minPullSpeed, pullSpeedBase / (1f + pullSize * pullSizePenalty));
                pullBody.WakeUp();
                pullBody.linearVelocity = toFrog.normalized * speed;
            }
        }

        if (tongueState == TongueState.Swinging && ExternalControl)
        {
            rb.AddForce(Vector2.right * swingInput * swingForce, ForceMode2D.Force);
            if (rb.linearVelocity.magnitude > maxSwingSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSwingSpeed;
            }
        }
    }

    private void FireTongue(Vector2 mouth)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 mp = Input.mousePosition;
        mp.z = -cam.transform.position.z;
        Vector2 target = cam.ScreenToWorldPoint(mp);

        Vector2 aim = target - mouth;
        if (aim.sqrMagnitude < 0.01f) return;
        tongueDir = aim.normalized;

        // Quay mặt ếch về phía bắn lưỡi
        if (tongueDir.x > 0.05f)
        {
            facingDirection = 1;
            SetSpriteFacing(true);
        }
        else if (tongueDir.x < -0.05f)
        {
            facingDirection = -1;
            SetSpriteFacing(false);
        }

        tongueHasHit = FindTongueTarget(mouth, tongueDir, out float hitDist);
        tongueTargetDist = tongueHasHit ? hitDist : maxTongueLength;
        tongueTipDist = 0f;
        tongueState = TongueState.Extending;
    }

    /// <summary>
    /// Tìm điểm lưỡi chạm đầu tiên: ưu tiên collider đặc, đồng thời tính cả vật chỉ có hình
    /// (không có collider, hoặc có collider nhưng là Trigger / đang bị tắt).
    /// Lưỡi dính vào mọi thứ nó chạm được, trừ vùng trigger vô hình và chính ếch.
    /// </summary>
    private bool FindTongueTarget(Vector2 origin, Vector2 direction, out float hitDistance)
    {
        hitDistance = maxTongueLength;
        tongueHitCol = null;
        tongueHitTf = null;
        bool found = false;

        // 1. Các collider đặc
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, maxTongueLength);
        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;

            hitDistance = h.distance;
            tongueHitPoint = h.point;
            tongueHitCol = h.collider;
            tongueHitTf = h.collider.transform;
            found = true;
            break;
        }

        // 2. Vật chỉ có hình ảnh (SpriteRenderer) mà không có collider đặc đang bật: tính theo khung bao.
        //    (Cube có collider Trigger hoặc collider bị tắt cũng rơi vào nhóm này nên không bị xuyên nữa.)
        Ray ray = new Ray(new Vector3(origin.x, origin.y, 0f), new Vector3(direction.x, direction.y, 0f));
        var renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude);
        foreach (var sr in renderers)
        {
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            if (sr.color.a < 0.2f) continue;
            if (sr.transform.IsChildOf(transform)) continue;

            // Chỉ bỏ qua nếu đã có collider đặc đang bật (bước 1 đã xử lý rồi)
            Collider2D ownCol = sr.GetComponent<Collider2D>();
            if (ownCol != null && ownCol.enabled && !ownCol.isTrigger) continue;

            Bounds bounds = sr.bounds;
            bounds.center = new Vector3(bounds.center.x, bounds.center.y, 0f);
            bounds.extents = new Vector3(bounds.extents.x, bounds.extents.y, 1f);

            if (bounds.IntersectRay(ray, out float d) && d >= 0.01f && d < hitDistance && d <= maxTongueLength)
            {
                hitDistance = d;
                tongueHitPoint = origin + direction * d;
                tongueHitCol = null;
                tongueHitTf = sr.transform;
                found = true;
            }
        }

        return found;
    }

    private void UpdateTongueExtending(Vector2 mouth)
    {
        tongueTipDist += tongueSpeed * Time.deltaTime;
        if (tongueTipDist >= tongueTargetDist)
        {
            tongueTipDist = tongueTargetDist;
            if (tongueHasHit) OnTongueHit();
            else tongueState = TongueState.Retracting;
        }
        tongueDrawTip = mouth + tongueDir * tongueTipDist;
    }

    private void OnTongueHit()
    {
        Collider2D col = tongueHitCol;
        Rigidbody2D body = col != null ? col.attachedRigidbody : null;
        float size = col != null ? Mathf.Max(col.bounds.size.x, col.bounds.size.y) : 0f;

        // Vật động và đủ nhỏ -> kéo về phía ếch. Còn lại (mặt phẳng, vật to, con mèo...) -> làm điểm bám để đu
        bool pullable = col != null && body != null && body.bodyType == RigidbodyType2D.Dynamic
                        && size <= heavySize && col.GetComponentInParent<CatEnemyAI>() == null;

        if (pullable)
        {
            pullBody = body;
            pullCol = col;
            pullSize = size;
            pullTimer = 0f;
            tongueState = TongueState.Pulling;
        }
        else
        {
            StartSwing(tongueHitPoint, tongueHitTf);
        }
    }

    private void UpdateTonguePulling(Vector2 mouth, bool pressed)
    {
        if (pullBody == null || pullCol == null)
        {
            ReleaseTongue();
            return;
        }

        pullTimer += Time.deltaTime;
        Vector2 objPos = pullCol.bounds.center;
        tongueDrawTip = objPos;

        float stopDist = pullStopDistance + pullSize * 0.5f;
        float d = Vector2.Distance(mouth, objPos);

        if (d <= stopDist || pullTimer > pullTimeout || pressed)
        {
            BeginTongueRetract(mouth, objPos);
        }
    }

    private void StartSwing(Vector2 point, Transform surface)
    {
        if (surface == null)
        {
            tongueState = TongueState.Retracting;
            return;
        }

        anchorTf = surface;
        anchorLocal = anchorTf.InverseTransformPoint(point);

        ropeLength = Mathf.Clamp(Vector2.Distance(transform.position, point), minRopeLength, maxTongueLength);
        ropeTarget = ropeLength;

        if (ropeJoint != null) Destroy(ropeJoint);
        ropeJoint = gameObject.AddComponent<DistanceJoint2D>();
        ropeJoint.autoConfigureConnectedAnchor = false;
        ropeJoint.autoConfigureDistance = false;
        ropeJoint.anchor = Vector2.zero;
        ropeJoint.connectedAnchor = point;
        ropeJoint.distance = ropeLength;
        ropeJoint.maxDistanceOnly = true; // Dây chỉ giới hạn khoảng cách tối đa, không đẩy ếch ra
        ropeJoint.enableCollision = true;

        tongueState = TongueState.Swinging;
    }

    private void UpdateTongueSwinging(Vector2 mouth, bool pressed)
    {
        if (anchorTf == null)
        {
            ReleaseTongue();
            return;
        }

        Vector2 anchor = anchorTf.TransformPoint(anchorLocal);

        if (pressed || Input.GetKeyDown(tongueReleaseKey))
        {
            BeginTongueRetract(mouth, anchor);
            return;
        }

        // Lăn chuột: lên = thu ngắn, xuống = nới dài
        float scroll = Input.mouseScrollDelta.y;
        if (invertScroll) scroll = -scroll;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            ropeTarget = Mathf.Clamp(ropeTarget - scroll * scrollStep, minRopeLength, maxTongueLength);
        }

        // Tời dây từ từ về độ dài mong muốn để ếch được kéo lên mượt, không bị giật
        ropeLength = Mathf.MoveTowards(ropeLength, ropeTarget, ropeReelSpeed * Time.deltaTime);

        if (ropeJoint != null)
        {
            ropeJoint.distance = ropeLength;
            ropeJoint.connectedAnchor = anchor;
        }

        swingInput = GameInput.GetHorizontalAxis();
        tongueDrawTip = anchor;
    }

    private void BeginTongueRetract(Vector2 mouth, Vector2 fromPoint)
    {
        Vector2 toTip = fromPoint - mouth;
        tongueDir = toTip.sqrMagnitude > 0.0001f ? toTip.normalized : Vector2.right;
        tongueTipDist = toTip.magnitude;

        if (ropeJoint != null) Destroy(ropeJoint);
        ropeJoint = null;
        pullBody = null;
        pullCol = null;
        anchorTf = null;
        ExternalControl = false;

        tongueState = TongueState.Retracting;
    }

    private void UpdateTongueRetracting(Vector2 mouth)
    {
        tongueTipDist -= tongueRetractSpeed * Time.deltaTime;
        if (tongueTipDist <= 0f)
        {
            ReleaseTongue();
            return;
        }
        tongueDrawTip = mouth + tongueDir * tongueTipDist;
    }

    /// <summary>
    /// Nhả mọi thứ ngay lập tức (gọi khi ếch bị đặt lại vào hộp, chui đất...)
    /// </summary>
    public void ReleaseTongue()
    {
        if (ropeJoint != null) Destroy(ropeJoint);
        ropeJoint = null;
        pullBody = null;
        pullCol = null;
        anchorTf = null;
        tongueState = TongueState.Idle;
        ExternalControl = false;

        if (tongueLine != null) tongueLine.enabled = false;
    }

    private void UpdateTongueLine(Vector2 mouth)
    {
        if (tongueLine == null) return;

        if (tongueState == TongueState.Idle)
        {
            tongueLine.enabled = false;
            return;
        }

        tongueLine.enabled = true;
        tongueLine.SetPosition(0, mouth);
        tongueLine.SetPosition(1, tongueDrawTip);
    }

    #endregion

    #region KNOCKBACK & DEATH

    public void ApplyKnockback(Vector2 force)
    {
        currentState = FrogState.Bounced;
        rb.gravityScale = originalGravityScale;
        rb.linearVelocity = force;
    }

    public void TriggerSquashDeath()
    {
        StartCoroutine(SquashDeathRoutine());
    }

    private IEnumerator SquashDeathRoutine()
    {
        currentState = FrogState.Splat;
        rb.linearVelocity = Vector2.zero;
        visualTransform.localScale = new Vector3(originalVisualScale.x * 2.2f, originalVisualScale.y * 0.15f, originalVisualScale.z);

        yield return new WaitForSeconds(2.0f);

        transform.position = new Vector3(-8.0f, 0.5f, 0f);
        visualTransform.localScale = originalVisualScale;
        currentState = FrogState.Grounded;
    }

    /// <summary>
    /// Bị người nuôi bế đặt lại vào đúng chiếc hộp ở dưới sàn nhà
    /// </summary>
    public void ResetToStartingBox(Vector3 boxPosition)
    {
        StopAllCoroutines();
        RestoreFromBurrow(); // Tránh kẹt ở trạng thái Kinematic nếu đang đào đất

        ReleaseTongue(); // Nhả lưỡi trước khi dịch chuyển

        if (boxPosition == Vector3.zero || boxPosition.sqrMagnitude < 0.05f)
        {
            GameObject petBox = GameObject.Find("Pet_Terrarium_Box");
            if (petBox != null)
            {
                boxPosition = new Vector3(petBox.transform.position.x, 0.6f, 0f);
            }
            else if (InitialSpawnPosition.sqrMagnitude > 0.05f)
            {
                boxPosition = InitialSpawnPosition;
            }
            else
            {
                boxPosition = new Vector3(-8.0f, 0.6f, 0f);
            }
        }

        transform.position = boxPosition;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = originalGravityScale;
        visualTransform.localScale = originalVisualScale;
        visualTransform.localPosition = Vector3.zero;
        currentState = FrogState.Grounded;

        Camera cam = Camera.main;
        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollow2D>();
            if (follow != null) follow.SnapToTarget();
        }

        Debug.Log($"📦 Chú ếch đã được người nuôi đặt lại vào đúng chiếc hộp dưới sàn tại tọa độ {boxPosition}!");
    }

    public void PulseVisualWhenFed()
    {
        StartCoroutine(FedPulseRoutine());
    }

    private IEnumerator FedPulseRoutine()
    {
        visualTransform.localScale = originalVisualScale * 1.3f;
        yield return new WaitForSeconds(0.2f);
        visualTransform.localScale = originalVisualScale;
    }

    #endregion

    #region VA CHẠM TƯỜNG & KIỂM TRA MẶT ĐẤT

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Chế độ Mario Movement: Người chơi lướt chạm tường mượt mà, không bị văng nảy khóa lái kiểu Jump King
    }

    private void CheckGrounded()
    {
        Vector2 checkPos = groundCheckPoint != null ? (Vector2)groundCheckPoint.position : (Vector2)transform.position + Vector2.down * 0.45f;
        Collider2D[] hits = Physics2D.OverlapBoxAll(checkPos, groundCheckSize, 0f);
        isGrounded = false;
        foreach (var hit in hits)
        {
            if (hit != null && !hit.isTrigger && hit.gameObject != gameObject && !hit.transform.IsChildOf(transform))
            {
                isGrounded = true;
                break;
            }
        }
    }

    // Các bộ phận con (mắt, con ngươi) cần lật vị trí khi ếch đổi hướng
    private Transform[] facingParts;
    private Vector3[] facingPartsRightPos;

    private void CacheFacingParts()
    {
        var list = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Eye_") || child.name.StartsWith("Pupil_"))
            {
                list.Add(child);
            }
        }

        facingParts = list.ToArray();
        facingPartsRightPos = new Vector3[facingParts.Length];
        for (int i = 0; i < facingParts.Length; i++)
        {
            // Vị trí gốc được dựng sẵn theo hướng nhìn sang PHẢI
            facingPartsRightPos[i] = facingParts[i].localPosition;
        }
    }

    private void SetSpriteFacing(bool faceRight)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = !faceRight;
        }

        if (facingParts == null || facingParts.Length == 0) CacheFacingParts();

        for (int i = 0; i < facingParts.Length; i++)
        {
            if (facingParts[i] == null) continue;
            Vector3 p = facingPartsRightPos[i];
            facingParts[i].localPosition = new Vector3(faceRight ? p.x : -p.x, p.y, p.z);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector2 checkPos = groundCheckPoint != null ? (Vector2)groundCheckPoint.position : (Vector2)transform.position + Vector2.down * 0.5f;
        Gizmos.DrawWireCube(checkPos, groundCheckSize);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, croakScareRadius);
    }

    #endregion
}