using UnityEngine;
using UnityEngine.InputSystem;

public class TopDownKinematicMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 movementInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        Vector2 newPosition = rb.position + movementInput * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);
    }

    // This method name MUST match the Action name: "Move"
    public void OnMove(InputValue value)
    {
        movementInput = value.Get<Vector2>().normalized;
    }
}
