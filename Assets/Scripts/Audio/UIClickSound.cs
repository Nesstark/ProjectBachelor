using UnityEngine;
using UnityEngine.UI;

public class UIClickSound : MonoBehaviour
{
    private void Start()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        Debug.Log($"UIClickSound found {buttons.Length} buttons");

        foreach (Button btn in buttons)
        {
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance == null)
                {
                    Debug.LogWarning("AudioManager instance is null!");
                    return;
                }
                AudioManager.Instance.Play("Click");
            });
        }
    }
}