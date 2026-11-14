using UnityEngine;

public class LightFlicker : MonoBehaviour
{
    public float flickerChance = 0.03f;   // chance per frame of a flicker
    Light lamp;
    float baseIntensity;

    void Start() { lamp = GetComponent<Light>(); baseIntensity = lamp.intensity; }

    void Update()
    {
        lamp.intensity = Random.value < flickerChance ? baseIntensity * Random.Range(0.3f, 0.7f) : baseIntensity;
    }
}