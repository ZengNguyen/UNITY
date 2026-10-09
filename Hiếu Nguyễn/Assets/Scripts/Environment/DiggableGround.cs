using UnityEngine;

/// <summary>
/// Bề mặt đất xốp (Chậu cây cảnh phòng khách, bồn hoa ngoài vườn):
/// - Cho phép chú ếch nhấn S (hoặc Mũi tên xuống) để đào đất chui xuống trốn.
/// - Khi chui xuống đất: Mèo và con người hoàn toàn không phát hiện được.
/// - Nhấn Space để nhảy vọt lên khỏi mặt đất.
/// </summary>
public class DiggableGround : MonoBehaviour
{
    [Tooltip("Tên loại đất (Đất mùn chậu cây, Bồn hoa ngoài vườn)")]
    public string soilName = "Đất mềm chậu cây";

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.TryGetComponent<FrogController>(out var frog))
        {
            frog.SetCanBurrow(true, this);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.TryGetComponent<FrogController>(out var frog))
        {
            frog.SetCanBurrow(false, null);
        }
    }
}
