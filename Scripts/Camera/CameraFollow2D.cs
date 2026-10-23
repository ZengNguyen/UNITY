using UnityEngine;

/// <summary>
/// Camera theo dõi chú ếch 2D:
/// - Khóa góc nhìn vào con ếch (Lock Target).
/// - CHỈ DI CHUYỂN KHI CON ẾCH DI CHUYỂN (Move only when frog moves):
///   + Khi ếch đứng yên: Camera đứng im hoàn toàn, không trôi dạt.
///   + Khi ếch đi bộ, gồng nhảy, bay trên không hoặc rơi: Camera lập tức di chuyển bám sát theo ếch.
/// - Cung cấp giới hạn biên màn hình (minX, maxX, minY) và hàm SnapToTarget khi respawn.
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    [Header("=== Mục tiêu theo dõi ===")]
    [Tooltip("Kéo chú ếch vào đây")]
    public Transform target;

    [Header("=== Chế độ Lock & Chỉ di chuyển khi ếch di chuyển ===")]
    [Tooltip("Khóa chặt góc nhìn theo con ếch")]
    public bool lockToFrog = true;

    [Tooltip("Camera CHỈ di chuyển khi con ếch thực sự di chuyển (vận tốc hoặc tọa độ thay đổi)")]
    public bool moveOnlyWhenFrogMoves = true;

    [Tooltip("Ngưỡng vận tốc tối thiểu để coi là con ếch đang di chuyển")]
    public float movementVelocityThreshold = 0.05f;

    [Header("=== Tinh chỉnh bám sát ===")]
    [Tooltip("Thời gian làm mượt (càng nhỏ bám càng dính chặt, 0.05 - 0.12f là rất dính và mượt)")]
    public float smoothTime = 0.08f;

    [Tooltip("Độ lệch vị trí so với ếch (nâng trục Y để quan sát phía trên)")]
    public Vector2 offset = new Vector2(0f, 1.2f);

    [Header("=== Giới hạn khung hình ===")]
    [Tooltip("Cố định trục X ở giữa phòng nếu muốn giữ khung ngang")]
    public bool lockXAxis = false;
    public float fixedXPosition = 0f;

    [Tooltip("Giới hạn mép trái và mép phải của căn phòng")]
    public float minX = -12f;
    public float maxX = 12f;

    [Tooltip("Giới hạn đáy sàn nhà (không cho camera tụt xuống dưới sàn)")]
    public float minY = 1.0f;

    // Cache tham chiếu
    private Rigidbody2D targetRb;
    private FrogController frogController;
    private Vector3 lastTargetPos;
    private Vector3 currentVelocity = Vector3.zero;

    private void Start()
    {
        FindFrogTargetIfNeeded();

        if (target != null)
        {
            lastTargetPos = target.position;
            SnapToTarget();
        }
    }

    private void FindFrogTargetIfNeeded()
    {
        if (target == null)
        {
            FrogController frog = FindAnyObjectByType<FrogController>();
            if (frog != null)
            {
                target = frog.transform;
                frogController = frog;
                targetRb = frog.GetComponent<Rigidbody2D>();
            }
        }
        else
        {
            if (frogController == null) frogController = target.GetComponent<FrogController>();
            if (targetRb == null) targetRb = target.GetComponent<Rigidbody2D>();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindFrogTargetIfNeeded();
            if (target == null) return;
        }

        bool isFrogMoving = CheckIsFrogMoving();

        if (moveOnlyWhenFrogMoves && !isFrogMoving)
        {
            currentVelocity = Vector3.zero;
            lastTargetPos = target.position;
            return;
        }

        float targetX;
        if (lockXAxis)
        {
            targetX = fixedXPosition;
        }
        else
        {
            targetX = Mathf.Clamp(target.position.x + offset.x, minX, maxX);
        }

        float targetY = Mathf.Max(target.position.y + offset.y, minY);
        Vector3 desiredPosition = new Vector3(targetX, targetY, transform.position.z);

        if (lockToFrog && smoothTime <= 0.001f)
        {
            transform.position = desiredPosition;
            currentVelocity = Vector3.zero;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);
        }

        lastTargetPos = target.position;
    }

    private bool CheckIsFrogMoving()
    {
        if (targetRb != null)
        {
            if (targetRb.linearVelocity.sqrMagnitude > movementVelocityThreshold * movementVelocityThreshold)
            {
                return true;
            }
        }

        if (frogController != null)
        {
            if (frogController.IsMoving ||
                frogController.CurrentState == FrogController.FrogState.InAir ||
                frogController.CurrentState == FrogController.FrogState.Bounced)
            {
                return true;
            }
        }

        float delta = (target.position - lastTargetPos).sqrMagnitude;
        if (delta > 0.0001f)
        {
            return true;
        }

        return false;
    }

    public void SnapToTarget()
    {
        if (target == null) return;

        float targetX = lockXAxis ? fixedXPosition : Mathf.Clamp(target.position.x + offset.x, minX, maxX);
        float targetY = Mathf.Max(target.position.y + offset.y, minY);
        transform.position = new Vector3(targetX, targetY, transform.position.z);
        currentVelocity = Vector3.zero;
        lastTargetPos = target.position;
    }
}
