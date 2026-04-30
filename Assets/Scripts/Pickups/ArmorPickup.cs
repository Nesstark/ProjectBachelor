using UnityEngine;

// ============================================================
//  ArmorPickup.cs — Permanently reduces incoming damage
// ============================================================
public class ArmorPickup : PickupBase
{
    [Header("Armor Pickup")]
    [SerializeField] private float damageReduction = 1f;  // flat damage off per hit

    protected override void OnPickedUp(GameObject player)
    {
        GameManager.Instance?.AddDamageReduction(damageReduction);
        Debug.Log($"[ArmorPickup] Damage reduction +{damageReduction}");
        AudioManager.Instance?.Play("PickupArmor");
    }
}