using System.Collections;
using UnityEngine;

/// <summary>
/// Quản lý Khu Vực Chiếc Hộp Tutorial (Starting Box Zone) ở Level 1:
/// 1. Là nơi người chơi bắt đầu game & cũng là nơi bị bắt về khi chủ phát hiện.
/// 2. Chứa bệ đất để test cơ chế trốn (phím S đào đất).
/// 3. Chứa 2 mặt phẳng lơ lửng để tập nhảy:
///    - Mặt phẳng 1 (thấp): tập gồng nhảy cơ bản.
///    - Mặt phẳng 2 (cao): từ đây mới đủ độ cao để nhảy vọt qua thành hộp vào Level 1.
/// 4. TRONG HỘP LÀ VÙNG AN TOÀN TUYỆT ĐỐI (Noise-Free Zone):
///    - Không bị ảnh hưởng bởi cơ chế làm ồn cảnh báo chủ nuôi.
///    - Người chơi có thể kêu Croak (F), rơi mạnh thoải mái mà không lo bị chủ bắt!
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TutorialBoxZone : MonoBehaviour
{
    public static TutorialBoxZone Instance { get; private set; }

    /// <summary>
    /// Trạng thái người chơi có đang ở trong chiếc hộp nuôi hay không
    /// </summary>
    public static bool IsPlayerInsideBox { get; private set; } = true;

    [Header("=== Điểm hồi sinh trong hộp ===")]
    [Tooltip("Vị trí đặt chú ếch khi bắt đầu hoặc bị chủ nuôi bắt về")]
    public Transform boxRespawnPoint;

    [Header("=== Tham chiếu các bộ phận của hộp ===")]
    public Transform groundSoilPlatform;
    public Transform floatingPlatform1;
    public Transform floatingPlatform2;
    public Transform leftWall;
    public Transform rightWall;

    [Header("=== Thông báo trạng thái ===")]
    public bool showFloatingTutorialTips = true;
    private GameObject tipTextObject;
    private TextMesh tipTextMesh;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        // Khi mới vào game, chú ếch luôn ở trong hộp
        IsPlayerInsideBox = true;
        CreateInBoxTutorialTip();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.GetComponent<FrogController>() != null)
        {
            IsPlayerInsideBox = true;
            Debug.Log("📦 [TutorialBox] Ếch đang ở TRONG HỘP: Vùng an toàn, làm ồn không bị cảnh báo chủ!");
            UpdateTipText("📦 KHU VỰC HỘP TUTORIAL (AN TOÀN)\n- Thử nhấn S trên đất để đào đất trốn\n- Nhảy lên Bục 1 -> Bục 2 để nhảy ra khỏi hộp!");
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.GetComponent<FrogController>() != null)
        {
            IsPlayerInsideBox = false;
            Debug.Log("🚪 [TutorialBox] Ếch ĐÃ NHẢY RA KHỎI HỘP -> BẮT ĐẦU LEVEL 1! Chú ý: Làm ồn sẽ báo động chủ!");
            UpdateTipText("🚨 ĐÃ THOÁT KHỎI HỘP - BẮT ĐẦU MÀN 1!\nCẩn thận: Tiếng ồn lúc này sẽ cảnh báo chủ nuôi!");
            StartCoroutine(HideTipAfterSeconds(4.0f));
        }
    }

    private void CreateInBoxTutorialTip()
    {
        if (!showFloatingTutorialTips) return;

        tipTextObject = new GameObject("Tutorial_FloatingTip");
        tipTextObject.transform.position = transform.position + new Vector3(0f, 3.8f, 0f);
        tipTextMesh = tipTextObject.AddComponent<TextMesh>();
        tipTextMesh.text = "📦 HỘP TUTORIAL (Vùng An Toàn)\n- Đất dưới chân: Nhấn S để đào đất trốn!\n- Nhảy lên Bục 1 -> Bục 2 để nhảy ra khỏi hộp!";
        tipTextMesh.fontSize = 20;
        tipTextMesh.characterSize = 0.075f;
        tipTextMesh.alignment = TextAlignment.Center;
        tipTextMesh.anchor = TextAnchor.MiddleCenter;
        tipTextMesh.color = new Color(0.9f, 0.95f, 0.4f, 0.9f);
    }

    private void UpdateTipText(string message)
    {
        if (tipTextMesh != null)
        {
            tipTextMesh.gameObject.SetActive(true);
            tipTextMesh.text = message;
        }
    }

    private IEnumerator HideTipAfterSeconds(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (tipTextMesh != null && !IsPlayerInsideBox)
        {
            tipTextMesh.gameObject.SetActive(false);
        }
    }

    public Vector3 GetRespawnPosition()
    {
        if (boxRespawnPoint != null) return boxRespawnPoint.position;
        return transform.position;
    }

    private void OnDestroy()
    {
        IsPlayerInsideBox = false;
    }
}
