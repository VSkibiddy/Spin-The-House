using UnityEngine;
using UnityEngine.UI;

/// Music and SFX volume sliders (pause menu). Values are saved and loaded with PlayerPrefs.
public class VolumeSettings : MonoBehaviour
{
    [Header("Sliders")]
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Audio")]
    public MusicManager music;        // Main Camera
    public AudioSource[] sfxSources;  // GameManager's Audio Source (win, lose, paid sounds)
    public RouletteWheel wheel;       // for the rolling sound

    float baseMusicVolume;
    float baseRollVolume;
    AudioSource musicSource;

    void Start()
    {
        if (music)
        {
            baseMusicVolume = music.volume;
            musicSource = music.GetComponent<AudioSource>();
        }
        if (wheel) baseRollVolume = wheel.rollVolume;

        musicSlider.minValue = 0f; musicSlider.maxValue = 1f;
        sfxSlider.minValue = 0f;   sfxSlider.maxValue = 1f;
        musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
        sfxSlider.value = PlayerPrefs.GetFloat("SfxVolume", 1f);

        musicSlider.onValueChanged.AddListener(SetMusic);
        sfxSlider.onValueChanged.AddListener(SetSfx);
        SetMusic(musicSlider.value);
        SetSfx(sfxSlider.value);
    }

    public void SetMusic(float v)
    {
        if (music)
        {
            music.volume = baseMusicVolume * v;                   // target used by the music fades
            if (musicSource) musicSource.volume = music.volume;   // apply right away
        }
        PlayerPrefs.SetFloat("MusicVolume", v);
    }

    public void SetSfx(float v)
    {
        foreach (var s in sfxSources) if (s) s.volume = v;
        if (wheel) wheel.rollVolume = baseRollVolume * v;
        PlayerPrefs.SetFloat("SfxVolume", v);
    }

    void OnDisable() => PlayerPrefs.Save();
}