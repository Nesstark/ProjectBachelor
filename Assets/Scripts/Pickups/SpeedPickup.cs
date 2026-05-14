using UnityEngine;

// ============================================================
//  SpeedPickup.cs — Permanently increases player move speed
//  SpeedBonus is now public so AIPlayerAgent can read it directly.
// ============================================================
public class SpeedPickup : PickupBase
{
    [Header("Speed Pickup")]
    [SerializeField] private float speedBonus = 1.5f;

    /// <summary>Exposed so AIPlayerAgent can apply the bonus locally.</summary>
    public float SpeedBonus => speedBonus;

    public override string Description =>
        $"Increase movement speed by {speedBonus} permanently.";

    protected override void OnPickedUp(GameObject player)
    {
        // PlayerController applies the bonus for human players.
        // When AIPlayerAgent is in control, PlayerController is disabled
        // and this returns null — AIPlayerAgent handles it via OnAnyPickupCollected.
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
            controller.AddMoveSpeed(speedBonus);

        PickupTracker.Instance?.Register($"Speed:{speedBonus}");
        Debug.Log($"[SpeedPickup] Move speed permanently increased by {speedBonus}");
        AudioManager.Instance?.Play("PickupSpeed");
    }
}