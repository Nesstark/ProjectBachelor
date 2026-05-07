using UnityEngine;

// ============================================================
//  ArmorPickup.cs — Permanently reduces incoming damage
//  Max total reduction is enforced by GameManager (capped at 20).
//  A single token reduces damage by damageReduction per hit,
//  but GameManager guarantees at least 1 damage always lands.
// ============================================================
public class ArmorPickup : PickupBase
{
    [Header("Armor Pickup")]
    [SerializeField]
    [Range(0.5f, 5f)]
    [Tooltip("Flat damage reduced per hit. GameManager caps total reduction at 20 and always deals minimum 1 dmg.")]
    private float damageReduction = 1f;

    public override string Description =>
        $"Reduce all incoming damage by {damageReduction} permanently.";

    protected override void OnPickedUp(GameObject player)
    {
        GameManager.Instance?.AddDamageReduction(damageReduction);
        PickupTracker.Instance?.Register($"Armor:{damageReduction}");
        Debug.Log($"[ArmorPickup] Damage reduction +{damageReduction}");
        AudioManager.Instance?.Play("PickupArmor");
    }
}