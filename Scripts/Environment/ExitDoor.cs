using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cánh cửa lớn ở cuối màn chơi (Exit Door):
/// - Ngăn người chơi đi tiếp bằng một collider chắn đường.
/// - Có tay nắm cửa (DoorKnob).
/// - Người chơi phải bắn lưỡi dính vào tay nắm cửa và kéo giật ra để mở cửa.
/// - Sau khi cửa mở, người chơi bước qua ô cửa sẽ tự động chuyển sang màn tiếp theo:
///   + LV1 -> "LV 2" (hoặc LV2)
///   + LV 2 -> "LV 3" (hoặc LV3)
///   + LV 3 -> "Main Menu"
/// </summary>
public class ExitDoor : MonoBehaviour
{
    [Header("=== Cấu hình chuyển màn ===")]
    [Tooltip("Tên scene mục tiêu khi người chơi bước qua cửa")]
    public string targetSceneName = "LV 2";

    [Header("=== Trạng thái cánh cửa ===")]
    public bool isOpened = false;

    [Header("=== Tham chiếu thành phần ===")]
    public Transform doorPanelTransform;
    public Collider2D doorBlockerCollider;
    public Collider2D passageTriggerCollider;
    public DoorKnob doorKnob;

    [Header("=== Âm thanh & Hoạt họa ===")]
    public AudioSource audioSource;
    private static AudioClip proceduralDoorOpenClip;
    private bool isTransitioning = false;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Tự động tìm targetSceneName nếu chưa gán
        if (string.IsNullOrEmpty(targetSceneName))
        {
            string currentScene = SceneManager.GetActiveScene().name.ToLower().Replace(" ", "");
            if (currentScene.Contains("lv1") || currentScene.Contains("level1"))
            {
                targetSceneName = "LV 2";
            }
            else if (currentScene.Contains("lv2") || currentScene.Contains("level2"))
            {
                targetSceneName = "LV 3";
            }
            else
            {
                targetSceneName = "Main Menu";
            }
        }
    }

    /// <summary>
    /// Kích hoạt khi chú ếch dùng lưỡi kéo tay nắm cửa
    /// </summary>
    public void OpenDoor()
    {
        if (isOpened) return;
        isOpened = true;

        Debug.Log("🚪 [ExitDoor] Tay nắm cửa đã bị kéo bởi lưỡi ếch! Cửa đang mở ra...");

        if (doorBlockerCollider != null)
        {
            doorBlockerCollider.enabled = false;
        }

        if (audioSource != null)
        {
            audioSource.PlayOneShot(GetOrCreateDoorOpenClip(), 0.95f);
        }

        StartCoroutine(OpenDoorAnimationRoutine());
    }

    private IEnumerator OpenDoorAnimationRoutine()
    {
        if (doorPanelTransform != null)
        {
            Vector3 startPos = doorPanelTransform.localPosition;
            Vector3 targetPos = startPos + new Vector3(0f, 3.2f, 0f); // Cửa trượt nâng lên
            float duration = 0.85f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float ease = Mathf.SmoothStep(0f, 1f, t);
                doorPanelTransform.localPosition = Vector3.Lerp(startPos, targetPos, ease);
                elapsed += Time.deltaTime;
                yield return null;
            }

            doorPanelTransform.localPosition = targetPos;
        }

        // Tạo hiệu ứng ánh sáng ấm áp chào đón qua cửa
        SpawnOpenDoorEffect();
    }

    private void SpawnOpenDoorEffect()
    {
        GameObject textObj = new GameObject("Door_Open_Text");
        textObj.transform.position = transform.position + Vector3.up * 3.5f;
        var tm = textObj.AddComponent<TextMesh>();
        tm.text = "✨ CỬA ĐÃ MỞ! BƯỚC QUA ĐỂ SANG MÀN ✨";
        tm.fontSize = 24;
        tm.characterSize = 0.08f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.9f, 0.3f);

        Destroy(textObj, 3.5f);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isOpened || isTransitioning) return;

        if (collision.CompareTag("Player") || collision.GetComponent<FrogController>() != null)
        {
            StartCoroutine(TransitionToNextLevelRoutine(collision.gameObject));
        }
    }

    private IEnumerator TransitionToNextLevelRoutine(GameObject player)
    {
        isTransitioning = true;
        Debug.Log($"🎉 [ExitDoor] Chú ếch đã bước qua cánh cửa! Bắt đầu chuyển màn sang: {targetSceneName}");

        // Dừng chuyển động của người chơi để hiệu ứng mượt mà
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
        }

        // Tạo chữ thông báo hoàn thành màn
        GameObject winTextObj = new GameObject("Win_Level_Text");
        winTextObj.transform.position = player.transform.position + Vector3.up * 1.5f;
        var tm = winTextObj.AddComponent<TextMesh>();
        string currentScene = SceneManager.GetActiveScene().name;
        tm.text = $"🎉 ĐÃ HOÀN THÀNH {currentScene.ToUpper()}!\nĐANG TIẾN VÀO {targetSceneName.ToUpper()}...";
        tm.fontSize = 28;
        tm.characterSize = 0.09f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = Color.green;

        yield return new WaitForSeconds(1.2f);

        // Chuyển sang màn tiếp theo an toàn
        LoadTargetSceneSafely(targetSceneName);
    }

    private void LoadTargetSceneSafely(string sceneName)
    {
        try
        {
            // Kiểm tra xem scene có trong Build Settings không
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

            // Thử phiên bản không dấu cách (vd: "LV2" thay vì "LV 2")
            string altName = sceneName.Replace(" ", "");
            if (Application.CanStreamedLevelBeLoaded(altName))
            {
                SceneManager.LoadScene(altName);
                return;
            }

            // Thử thêm khoảng trắng (vd: "LV 2" thay vì "LV2")
            if (sceneName.StartsWith("LV") && sceneName.Length == 3 && char.IsDigit(sceneName[2]))
            {
                string spaceName = "LV " + sceneName[2];
                if (Application.CanStreamedLevelBeLoaded(spaceName))
                {
                    SceneManager.LoadScene(spaceName);
                    return;
                }
            }

            Debug.LogWarning($"[ExitDoor] Không tìm thấy scene '{sceneName}' trong Build Settings! Đang trở về Main Menu.");
            SceneManager.LoadScene("Main Menu");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ExitDoor] Lỗi khi chuyển sang scene '{sceneName}': {ex.Message}");
            SceneManager.LoadScene(0); // Load scene đầu tiên
        }
    }

    private static AudioClip GetOrCreateDoorOpenClip()
    {
        if (proceduralDoorOpenClip != null) return proceduralDoorOpenClip;

        int sampleRate = 44100;
        float duration = 0.75f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Sin(t / duration * Mathf.PI);

            // Tiếng kẽo kẹt mở cửa gỗ (Door Creak) kết hợp tần số thấp và rung gợn
            float creakFreq = Mathf.Lerp(120f, 320f, t / duration) + Mathf.Sin(2f * Mathf.PI * 35f * t) * 45f;
            float creak = Mathf.Sin(2f * Mathf.PI * creakFreq * t);
            float heavyWood = Mathf.Sin(2f * Mathf.PI * 80f * t) * 0.4f;

            samples[i] = (creak * 0.7f + heavyWood) * envelope * 0.8f;
        }

        proceduralDoorOpenClip = AudioClip.Create("Door_Open_Sound", sampleCount, 1, sampleRate, false);
        proceduralDoorOpenClip.SetData(samples, 0);
        return proceduralDoorOpenClip;
    }
}
