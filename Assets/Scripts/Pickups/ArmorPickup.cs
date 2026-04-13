using UnityEngine;

// ============================================================
//  ArmorPickup.cs — Blocks the first hit the player takes
//  Works by temporarily patching GameManager.ApplyDamageToPlayer
//  via a PlayerArmor component added to the player at runtime.
//
//  PREFAB SETUP:
//  1. Create a GameObject with a sprite or 3D model
//  2. Add a SphereCollider — Is Trigger ON
//  3. Attach this script
// ============================================================
public class ArmorPickup : PickupBase
{
    protected override void OnPickedUp(GameObject player)
    {
        // Add armor component to player if not already present
        PlayerArmor armor = player.GetComponent<PlayerArmor>();
        if (armor == null)
            armor = player.AddComponent<PlayerArmor>();

        armor.Activate();
        GameManager.Instance?.RegisterArmor(armor);
        Debug.Log("[ArmorPickup] Armor activated — next hit blocked");
        AudioManager.Instance?.Play("PickupArmor");
    }
}