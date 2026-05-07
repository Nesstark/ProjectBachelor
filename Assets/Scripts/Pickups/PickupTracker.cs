using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks all pickup IDs collected this run. Persists across scene loads.
/// Reset via ClearAll() on New Game. Reapply on continue via ApplySavedPickups().
/// </summary>
public class PickupTracker : MonoBehaviour
{
    public static PickupTracker Instance { get; private set; }

    public List<string> CollectedIds { get; private set; } = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Register(string pickupId) => CollectedIds.Add(pickupId);

    public void ClearAll() => CollectedIds.Clear();

    /// <summary>
    /// Reapplies all saved pickups to the player after a continue-load.
    /// Call this from RoomManager.Start() after restoring the run.
    /// </summary>
    public void ApplySavedPickups(GameObject player, List<string> ids)
    {
        PlayerController controller = player.GetComponent<PlayerController>();

        foreach (string id in ids)
        {
            // Split "Type:Value" format
            string[] parts = id.Split(':');
            if (parts.Length != 2) continue;
            if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value)) continue;

            switch (parts[0])
            {
                case "Armor":
                    GameManager.Instance?.AddDamageReduction(value);
                    break;
                case "Range":
                    controller?.AddAttackRange(value);
                    break;
                case "Speed":
                    controller?.AddMoveSpeed(value);
                    break;
                // "Health" is intentionally absent — HealthPickup is consumable,
                // not permanent. It is not tracked by PickupTracker.
            }
        }

        Debug.Log($"[PickupTracker] Reapplied {ids.Count} pickups.");
    }
}