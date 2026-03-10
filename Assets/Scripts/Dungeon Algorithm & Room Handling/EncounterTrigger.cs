using UnityEngine;

public class EncounterTrigger : MonoBehaviour
{
    bool hasTriggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player")) return;
        if (RoomManager.Instance.IsRoomCleared(RoomManager.Instance.CurrentCellPublic)) return;

        hasTriggered = true;
        RoomManager.Instance.CurrentRoom.TriggerEncounter();
    }
}
