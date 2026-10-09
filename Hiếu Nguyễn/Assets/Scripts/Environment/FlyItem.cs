using UnityEngine;

/// <summary>
/// Ruồi bay lượn lờ trong phòng:
/// - Bay lượn tự do quanh vị trí ban đầu (dạng sóng hình sin).
/// - Chú ếch có thể đến gần và nhấn E để táp vào miệng hoặc nuốt.
/// </summary>
public class FlyItem : MonoBehaviour
{
    [Header("=== Hành vi bay lượn ===")]
    public float flySpeed = 1.5f;
    public float hoverRadius = 1.2f;
    public float flutterFrequency = 4f;

    private Vector3 spawnPosition;
    private float randomOffset;
    private bool isEaten = false;

    public bool IsEaten => isEaten;

    private void Start()
    {
        spawnPosition = transform.position;
        randomOffset = Random.Range(0f, 10f);
    }

    private void Update()
    {
        if (isEaten) return;

        // Chuyển động lượn lờ quanh tâm
        float time = Time.time * flySpeed + randomOffset;
        float x = Mathf.Sin(time) * hoverRadius;
        float y = Mathf.Sin(time * flutterFrequency) * (hoverRadius * 0.4f);

        transform.position = spawnPosition + new Vector3(x, y, 0f);
    }

    /// <summary>
    /// Gọi khi ếch táp trúng ruồi
    /// </summary>
    public void OnCaughtByFrog()
    {
        isEaten = true;
        gameObject.SetActive(false);
    }

    public void Respawn(Vector3 pos)
    {
        transform.position = pos;
        spawnPosition = pos;
        isEaten = false;
        gameObject.SetActive(true);
    }
}

/// <summary>
/// Ruồi bị nhè ra nằm trên sàn -> Trở thành mồi câu mèo cực thơm!
/// </summary>
public class FlyBait : MonoBehaviour
{
    [Tooltip("Thời gian tồn tại của mồi ruồi trước khi biến mất")]
    public float lifetime = 15f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}

/// <summary>
/// Đồ vật nhỏ có thể ngậm vào miệng ếch (viên sỏi, nắp chai, chìa khóa)
/// </summary>
public class GrabbableItem : MonoBehaviour
{
    public string itemName = "Vật nhỏ";
}
