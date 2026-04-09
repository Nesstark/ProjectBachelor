using UnityEngine;
using UnityEngine.InputSystem;

public class DebugRoomUnlock : MonoBehaviour
{
    private InputAction interactAction;

    void Start()
    {
        PlayerInput playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("[DebugRoomUnlock] No PlayerInput found in scene!");
            return;
        }
        interactAction = playerInput.actions["Interact"];
    }

    void Update()
    {
        if (interactAction == null) return;

        if (interactAction.WasPressedThisFrame())
        {
            if (RoomManager.Instance?.CurrentRoom == null)
            {
                Debug.LogError("DEBUG: CurrentRoom er null!");
                return;
            }

            RoomManager.Instance.CurrentRoom.UnlockDoors();
            Debug.Log("DEBUG: Rum unlocked via E");
        }
    }
}