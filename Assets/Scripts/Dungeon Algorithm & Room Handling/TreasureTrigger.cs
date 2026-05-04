using UnityEngine;

public class TreasureTrigger : MonoBehaviour
{
    bool hasTriggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player")) return;

        // If the player has already cleared this room (re-entering),
        // let RoomController handle the already-cleared guard — same
        // behaviour as EncounterTrigger skipping cleared rooms.
        if (RoomManager.Instance.IsRoomCleared(RoomManager.Instance.CurrentCellPublic)) return;

        hasTriggered = true;
        RoomManager.Instance.CurrentRoom.TriggerTreasure();
    }
}