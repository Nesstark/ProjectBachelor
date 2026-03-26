using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class SoundData
{
    public string id; // Unique name used to call this sound, e.g. "PlayerJump"

    public AudioClip[] clips; // Multiple clips = random selection on play

    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.5f, 2f)] public float pitch = 1f;

    [Header("Randomization")]
    public bool randomizePitch = false;
    [Range(0f, 0.5f)] public float pitchVariance = 0.1f;
    public bool randomizeVolume = false;
    [Range(0f, 0.3f)] public float volumeVariance = 0.05f;

    [Header("Playback")]
    public bool loop = false;
    public bool playOnAwake = false;

    [Header("Mixer Routing")]
    public AudioMixerGroup mixerGroup; // Drag the SFX/Music/etc. group here

    [HideInInspector] public AudioSource source;
}