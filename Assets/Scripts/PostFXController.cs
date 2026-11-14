using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Dynamic post processing:
/// - Vignette closes in as spins run out (pressure)
/// - Bloom spikes on a win
/// - Chromatic Aberration flashes on a loss
/// The Volume profile needs Vignette, Bloom and Chromatic Aberration overrides with "Intensity" ticked.
public class PostFXController : MonoBehaviour
{
    public Volume volume;
    public float bloomFlash = 6f;

    Vignette vignette;
    Bloom bloom;
    ChromaticAberration chroma;
    float baseBloom, pressure, win, lose;

    void Start()
    {
        volume.profile.TryGet(out vignette);
        volume.profile.TryGet(out bloom);
        volume.profile.TryGet(out chroma);
        if (bloom) baseBloom = bloom.intensity.value;
    }

    public void SetPressure(float p) => pressure = Mathf.Clamp01(p);
    public void WinFlash() => win = 1f;
    public void LoseFlash() => lose = 1f;

    void Update()
    {
        win = Mathf.MoveTowards(win, 0f, Time.deltaTime * 1.5f);
        lose = Mathf.MoveTowards(lose, 0f, Time.deltaTime * 2f);

        if (vignette) vignette.intensity.value = Mathf.Lerp(0.2f, 0.55f, pressure);
        if (bloom) bloom.intensity.value = baseBloom + win * bloomFlash;
        if (chroma) chroma.intensity.value = lose;
    }
}
