using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  PickupCardUI.cs
//  World-space info card that floats above each pickup in the
//  Treasure Room — same approach as EnemyHealthBar.
// ============================================================

public class PickupCardUI : MonoBehaviour
{
    [Header("UI Slots — wire up in the Inspector")]
    [SerializeField] private TMP_Text    titleText;
    [SerializeField] private TMP_Text    descText;
    [SerializeField] private Image       iconImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Card Content — fill in per pickup")]
    [SerializeField] private string cardTitle = "Pickup";
    [SerializeField] private Sprite cardIcon;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.4f;

    private Camera _cam;
    private float  _elapsed;


    // ─────────────────────────────────────────────────────────


    private void Awake()
    {
        _cam = Camera.main;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }


    private void Start()
    {
        if (titleText != null) titleText.text = cardTitle;

        PickupBase pickup = GetComponentInParent<PickupBase>();
        if (descText != null && pickup != null)
            descText.text = pickup.Description;

        if (iconImage != null)
        {
            iconImage.sprite  = cardIcon;
            iconImage.enabled = cardIcon != null;
        }
    }


    private void LateUpdate()
    {
        // Billboard — always face the camera
        if (_cam != null)
            transform.rotation = Quaternion.LookRotation(
                transform.position - _cam.transform.position);

        // Fade in
        if (canvasGroup != null && canvasGroup.alpha < 1f)
        {
            _elapsed         += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(_elapsed / fadeInDuration);
        }
    }
}