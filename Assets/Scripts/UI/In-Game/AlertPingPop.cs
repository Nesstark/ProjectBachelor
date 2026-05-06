using System.Collections;
using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(Animator))]
public class AlertPingPop : MonoBehaviour
{
    [Tooltip("If true, the ping always rotates to face the main camera")]
    [SerializeField] private bool billboard = true;

    [Header("Glow")]
    [SerializeField] private Color  glowColor = new Color(1f, 0.8f, 0f, 1f);
    [SerializeField] [Range(0f, 1f)] private float glowOuter = 0.3f;
    [SerializeField] [Range(0f, 1f)] private float glowInner = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float glowPower = 0.5f;

    [Header("Glow Backdrop")]
    [SerializeField] private UnityEngine.UI.Image glowBackdrop;
    [SerializeField] private Color backdropColor    = new Color(1f, 0.6f, 0f, 0.5f);
    [SerializeField] private float backdropScale    = 3f;
    [SerializeField] private float gradientFalloff  = 2f; // higher = tighter/sharper, lower = wider/softer

    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        ApplyGlow();
    }

    private void ApplyGlow()
    {
        TextMeshProUGUI tmp = GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            Material mat = tmp.fontMaterial;
            mat.EnableKeyword("GLOW_ON");
            mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
            mat.SetColor(ShaderUtilities.ID_GlowColor, glowColor);
            mat.SetFloat(ShaderUtilities.ID_GlowOuter, glowOuter);
            mat.SetFloat(ShaderUtilities.ID_GlowInner, glowInner);
            mat.SetFloat(ShaderUtilities.ID_GlowPower, glowPower);
        }

        if (glowBackdrop != null)
        {
            glowBackdrop.sprite = CreateRadialGradient(128);
            glowBackdrop.color  = backdropColor;
            glowBackdrop.transform.localScale = Vector3.one * backdropScale;
        }
    }

    private Sprite CreateRadialGradient(int size)
    {
        Texture2D tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[]   pixels = new Color[size * size];
        float     center = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist    = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float norm    = dist / center;                        // 0 = centre, 1 = edge
                float alpha   = Mathf.Clamp01(1f - norm);
                alpha         = Mathf.Pow(alpha, gradientFalloff);   // controls softness
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void LateUpdate()
    {
        if (billboard && Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }
}