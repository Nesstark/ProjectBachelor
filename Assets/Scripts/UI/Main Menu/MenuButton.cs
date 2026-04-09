using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform underline;

    private static readonly Color normalColor = new Color(0.165f, 0.122f, 0.055f);
    private static readonly Color hoverColor  = new Color(0.545f, 0.102f, 0.102f);

    private Coroutine underlineRoutine;
    private float targetWidth;
    private const float fullWidth = 140f;
    private const float animSpeed = 6f;

    private void Awake()
    {
        // start with zero width
        SetUnderlineWidth(0f);
    }

    public void OnPointerEnter(PointerEventData _)
    {
        label.color = hoverColor;
        AnimateUnderline(fullWidth);
    }

    public void OnPointerExit(PointerEventData _)
    {
        label.color = normalColor;
        AnimateUnderline(0f);
    }

    public void OnPointerClick(PointerEventData _)
    {
        AudioManager.Instance.Play("Click");
    }

    private void AnimateUnderline(float target)
    {
        targetWidth = target;
        if (underlineRoutine != null) StopCoroutine(underlineRoutine);
        underlineRoutine = StartCoroutine(TweenUnderline());
    }

    private IEnumerator TweenUnderline()
    {
        float current = underline.sizeDelta.x;
        while (Mathf.Abs(current - targetWidth) > 0.5f)
        {
            current = Mathf.Lerp(current, targetWidth, Time.unscaledDeltaTime * animSpeed * 10f);
            SetUnderlineWidth(current);
            yield return null;
        }
        SetUnderlineWidth(targetWidth);
    }

    private void SetUnderlineWidth(float w)
    {
        underline.sizeDelta = new Vector2(w, underline.sizeDelta.y);
    }
}