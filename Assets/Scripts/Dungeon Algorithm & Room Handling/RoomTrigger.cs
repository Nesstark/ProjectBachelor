using UnityEngine;

public class RoomTrigger : MonoBehaviour
{
    public enum TriggerType { Door, LevelExit }
    public TriggerType type;
    public Direction direction; // only relevant for Door type

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (type == TriggerType.Door)
        {
            RoomManager.Instance.TryMove(direction);
            return;
        }

        // ── Level Exit ──────────────────────────────────────────────────────
        // Guard: the boss room must be cleared before the exit can be used.
        // This prevents the agent (or a player) from skipping the boss entirely
        // by walking into the trigger before the boss is killed.
        if (!RoomManager.Instance.IsRoomCleared(RoomManager.Instance.CurrentCellPublic))
        {
            Debug.Log("[RoomTrigger] Level exit blocked — boss room not yet cleared.");
            return;
        }

        RoomManager.Instance.LoadNextLevel();
    }
}