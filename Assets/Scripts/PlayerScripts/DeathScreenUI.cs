using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Hierarchy setup in Unity:
//   Canvas (Screen Space — Overlay)
//     └── DeathPanel          [Image (black), CanvasGroup]
//           ├── YouDiedText   [TextMeshProUGUI]
//           └── ButtonGroup   [CanvasGroup]
//                 └── RestartButton [Button + TextMeshProUGUI]
//
// Attach this script to the Canvas or DeathPanel GameObject.
// Wire all four references in the Inspector.

public class DeathScreenUI : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private CanvasGroup     deathPanel;
    [SerializeField] private TextMeshProUGUI youDiedText;
    [SerializeField] private CanvasGroup     buttonGroup;
    [SerializeField] private Button          restartButton;

    [Header("Timing")]
    [Tooltip("Wait this many seconds after death before the overlay begins")]
    [SerializeField] private float delayBeforeShow     = 0.9f;
    [SerializeField] private float overlayFadeDuration = 1.0f;
    [SerializeField] private float textFadeDuration    = 0.7f;
    [SerializeField] private float buttonDelay         = 0.8f;
    [SerializeField] private float buttonFadeDuration  = 0.4f;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        SetAlpha(deathPanel,  0f, false);
        SetAlpha(buttonGroup, 0f, false);

        if (youDiedText != null)
            youDiedText.color = new Color(1f, 1f, 1f, 0f);

        restartButton?.onClick.AddListener(RestartGame);
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerDied.AddListener(OnPlayerDied);
    }

    private void OnDestroy()
    {
        GameManager.Instance?.OnPlayerDied.RemoveListener(OnPlayerDied);
    }

    // ─────────────────────────────────────────────────────────
    private void OnPlayerDied() => StartCoroutine(ShowDeathScreen());

    private IEnumerator ShowDeathScreen()
    {
        yield return new WaitForSecondsRealtime(delayBeforeShow);

        if (deathPanel != null)
        {
            deathPanel.blocksRaycasts = true;
            yield return StartCoroutine(FadeGroup(deathPanel, 0f, 1.0f, overlayFadeDuration));
        }

        if (youDiedText != null)
            yield return StartCoroutine(FadeText(youDiedText, 0f, 1f, textFadeDuration));

        yield return new WaitForSecondsRealtime(buttonDelay);

        if (buttonGroup != null)
        {
            buttonGroup.blocksRaycasts = true;
            buttonGroup.interactable   = true;
            yield return StartCoroutine(FadeGroup(buttonGroup, 0f, 1f, buttonFadeDuration));
        }
    }

    // Uses unscaled time so it works even if you later freeze Time.timeScale
    private IEnumerator FadeGroup(CanvasGroup cg, float from, float to, float dur)
    {
        float t = 0f;
        cg.alpha = from;
        while (t < dur) { t += Time.unscaledDeltaTime; cg.alpha = Mathf.Lerp(from, to, t / dur); yield return null; }
        cg.alpha = to;
    }

    private IEnumerator FadeText(TextMeshProUGUI tmp, float from, float to, float dur)
    {
        float t = 0f;
        Color c = tmp.color; c.a = from; tmp.color = c;
        while (t < dur) { t += Time.unscaledDeltaTime; c.a = Mathf.Lerp(from, to, t / dur); tmp.color = c; yield return null; }
        c.a = to; tmp.color = c;
    }

    private void SetAlpha(CanvasGroup cg, float a, bool block)
    {
        if (cg == null) return;
        cg.alpha = a; cg.blocksRaycasts = block; cg.interactable = block;
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}