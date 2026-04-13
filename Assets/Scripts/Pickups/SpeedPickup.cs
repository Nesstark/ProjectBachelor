using UnityEngine;
using System.Collections;

// ============================================================
//  SpeedPickup.cs — Temporarily boosts player move speed
//  PREFAB SETUP:
//  1. Create a GameObject with a sprite or 3D model
//  2. Add a SphereCollider — Is Trigger ON
//  3. Attach this script
// ============================================================
public class SpeedPickup : PickupBase
{
    [Header("Speed Pickup")]
    [SerializeField] private float speedBonus    = 5f;   // added on top of base speed
    [SerializeField] private float duration      = 8f;   // seconds the buff lasts

    protected override void OnPickedUp(GameObject player)
    {
        PlayerSpeedBuff buff = player.GetComponent<PlayerSpeedBuff>();
        if (buff == null)
            buff = player.AddComponent<PlayerSpeedBuff>();

        buff.Activate(speedBonus, duration);
        Debug.Log($"[SpeedPickup] Speed +{speedBonus} for {duration}s");
        AudioManager.Instance?.Play("PickupSpeed");
    }
}