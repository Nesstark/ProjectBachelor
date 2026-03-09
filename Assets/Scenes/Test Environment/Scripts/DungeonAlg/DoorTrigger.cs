using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    public Direction direction;

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Trigger ramt af: {other.gameObject.name} med tag: {other.tag}");
        
        if (other.CompareTag("Player"))
            RoomManager.Instance.TryMove(direction);
    }

}