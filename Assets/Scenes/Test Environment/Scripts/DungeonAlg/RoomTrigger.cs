using UnityEngine;

public class RoomTrigger : MonoBehaviour
{
    public enum TriggerType { Door, LevelExit }
    public TriggerType type;
    public Direction direction; // kun relevant hvis Door

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (type == TriggerType.Door)
            RoomManager.Instance.TryMove(direction);
        else
            RoomManager.Instance.LoadNextLevel();
    }
}
