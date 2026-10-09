using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý túi đồ trong miệng chú ếch (Mouth Inventory):
/// - Miệng to nên có thể ngậm tối đa 2 đồ vật (Ruồi, mồi nhử, đồ vật nhỏ).
/// - Phím E: Táp/Ngậm đồ vật hoặc con ruồi gần đó vào miệng (hoặc nuốt vào bụng nếu giữ phím).
/// - Phím Q: Nhè/Phun vật phẩm trong miệng ra phía trước (đặc biệt là ruồi làm mồi dụ mèo!).
/// </summary>
public class FrogMouthInventory : MonoBehaviour
{
    [System.Serializable]
    public class StoredItem
    {
        public string itemName;
        public Sprite itemIcon;
        public GameObject itemPrefab;
        public bool isFly;

        public StoredItem(string name, bool fly, GameObject prefab = null, Sprite icon = null)
        {
            itemName = name;
            isFly = fly;
            itemPrefab = prefab;
            itemIcon = icon;
        }
    }

    [Header("=== Sức chứa miệng ===")]
    [Tooltip("Số lượng đồ vật tối đa có thể ngậm trong miệng")]
    public int maxCapacity = 2;

    [Header("=== Danh sách đồ trong miệng ===")]
    public List<StoredItem> mouthItems = new List<StoredItem>();

    [Header("=== Phím bấm & Tương tác ===")]
    public KeyCode pickupKey = KeyCode.E;   // Phím ngậm/ăn
    public KeyCode spitKey = KeyCode.Q;     // Phím nhè ra
    public float pickupRadius = 1.3f;       // Khoảng cách táp tới
    public float spitForce = 6f;            // Lực bắn vật phẩm ra

    [Header("=== Điểm nhổ đồ ra (Mouth Point) ===")]
    public Transform mouthTransform;

    [Header("=== Prefab ruồi mồi khi nhè ra đất ===")]
    public GameObject flyBaitPrefab;

    private FrogController frogController;
    private FrogHungerWeight hungerWeight;

    public int ItemCount => mouthItems.Count;
    public bool IsFull => mouthItems.Count >= maxCapacity;

    private void Awake()
    {
        frogController = GetComponent<FrogController>();
        hungerWeight = GetComponent<FrogHungerWeight>();
        if (mouthTransform == null) mouthTransform = transform;
    }

    private void Update()
    {
        // 1. Phím E: Táp đồ gần đó
        if (GameInput.GetKeyDown(pickupKey))
        {
            TryPickupNearbyItem();
        }

        // 2. Phím Q: Nhè đồ trong miệng ra
        if (GameInput.GetKeyDown(spitKey))
        {
            TrySpitItem();
        }
    }

    /// <summary>
    /// Thử táp đồ vật hoặc ruồi ở gần
    /// </summary>
    public void TryPickupNearbyItem()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, pickupRadius);
        foreach (var hit in hits)
        {
            // Kiểm tra xem có phải con ruồi không
            FlyItem fly = hit.GetComponent<FlyItem>();
            if (fly != null && !fly.IsEaten)
            {
                if (!IsFull)
                {
                    // Ngậm ruồi vào miệng
                    fly.OnCaughtByFrog();
                    mouthItems.Add(new StoredItem("Ruồi thơm ngon", true, flyBaitPrefab));
                    Debug.Log($"[MouthInventory] Đã ngậm 1 con ruồi vào miệng! Hiện có: {mouthItems.Count}/{maxCapacity}");
                    return;
                }
                else
                {
                    // Miệng đã đầy 2 món -> Tự động nuốt thẳng vào bụng luôn!
                    fly.OnCaughtByFrog();
                    if (hungerWeight != null) hungerWeight.DigestFly(35f);
                    Debug.Log("[MouthInventory] Miệng đầy, nuốt thẳng ruồi vào dạ dày!");
                    return;
                }
            }

            // Kiểm tra các item khác có thể ngậm
            GrabbableItem grabbable = hit.GetComponent<GrabbableItem>();
            if (grabbable != null && !IsFull)
            {
                mouthItems.Add(new StoredItem(grabbable.itemName, false, grabbable.gameObject));
                grabbable.gameObject.SetActive(false);
                Debug.Log($"[MouthInventory] Đã ngậm {grabbable.itemName} vào miệng!");
                return;
            }
        }
    }

    /// <summary>
    /// Nhè vật phẩm trên cùng ra phía trước theo hướng nhìn
    /// </summary>
    public void TrySpitItem()
    {
        if (mouthItems.Count == 0) return;

        // Lấy vật phẩm mới nhất trong miệng ra
        int lastIndex = mouthItems.Count - 1;
        StoredItem item = mouthItems[lastIndex];
        mouthItems.RemoveAt(lastIndex);

        int facing = frogController != null ? frogController.FacingDirection : 1;
        Vector2 spitDirection = new Vector2(facing, 0.4f).normalized;
        Vector3 spawnPos = mouthTransform.position + (Vector3)(spitDirection * 0.5f);

        if (item.isFly)
        {
            // Tạo ra 1 mồi ruồi nằm trên sàn để dụ mèo!
            GameObject baitObj = null;
            if (flyBaitPrefab != null)
            {
                baitObj = Instantiate(flyBaitPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // Nếu chưa có prefab, tự tạo 1 mồi ruồi dạng hình tròn nhỏ
                baitObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                baitObj.name = "FlyBait_Dropped";
                baitObj.transform.position = spawnPos;
                baitObj.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                Destroy(baitObj.GetComponent<Collider>());
                var col2d = baitObj.AddComponent<CircleCollider2D>();
                col2d.radius = 0.15f;
                var rb2d = baitObj.AddComponent<Rigidbody2D>();
                rb2d.mass = 0.2f;
                baitObj.AddComponent<FlyBait>();
            }

            if (baitObj.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.linearVelocity = spitDirection * spitForce;
            }

            Debug.Log("[MouthInventory] Đã nhè con ruồi ra sàn làm mồi nhử mèo!");
        }
        else if (item.itemPrefab != null)
        {
            item.itemPrefab.transform.position = spawnPos;
            item.itemPrefab.SetActive(true);
            if (item.itemPrefab.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.linearVelocity = spitDirection * spitForce;
            }
        }
    }

    /// <summary>
    /// Nuốt 1 món từ trong miệng vào bụng (tiêu hóa)
    /// </summary>
    public void SwallowItemToStomach(int index)
    {
        if (index < 0 || index >= mouthItems.Count) return;
        StoredItem item = mouthItems[index];
        if (item.isFly && hungerWeight != null)
        {
            hungerWeight.DigestFly(35f);
            mouthItems.RemoveAt(index);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
