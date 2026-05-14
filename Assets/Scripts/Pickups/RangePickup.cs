using UnityEngine;

// ============================================================
//  RangePickup.cs — Permanently increases player attack range
//  RangeBonus is now public so AIPlayerAgent can read it directly.
// ============================================================
public class RangePickup : PickupBase
{
    [Header("Range Pickup")]
    [SerializeField] private float rangeBonus = 0.75f;

    /// <summary>Exposed so AIPlayerAgent can apply the bonus locally.</summary>
    public float RangeBonus => rangeBonus;

    public override string Description =>
        $"Increase attack range by {rangeBonus} permanently.";

    protected override void OnPickedUp(GameObject player)
    {
        // PlayerController applies the bonus for human players.
        // When AIPlayerAgent is in control, PlayerController is disabled
        // and this returns null — AIPlayerAgent handles it via OnAnyPickupCollected.
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
            controller.AddAttackRange(rangeBonus);

        PickupTracker.Instance?.Register($"Range:{rangeBonus}");
        Debug.Log($"[RangePickup] Attack range increased by {rangeBonus}");
        AudioManager.Instance?.Play("PickupRange");
    }
}