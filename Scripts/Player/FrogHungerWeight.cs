using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý độ đói (Hunger) và trọng lượng cơ thể (Weight) của chú ếch.
/// - Ăn ruồi: Tăng no, tăng trọng lượng tạm thời.
/// - Theo thời gian: Thức ăn tiêu hóa, độ đói tăng dần (thanh no tụt), trọng lượng trở lại bình thường.
/// - Trọng lượng nặng ảnh hưởng:
///   + Lực nhảy nặng nề hơn.
///   + Không bám được vào Gel bám tường (bị tuột hoặc rơi).
/// </summary>
public class FrogHungerWeight : MonoBehaviour
{
    [Header("=== Cân nặng (Body Weight) ===")]
    [Tooltip("Trọng lượng cơ bản ban đầu (kg)")]
    public float baseWeight = 1.0f;

    [Tooltip("Trọng lượng tăng thêm tối đa khi no căng bụng")]
    public float maxExtraWeight = 1.2f;

    [Header("=== Thanh Đói & No (Hunger / Fullness) ===")]
    [Tooltip("Mức no hiện tại (0: Đói cồn cào, 100: No căng tròn bụng)")]
    [Range(0f, 100f)]
    public float fullness = 20f;

    [Tooltip("Tốc độ tiêu hóa mỗi giây (độ no giảm dần)")]
    public float digestionRate = 2.5f;

    [Tooltip("Trọng lượng ngưỡng mà Gel bám tường bắt đầu bị quá tải")]
    public float gelOverloadWeight = 1.6f;

    // Trọng lượng thực tế hiện tại
    public float CurrentWeight => baseWeight + (fullness / 100f) * maxExtraWeight;
    public bool IsOverweightForGel => CurrentWeight >= gelOverloadWeight;
    public float FullnessPercentage => Mathf.Clamp01(fullness / 100f);

    private FrogController frogController;

    private void Awake()
    {
        frogController = GetComponent<FrogController>();
    }

    private void Update()
    {
        // Tiêu hóa thức ăn theo thời gian
        if (fullness > 0f)
        {
            fullness -= digestionRate * Time.deltaTime;
            if (fullness < 0f) fullness = 0f;
        }
    }

    /// <summary>
    /// Ăn/Nuốt một con ruồi vào dạ dày
    /// </summary>
    public void DigestFly(float hungerGain = 35f)
    {
        fullness = Mathf.Clamp(fullness + hungerGain, 0f, 100f);
        Debug.Log($"[FrogHungerWeight] Ếch vừa ăn 1 con ruồi! Độ no: {fullness:F0}% | Trọng lượng: {CurrentWeight:F2}kg");

        // Hiệu ứng phình to nhẹ cơ thể khi no
        if (frogController != null)
        {
            frogController.PulseVisualWhenFed();
        }
    }

    /// <summary>
    /// Đốt cháy năng lượng nhanh (khi nhảy liên tục)
    /// </summary>
    public void BurnEnergy(float amount)
    {
        fullness = Mathf.Max(0f, fullness - amount);
    }
}
