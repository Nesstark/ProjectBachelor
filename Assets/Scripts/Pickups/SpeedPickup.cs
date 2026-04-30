using UnityEngine;

// ============================================================
//  SpeedPickup.cs — Permanently increases player move speed
// ============================================================
public class SpeedPickup : PickupBase
{
    [Header("Speed Pickup")]
    [SerializeField] private float speedBonus = 1.5f;  // small increment per pickup

    protected override void OnPickedUp(GameObject player)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) return;

        controller.AddMoveSpeed(speedBonus);
        Debug.Log($"[SpeedPickup] Move speed permanently increased by {speedBonus}");
        AudioManager.Instance?.Play("PickupSpeed");
    }
}