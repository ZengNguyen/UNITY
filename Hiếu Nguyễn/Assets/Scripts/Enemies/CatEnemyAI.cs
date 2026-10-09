using System.Collections;
using UnityEngine;

/// <summary>
/// AI Mèo tuần tra & rình bắt trong phòng khách:
/// 1. Tuần tra (Patrol): Đi qua lại giữa các mốc trong phòng.
/// 2. Phát hiện ếch (Detect): Dựa trên tầm nhìn (Line of Sight). Nếu ếch nấp sau đồ vật -> an toàn!
/// 3. Chực chờ (Stalking): KHÔNG tấn công ngay, mèo dừng lại rình rập, lắc mông chực vồ.
///    - Nếu ếch đứng yên quá lâu (> stillThreshold) -> Mèo nhảy VỒ (Pounce)!
///    - Nếu ếch di chuyển liên tục -> Mèo chỉ bám theo chực chờ.
/// 4. Hai cách thoát mèo:
///    - Cách 1: Nấp sau đồ vật (phá tầm nhìn).
///    - Cách 2: Vừa di chuyển vừa KÊU (Phím F - Croak) để dọa mèo sợ chạy biến đi.
///    - (Cách phụ): Nhè con ruồi làm mồi dụ mèo (FlyBait) để mèo mải chơi với ruồi.
/// </summary>
public class CatEnemyAI : MonoBehaviour
{
    public enum CatState
    {
        Patrol,         // Đi tuần tra
        Stalking,       // Đứng chực chờ, rình rập
        Pounce,         // Nhảy vồ ếch
        Scared,         // Bị tiếng ếch dọa sợ bỏ chạy
        Distracted      // Mải chơi với mồi ruồi
    }

    [Header("=== Trạng thái hiện tại ===")]
    [SerializeField] private CatState currentState = CatState.Patrol;
    public CatState CurrentState => currentState;

    [Header("=== Tuần tra (Patrol) ===")]
    public Transform patrolPointA;
    public Transform patrolPointB;
    public float patrolSpeed = 2.0f;
    private int currentTargetPoint = 1;

    [Header("=== Phát hiện tầm nhìn (Line of Sight) ===")]
    public float visionRadius = 7.0f;
    public LayerMask obstacleLayer;     // Lớp đồ đạc cản tầm nhìn
    public LayerMask playerLayer;

    [Header("=== Rình rập & Vồ mồi (Stalking & Pounce) ===")]
    [Tooltip("Thời gian ếch đứng yên tối đa trước khi bị mèo vồ (giây)")]
    public float maxStillTimeBeforePounce = 2.0f;

    [Tooltip("Lực nhảy vồ tới chú ếch")]
    public float pounceForce = 12.0f;

    [Tooltip("Khoảng cách mèo giữ khi rình")]
    public float stalkKeepDistance = 3.5f;

    [Header("=== Bị dọa sợ (Scared) ===")]
    [Tooltip("Thời gian mèo sợ hãi bỏ chạy khi nghe tiếng ếch kêu (giây)")]
    public float scaredDuration = 3.5f;
    public float runAwaySpeed = 4.5f;

    [Header("=== Hình ảnh & Hoạt họa ===")]
    public SpriteRenderer spriteRenderer;

    private Rigidbody2D rb;
    private FrogController targetFrog;
    private float frogStillTimer = 0f;
    private float lostSightTimer = 0f;
    private bool isPouncing = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        targetFrog = FindAnyObjectByType<FrogController>();
    }

    private void Update()
    {
        if (targetFrog == null) return;

        switch (currentState)
        {
            case CatState.Patrol:
                HandlePatrol();
                CheckForFrogDetection();
                CheckForFlyBait();
                break;

            case CatState.Stalking:
                HandleStalking();
                CheckForFlyBait();
                break;

            case CatState.Pounce:
                // Đang trên đà vồ, để vật lý tự xử lý
                break;

            case CatState.Scared:
                // Đang sợ hãi bỏ chạy
                break;

            case CatState.Distracted:
                // Mải chơi với mồi ruồi
                break;
        }
    }

    /// <summary>
    /// Tuần tra qua lại
    /// </summary>
    private void HandlePatrol()
    {
        Transform targetWaypoint = (currentTargetPoint == 0) ? patrolPointA : patrolPointB;
        if (targetWaypoint == null) return;

        float dir = Mathf.Sign(targetWaypoint.position.x - transform.position.x);
        rb.linearVelocity = new Vector2(dir * patrolSpeed, rb.linearVelocity.y);
        SetFacing(dir > 0);

        if (Mathf.Abs(transform.position.x - targetWaypoint.position.x) < 0.5f)
        {
            currentTargetPoint = (currentTargetPoint == 0) ? 1 : 0;
        }
    }

    /// <summary>
    /// Kiểm tra tầm nhìn tới chú ếch
    /// </summary>
    private void CheckForFrogDetection()
    {
        if (targetFrog.IsBurrowed) return; // Ếch đang đào đất trốn -> Không thấy được!

        float dist = Vector2.Distance(transform.position, targetFrog.transform.position);
        if (dist <= visionRadius)
        {
            // Bắn tia Raycast kiểm tra vật cản che khuất tầm nhìn (nấp sau đồ vật)
            Vector2 dirToFrog = (targetFrog.transform.position - transform.position).normalized;
            RaycastHit2D hitObstacle = Physics2D.Raycast(transform.position, dirToFrog, dist, obstacleLayer);

            if (hitObstacle.collider == null)
            {
                // Không có vật cản -> Mèo đã nhìn thấy ếch!
                Debug.Log("🐱 Mèo: 'Meo...? Có con gì động đậy đằng kia!' -> Bắt đầu rình rập!");
                currentState = CatState.Stalking;
                frogStillTimer = 0f;
                lostSightTimer = 0f;
            }
        }
    }

    /// <summary>
    /// Xử lý trạng thái rình rập chực chờ
    /// </summary>
    private void HandleStalking()
    {
        // Quay mặt về phía ếch
        float dirToFrogX = Mathf.Sign(targetFrog.transform.position.x - transform.position.x);
        SetFacing(dirToFrogX > 0);

        // Kiểm tra xem ếch có còn trong tầm nhìn không hay đã chạy trốn sau đồ vật
        float dist = Vector2.Distance(transform.position, targetFrog.transform.position);
        Vector2 dir = (targetFrog.transform.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, dist, obstacleLayer);

        if (hit.collider != null || targetFrog.IsBurrowed || dist > visionRadius * 1.3f)
        {
            // Mất dấu ếch
            lostSightTimer += Time.deltaTime;
            if (lostSightTimer > 2.2f)
            {
                Debug.Log("🐱 Mèo: 'Nó trốn đâu mất rồi nhỉ?' -> Quay lại đi tuần tra.");
                currentState = CatState.Patrol;
                return;
            }
        }
        else
        {
            lostSightTimer = 0f;
        }

        // Kiểm tra xem ếch có ĐỨNG YÊN không
        bool isFrogMoving = targetFrog.IsMoving;
        if (!isFrogMoving)
        {
            frogStillTimer += Time.deltaTime;

            // Rung lắc nhẹ (Hiệu ứng mèo lắc mông trước khi vồ)
            float wiggle = Mathf.Sin(Time.time * 20f) * 0.08f;
            transform.position = new Vector3(transform.position.x, transform.position.y + wiggle * 0.02f, transform.position.z);

            // Nếu đứng yên quá lâu -> VỒ!
            if (frogStillTimer >= maxStillTimeBeforePounce)
            {
                StartCoroutine(ExecutePounceRoutine());
                return;
            }
        }
        else
        {
            // Ếch di chuyển liên tục -> Mèo không dám vồ ngay, chỉ bám theo chầm chậm
            frogStillTimer = 0f;
            if (dist > stalkKeepDistance)
            {
                rb.linearVelocity = new Vector2(dirToFrogX * (patrolSpeed * 0.8f), rb.linearVelocity.y);
            }
            else
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }
    }

    /// <summary>
    /// Thực hiện cú nhảy vồ cực nhanh
    /// </summary>
    private IEnumerator ExecutePounceRoutine()
    {
        currentState = CatState.Pounce;
        isPouncing = true;
        Debug.LogWarning("🐱⚡ MÈO NHẢY VỒ! 'GÀOOOO!'");

        // Co người lấy đà 0.2s
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.2f);

        // Phóng tới hướng con ếch
        Vector2 pounceDir = (targetFrog.transform.position - transform.position).normalized;
        pounceDir = (pounceDir + Vector2.up * 0.5f).normalized;
        rb.linearVelocity = pounceDir * pounceForce;

        yield return new WaitForSeconds(1.0f);

        isPouncing = false;
        currentState = CatState.Patrol;
    }

    /// <summary>
    /// Ếch kêu to (Croak - Phím F) -> Mèo bị dọa sợ dựng lông bỏ chạy!
    /// </summary>
    public void ScareAway(Vector3 noiseSource)
    {
        if (currentState == CatState.Scared) return;

        Debug.LogWarning("🐱💦 MÈO BỊ TIẾNG ẾCH KÊU DỌA HOẢNG HỐT! Bỏ chạy trối chết!");
        StartCoroutine(ScaredFleeRoutine(noiseSource));
    }

    private IEnumerator ScaredFleeRoutine(Vector3 source)
    {
        currentState = CatState.Scared;
        StopCoroutine(nameof(ExecutePounceRoutine));
        isPouncing = false;

        // Chạy ngược hướng tiếng kêu
        float fleeDir = Mathf.Sign(transform.position.x - source.x);
        SetFacing(fleeDir > 0);

        float timer = 0f;
        while (timer < scaredDuration)
        {
            rb.linearVelocity = new Vector2(fleeDir * runAwaySpeed, rb.linearVelocity.y);
            timer += Time.deltaTime;
            yield return null;
        }

        currentState = CatState.Patrol;
    }

    /// <summary>
    /// Mèo phát hiện mồi ruồi (FlyBait) nằm trên sàn -> Bị phân tâm
    /// </summary>
    private void CheckForFlyBait()
    {
        FlyBait bait = FindAnyObjectByType<FlyBait>();
        if (bait != null)
        {
            float dist = Vector2.Distance(transform.position, bait.transform.position);
            if (dist < 5.0f && currentState != CatState.Distracted && currentState != CatState.Scared)
            {
                StartCoroutine(DistractedByBaitRoutine(bait));
            }
        }
    }

    private IEnumerator DistractedByBaitRoutine(FlyBait bait)
    {
        currentState = CatState.Distracted;
        Debug.Log("🐱🍽️ Mèo: 'Oa! Có con ruồi béo ngậy!' -> Bỏ qua ếch để vồ ruồi!");

        while (bait != null && Vector2.Distance(transform.position, bait.transform.position) > 0.6f)
        {
            float dir = Mathf.Sign(bait.transform.position.x - transform.position.x);
            rb.linearVelocity = new Vector2(dir * patrolSpeed, rb.linearVelocity.y);
            SetFacing(dir > 0);
            yield return null;
        }

        if (bait != null)
        {
            Destroy(bait.gameObject);
        }

        // Mèo ngồi liếm mép chơi đùa 3 giây
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(3.0f);

        currentState = CatState.Patrol;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<FrogController>(out var frog))
        {
            if (isPouncing || currentState == CatState.Stalking)
            {
                // Tát hoặc vồ trúng ếch -> Hất văng ếch cực mạnh theo kiểu Jump King!
                Debug.LogError("💥 MÈO ĐÃ TÁT TRÚNG ẾCH! Hất văng xuống sàn!");
                Vector2 dirToFrog = (frog.transform.position - transform.position).normalized;
                Vector2 knockbackDir = dirToFrog + Vector2.up * 0.7f;
                frog.ApplyKnockback(knockbackDir.normalized * 14.0f);
            }
        }
    }

    private void SetFacing(bool faceRight)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = !faceRight;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRadius);

        if (patrolPointA != null && patrolPointB != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(patrolPointA.position, patrolPointB.position);
        }
    }
}
