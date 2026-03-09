using UnityEngine;

public class LevelExit : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        RoomManager.Instance.LoadNextLevel();
    }
}
