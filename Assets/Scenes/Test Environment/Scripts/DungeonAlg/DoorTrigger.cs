using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    public Direction direction;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            RoomManager.Instance.TryMove(direction);
    }
}