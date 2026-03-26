using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer Reference")]
    public AudioMixer audioMixer;

    [Header("Sound Library")]
    public SoundData[] sfxSounds;
    public SoundData[] uiSounds;

    private Dictionary<string, SoundData> _soundMap = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeSounds(sfxSounds);
        InitializeSounds(uiSounds);
    }

    private void InitializeSounds(SoundData[] sounds)
    {
        foreach (var s in sounds)
        {
            if (_soundMap.ContainsKey(s.id)) continue;
            s.source = gameObject.AddComponent<AudioSource>();
            s.source.loop = s.loop;
            s.source.playOnAwake = s.playOnAwake;
            s.source.outputAudioMixerGroup = s.mixerGroup;
            _soundMap[s.id] = s;
        }
    }

    // ── Playback ──────────────────────────────────────────────

    public void Play(string id)
    {
        if (!_soundMap.TryGetValue(id, out SoundData s)) { Debug.LogWarning($"[AudioManager] Sound '{id}' not found."); return; }

        s.source.clip = s.clips[Random.Range(0, s.clips.Length)];
        s.source.volume = s.randomizeVolume ? s.volume + Random.Range(-s.volumeVariance, s.volumeVariance) : s.volume;
        s.source.pitch  = s.randomizePitch  ? s.pitch  + Random.Range(-s.pitchVariance,  s.pitchVariance)  : s.pitch;
        s.source.Play();
    }

    public void Stop(string id)
    {
        if (_soundMap.TryGetValue(id, out SoundData s)) s.source.Stop();
    }

    public void PlayAtPosition(string id, Vector3 position)
    {
        if (!_soundMap.TryGetValue(id, out SoundData s)) return;
        AudioClip clip = s.clips[Random.Range(0, s.clips.Length)];
        AudioSource.PlayClipAtPoint(clip, position, s.volume);
    }

    // ── Volume Control (via Mixer) ────────────────────────────

    public void SetVolume(string exposedParam, float normalizedValue)
    {
        // normalizedValue: 0.0001 (silent) to 1 (full). Converts to dB.
        float dB = normalizedValue > 0.0001f ? Mathf.Log10(normalizedValue) * 20f : -80f;
        audioMixer.SetFloat(exposedParam, dB);
    }

    public void SetSFXVolume(float val)    => SetVolume("SFXVolume", val);
    public void SetMusicVolume(float val)  => SetVolume("MusicVolume", val);
    public void SetAmbianceVolume(float v) => SetVolume("AmbianceVolume", v);
    public void SetUIVolume(float val)     => SetVolume("UIVolume", val);

    // ── Fade Helpers ──────────────────────────────────────────

    public void FadeIn(string id, float duration)  => StartCoroutine(FadeCoroutine(id, 0f, _soundMap[id].volume, duration));
    public void FadeOut(string id, float duration) => StartCoroutine(FadeCoroutine(id, _soundMap[id].source.volume, 0f, duration, stopOnEnd: true));

    private IEnumerator FadeCoroutine(string id, float from, float to, float duration, bool stopOnEnd = false)
    {
        if (!_soundMap.TryGetValue(id, out SoundData s)) yield break;
        if (from == 0f) { s.source.volume = 0f; s.source.Play(); }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            s.source.volume = Mathf.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        s.source.volume = to;
        if (stopOnEnd) s.source.Stop();
    }

    // ── Persistence ───────────────────────────────────────────

    public void SaveVolumes()
    {
        audioMixer.GetFloat("MusicVolume",   out float m);
        audioMixer.GetFloat("SFXVolume",     out float s);
        audioMixer.GetFloat("AmbianceVolume",out float a);
        PlayerPrefs.SetFloat("Vol_Music",    m);
        PlayerPrefs.SetFloat("Vol_SFX",      s);
        PlayerPrefs.SetFloat("Vol_Ambiance", a);
        PlayerPrefs.Save();
    }

    public void LoadVolumes()
    {
        audioMixer.SetFloat("MusicVolume",    PlayerPrefs.GetFloat("Vol_Music",    0f));
        audioMixer.SetFloat("SFXVolume",      PlayerPrefs.GetFloat("Vol_SFX",      0f));
        audioMixer.SetFloat("AmbianceVolume", PlayerPrefs.GetFloat("Vol_Ambiance", 0f));
    }
}