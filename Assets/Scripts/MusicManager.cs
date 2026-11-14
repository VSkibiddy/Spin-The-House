using System.Collections;
using UnityEngine;

/// Plays background music; plays a sinister track once at the start of each cursed deadline.
/// Put this on the Main Camera next to its Audio Source (leave the Audio Source's clip field empty).
[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public AudioClip normalMusic;
    public AudioClip sinisterMusic;
    [Range(0f, 1f)] public float volume = 0.4f;
    public float fadeTime = 1.5f;
    public float sinisterMaxTime = 0f;   // 0 = play the whole sinister track; otherwise cut it after this many seconds

    AudioSource src;
    Coroutine fade;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;
        src.volume = volume;
        src.clip = normalMusic;
        if (normalMusic) src.Play();
    }

    public void SetSinister(bool on)
    {
        AudioClip target = on ? sinisterMusic : normalMusic;
        if (target == null || src.clip == target) return;
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(SwitchTo(target));
    }

    IEnumerator SwitchTo(AudioClip clip, bool loop = true)
    {
        float start = src.volume;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)   // unscaled: keeps fading even while paused
        {
            src.volume = Mathf.Lerp(start, 0f, t / fadeTime);
            yield return null;
        }

        src.clip = clip;
        src.loop = loop;
        src.Play();

        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            src.volume = Mathf.Lerp(0f, volume, t / fadeTime);
            yield return null;
        }
        src.volume = volume;
    }

    /// Plays the sinister track once (used when a cursed deadline starts), then fades back to the normal music.
    public void PlaySinisterOnce()
    {
        if (sinisterMusic == null) return;
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(SinisterOnceRoutine());
    }

    IEnumerator SinisterOnceRoutine()
    {
        yield return SwitchTo(sinisterMusic, false);   // fade in, no loop

        float t = 0f;
        while (src.isPlaying && (sinisterMaxTime <= 0f || t < sinisterMaxTime))
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return SwitchTo(normalMusic, true);      // back to the normal loop
    }

    /// Fades the music out completely (used on game over).
    public void FadeOut()
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeOutRoutine());
    }

    IEnumerator FadeOutRoutine()
    {
        float start = src.volume;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            src.volume = Mathf.Lerp(start, 0f, t / fadeTime);
            yield return null;
        }
        src.Stop();
    }
}