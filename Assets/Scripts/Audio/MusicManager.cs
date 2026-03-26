using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Mixer Group")]
    public AudioMixerGroup musicMixerGroup;

    [Header("Settings")]
    public float defaultCrossfadeDuration = 2f;

    private AudioSource _sourceA;
    private AudioSource _sourceB;
    private bool _isPlayingOnA = true;

    private AudioSource Active   => _isPlayingOnA ? _sourceA : _sourceB;
    private AudioSource Inactive => _isPlayingOnA ? _sourceB : _sourceA;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sourceA = CreateMusicSource("MusicSource_A");
        _sourceB = CreateMusicSource("MusicSource_B");
    }

    private AudioSource CreateMusicSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;
        src.outputAudioMixerGroup = musicMixerGroup;
        return src;
    }

    public void PlayTrack(AudioClip clip, float fadeDuration = -1f)
    {
        float duration = fadeDuration < 0 ? defaultCrossfadeDuration : fadeDuration;
        StartCoroutine(CrossfadeRoutine(clip, duration));
    }

    public void StopMusic(float fadeDuration = 1.5f) => StartCoroutine(FadeOutActive(fadeDuration));

    private IEnumerator CrossfadeRoutine(AudioClip newClip, float duration)
    {
        Inactive.clip = newClip;
        Inactive.volume = 0f;
        Inactive.Play();

        float elapsed = 0f;
        float startVol = Active.volume;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Active.volume   = Mathf.Lerp(startVol, 0f, t);
            Inactive.volume = Mathf.Lerp(0f, 1f, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Active.Stop();
        Active.volume = 0f;
        Inactive.volume = 1f;
        _isPlayingOnA = !_isPlayingOnA;
    }

    private IEnumerator FadeOutActive(float duration)
    {
        float start = Active.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            Active.volume = Mathf.Lerp(start, 0f, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Active.Stop();
    }
}