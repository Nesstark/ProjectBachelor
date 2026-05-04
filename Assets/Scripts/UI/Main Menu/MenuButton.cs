using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Attach to BtnPlay, BtnSettings, BtnQuit.
/// Handles hover and controller-select visuals only.
/// Navigation is handled entirely by Unity's EventSystem.
/// </summary>
public class MenuButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    ISelectHandler, IDeselectHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform underline;

    private static readonly Color normalColor = new Color(0.165f, 0.122f, 0.055f);
    private static readonly Color hoverColor  = new Color(0.545f, 0.102f, 0.102f);

    private Coroutine underlineRoutine;
    private const float fullWidth = 140f;
    private const float animSpeed = 6f;

    private bool isPointerOver = false;
    private bool isSelected    = false;

    private void Awake() => SetUnderlineWidth(0f);

    private void OnDisable()
    {
        isPointerOver = false;
        isSelected    = false;
        StopAllCoroutines();
        SetUnderlineWidth(0f);
        if (label != null) label.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData _) { isPointerOver = true;  RefreshVisuals(); }
    public void OnPointerExit (PointerEventData _) { isPointerOver = false; RefreshVisuals(); }
    public void OnPointerClick(PointerEventData _) => AudioManager.Instance.Play("Click");

    public void OnSelect  (BaseEventData _) { isSelected = true;  RefreshVisuals(); }
    public void OnDeselect(BaseEventData _) { isSelected = false; RefreshVisuals(); }

    private void RefreshVisuals()
    {
        bool on = isPointerOver || isSelected;
        label.color = on ? hoverColor : normalColor;
        AnimateUnderline(on ? fullWidth : 0f);
    }

    private void AnimateUnderline(float target)
    {
        if (underlineRoutine != null) StopCoroutine(underlineRoutine);
        underlineRoutine = StartCoroutine(TweenUnderline(target));
    }

    private IEnumerator TweenUnderline(float target)
    {
        float cur = underline.sizeDelta.x;
        while (Mathf.Abs(cur - target) > 0.5f)
        {
            cur = Mathf.Lerp(cur, target, Time.unscaledDeltaTime * animSpeed * 10f);
            SetUnderlineWidth(cur);
            yield return null;
        }
        SetUnderlineWidth(target);
    }

    private void SetUnderlineWidth(float w) =>
        underline.sizeDelta = new Vector2(w, underline.sizeDelta.y);
}