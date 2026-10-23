using System.Collections;
using UnityEngine;

/// <summary>
/// Script điều khiển chú ếch theo phong cách vật lý Jump King 2D:
/// - Di chuyển đi bộ dưới đất, gồng nhảy parabol, khóa lái trên không, đập tường nảy ra, rơi đau bẹp dí.
/// - TÍCH HỢP CÁC CƠ CHẾ NÂNG CAO:
///   1. Trọng lượng cơ thể: Ảnh hưởng lực nhảy và khả năng bám Gel.
///   2. Gel bám tường: Dính trên tường, gồng nhảy vọt ra khỏi tường (Wall Jump). Nếu quá nặng sẽ bị tuột dốc!
///   3. Gió & Bụi: Gió bạt góc nhảy; Bụi bay vào mắt gây Stun đứng yên dụi mắt tại chỗ (không bị teleport).
///   4. Tiếng kêu Croak (Phím F / Phím C): Phát âm thanh ộp ộp + sóng âm dọa mèo, gây ồn báo động người nuôi.
///   5. Đào đất trốn (Phím S): Chui xuống đất tại chậu cây/bồn hoa để ẩn mình trước mèo.
///   6. Khóa phím A và D khi đang nhảy trên không trung (chuẩn Jump King).
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

    // Biến gel bám tường
    private StickyGelSurface currentGel;
    private Vector2 wallNormal = Vector2.zero;
    private bool isSlidingOnGel = false;

    // Biến đào đất
    private bool canBurrowHere = false;
    private DiggableGround nearbySoil;

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
    }

    private void Update()
    {
        CheckGrounded();

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
        if (currentState != FrogState.Burrowed && currentState != FrogState.Stunned && currentState != FrogState.WallStuck)
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
        else if (currentState == FrogState.WallStuck)
        {
            HandleWallStuckInput();
        }
        else if (currentState == FrogState.Burrowed)
        {
            HandleBurrowedInput();
        }
    }

    private void FixedUpdate()
    {
        if (currentState == FrogState.Grounded || currentState == FrogState.InAir || currentState == FrogState.Bounced)
        {
            // Di chuyển mượt mà cả dưới đất và trên không (Air Control Mario)
            rb.linearVelocity = new Vector2(horizontalInput * walkSpeed, rb.linearVelocity.y);
        }
        else if (currentState == FrogState.Charging || currentState == FrogState.Splat || currentState == FrogState.Stunned || currentState == FrogState.Burrowed)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
        else if (currentState == FrogState.WallStuck)
        {
            if (isSlidingOnGel)
            {
                rb.linearVelocity = new Vector2(0f, -currentGel.slipSlideSpeed);
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
            }
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

    #region GEL BÁM TƯỜNG (STICKY GEL)

    /// <summary>
    /// Gắn bám vào bề mặt Gel dính
    /// </summary>
    public void AttachToGelWall(StickyGelSurface gel, Vector2 normal, bool isOverweight)
    {
        if (currentState == FrogState.WallStuck) return;

        currentGel = gel;
        wallNormal = normal;
        isSlidingOnGel = isOverweight;

        currentState = FrogState.WallStuck;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        // Quay mặt nhìn ra ngoài tường
        SetSpriteFacing(normal.x > 0);

        if (isOverweight)
        {
            Debug.LogWarning("⚠️ Ếch quá nặng! Gel không giữ nổi, đang bị tuột xuống!");
        }
        else
        {
            Debug.Log("✨ Ếch đã bám chặt vào Gel trên tường!");
        }
    }

    private void HandleWallStuckInput()
    {
        // Nhấn Space để nhảy bật ra khỏi tường
        if (GameInput.GetKeyDown(KeyCode.Space))
        {
            StartCharging();
        }

        // Nếu đang gồng từ trên tường -> Bung nhảy ra khỏi tường
        if (currentState == FrogState.Charging && GameInput.GetKeyUp(KeyCode.Space))
        {
            ExecuteWallJump();
        }
    }

    private void ExecuteWallJump()
    {
        float chargeRatio = ChargePercentage;
        float actualForce = Mathf.Lerp(minJumpForce, maxJumpForce, chargeRatio);

        // Hướng nhảy bật chéo ra xa khỏi tường và hơi hướng lên
        Vector2 jumpDir = (wallNormal * 0.7f + Vector2.up * 0.85f).normalized;

        currentGel = null;
        isSlidingOnGel = false;
        rb.gravityScale = originalGravityScale;
        rb.linearVelocity = jumpDir * actualForce;

        horizontalInput = 0f;
        currentState = FrogState.InAir;
        highestFallSpeed = 0f;
        currentChargeTime = 0f;

        facingDirection = wallNormal.x > 0 ? 1 : -1;
        SetSpriteFacing(facingDirection > 0);

        StartCoroutine(ResetVisualScaleRoutine());
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
        currentState = FrogState.Burrowed;
        rb.linearVelocity = Vector2.zero;
        visualTransform.localScale = new Vector3(originalVisualScale.x * 0.9f, originalVisualScale.y * 0.2f, originalVisualScale.z);
        Debug.Log($"[Burrow] Ếch đã đào đất chui xuống trốn tại {nearbySoil?.soilName}!");
    }

    private void HandleBurrowedInput()
    {
        if (GameInput.GetKeyDown(KeyCode.Space))
        {
            visualTransform.localScale = originalVisualScale;
            currentState = FrogState.Grounded;
            rb.linearVelocity = Vector2.up * minJumpForce;
            Debug.Log("[Burrow] Ếch nhảy vọt lên khỏi đất!");
        }
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
        currentGel = null;
        isSlidingOnGel = false;
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

    private void SetSpriteFacing(bool faceRight)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = !faceRight;
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
