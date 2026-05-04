using System.Collections;
using UnityEngine;

// ============================================================
// AlertPingPop.cs
// Attach to the AlertPing prefab root.
// Plays the pop animation, optionally faces the camera,
// then destroys itself when the animation finishes.
// ============================================================


[RequireComponent(typeof(Animator))]
public class AlertPingPop : MonoBehaviour
{
    [Tooltip("If true, the ping always rotates to face the main camera")]
    [SerializeField] private bool billboard = true;

    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void LateUpdate()
    {
        if (billboard && Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }
}