using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance;
    public Image vignetteImage;
    public float fadeDuration = 0.3f;

    void Awake()
    {
        Instance = this;
        SetAlpha(0f);
    }

    public IEnumerator Transition(Action onMidpoint)
    {
        yield return Fade(0f, 1f);
        onMidpoint?.Invoke();
        yield return new WaitForSeconds(0.1f);
        yield return Fade(1f, 0f);
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

    void SetAlpha(float alpha)
    {
        Color c = vignetteImage.color;
        vignetteImage.color = new Color(c.r, c.g, c.b, alpha);
    }
}