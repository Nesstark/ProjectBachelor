using UnityEngine;

// ============================================================
//  RangePickup.cs — Permanently increases player attack range
//  PREFAB SETUP:
//  1. Create a GameObject with a sprite or 3D model
//  2. Add a SphereCollider — Is Trigger ON
//  3. Attach this script
// ============================================================
public class RangePickup : PickupBase
{
    [Header("Range Pickup")]
    [SerializeField] private float rangeBonus = 1.5f;

    protected override void OnPickedUp(GameObject player)
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.IncreaseAttackRange(rangeBonus);
        Debug.Log($"[RangePickup] Attack range increased by {rangeBonus}");
        AudioManager.Instance?.Play("PickupRange");
    }
}