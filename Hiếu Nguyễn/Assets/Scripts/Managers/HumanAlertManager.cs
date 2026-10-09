using System.Collections;
using UnityEngine;

/// <summary>
/// Quản lý mức độ cảnh báo của Con Người (Người Nuôi Ếch):
/// - Tiếng ồn làm tăng mức cảnh báo (Đồ vỡ, ếch kêu Croak dọa mèo, tiếp đất rơi quá mạnh).
/// - Giảm dần theo thời gian nếu không gian yên tĩnh.
/// - Qua mỗi màn chơi, độ nhạy cảm của người nuôi sẽ tăng lên (sensitivityMultiplier).
/// - Khi đạt 100%: Người nuôi phát hiện tiếng động và chạy lại!
///   + Bàn tay người thò xuống tóm lấy chú ếch.
///   + Đen màn hình (Fade out) / Tua nhanh cutscene: "Ơ kìa! Bé ếch lại nghịch ngợm trốn ra ngoài rồi!"
///   + Chuyển cảnh: Chú ếch đã được đặt lại an toàn vào Hộp Nuôi ở tuốt dưới sàn (chỗ bắt đầu).
/// </summary>
public class HumanAlertManager : MonoBehaviour
{
    public static HumanAlertManager Instance { get; private set; }

    [Header("=== Mức độ Cảnh Báo (0 - 100%) ===")]
    [Range(0f, 100f)]
    public float currentAlert = 0f;

    [Tooltip("Tốc độ giảm độ ồn mỗi giây khi không có tiếng động")]
    public float alertDecayRate = 3.0f;

    [Header("=== Màn chơi & Độ nhạy cảm ===")]
    [Tooltip("Hệ số nhân độ nhạy: Càng qua màn sau người nuôi càng dễ để ý")]
    public float sensitivityMultiplier = 1.3f; // Mặc định cho Phòng Khách là 1.3x

    [Header("=== Vị trí Hộp Nuôi Ban Đầu (Chỗ bắt đầu) ===")]
    [Tooltip("Vị trí chiếc hộp nuôi ếch ở dưới sàn nhà")]
    public Transform startingBoxTransform;
    public Vector3 defaultBoxPosition = new Vector3(-8.0f, 0.5f, 0f);

    public bool IsAlertTriggered => currentAlert >= 100f;
    private bool isExecutingCapture = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (startingBoxTransform == null)
        {
            GameObject petBox = GameObject.Find("Pet_Terrarium_Box");
            if (petBox != null) startingBoxTransform = petBox.transform;
        }
        if (defaultBoxPosition == Vector3.zero || defaultBoxPosition.sqrMagnitude < 0.05f)
        {
            defaultBoxPosition = new Vector3(-8.0f, 0.6f, 0f);
        }
    }

    private void Update()
    {
        // Tự động lắng dịu tiếng ồn theo thời gian
        if (currentAlert > 0f && !isExecutingCapture)
        {
            currentAlert -= alertDecayRate * Time.deltaTime;
            if (currentAlert < 0f) currentAlert = 0f;
        }
    }

    /// <summary>
    /// Ghi nhận nguồn gây ồn trong phòng
    /// </summary>
    public void AddNoise(float rawNoiseAmount, Vector3 noisePosition)
    {
        float actualGain = rawNoiseAmount * sensitivityMultiplier;
        currentAlert = Mathf.Clamp(currentAlert + actualGain, 0f, 100f);

        Debug.LogWarning($"[HumanAlert] TIẾNG ỒN PHÁT RA! +{actualGain:F1}% -> Tổng cảnh báo: {currentAlert:F1}%");

        // Nếu chạm mốc 100% -> Kích hoạt người nuôi tới bắt lại vào hộp
        if (currentAlert >= 100f && !isExecutingCapture)
        {
            StartCoroutine(ExecuteOwnerCaptureRoutine());
        }
    }

    private IEnumerator ExecuteOwnerCaptureRoutine()
    {
        isExecutingCapture = true;
        Debug.LogWarning("🚨 BÁO ĐỘNG ĐỎ: NGƯỜI NUÔI PHÁT HIỆN! 'Bé ếch trốn đi đâu đấy?!'");

        FrogController frog = FindAnyObjectByType<FrogController>();
        if (frog == null)
        {
            isExecutingCapture = false;
            yield break;
        }

        Vector3 frogPos = frog.transform.position;

        // 1. Tạo bàn tay người nuôi khổng lồ hạ xuống từ trần nhà
        GameObject handObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handObj.name = "Owner_GiantHand";
        handObj.transform.position = new Vector3(frogPos.x, frogPos.y + 6.0f, frogPos.z);
        handObj.transform.localScale = new Vector3(2.0f, 3.0f, 1.2f);
        var handRend = handObj.GetComponent<Renderer>();
        if (handRend != null) handRend.material.color = new Color(0.95f, 0.76f, 0.65f); // Màu da tay người
        Destroy(handObj.GetComponent<Collider>());

        // Hạ bàn tay xuống bế ếch
        float elapsed = 0f;
        float reachDuration = 0.6f;
        Vector3 startPos = handObj.transform.position;
        Vector3 catchPos = new Vector3(frogPos.x, frogPos.y + 0.8f, frogPos.z);
        while (elapsed < reachDuration)
        {
            handObj.transform.position = Vector3.Lerp(startPos, catchPos, elapsed / reachDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Bế chú ếch nhấc lên một chút
        frog.transform.position = catchPos + Vector3.down * 0.4f;

        // 2. Kích hoạt hiệu ứng Đen Màn Hình (Fade Cutscene) & Phụ đề
        string cutsceneDialogue = "Chủ nuôi: 'Ơ kìa! Bé ếch lại nghịch ngợm trèo ra ngoài chuồng rồi!'\n...(Bị bế bỏ lại vào chiếc hộp nuôi dưới sàn)...";

        // Xác định chính xác vị trí hộp nuôi để thả ếch về (tuyệt đối không để về 0,0,0)
        Vector3 targetRespawn = new Vector3(-8.0f, 0.6f, 0f);
        if (startingBoxTransform != null && Mathf.Abs(startingBoxTransform.position.x) > 0.1f)
        {
            targetRespawn = new Vector3(startingBoxTransform.position.x, 0.6f, 0f);
        }
        else
        {
            GameObject petBox = GameObject.Find("Pet_Terrarium_Box");
            if (petBox != null)
            {
                targetRespawn = new Vector3(petBox.transform.position.x, 0.6f, 0f);
            }
            else if (frog.InitialSpawnPosition.sqrMagnitude > 0.05f)
            {
                targetRespawn = frog.InitialSpawnPosition;
            }
            else if (defaultBoxPosition.sqrMagnitude > 0.05f)
            {
                targetRespawn = defaultBoxPosition;
            }
        }

        if (LivingRoomUIManager.Instance != null)
        {
            LivingRoomUIManager.Instance.PlayCapturedCutscene(cutsceneDialogue, () =>
            {
                // Khi màn hình đen hoàn toàn: Đặt chú ếch lại vào trong hộp
                frog.ResetToStartingBox(targetRespawn);
                Destroy(handObj);
            });
        }
        else
        {
            // Fallback nếu không có UI
            yield return new WaitForSeconds(1.0f);
            frog.ResetToStartingBox(targetRespawn);
            Destroy(handObj);
        }

        // Đợi cutscene hoàn tất
        yield return new WaitForSeconds(2.0f);

        currentAlert = 0f; // Reset cảnh báo về 0 vì đã bắt ếch vào hộp yên vị
        isExecutingCapture = false;
    }
}
