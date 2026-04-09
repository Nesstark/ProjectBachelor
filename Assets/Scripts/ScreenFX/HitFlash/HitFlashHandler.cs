using System.Collections;
using UnityEngine;

// Attach to the SpriteVisual child on any character (player or enemy).
// Call Flash() from any script when the character takes a hit.
public class HitFlashHandler : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color          flashColor         = Color.red;
    [SerializeField] private int            flashCount         = 3;
    [SerializeField] private float          flashInterval      = 0.05f;

    // URP Lit shader uses _BaseColor — change if your shader differs
    [SerializeField] private string colorPropertyName = "_BaseColor";

    private MaterialPropertyBlock _mpb;
    private int                   _colorID;

    private void Awake()
    {
        _mpb     = new MaterialPropertyBlock();
        _colorID = Shader.PropertyToID(colorPropertyName);

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Flash()
    {
        if (spriteRenderer == null) return;
        StopAllCoroutines();
        StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        for (int i = 0; i < flashCount; i++)
        {
            SetColor(flashColor);
            yield return new WaitForSeconds(flashInterval);
            SetColor(Color.white);
            yield return new WaitForSeconds(flashInterval);
        }

        SetColor(Color.white);
    }

    private void SetColor(Color color)
    {
        spriteRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(_colorID, color);
        spriteRenderer.SetPropertyBlock(_mpb);
    }
}