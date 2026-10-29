using UnityEngine;
using UnityEngine.Events;

public class CatPatrolAI : MonoBehaviour
{
    public enum State { Patrol, Alert, Investigate, Search, Chase, Attack }

    [Header("Trạng thái (chỉ để xem)")]
    [SerializeField] private State state = State.Patrol;
    public State CurrentState => state;

    [Header("Điểm tuần tra")]
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;
    public float waitTime = 1.5f;
    public float arriveDistance = 0.05f;

    [Header("Nghe tiếng động")]
    public float hearingMultiplier = 1f;
    public float alertDuration = 1.0f;
    public float investigateSpeed = 4f;
    public float searchDuration = 3f;

    [Header("Nhìn thấy người chơi")]
    public Transform target;               // con ếch; để trống thì tự tìm Tag "Player"
    public float visionRange = 7f;
    public float verticalTolerance = 2f;
    public bool alertedSeesAllAround = true;

    [Header("Thanh chú ý (Attention)")]
    [Range(0f, 1f)][SerializeField] private float attention;
    public float Attention => attention;
    [Tooltip("Vạch nghi ngờ: thanh vượt mức này mà vẫn thấy ếch thì mèo dừng lại nhìn")]
    public float suspiciousThreshold = 0.3f;
    [Tooltip("Tốc độ tăng thanh (mỗi giây) khi ếch ở xa tầm nhìn")]
    public float seeRateFar = 0.35f;
    [Tooltip("Tốc độ tăng thanh (mỗi giây) khi ếch ở sát mèo")]
    public float seeRateNear = 2.5f;
    [Tooltip("Tốc độ tụt thanh (mỗi giây) khi mèo không thấy ếch")]
    public float decayRate = 0.15f;
    [Tooltip("Mức thanh nhảy lên khi nghe tiếng động nhỏ")]
    public float noiseAttentionMin = 0.45f;
    [Tooltip("Mức thanh nhảy lên khi nghe tiếng động rất to")]
    public float noiseAttentionMax = 0.85f;
    [Tooltip("Bán kính tiếng động được coi là to hết cỡ")]
    public float noiseFullLoudnessRadius = 12f;

    [Header("Đuổi & Bắt")]
    public float chaseSpeed = 5.5f;
    public float attackRange = 1.2f;
    public float attackHitTime = 0.3f;
    public float attackDuration = 1.0f;
    public float loseSightRange = 10f;
    public float loseSightTime = 2f;
    public UnityEvent onCaughtPlayer;

    [Header("Hình ảnh / Animation")]
    public SpriteRenderer spriteRenderer;
    public Animator animator;
    public bool spriteFacesRight = true;
    public string idleState = "CatIdle";
    public string walkState = "CatWalk";
    public string alertState = "CatAlert";
    public string runState = "CatRun";
    public string attackState = "CatAttack";

    private float leftX, rightX, targetX;
    private float waitTimer;
    private bool isWaiting;
    private float stateTimer;
    private float noiseX;
    private float loseTimer;
    private bool hitDone;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    private void OnEnable() { NoiseSystem.OnNoise += HearNoise; }
    private void OnDisable() { NoiseSystem.OnNoise -= HearNoise; }

    private void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogError("[CatPatrolAI] Chưa gán Point A / Point B!");
            enabled = false;
            return;
        }

        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
            else Debug.LogWarning("[CatPatrolAI] Không có Target / Tag 'Player'. Mèo chỉ tuần tra.");
        }

        // Lưu vị trí A/B một lần để chúng không chạy theo mèo
        float ax = pointA.position.x;
        float bx = pointB.position.x;
        leftX = Mathf.Min(ax, bx);
        rightX = Mathf.Max(ax, bx);
        targetX = rightX;
        state = State.Patrol;
        attention = 0f;
    }

    private void Update()
    {
        switch (state)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Alert: UpdateAlert(); break;
            case State.Investigate: UpdateInvestigate(); break;
            case State.Search: UpdateSearch(); break;
            case State.Chase: UpdateChase(); break;
            case State.Attack: UpdateAttack(); break;
        }
    }

    // ---------- Thanh chú ý ----------
    /// <summary>
    /// Cập nhật thanh chú ý theo việc có nhìn thấy ếch hay không.
    /// Trả về true nếu thanh đầy và mèo vừa chuyển sang đuổi.
    /// </summary>
    private bool UpdateAttention(bool alerted)
    {
        if (target != null && CanSeeTarget(alerted))
        {
            float dist = Mathf.Abs(target.position.x - transform.position.x);
            float closeness = 1f - Mathf.Clamp01(dist / visionRange);
            float rate = Mathf.Lerp(seeRateFar, seeRateNear, closeness);
            attention = Mathf.Min(1f, attention + rate * Time.deltaTime);

            if (attention >= 1f)
            {
                StartChase();
                return true;
            }

            // Bắt đầu nghi ngờ khi đang tuần tra: dừng lại nhìn về phía ếch
            if (state == State.Patrol && attention >= suspiciousThreshold)
            {
                noiseX = target.position.x;
                state = State.Alert;
                stateTimer = alertDuration;
                FaceDirection(Mathf.Sign(noiseX - transform.position.x));
                Debug.Log("Mèo thấy gì đó đáng ngờ...");
            }
        }
        else
        {
            attention = Mathf.Max(0f, attention - decayRate * Time.deltaTime);
        }
        return false;
    }

    // ---------- Tuần tra ----------
    private void UpdatePatrol()
    {
        if (UpdateAttention(false)) return;
        if (state != State.Patrol) return; // vừa chuyển sang Alert vì nghi ngờ

        if (isWaiting)
        {
            PlayLoop(idleState, idleState);
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                targetX = Mathf.Approximately(targetX, rightX) ? leftX : rightX;
            }
            return;
        }

        if (MoveTowardsX(targetX, speed, walkState, walkState))
        {
            isWaiting = true;
            waitTimer = waitTime;
        }
    }

    private void ReturnToPatrol()
    {
        float x = transform.position.x;
        targetX = Mathf.Abs(x - leftX) < Mathf.Abs(x - rightX) ? leftX : rightX;
        isWaiting = false;
        state = State.Patrol;
    }

    // ---------- Nghe tiếng động ----------
    private void HearNoise(Vector2 noisePos, float radius)
    {
        if (!isActiveAndEnabled) return;
        if (state == State.Chase || state == State.Attack) return;

        float dist = Vector2.Distance(transform.position, noisePos);
        if (dist > radius * hearingMultiplier) return;

        // Tiếng càng to thì thanh chú ý nhảy càng cao
        float loudness = Mathf.Clamp01(radius / noiseFullLoudnessRadius);
        float noiseAttention = Mathf.Lerp(noiseAttentionMin, noiseAttentionMax, loudness);
        attention = Mathf.Max(attention, noiseAttention);

        noiseX = noisePos.x;
        if (state == State.Investigate) return;

        state = State.Alert;
        stateTimer = alertDuration;
        FaceDirection(Mathf.Sign(noiseX - transform.position.x));
        Debug.Log("Mèo nghe thấy tiếng động!");
    }

    private void UpdateAlert()
    {
        if (UpdateAttention(true)) return;

        PlayLoop(alertState, idleState);
        FaceDirection(Mathf.Sign(noiseX - transform.position.x));

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f) state = State.Investigate;
    }

    private void UpdateInvestigate()
    {
        if (UpdateAttention(true)) return;

        if (MoveTowardsX(noiseX, investigateSpeed, runState, walkState))
        {
            state = State.Search;
            stateTimer = searchDuration;
        }
    }

    private void UpdateSearch()
    {
        if (UpdateAttention(true)) return;

        PlayLoop(idleState, idleState);
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f) ReturnToPatrol();
    }

    // ---------- Đuổi & Bắt ----------
    private void StartChase()
    {
        state = State.Chase;
        attention = 1f;
        loseTimer = 0f;
        Debug.Log("Mèo thấy ếch! Đuổi theo!");
    }

    private void UpdateChase()
    {
        if (target == null) { ReturnToPatrol(); return; }

        float dx = target.position.x - transform.position.x;
        float dy = Mathf.Abs(target.position.y - transform.position.y);
        float dist = Mathf.Abs(dx);

        if (dist <= attackRange && dy <= verticalTolerance)
        {
            StartAttack();
            return;
        }

        if (dist > loseSightRange || dy > verticalTolerance * 2f)
        {
            loseTimer += Time.deltaTime;
            if (loseTimer >= loseSightTime)
            {
                noiseX = target.position.x;
                attention = 0.7f;
                state = State.Investigate;
                Debug.Log("Mèo mất dấu ếch.");
                return;
            }
        }
        else
        {
            loseTimer = 0f;
        }

        MoveTowardsX(target.position.x, chaseSpeed, runState, walkState);
    }

    private void StartAttack()
    {
        state = State.Attack;
        stateTimer = attackDuration;
        hitDone = false;
        FaceDirection(Mathf.Sign(target.position.x - transform.position.x));
        PlayOnce(attackState, idleState);
        Debug.Log("Mèo VỒ!");
    }

    private void UpdateAttack()
    {
        float elapsed = attackDuration - stateTimer;
        stateTimer -= Time.deltaTime;

        if (!hitDone && elapsed >= attackHitTime)
        {
            hitDone = true;
            if (target != null)
            {
                float dx = Mathf.Abs(target.position.x - transform.position.x);
                float dy = Mathf.Abs(target.position.y - transform.position.y);
                if (dx <= attackRange * 1.5f && dy <= verticalTolerance)
                {
                    Debug.Log("Mèo BẮT ĐƯỢC ẾCH!");
                    onCaughtPlayer?.Invoke();
                }
            }
        }

        if (stateTimer <= 0f)
        {
            if (CanSeeTarget(true)) StartChase();
            else
            {
                state = State.Search;
                stateTimer = searchDuration;
                noiseX = transform.position.x;
                attention = Mathf.Min(attention, 0.6f);
            }
        }
    }

    // ---------- Tiện ích ----------
    private bool CanSeeTarget(bool alerted)
    {
        if (target == null) return false;

        float dx = target.position.x - transform.position.x;
        float dy = Mathf.Abs(target.position.y - transform.position.y);
        if (Mathf.Abs(dx) > visionRange || dy > verticalTolerance) return false;

        if (alerted && alertedSeesAllAround) return true;
        return Mathf.Sign(dx) == FacingSign();
    }

    private bool MoveTowardsX(float x, float moveSpeed, string stateName, string fallback)
    {
        Vector3 pos = transform.position;
        FaceDirection(Mathf.Sign(x - pos.x));
        PlayLoop(stateName, fallback);

        pos.x = Mathf.MoveTowards(pos.x, x, moveSpeed * Time.deltaTime);
        transform.position = pos;

        if (Mathf.Abs(x - pos.x) <= arriveDistance)
        {
            pos.x = x;
            transform.position = pos;
            return true;
        }
        return false;
    }

    private float FacingSign()
    {
        if (spriteRenderer == null) return 1f;
        bool facingRight = spriteFacesRight ? !spriteRenderer.flipX : spriteRenderer.flipX;
        return facingRight ? 1f : -1f;
    }

    private void FaceDirection(float dir)
    {
        if (spriteRenderer == null || dir == 0f) return;
        bool movingRight = dir > 0f;
        spriteRenderer.flipX = spriteFacesRight ? !movingRight : movingRight;
    }

    private string Resolve(string stateName, string fallback)
    {
        if (animator == null) return stateName;
        if (animator.HasState(0, Animator.StringToHash(stateName))) return stateName;
        return fallback;
    }

    private void PlayLoop(string stateName, string fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        string s = Resolve(stateName, fallback);
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName(s))
            animator.Play(s, 0);
    }

    private void PlayOnce(string stateName, string fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.Play(Resolve(stateName, fallback), 0, 0f);
    }
}