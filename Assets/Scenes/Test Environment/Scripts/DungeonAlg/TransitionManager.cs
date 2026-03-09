using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance;
    public Image vignetteImage;
    public float fadeDuration = 0.3f;

    [Header("Level Up")]
    public CanvasGroup levelUpGroup;
    public TextMeshProUGUI levelUpText;
    public float levelUpHoldDuration = 1.5f;

    void Awake()
    {
        Instance = this;
        SetAlpha(0f);
        if (levelUpGroup != null) levelUpGroup.alpha = 0f;
    }

    // Normal dør-transition — kun vignette
    public IEnumerator Transition(Action onMidpoint)
    {
        yield return Fade(0f, 1f);
        onMidpoint?.Invoke();
        yield return new WaitForSeconds(0.1f);
        yield return Fade(1f, 0f);
    }

    public IEnumerator LevelUpTransition(int newLevel, Action onMidpoint)
    {
        Debug.Log("LevelUpTransition START");
        yield return FadeGroup(levelUpGroup, 0f, 1f);

        onMidpoint?.Invoke();

        if (levelUpText != null)
        {
            levelUpText.text = $"LEVEL {newLevel}";
            Debug.Log("Tekst sat til: " + levelUpText.text);
            Debug.Log("Text objekt aktivt: " + levelUpText.gameObject.activeInHierarchy);
            Debug.Log("CanvasGroup alpha efter fade: " + levelUpGroup.alpha);
        }
        else Debug.LogError("levelUpText er NULL!");

        yield return new WaitForSeconds(0.2f);
        yield return new WaitForSeconds(levelUpHoldDuration);
        yield return FadeGroup(levelUpGroup, 1f, 0f);
    }


    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, t / fadeDuration));
            yield return null;
        }
        SetAlpha(to);
    }

    IEnumerator FadeGroup(CanvasGroup group, float from, float to)
    {
        if (group == null) yield break;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        group.alpha = to;
    }

    void SetAlpha(float alpha)
    {
        Color c = vignetteImage.color;
        vignetteImage.color = new Color(c.r, c.g, c.b, alpha);
    }
}
