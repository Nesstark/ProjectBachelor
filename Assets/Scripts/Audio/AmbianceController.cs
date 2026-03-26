using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(Collider))]
public class AmbianceController : MonoBehaviour
{
    [Header("Ambiance Settings")]
    public AudioClip ambianceClip;
    public AudioMixerGroup ambianceMixerGroup;
    [Range(0f, 1f)] public float targetVolume = 0.6f;
    public float fadeDuration = 2.5f;

    private AudioSource _source;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = ambianceClip;
        _source.loop = true;
        _source.playOnAwake = false;
        _source.volume = 0f;
        _source.spatialBlend = 0f; // 2D ambient; set to 1 for 3D positional
        _source.outputAudioMixerGroup = ambianceMixerGroup;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _source.Play();
        StopAllCoroutines();
        StartCoroutine(Fade(targetVolume));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        StopAllCoroutines();
        StartCoroutine(Fade(0f, stopOnEnd: true));
    }

    private IEnumerator Fade(float target, bool stopOnEnd = false)
    {
        float start = _source.volume, elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            _source.volume = Mathf.Lerp(start, target, elapsed / fadeDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        _source.volume = target;
        if (stopOnEnd) _source.Stop();
    }
}