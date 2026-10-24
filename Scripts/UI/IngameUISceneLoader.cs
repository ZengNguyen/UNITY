using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Script gắn vào các màn chơi lv1, lv2, lv3:
/// - Tự động tải scene "Ingame UI" (hoặc "IngameUI") theo chế độ Additive nếu scene đó chưa được tải.
/// - Nếu người chơi không dùng scene riêng cho Ingame UI, script sẽ tự động khởi tạo IngameUIController vào Canvas hiện tại!
/// </summary>
public class IngameUISceneLoader : MonoBehaviour
{
    [Header("=== Tên Scene Ingame UI ===")]
    [Tooltip("Tên scene chứa UI in-game trong Build Settings (mặc định 'Ingame UI')")]
    public string ingameUISceneName = "Ingame UI";
    public string fallbackSceneName = "IngameUI";

    private void Start()
    {
        EnsureIngameUILoaded();
    }

    public void EnsureIngameUILoaded()
    {
        // 1. Nếu đã có IngameUIController tồn tại trong scene -> Đã sẵn sàng!
        if (IngameUIController.Instance != null) return;

        // 2. Kiểm tra xem scene Ingame UI đã được load chưa
        Scene uiScene = SceneManager.GetSceneByName(ingameUISceneName);
        if (uiScene.isLoaded) return;

        Scene fallbackUiScene = SceneManager.GetSceneByName(fallbackSceneName);
        if (fallbackUiScene.isLoaded) return;

        // 3. Thử tải scene UI theo chế độ Additive
        StartCoroutine(LoadUISceneRoutine());
    }

    private IEnumerator LoadUISceneRoutine()
    {
        bool loaded = false;

        // Thử tên 1
        if (Application.CanStreamedLevelBeLoaded(ingameUISceneName))
        {
            var op = SceneManager.LoadSceneAsync(ingameUISceneName, LoadSceneMode.Additive);
            while (!op.isDone) yield return null;
            loaded = true;
            Debug.Log($"[IngameUISceneLoader] Đã tải thành công scene UI: {ingameUISceneName}");
        }
        else if (Application.CanStreamedLevelBeLoaded(fallbackSceneName))
        {
            var op = SceneManager.LoadSceneAsync(fallbackSceneName, LoadSceneMode.Additive);
            while (!op.isDone) yield return null;
            loaded = true;
            Debug.Log($"[IngameUISceneLoader] Đã tải thành công scene UI: {fallbackSceneName}");
        }

        // 4. Nếu không thể tải bằng scene (chưa add vào build settings), tự tạo Controller trực tiếp
        if (!loaded && IngameUIController.Instance == null)
        {
            Debug.Log("[IngameUISceneLoader] Tự động tạo IngameUIController trực tiếp trong màn chơi...");
            GameObject uiHost = new GameObject("IngameUI_Host");
            uiHost.AddComponent<IngameUIController>();
        }
    }
}
