using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private CanvasGroup menuGroup;

    private IEnumerator Start()
    {
        menuGroup.alpha = 0f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;
            menuGroup.alpha = t;
            yield return null;
        }
        menuGroup.alpha = 1f;
    }

    public void OnPlayPressed()
    {
        AudioManager.Instance.Play("Click");
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnSettingsPressed()
    {
        AudioManager.Instance.Play("Click");
        settingsPanel.SetActive(true);
    }

    public void OnQuitPressed()
    {
        AudioManager.Instance.Play("Click");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}