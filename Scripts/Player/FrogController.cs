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

    [Header("=== Trớn (Momentum) sau khi đu dây / rơi ===")]
    [Tooltip("Bật: khi ếch đang mang đà lớn (vd: vừa nhả dây đu) thì giữ trớn đó thêm một chút, kể cả sau khi chạm đất. Đi bộ bình thường KHÔNG bị ảnh hưởng")]
    public bool useMomentum = true;
    [Tooltip("Tốc độ ngang vượt walkSpeed x hệ số này thì tính là có trớn")]
    public float momentumThreshold = 1.05f;
    [Tooltip("Lực cản trớn khi đang ở trên không (nhỏ = bay đà xa hơn)")]
    public float momentumAirDrag = 3f;
    [Tooltip("Ma sát làm trớn tắt dần khi đã chạm đất và người chơi không bấm phím (nhỏ = trượt xa hơn)")]
    public float momentumGroundFriction = 14f;
    [Tooltip("Gia tốc người chơi tác động lên ếch khi đang có trớn (nhỏ = bị trớn cản nhiều hơn, khó đổi hướng)")]
    public float momentumControlAccel = 14f;

    [Header("=== Điều khiển kiểu Mario - Nhảy ===")]
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

    [Header("=== Rơi từ trên cao & Máu (ếch Pacman rất dễ chấn thương) ===")]
    public bool enableFallDamage = true;
    [Tooltip("Rơi từ độ cao không quá mức này thì an toàn (cỡ bàn / ghế / sofa xuống sàn). Đơn vị Unity")]
    [System.NonSerialized] public float safeFallHeight = 8.0f; // NonSerialized: luôn dùng giá trị trong code, Inspector không ghi đè
    [Tooltip("Làm tròn độ cao rơi theo bước này (đơn vị Unity) để cùng một địa hình luôn ra cùng sát thương. 0 = không làm tròn")]
    public float fallHeightRounding = 0.5f;
    [Tooltip("Rơi từ độ cao bằng hoặc hơn mức này là chết (đơn vị Unity)")]
    [System.NonSerialized] public float lethalFallHeight = 14.0f; // NonSerialized: luôn dùng giá trị trong code, Inspector không ghi đè
    [Tooltip("Sát thương tối thiểu ngay khi vượt mức an toàn")]
    [System.NonSerialized] public float minFallDamage = 20f; // NonSerialized: luôn dùng giá trị trong code
    [Tooltip("Độ cong của đường tăng sát thương: >1 thì rơi vừa phải đỡ đau, rơi rất cao mới đau nặng")]
    [System.NonSerialized] public float fallDamageCurve = 1.0f; // NonSerialized: luôn dùng giá trị trong code
    [Tooltip("Ếch càng mập (nặng) càng đau: cộng thêm tối đa bấy nhiêu % sát thương khi ếch nặng nhất")]
    public float weightDamageBonus = 0.35f;
    [Tooltip("Sát thương từ mức này trở lên thì ếch bẹp dí choáng một lúc (Splat)")]
    public float heavyFallSplatDamage = 25f;
    [Tooltip("Thời gian nằm bẹp trước khi hồi sinh trong hộp khi ếch chết (giây)")]
    public float deathRespawnDelay = 2.0f;
    [Tooltip("Lượng máu hồi lại mỗi lần ăn một con ruồi")]
    [System.NonSerialized] public float flyHealAmount = 25f; // NonSerialized: luôn dùng giá trị trong code
    [Tooltip("Mỗi con ruồi ăn được làm sát thương khi té tăng thêm bấy nhiêu (0.2 = +20%/con, cộng dồn). Reset khi bị bắt hoặc chết")]
    [System.NonSerialized] public float flyFallDamageBonus = 0.2f; // NonSerialized: luôn dùng giá trị trong code

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

    [Header("=== Lưỡi bám dính - Va chạm ===")]
    [Tooltip("Bán kính 'độ dày' của lưỡi khi quét va chạm - lớn hơn thì khó xuyên qua cạnh/góc vật thể hơn")]
    public float tongueRadius = 0.08f;
    [Tooltip("Tự tắt script FrogTongue cũ (nếu có) vì nó chạy song song và gây xung đột với lưỡi mới")]
    public bool disableLegacyTongue = true;

    [Header("=== Lưỡi bám dính - Bắt ruồi ===")]
    [Tooltip("Tốc độ con ruồi bị lưỡi kéo về miệng ếch")]
    public float flyPullSpeed = 18f;

    [Header("=== Lưỡi bám dính - Kéo ếch tới điểm bám ===")]
    [Tooltip("Lưỡi dính vào vật to/cố định/không kéo được thì ếch bị kéo tới chỗ lưỡi bám")]
    public bool autoReelToTarget = true;
    [Tooltip("Tốc độ rút dây (đơn vị/giây) khi ếch bị kéo về phía điểm lưỡi bám")]
    public float frogPullSpeed = 16f;
    [Tooltip("Dây được rút ngắn tới độ dài này rồi dừng (cũng là độ dài xích đu). Lớn = đu rộng hơn")]
    public float reelStopDistance = 2.0f;

    [Header("=== Lưỡi bám dính - Đu dây ===")]
    public float minRopeLength = 0.8f;
    [Tooltip("Mỗi nấc lăn chuột đổi độ dài dây bao nhiêu")]
    public float scrollStep = 0.6f;
    [Tooltip("Tốc độ tời dây (đơn vị/giây) khi lăn chuột để thu ngắn hoặc nới dài")]
    public float ropeReelSpeed = 8f;
    [Tooltip("Lực đẩy khi giữ A/D: đẩy theo hướng tiếp tuyến của dây giống như nhún xích đu để lấy đà")]
    public float swingForce = 35f;
    [Tooltip("Trọng lực của ếch lúc đang đu (nhỏ hơn bình thường để xích đu bay bổng, chậm rãi hơn)")]
    public float swingGravityScale = 2.5f;
    [Tooltip("Lực cản không khí khi đu: 0 = đu mãi không tắt, lớn = nhanh tắt đà")]
    public float swingDamping = 0.12f;
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
    private FrogHealth health;
    private int fliesEaten = 0; // Số ruồi đã ăn kể từ lần bị bắt / chết gần nhất (mỗi con làm té đau thêm)
    public int FliesEaten => fliesEaten;
    private float deadTimer = 0f; // Đếm thời gian nằm chết: quá hạn mà chưa hồi sinh thì tự hồi sinh (chống kẹt)
    private float airPeakY = 0f;      // Độ cao cao nhất kể từ lúc rời mặt đất (để tính độ cao rơi)
    private bool wasGroundedFall = false;
    private bool pullIsFly = false;
    private float lastFlyDist = 99f;
    public bool IsDead => health != null && health.IsDead;
    public FrogHealth Health => health;
    private bool hasMomentum = false; // Đang mang trớn sau khi đu dây / rơi
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
    private Vector2 tongueHitPoint;
    private SpriteRenderer tongueHitSprite;
    private readonly System.Collections.Generic.List<SpriteRenderer> spriteTargets = new System.Collections.Generic.List<SpriteRenderer>();
    private Transform pullTf;        // Con ruồi (không có Rigidbody) được kéo bằng transform
    private Vector3 pullPos;         // Vị trí do ếch tự tính để ruồi không bị script bay lượn của nó kéo ngược lại
    private bool reeling;            // Ếch đang bị kéo tới điểm bám
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

        health = GetComponent<FrogHealth>();
        if (health == null) health = gameObject.AddComponent<FrogHealth>();
        airPeakY = transform.position.y;

        if (disableLegacyTongue)
        {
            var legacy = GetComponent<FrogTongue>();
            if (legacy != null)
            {
                if (legacy.TryGetComponent<LineRenderer>(out var legacyLine)) legacyLine.enabled = false;
                legacy.enabled = false;
            }
        }

        CreateTongueLine();
    }

    private void OnDisable()
    {
        ReleaseTongue();
    }

    private void Update()
    {
        CheckGrounded();
        TrackFall();

        // Ếch đã chết (đang nằm bẹp chờ hồi sinh): không nhận điều khiển
        if (IsDead)
        {
            if (tongueState != TongueState.Idle) ReleaseTongue();
            horizontalInput = 0f;

            // Phòng hờ: nếu coroutine hồi sinh bị dừng giữa chừng (vd người nuôi bế ếch làm StopAllCoroutines)
            // thì tự hồi sinh sau khi quá hạn, tránh ếch nằm chết mãi không di chuyển được
            deadTimer += Time.deltaTime;
            if (deadTimer > deathRespawnDelay + 1.0f)
            {
                deadTimer = 0f;
                if (health != null) health.ResetFull();
                ResetToStartingBox(Vector3.zero);
            }
            return;
        }
        deadTimer = 0f;

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
            float vx = rb.linearVelocity.x;
            float targetVx = horizontalInput * walkSpeed;

            // Bắt đầu có trớn khi tốc độ ngang vượt tốc độ đi bộ (vd: vừa nhả dây đu)
            if (useMomentum && !hasMomentum && Mathf.Abs(vx) > walkSpeed * momentumThreshold)
            {
                hasMomentum = true;
            }

            if (hasMomentum)
            {
                // Hết trớn khi vận tốc đã về sát vận tốc người chơi muốn
                if (Mathf.Abs(vx - targetVx) <= 0.25f)
                {
                    hasMomentum = false;
                    vx = targetVx;
                }
                else
                {
                    bool hasInput = Mathf.Abs(horizontalInput) > 0.05f;
                    float rate;
                    if (hasInput) rate = momentumControlAccel;                                        // Người chơi vẫn điều khiển được nhưng bị trớn cản
                    else rate = isGrounded ? momentumGroundFriction : momentumAirDrag;                // Không bấm phím: trớn tắt dần
                    vx = Mathf.MoveTowards(vx, targetVx, rate * Time.fixedDeltaTime);
                }
            }
            else
            {
                vx = targetVx; // Đi bộ / điều khiển bình thường: phản hồi tức thì như cũ
            }

            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
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

        // Khóa điều khiển đi bộ suốt lúc ếch đang bị lưỡi kéo / đu dây (cả khi đang đứng trên đất),
        // nếu không FixedUpdate sẽ ghi đè vận tốc ngang và ếch không bị kéo đi được.
        ExternalControl = tongueState == TongueState.Swinging;

        UpdateTongueLine(mouth);
    }

    private void LateUpdate()
    {
        // Kéo con ruồi về miệng ếch (đặt ở LateUpdate để thắng script bay lượn FlyItem)
        if (tongueState == TongueState.Pulling && pullBody == null && pullTf != null)
        {
            Vector2 mouth = (Vector2)transform.position + mouthOffset;
            pullPos = Vector3.MoveTowards(pullPos, new Vector3(mouth.x, mouth.y, pullPos.z), flyPullSpeed * Time.deltaTime);
            pullTf.position = pullPos;
        }
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

        if (tongueState == TongueState.Swinging && anchorTf != null)
        {
            // Vật lý con lắc do DistanceJoint2D + trọng lực lo, ở đây chỉ thêm lực "nhún" của người chơi và lực cản nhẹ
            Vector2 anchor = anchorTf.TransformPoint(anchorLocal);
            Vector2 toAnchor = anchor - rb.position;
            float dist = toAnchor.magnitude;

            if (Mathf.Abs(swingInput) > 0.05f && dist > 0.1f)
            {
                // Hướng tiếp tuyến (vuông góc với dây) cùng phía phím đang bấm -> đẩy như đẩy xích đu
                Vector2 tangent = new Vector2(-toAnchor.y, toAnchor.x) / dist;
                if (Mathf.Abs(tangent.x) < 0.25f) tangent = Vector2.right * Mathf.Sign(swingInput); // dây nằm ngang: đẩy ngang
                else if (tangent.x * swingInput < 0f) tangent = -tangent;

                rb.AddForce(tangent * (Mathf.Abs(swingInput) * swingForce), ForceMode2D.Force);
            }

            if (swingDamping > 0f)
            {
                rb.linearVelocity *= Mathf.Clamp01(1f - swingDamping * Time.fixedDeltaTime);
            }

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

        CacheSpriteTargets();
        tongueTipDist = 0f;
        tongueDrawTip = mouth;
        tongueState = TongueState.Extending;
    }

    /// <summary>
    /// Gom các vật CHỈ có hình (SpriteRenderer) mà không có collider đặc đang bật
    /// (vd: con ruồi là Trigger, cánh cửa đang tắt collider...). Chỉ gom 1 lần mỗi lần bắn,
    /// còn khung bao của chúng được đọc lại mỗi khung hình nên vật đang di chuyển vẫn bị bắt trúng.
    /// </summary>
    private void CacheSpriteTargets()
    {
        spriteTargets.Clear();
        var renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var sr in renderers)
        {
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            if (sr.color.a < 0.2f) continue;
            if (sr.transform.IsChildOf(transform)) continue;
            if (sr.GetComponentInParent<Canvas>() != null) continue;

            // Đã có collider đặc đang bật (ở chính nó hoặc cha) thì phần quét collider lo rồi
            bool coveredBySolid = false;
            foreach (var c in sr.GetComponentsInParent<Collider2D>())
            {
                if (c.enabled && !c.isTrigger) { coveredBySolid = true; break; }
            }
            if (coveredBySolid) continue;

            spriteTargets.Add(sr);
        }
    }

    /// <summary>
    /// Quét lưỡi từ miệng (vị trí HIỆN TẠI) dọc theo hướng bắn tới độ dài 'reach'.
    /// Quét cả đoạn lưỡi đã thò ra (không chỉ phần mới) và dùng CircleCast có độ dày, nên:
    /// vật di chuyển cắt ngang lưỡi, hay cạnh/góc mỏng, đều không bị xuyên qua nữa.
    /// </summary>
    private bool SweepTongue(Vector2 mouth, Vector2 dir, float reach, out float hitDistance)
    {
        hitDistance = reach;
        tongueHitCol = null;
        tongueHitTf = null;
        tongueHitSprite = null;
        bool found = false;

        // 1. Collider đặc
        RaycastHit2D[] hits = Physics2D.CircleCastAll(mouth, tongueRadius, dir, reach);
        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;

            // Nếu lưỡi bắt đầu nằm sẵn trong collider thì point không đáng tin -> coi như dính ngay tại miệng
            Vector2 p = h.distance <= 0.0001f ? mouth : h.point;
            float d = Mathf.Clamp(Vector2.Dot(p - mouth, dir), 0f, reach);

            hitDistance = d;
            tongueHitPoint = mouth + dir * d;
            tongueHitCol = h.collider;
            tongueHitTf = h.collider.transform;
            found = true;
            break;
        }

        // 2. Vật chỉ có hình (không có collider đặc)
        Ray ray = new Ray(new Vector3(mouth.x, mouth.y, 0f), new Vector3(dir.x, dir.y, 0f));
        foreach (var sr in spriteTargets)
        {
            if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy) continue;

            Bounds bounds = sr.bounds;
            bounds.center = new Vector3(bounds.center.x, bounds.center.y, 0f);
            bounds.extents = new Vector3(bounds.extents.x + tongueRadius, bounds.extents.y + tongueRadius, 1f);

            if (bounds.IntersectRay(ray, out float d) && d <= reach && d < hitDistance + 0.0001f && (!found || d < hitDistance))
            {
                hitDistance = Mathf.Max(0f, d);
                tongueHitPoint = mouth + dir * hitDistance;
                tongueHitCol = null; // Giống code gốc: vật chỉ có hình không có collider đặc -> làm điểm bám
                tongueHitTf = sr.transform;
                tongueHitSprite = sr;
                found = true;
            }
        }

        return found;
    }

    private void UpdateTongueExtending(Vector2 mouth)
    {
        float reach = Mathf.Min(tongueTipDist + tongueSpeed * Time.deltaTime, maxTongueLength);

        if (SweepTongue(mouth, tongueDir, reach, out float hitDist))
        {
            tongueTipDist = hitDist;
            tongueDrawTip = mouth + tongueDir * hitDist;
            OnTongueHit();
            return;
        }

        tongueTipDist = reach;
        tongueDrawTip = mouth + tongueDir * reach;
        if (reach >= maxTongueLength) tongueState = TongueState.Retracting;
    }

    private void OnTongueHit()
    {
        Collider2D col = tongueHitCol;
        Rigidbody2D body = (col != null && !col.isTrigger) ? col.attachedRigidbody : null;

        float size;
        if (col != null) size = Mathf.Max(col.bounds.size.x, col.bounds.size.y);
        else if (tongueHitSprite != null) size = Mathf.Max(tongueHitSprite.bounds.size.x, tongueHitSprite.bounds.size.y);
        else size = 0f;

        bool isCat = tongueHitTf != null && tongueHitTf.GetComponentInParent<CatEnemyAI>() != null;
        bool isLight = size <= heavySize && !isCat;

        // (a) Vật động có Rigidbody, đủ nhẹ -> kéo vật về phía ếch
        bool pullableBody = isLight && col != null && !col.isTrigger && body != null
                            && body.bodyType == RigidbodyType2D.Dynamic;

        FlyItem fly = tongueHitTf != null ? tongueHitTf.GetComponentInParent<FlyItem>() : null;

        if (fly != null)
        {
            // Bắn lưỡi trúng con ruồi: kéo RUỒI về miệng ếch (ếch không bị kéo / không đu)
            pullBody = null;
            pullCol = null;
            pullTf = fly.transform;
            pullIsFly = true;
            lastFlyDist = 99f;
            pullPos = pullTf.position;
            pullSize = 0f;
            pullTimer = 0f;
            tongueState = TongueState.Pulling;
        }
        else if (isCat)
        {
            // Bắn lưỡi vào mèo: CHỈ làm mèo chú ý tới ếch (nếu chưa chú ý), không kéo ếch / không kéo mèo.
            CatEnemyAI cat = tongueHitTf.GetComponentInParent<CatEnemyAI>();
            if (cat != null) cat.AlertByTongue(transform.position);
            BeginTongueRetract((Vector2)transform.position + mouthOffset, tongueHitPoint);
        }
        else if (pullableBody)
        {
            pullBody = body;
            pullCol = col;
            pullSize = size;
            pullTimer = 0f;
            tongueState = TongueState.Pulling;
        }
        else
        {
            // Còn lại (tường, bàn, vật to, vật tĩnh, con ruồi...) -> ếch bị KÉO TỚI điểm lưỡi bám
            StartSwing(tongueHitPoint, tongueHitTf);
        }
    }

    private void UpdateTonguePulling(Vector2 mouth, bool pressed)
    {
        bool bodyMode = pullBody != null && pullCol != null;
        bool flyMode = pullBody == null && pullTf != null;
        if (!bodyMode && !flyMode)
        {
            // Con ruồi biến mất ngay sát miệng (script của ruồi tự "ăn" khi chạm ếch) cũng tính là đã ăn
            if (pullIsFly && lastFlyDist < 1.0f) OnAteFly();
            ReleaseTongue();
            return;
        }

        pullTimer += Time.deltaTime;
        Vector2 objPos = bodyMode ? (Vector2)pullCol.bounds.center : (Vector2)pullTf.position;
        tongueDrawTip = objPos;

        // Ruồi phải được kéo vào tận miệng (chồng lên thân ếch) mới thu lưỡi
        float stopDist = flyMode ? 0.2f : pullStopDistance + pullSize * 0.5f;
        float d = Vector2.Distance(mouth, objPos);

        if (flyMode) lastFlyDist = d;

        if (flyMode)
        {
            // Ruồi tới miệng (hoặc script khác đã ẩn nó đi do bị ăn) -> ĂN: xóa hẳn con ruồi, không cho hiện lại
            if (d <= stopDist || !pullTf.gameObject.activeInHierarchy)
            {
                EatFly(pullTf, mouth);
                return;
            }
        }

        if (d <= stopDist || pullTimer > pullTimeout || pressed)
        {
            BeginTongueRetract(mouth, objPos);
        }
    }

    /// <summary>
    /// Ăn con ruồi: xóa hẳn khỏi màn chơi (nên không thể bay lượn / hiện lại) rồi thu lưỡi về.
    /// </summary>
    private void EatFly(Transform fly, Vector2 mouth)
    {
        Vector2 flyPos = fly != null ? (Vector2)fly.position : mouth;
        if (fly != null)
        {
            Debug.Log($"🐸😋 Ếch đã ăn con ruồi {fly.name}!");
            Destroy(fly.gameObject);
            OnAteFly();
        }
        BeginTongueRetract(mouth, flyPos);
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

        float dist = Vector2.Distance(transform.position, point);
        ropeLength = Mathf.Clamp(dist, minRopeLength, maxTongueLength);

        // Mặc định: kéo ếch lại gần điểm bám rồi mới treo/đu
        float stop = Mathf.Max(minRopeLength, reelStopDistance);
        if (autoReelToTarget && ropeLength > stop + 0.1f)
        {
            ropeTarget = stop;
            reeling = true;
        }
        else
        {
            ropeTarget = ropeLength;
            reeling = false;
        }

        if (ropeJoint != null) Destroy(ropeJoint);
        ropeJoint = gameObject.AddComponent<DistanceJoint2D>();
        ropeJoint.autoConfigureConnectedAnchor = false;
        ropeJoint.autoConfigureDistance = false;
        ropeJoint.anchor = Vector2.zero;
        ropeJoint.connectedAnchor = point;
        ropeJoint.distance = ropeLength;
        ropeJoint.maxDistanceOnly = true; // Dây chỉ giới hạn khoảng cách tối đa, không đẩy ếch ra
        ropeJoint.enableCollision = true;

        rb.gravityScale = swingGravityScale; // Trọng lực nhẹ hơn khi đu cho cảm giác xích đu
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

        // Lăn chuột: lên = thu ngắn dây, xuống = nới dài
        float scroll = Input.mouseScrollDelta.y;
        if (invertScroll) scroll = -scroll;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            ropeTarget = Mathf.Clamp(ropeTarget - scroll * scrollStep, minRopeLength, maxTongueLength);
        }

        // Tời dây từ từ về độ dài mong muốn. Lúc mới bắn trúng thì rút nhanh (kéo ếch lại gần) nhưng
        // ếch vẫn bị kéo bằng chính sợi dây nên giữ nguyên đà, không bị ghi đè vận tốc.
        float reelSpeed = reeling ? frogPullSpeed : ropeReelSpeed;
        ropeLength = Mathf.MoveTowards(ropeLength, ropeTarget, reelSpeed * Time.deltaTime);
        if (reeling && Mathf.Approximately(ropeLength, ropeTarget)) reeling = false;

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
        pullTf = null;
        pullIsFly = false;
        anchorTf = null;
        reeling = false;
        ExternalControl = false;
        RestoreGravityAfterSwing();

        tongueState = TongueState.Retracting;
    }

    private void RestoreGravityAfterSwing()
    {
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.gravityScale = originalGravityScale;
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
        pullTf = null;
        pullIsFly = false;
        anchorTf = null;
        reeling = false;
        tongueState = TongueState.Idle;
        ExternalControl = false;
        RestoreGravityAfterSwing();

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

    #region SÁT THƯƠNG KHI RƠI & MÁU

    /// <summary>
    /// Theo dõi độ cao rơi: ghi lại điểm cao nhất kể từ lúc rời đất, tới lúc chạm đất thì tính độ cao rơi.
    /// Đang treo trên dây thì dây đỡ ếch nên độ cao được tính lại từ chỗ đó.
    /// </summary>
    private void TrackFall()
    {
        float y = transform.position.y;

        if (currentState == FrogState.Burrowed || tongueState == TongueState.Swinging || IsDead)
        {
            airPeakY = y;
            wasGroundedFall = isGrounded;
            return;
        }

        if (isGrounded)
        {
            if (!wasGroundedFall)
            {
                float fallHeight = airPeakY - y;
                if (fallHeight > safeFallHeight) ApplyFallDamage(fallHeight);
            }
            airPeakY = y;
        }
        // Không cập nhật airPeakY khi đang trên không: độ cao rơi tính từ chỗ ếch rời mặt đất (mép bàn...),
        // nên nhảy lên cao hay thấp trước khi rơi cũng không làm thay đổi sát thương

        wasGroundedFall = isGrounded;
    }

    /// <summary>
    /// Sát thương theo độ cao rơi: dưới safeFallHeight không mất máu; từ đó tăng dần tới lethalFallHeight là chết.
    /// Ếch càng mập càng đau.
    /// </summary>
    private void ApplyFallDamage(float fallHeight)
    {
        if (!enableFallDamage || health == null || health.IsDead) return;

        if (fallHeightRounding > 0f) fallHeight = Mathf.Round(fallHeight / fallHeightRounding) * fallHeightRounding;

        float t = Mathf.InverseLerp(safeFallHeight, Mathf.Max(lethalFallHeight, safeFallHeight + 0.01f), fallHeight);
        float damage = Mathf.Lerp(minFallDamage, health.maxHealth, Mathf.Pow(t, Mathf.Max(0.1f, fallDamageCurve)));

        float weight01 = Mathf.Clamp01((CurrentWeight - 1f) / 1.2f);
        damage *= 1f + weightDamageBonus * weight01;

        // Ăn càng nhiều ruồi thì té càng đau (cộng dồn tới khi bị bắt hoặc chết)
        damage *= 1f + flyFallDamageBonus * fliesEaten;

        if (fallHeight >= lethalFallHeight) damage = health.maxHealth;

        Debug.Log($"🐸💥 Ếch rơi từ độ cao {fallHeight:F1} (đã ăn {fliesEaten} ruồi, x{1f + flyFallDamageBonus * fliesEaten:F1}) -> sát thương {damage:F0}");

        bool died = health.TakeDamage(damage, $"rơi từ độ cao {fallHeight:F1}");

        // Rơi mạnh tạo tiếng động làm người nuôi chú ý
        if (HumanAlertManager.Instance != null)
        {
            HumanAlertManager.Instance.AddNoise(Mathf.Lerp(5f, 20f, t), transform.position);
        }

        if (died)
        {
            StartCoroutine(FallDeathRoutine());
        }
        else if (damage >= heavyFallSplatDamage)
        {
            StartCoroutine(SplatRoutine());
        }
        else
        {
            // Đau nhẹ: chỉ bẹp xuống một thoáng
            visualTransform.localScale = new Vector3(originalVisualScale.x * 1.25f, originalVisualScale.y * 0.7f, originalVisualScale.z);
            StartCoroutine(ResetVisualScaleRoutine());
        }
    }

    private IEnumerator FallDeathRoutine()
    {
        currentState = FrogState.Splat;
        ReleaseTongue();
        horizontalInput = 0f;
        rb.linearVelocity = Vector2.zero;
        visualTransform.localScale = new Vector3(originalVisualScale.x * 2.0f, originalVisualScale.y * 0.15f, originalVisualScale.z);
        Debug.LogWarning("💀 Ếch rơi từ quá cao và gục ngã! Sẽ được người nuôi đặt lại vào hộp.");

        yield return new WaitForSeconds(deathRespawnDelay);

        if (health != null) health.ResetFull();
        ResetToStartingBox(Vector3.zero);
    }

    /// <summary>
    /// Ếch vừa ăn một con ruồi: hồi một ít máu. Các script khác (nhặt ruồi bằng phím E, ruồi chạm trigger...) cũng có thể gọi hàm này.
    /// </summary>
    public void OnAteFly()
    {
        fliesEaten++;
        if (health != null) health.Heal(flyHealAmount);
        PulseVisualWhenFed();
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
        fliesEaten = 0; // Chết do bị đè -> reset
    }

    /// <summary>
    /// Bị người nuôi bế đặt lại vào đúng chiếc hộp ở dưới sàn nhà
    /// </summary>
    public void ResetToStartingBox(Vector3 boxPosition)
    {
        StopAllCoroutines();

        // Nếu ếch đang chết mà bị đặt lại vào hộp (do người nuôi hoặc script khác) thì phải hồi máu,
        // vì StopAllCoroutines ở trên đã dừng luôn coroutine hồi sinh nên không ai hồi máu nữa
        if (health != null && health.IsDead) health.ResetFull();
        deadTimer = 0f;
        fliesEaten = 0; // Bị bắt / chết -> mất hết hiệu ứng ăn ruồi

        RestoreFromBurrow(); // Tránh kẹt ở trạng thái Kinematic nếu đang đào đất

        ReleaseTongue(); // Nhả lưỡi trước khi dịch chuyển
        hasMomentum = false;

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
        airPeakY = transform.position.y; // Dịch chuyển không tính là rơi
        wasGroundedFall = true;
        hasMomentum = false;

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