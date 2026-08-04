using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class TorchFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    [Tooltip("The lowest intensity the light will drop to.")]
    public float minIntensity = 0.7f;

    [Tooltip("The highest intensity the light will reach.")]
    public float maxIntensity = 1.3f;

    [Tooltip("How fast the flame flickers.")]
    public float flickerSpeed = 5f;

    private Light2D fireLight;
    private float randomOffset;


    void Start()
    {
        // Grab the Light2D component on this object
        fireLight = GetComponent<Light2D>();

        // Pick a random starting point so multiple torches don't flicker in perfect sync
        randomOffset = Random.Range(0f, 1000f);
    }

    void Update()
    {
        // Generate smooth random noise based on time
        float noise = Mathf.PerlinNoise(randomOffset, Time.time * flickerSpeed);

        // Lerp (blend) between the min and max intensity based on the noise value
        fireLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }
}