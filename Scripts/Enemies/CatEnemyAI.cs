using System.Collections;
using UnityEngine;

/// <summary>
/// AI Mèo tuần tra & rình bắt trong phòng khách:
/// 1. Nhận diện người chơi chuẩn xác 100%:
///    - Tia tầm nhìn bắn từ ngoài thân mèo, không bị chính collider của mèo chặn.
///    - Đứng trước mặt mèo là mèo phát hiện ngay lập tức.
/// 2. Quay đầu chuẩn xác:
///    - Lật transform.localScale.x để toàn bộ thân và mắt mèo đều quay theo hướng di chuyển.
/// 3. Phát hiện & Vồ mồi:
///    - Khi phát hiện ếch -> đứng rình nhấp nhổm đúng 2 giây rồi phóng vồ tới ếch.
///    - Đẩy ếch một khoảng nhỏ, sau đó mèo lập tức quay đầu chạy biến đi mất (Flee).
/// 4. Bỏ chạy & Tự quay lại sau 1 - 2 phút:
///    - Chạy nhanh về phía góc phòng hoặc ra sau vật thể solid, sau đó ẩn đi.
///    - Sau 60 - 120 giây (1-2 phút) mèo xuất hiện lại và tiếp tục đi tuần.
///    - Bấm phím F (kêu ộp) cũng làm mèo sợ hãi bỏ chạy và 1-2 phút sau mới quay lại.
/// 5. Tuần tra chuẩn trong phòng, không nhảy lơ lửng, không văng map.
/// </summary>
public class CatEnemyAI : MonoBehaviour
{
    public enum CatState
    {
        Patrol,             // Đi tuần tra
        PreparingPounce,    // Phát hiện ếch, đếm ngược 2s chuẩn bị vồ
        Pounce,             // Đang phóng mình vồ tới ếch
        Fleeing,            // Chạy trốn khỏi màn hình
        HiddenWaitingReturn // Đang ẩn, chờ 1-2 phút sau quay lại
    }

    [Header("=== Trạng thái hiện tại ===")]
    [SerializeField] private CatState currentState = CatState.Patrol;
    public CatState CurrentState => currentState;

    [Header("=== Tuần tra (Patrol) ===")]
    public Transform patrolPointA;
    public Transform patrolPointB;
    public float patrolSpeed = 2.2f;
    [Tooltip("Thời gian mèo dừng lại ngửi ngửi khi tới điểm mốc trước khi quay đầu (giây)")]
    public float patrolPauseDuration = 1.0f;

    [Header("=== Phạm vi tuần tra an toàn trong phòng ===")]
    public float minPatrolX = -5.2f;
    public float maxPatrolX = 12.0f;

    [Header("=== Cơ chế Nhảy Lên Bục Solid (Bàn, Ghế) ===")]
    public float jumpForce = 7.0f;
    public float obstacleCheckDistance = 0.8f;
    public LayerMask solidLayer;

    [Header("=== Phát hiện tầm nhìn (Line of Sight) ===")]
    public float visionRadius = 8.5f;
    public LayerMask obstacleLayer;

    [Header("=== Phát hiện & Vồ mồi (Detect & Pounce) ===")]
    [Tooltip("Thời gian chờ từ lúc phát hiện đến lúc vồ (đúng 2 giây)")]
    public float detectToPounceDelay = 2.0f;

    [System.Obsolete("Dùng detectToPounceDelay thay thế")]
    [HideInInspector]
    public float maxStillTimeBeforePounce
    {
        get => detectToPounceDelay;
        set => detectToPounceDelay = value;
    }

    [Tooltip("Lực phóng tới chú ếch")]
    public float pounceForce = 11.0f;

    [Tooltip("Lực đẩy nhẹ chú ếch đi 1 khoảng nhỏ khi vồ trúng")]
    public float smallKnockbackForce = 6.0f;

    [Header("=== Bỏ chạy & Thời gian quay lại (Flee & Return) ===")]
    public float fleeSpeed = 5.5f;
    public float fleeRunDuration = 3.5f;
    public float minReturnTime = 60.0f;
    public float maxReturnTime = 120.0f;

    [Header("=== Hình ảnh & Hoạt họa ===")]
    public SpriteRenderer spriteRenderer;

    private Rigidbody2D rb;
    private Collider2D catCollider;
    private FrogController targetFrog;
    private Vector3 initialScale;

    private float currentMoveDir = 1f;
    private float pauseTimer = 0f;
    private float detectTimer = 0f;
    private float lastJumpTime = -1f;
    private Coroutine activeBehaviorRoutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        catCollider = GetComponent<Collider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        initialScale = transform.localScale;

        if (solidLayer.value == 0) solidLayer = LayerMask.GetMask("Default");
        if (obstacleLayer.value == 0) obstacleLayer = LayerMask.GetMask("Default");

        if (patrolPointA != null && patrolPointB != null)
        {
            minPatrolX = Mathf.Min(patrolPointA.position.x, patrolPointB.position.x) - 0.5f;
            maxPatrolX = Mathf.Max(patrolPointA.position.x, patrolPointB.position.x) + 0.5f;
        }
    }

    private void Start()
    {
        targetFrog = FindAnyObjectByType<FrogController>();
        currentMoveDir = 1f;
        SetFacing(true);
    }

    private void Update()
    {
        ClampPositionWithinEnvironment();

        if (targetFrog == null)
        {
            targetFrog = FindAnyObjectByType<FrogController>();
            if (targetFrog == null) return;
        }

        switch (currentState)
        {
            case CatState.Patrol:
                HandlePatrol();
                CheckForFrogDetection();
                break;

            case CatState.PreparingPounce:
                HandlePreparingPounce();
                break;

            case CatState.Pounce:
                // Vật lý vồ tự động xử lý
                break;

            case CatState.Fleeing:
                // Xử lý bởi FleeRoutine
                break;

            case CatState.HiddenWaitingReturn:
                // Xử lý bởi ReturnCooldownRoutine
                break;
        }
    }

    #region 1. TUẦN TRA & QUAY ĐẦU CHUẨN XÁC
    private void HandlePatrol()
    {
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // Kiểm tra chạm biên tuần tra
        if (transform.position.x <= minPatrolX && currentMoveDir < 0f)
        {
            ReverseDirectionWithPause();
            return;
        }
        if (transform.position.x >= maxPatrolX && currentMoveDir > 0f)
        {
            ReverseDirectionWithPause();
            return;
        }

        rb.linearVelocity = new Vector2(currentMoveDir * patrolSpeed, rb.linearVelocity.y);
        SetFacing(currentMoveDir > 0f);

        CheckObstacleAhead(currentMoveDir);
    }

    private void ReverseDirectionWithPause()
    {
        currentMoveDir = -currentMoveDir;
        SetFacing(currentMoveDir > 0f);
        pauseTimer = patrolPauseDuration;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void CheckObstacleAhead(float dir)
    {
        if (catCollider == null) return;

        Bounds bounds = catCollider.bounds;
        float frontX = (dir > 0f) ? bounds.max.x + 0.05f : bounds.min.x - 0.05f;

        Vector2 originFeet = new Vector2(frontX, bounds.min.y + 0.15f);
        Vector2 originHigh = new Vector2(frontX, bounds.max.y + 1.1f);
        Vector2 rayDir = new Vector2(dir, 0f);

        RaycastHit2D hitFeet = Physics2D.Raycast(originFeet, rayDir, obstacleCheckDistance, solidLayer);
        RaycastHit2D hitHigh = Physics2D.Raycast(originHigh, rayDir, obstacleCheckDistance + 0.25f, solidLayer);

        bool hasLowObstacle = IsValidObstacle(hitFeet);
        bool hasHighObstacle = IsValidObstacle(hitHigh);

        if (hasHighObstacle)
        {
            // Bức tường cao chắn trước mặt -> Quay đầu lại
            ReverseDirectionWithPause();
        }
        else if (hasLowObstacle)
        {
            // Bục thấp (bàn trà, ghế) -> Nhảy lên trèo lên bục
            if (IsGrounded() && (Time.time - lastJumpTime > 0.8f))
            {
                PerformJump();
            }
        }
    }

    private bool IsValidObstacle(RaycastHit2D hit)
    {
        if (hit.collider == null) return false;
        if (hit.collider == catCollider) return false;
        if (hit.collider.isTrigger) return false;
        if (hit.collider.CompareTag("Player") || hit.collider.GetComponent<FrogController>() != null) return false;
        return true;
    }

    private bool IsGrounded()
    {
        if (catCollider == null) return false;
        if (Mathf.Abs(rb.linearVelocity.y) > 0.35f) return false;

        Bounds bounds = catCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + 0.05f);
        Vector2 size = new Vector2(bounds.size.x * 0.7f, 0.12f);

        RaycastHit2D[] hits = Physics2D.BoxCastAll(origin, size, 0f, Vector2.down, 0.15f, solidLayer);
        foreach (var hit in hits)
        {
            if (hit.collider != null && hit.collider != catCollider && !hit.collider.isTrigger)
            {
                return true;
            }
        }
        return false;
    }

    private void PerformJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        lastJumpTime = Time.time;
    }

    private void ClampPositionWithinEnvironment()
    {
        if (currentState == CatState.HiddenWaitingReturn) return;

        if (transform.position.x < minPatrolX - 1.5f || transform.position.x > maxPatrolX + 1.5f || transform.position.y > 10f)
        {
            float safeX = Mathf.Clamp(transform.position.x, minPatrolX, maxPatrolX);
            transform.position = new Vector3(safeX, 0.6f, 0f);
            rb.linearVelocity = Vector2.zero;
            currentMoveDir = (safeX <= minPatrolX) ? 1f : -1f;
            SetFacing(currentMoveDir > 0f);
        }
    }

    /// <summary>
    /// Quay đầu toàn bộ con mèo (thân + mắt) theo hướng di chuyển
    /// </summary>
    public void SetFacing(bool faceRight)
    {
        float sign = faceRight ? 1f : -1f;
        transform.localScale = new Vector3(Mathf.Abs(initialScale.x) * sign, initialScale.y, initialScale.z);
    }
    #endregion

    #region 2. PHÁT HIỆN NGƯỜI CHƠI CHUẨN XÁC & VỒ
    private void CheckForFrogDetection()
    {
        if (targetFrog == null || targetFrog.IsBurrowed) return;

        // Điểm ngang mắt mèo và ngang ngực ếch (tránh quét dính collider sàn nhà)
        Vector2 catEye = (Vector2)transform.position + Vector2.up * 0.35f;
        Vector2 frogChest = (Vector2)targetFrog.transform.position + Vector2.up * 0.3f;

        float dist = Vector2.Distance(catEye, frogChest);
        if (dist > visionRadius) return;

        float facingDir = transform.localScale.x > 0 ? 1f : -1f;
        float dx = frogChest.x - catEye.x;
        bool inFrontOfCat = (dx * facingDir) > -0.25f; // Đứng trước mặt hoặc hơi chếch
        bool isVeryClose = dist < 2.5f;                // Ở rất gần thì mèo đánh hơi được

        if (!inFrontOfCat && !isVeryClose) return;

        // Bắn tia kiểm tra vật cản
        Vector2 dir = (frogChest - catEye).normalized;
        RaycastHit2D[] hits = Physics2D.RaycastAll(catEye, dir, dist);

        // Sắp xếp theo khoảng cách từ gần đến xa
        System.Array.Sort(hits, (a, b) => a.fraction.CompareTo(b.fraction));

        bool canSeeFrog = false;
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider == catCollider || hit.collider.isTrigger) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;

            // Chạm trúng người chơi trước tiên -> Tầm nhìn thông suốt!
            if (hit.collider.gameObject == targetFrog.gameObject || 
                hit.collider.transform.IsChildOf(targetFrog.transform) || 
                hit.collider.GetComponent<FrogController>() != null)
            {
                canSeeFrog = true;
                break;
            }

            // Bị vật cản cứng chắn tầm nhìn (tường, chậu cây, bàn)
            if (!hit.collider.isTrigger)
            {
                canSeeFrog = false;
                break;
            }
        }

        if (canSeeFrog)
        {
            currentState = CatState.PreparingPounce;
            detectTimer = 0f;
            SpawnDetectionFeedback();
            Debug.Log("🐱👀 [Mèo] ĐÃ PHÁT HIỆN CON ÉCH TRƯỚC MẶT! Bắt đầu rình đếm ngược 2s để vồ!");
        }
    }

    private GameObject alertTextObj;
    private void SpawnDetectionFeedback()
    {
        if (alertTextObj != null) Destroy(alertTextObj);
        alertTextObj = new GameObject("Cat_Alert_Text");
        alertTextObj.transform.position = transform.position + Vector3.up * 1.1f;
        alertTextObj.transform.parent = transform;
        var tm = alertTextObj.AddComponent<TextMesh>();
        tm.text = "😼 ❗ VỒ!";
        tm.fontSize = 24;
        tm.characterSize = 0.08f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.2f, 0.2f);
    }

    private void HandlePreparingPounce()
    {
        if (targetFrog == null) return;

        // Quay đầu nhìn thẳng vào con ếch
        float dirToFrogX = Mathf.Sign(targetFrog.transform.position.x - transform.position.x);
        SetFacing(dirToFrogX > 0f);

        // Đứng im tại chỗ rình, co người hạ thấp trọng tâm
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        float sign = dirToFrogX > 0f ? 1f : -1f;
        transform.localScale = new Vector3(Mathf.Abs(initialScale.x) * sign * 1.1f, initialScale.y * 0.75f, initialScale.z);

        detectTimer += Time.deltaTime;

        // Đúng sau 2 giây kể từ khi phát hiện -> VỒ!
        if (detectTimer >= detectToPounceDelay)
        {
            transform.localScale = initialScale;
            SetFacing(dirToFrogX > 0f);
            if (alertTextObj != null) Destroy(alertTextObj);

            if (activeBehaviorRoutine != null) StopCoroutine(activeBehaviorRoutine);
            activeBehaviorRoutine = StartCoroutine(ExecutePounceRoutine());
        }
    }

    private IEnumerator ExecutePounceRoutine()
    {
        currentState = CatState.Pounce;

        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.12f);

        // Phóng mình vồ tới chú ếch
        Vector2 pounceDir = ((Vector2)targetFrog.transform.position - (Vector2)transform.position).normalized;
        pounceDir = (pounceDir + Vector2.up * 0.35f).normalized;
        rb.linearVelocity = pounceDir * pounceForce;

        Debug.Log("🐾 [Mèo] PHÓNG VỒ TỚI CHÚ ÉCH!");

        yield return new WaitForSeconds(1.0f);

        // Nếu vồ xong mà chưa chạm trúng người chơi -> Mèo vẫn quay đầu chạy biến đi
        if (currentState == CatState.Pounce)
        {
            StartFleeing();
        }
    }
    #endregion

    #region 3. BỎ CHẠY (FLEE) & TỰ QUAY LẠI SAU 1-2 PHÚT
    public void StartFleeing(Vector3? dangerSource = null)
    {
        if (activeBehaviorRoutine != null) StopCoroutine(activeBehaviorRoutine);
        activeBehaviorRoutine = StartCoroutine(FleeRoutine(dangerSource));
    }

    private IEnumerator FleeRoutine(Vector3? dangerSource)
    {
        currentState = CatState.Fleeing;
        if (alertTextObj != null) Destroy(alertTextObj);

        // Tạo chữ bỏ chạy trên đầu
        GameObject fleeText = new GameObject("Cat_Flee_Text");
        fleeText.transform.position = transform.position + Vector3.up * 1.0f;
        fleeText.transform.parent = transform;
        var tm = fleeText.AddComponent<TextMesh>();
        tm.text = "💨 BỎ CHẠY!";
        tm.fontSize = 20;
        tm.characterSize = 0.08f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.85f, 0.2f);
        Destroy(fleeText, 2.5f);

        Vector3 referencePoint = dangerSource.HasValue ? dangerSource.Value : (targetFrog != null ? targetFrog.transform.position : transform.position);
        float fleeDir = Mathf.Sign(transform.position.x - referencePoint.x);
        if (Mathf.Abs(fleeDir) < 0.1f) fleeDir = 1f;

        SetFacing(fleeDir > 0f);

        float elapsed = 0f;
        while (elapsed < fleeRunDuration)
        {
            // Nếu chạy đến sát biên tường -> Đổi hướng chạy
            if ((transform.position.x <= minPatrolX && fleeDir < 0f) || (transform.position.x >= maxPatrolX && fleeDir > 0f))
            {
                fleeDir = -fleeDir;
                SetFacing(fleeDir > 0f);
            }

            rb.linearVelocity = new Vector2(fleeDir * fleeSpeed, rb.linearVelocity.y);

            // Tự nhảy khi gặp chướng ngại vật cản chân
            if (IsGrounded() && Time.time - lastJumpTime > 0.8f)
            {
                Bounds bounds = catCollider.bounds;
                float frontX = (fleeDir > 0f) ? bounds.max.x + 0.05f : bounds.min.x - 0.05f;
                RaycastHit2D hitLow = Physics2D.Raycast(new Vector2(frontX, bounds.min.y + 0.15f), new Vector2(fleeDir, 0f), obstacleCheckDistance, solidLayer);
                RaycastHit2D hitHigh = Physics2D.Raycast(new Vector2(frontX, bounds.max.y + 1.1f), new Vector2(fleeDir, 0f), obstacleCheckDistance + 0.25f, solidLayer);

                if (IsValidObstacle(hitLow) && !IsValidObstacle(hitHigh))
                {
                    PerformJump();
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        StartReturnCooldown();
    }

    public void ScareAway(Vector3 noiseSource)
    {
        if (currentState == CatState.Fleeing || currentState == CatState.HiddenWaitingReturn) return;
        StartFleeing(noiseSource);
    }

    private void StartReturnCooldown()
    {
        if (activeBehaviorRoutine != null) StopCoroutine(activeBehaviorRoutine);
        activeBehaviorRoutine = StartCoroutine(ReturnCooldownRoutine());
    }

    private IEnumerator ReturnCooldownRoutine()
    {
        currentState = CatState.HiddenWaitingReturn;

        // Dừng và tạm ẩn mèo đi
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (catCollider != null) catCollider.enabled = false;

        Debug.Log("🐾 [Mèo] Đã chạy trốn khỏi phòng! Đang ẩn mình chờ 1-2 phút sau quay lại...");

        // Chờ từ 1 đến 2 phút (60 đến 120 giây)
        float waitSeconds = Random.Range(minReturnTime, maxReturnTime);
        yield return new WaitForSeconds(waitSeconds);

        // Xuất hiện trở lại tại điểm tuần tra an toàn
        Transform spawnPoint = patrolPointA != null ? patrolPointA : (patrolPointB != null ? patrolPointB : transform);
        transform.position = new Vector3(Mathf.Clamp(spawnPoint.position.x, minPatrolX, maxPatrolX), 0.6f, 0f);

        rb.simulated = true;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (catCollider != null) catCollider.enabled = true;

        currentMoveDir = 1f;
        SetFacing(true);
        pauseTimer = 0.5f;
        currentState = CatState.Patrol;

        // Thông báo mèo đã quay lại
        GameObject backText = new GameObject("Cat_Back_Text");
        backText.transform.position = transform.position + Vector3.up * 1.0f;
        var tm = backText.AddComponent<TextMesh>();
        tm.text = "😼 MÈO ĐÃ QUAY LẠI!";
        tm.fontSize = 20;
        tm.characterSize = 0.08f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = Color.white;
        Destroy(backText, 3.0f);

        Debug.Log("🐾 [Mèo] Đã quay lại và tiếp tục tuần tra trong phòng!");
    }
    #endregion

    #region 4. VA CHẠM
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<FrogController>(out var frog))
        {
            if (currentState == CatState.Pounce || currentState == CatState.PreparingPounce)
            {
                // Vồ trúng: Hất đẩy ếch đi một khoảng nhỏ
                Vector2 dirToFrog = ((Vector2)frog.transform.position - (Vector2)transform.position).normalized;
                Vector2 knockbackDir = (dirToFrog + Vector2.up * 0.4f).normalized;
                frog.ApplyKnockback(knockbackDir * smallKnockbackForce);

                // Sau đó mèo lập tức quay đầu chạy biến đi mất
                StartFleeing();
            }
        }
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(minPatrolX, 0f, 0f), new Vector3(minPatrolX, 3f, 0f));
        Gizmos.DrawLine(new Vector3(maxPatrolX, 0f, 0f), new Vector3(maxPatrolX, 3f, 0f));
        Gizmos.DrawLine(new Vector3(minPatrolX, 0.5f, 0f), new Vector3(maxPatrolX, 0.5f, 0f));
    }
}
