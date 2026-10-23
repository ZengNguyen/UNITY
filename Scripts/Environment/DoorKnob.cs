using System.Collections;
using UnityEngine;

/// <summary>
/// Tay nắm của cánh cửa lớn (Door Knob):
/// - Có collider nhận diện va chạm từ lưỡi của chú ếch (FrogTongue).
/// - Khi lưỡi ếch chạm trúng và kéo, sẽ kích hoạt xoay/giật tay nắm và ra lệnh cho ExitDoor mở cửa.
/// </summary>
public class DoorKnob : MonoBehaviour
{
    [Header("=== Cánh cửa liên kết ===")]
    public ExitDoor exitDoor;

    [Header("=== Trạng thái tay nắm ===")]
    public bool isPulled = false;

    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;

    public bool IsOpened => exitDoor != null && exitDoor.isOpened;

    private void Awake()
    {
        originalLocalPos = transform.localPosition;
        originalLocalRot = transform.localRotation;

        if (exitDoor == null)
        {
            exitDoor = GetComponentInParent<ExitDoor>();
        }
    }

    /// <summary>
    /// Được gọi bởi FrogTongue khi lưỡi bắn trúng và kéo tay nắm cửa
    /// </summary>
    public void OnPulledByTongue(Vector2 pullOrigin)
    {
        if (isPulled) return;
        isPulled = true;

        Debug.Log("✊ [DoorKnob] Tay nắm cửa đã được chú ếch kéo giật!");

        StartCoroutine(PullKnobRoutine(pullOrigin));

        if (exitDoor != null)
        {
            exitDoor.OpenDoor();
        }
    }

    private IEnumerator PullKnobRoutine(Vector2 pullOrigin)
    {
        Vector2 dir = (pullOrigin - (Vector2)transform.position).normalized;
        Vector3 pullOffset = new Vector3(dir.x * 0.15f, dir.y * 0.1f, 0f);

        // Hiệu ứng giật tay nắm theo hướng kéo
        transform.localPosition = originalLocalPos + pullOffset;
        transform.localRotation = Quaternion.Euler(0f, 0f, -25f);

        yield return new WaitForSeconds(0.35f);

        // Trả về vị trí cũ
        transform.localPosition = originalLocalPos;
        transform.localRotation = originalLocalRot;
    }
}
