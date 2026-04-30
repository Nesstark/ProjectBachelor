using UnityEngine;

// ============================================================
//  RangePickup.cs — Permanently increases player attack range
// ============================================================
public class RangePickup : PickupBase
{
    [Header("Range Pickup")]
    [SerializeField] private float rangeBonus = 0.75f;

    protected override void OnPickedUp(GameObject player)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) return;

        controller.AddAttackRange(rangeBonus);
        Debug.Log($"[RangePickup] Attack range increased by {rangeBonus}");
        AudioManager.Instance?.Play("PickupRange");
    }
}