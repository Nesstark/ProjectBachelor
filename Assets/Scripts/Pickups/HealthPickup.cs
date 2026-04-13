using UnityEngine;

// ============================================================
//  HealthPickup.cs — Heals the player by healAmount
//  PREFAB SETUP:
//  1. Create a GameObject with a sprite or 3D model
//  2. Add a SphereCollider — Is Trigger ON
//  3. Attach this script
// ============================================================
public class HealthPickup : PickupBase
{
    [Header("Health Pickup")]
    [SerializeField] private float healAmount = 40f;

    protected override void OnPickedUp(GameObject player)
    {
        GameManager.Instance?.HealPlayer(healAmount);
        Debug.Log($"[HealthPickup] Healed player for {healAmount}");
        AudioManager.Instance?.Play("PickupHealth");
    }
}