using UnityEngine;

/// <summary>
/// Zoom cận vào hộp tutorial lúc đầu màn. Khi ếch thoát khỏi hộp thì zoom rộng ra nhanh
/// và giao lại cho CameraFollow2D bám theo ếch.
/// </summary>
[RequireComponent(typeof(Camera))]
public class TutorialCameraZoom : MonoBehaviour
{
    [Header("=== Vùng hộp tutorial (tọa độ thế giới) ===")]
    [Tooltip("Tâm camera khi ở trong hộp")]
    public Vector2 boxCenter = new Vector2(-8.0f, 2.4f);

    [Tooltip("Vùng được tính là 'đang ở trong hộp' (x, y, rộng, cao)")]
    public Rect boxArea = new Rect(-10.8f, -1f, 5.6f, 7f);

    [Header("=== Zoom ===")]
    [Tooltip("Orthographic Size khi ở trong hộp (nhỏ = cận hơn)")]
    public float tutorialSize = 3.0f;

    [Tooltip("Orthographic Size sau khi thoát hộp")]
    public float normalSize = 6.0f;

    [Tooltip("Thời gian zoom cận vào hộp (giây)")]
    public float zoomInTime = 0.35f;

    [Tooltip("Thời gian zoom rộng ra khi thoát hộp (giây) - càng nhỏ càng nhanh")]
    public float zoomOutTime = 0.25f;

    private Camera cam;
    private CameraFollow2D follow;
    private Transform frog;
    private bool inBox = true;
    private float sizeVelocity;
    private Vector3 posVelocity;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        follow = GetComponent<CameraFollow2D>();
    }

    private void Start()
    {
        // Bắt đầu màn: vào thẳng chế độ cận hộp, không có hiệu ứng trượt
        cam.orthographic = true;
        cam.orthographicSize = tutorialSize;
        transform.position = new Vector3(boxCenter.x, boxCenter.y, -10f);
        if (follow != null) follow.enabled = false;
        inBox = true;
    }

    private void LateUpdate()
    {
        if (frog == null)
        {
            GameObject f = GameObject.Find("Frog_Player");
            if (f == null) return;
            frog = f.transform;
        }

        bool nowInBox = boxArea.Contains(frog.position);

        // Đổi trạng thái vào/ra hộp
        if (nowInBox != inBox)
        {
            inBox = nowInBox;
            if (follow != null) follow.enabled = !inBox; // Ra khỏi hộp -> camera bám theo ếch
        }

        if (inBox)
        {
            // Khóa camera giữa hộp, zoom cận
            Vector3 target = new Vector3(boxCenter.x, boxCenter.y, -10f);
            transform.position = Vector3.SmoothDamp(transform.position, target, ref posVelocity, zoomInTime);
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, tutorialSize, ref sizeVelocity, zoomInTime);
        }
        else
        {
            // Zoom rộng ra nhanh, vị trí do CameraFollow2D lo
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, normalSize, ref sizeVelocity, zoomOutTime);
        }
    }
}