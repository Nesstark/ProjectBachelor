using UnityEngine;
using UnityEngine.InputSystem;

public class DebugRoomUnlock : MonoBehaviour
{
    private PlayerInput playerInput;
    private InputAction interactAction;

    void Awake()
    {
        playerInput = FindFirstObjectByType<PlayerInput>();
        interactAction = playerInput.actions["Interact"];
    }

    void Update()
    {
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
