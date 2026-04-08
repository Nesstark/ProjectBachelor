using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    private PlayerController _player;

    private void Awake()
    {
        _player = GetComponentInParent<PlayerController>();
    }

    public void OnFootstep()
    {
        _player.OnFootstep();
    }
}