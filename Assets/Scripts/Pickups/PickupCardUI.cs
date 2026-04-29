using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  PickupCardUI.cs
//  World-space info card that floats above each pickup in the
//  Treasure Room — same approach as EnemyHealthBar.
//
//  PREFAB SETUP:
//  ─────────────────────────────────────────────────────────
//  1. Open each pickup prefab.
//  2. Add a child GameObject → name it "PickupCard"
//  3. Add a Canvas component to it:
//       Render Mode → World Space
//       Scale       → 0.01, 0.01, 0.01
//  4. Add a CanvasGroup component to that same Canvas root.
//  5. Inside the Canvas, build your card however you want, e.g.:
//       ├─ CardBG       (Image — your background panel)
//       ├─ IconImage    (Image — pickup icon slot)
//       ├─ TitleText    (TextMeshPro — pickup name)
//       └─ DescText     (TextMeshPro — effect description)
//  6. Attach THIS script to the "PickupCard" Canvas GameObject.
//  7. In the Inspector on this script:
//       - Wire up TitleText, DescText, IconImage, CanvasGroup
//       - Fill in Card Title, Card Description, Card Icon
//  8. Position "PickupCard" above the pickup (Y ~2 units).
// ============================================================

public class PickupCardUI : MonoBehaviour
{
    [Header("UI Slots — wire up in the Inspector")]
    [SerializeField] private TMP_Text    titleText;
    [SerializeField] private TMP_Text    descText;
    [SerializeField] private Image       iconImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Card Content — fill in per pickup")]
    [SerializeField] private string cardTitle       = "Pickup";
    [SerializeField, TextArea(2, 4)] private string cardDescription = "A mysterious item.";
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
        // Populate the card from the fields set in the Inspector — no Init() call needed
        if (titleText != null) titleText.text = cardTitle;
        if (descText  != null) descText.text  = cardDescription;

        if (iconImage != null)
        {
            iconImage.sprite  = cardIcon;
            iconImage.enabled = cardIcon != null;
        }
    }

    private void LateUpdate()
    {
        // Billboard — always face the camera (same trick as EnemyHealthBar)
        if (_cam != null)
            transform.rotation = Quaternion.LookRotation(
                transform.position - _cam.transform.position);

        // Fade in
        if (canvasGroup != null && canvasGroup.alpha < 1f)
        {
            _elapsed          += Time.deltaTime;
            canvasGroup.alpha  = Mathf.Clamp01(_elapsed / fadeInDuration);
        }
    }
}