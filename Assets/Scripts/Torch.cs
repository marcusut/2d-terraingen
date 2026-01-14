using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Torch : MonoBehaviour
{
    public Light2D torchLight;
    public float flickerSpeed = 5f;
    public float flickerAmount = 0.1f;
    
    private float baseIntensity;

    void Start()
    {
        if (torchLight == null) torchLight = GetComponentInChildren<Light2D>();
        if (torchLight != null) baseIntensity = torchLight.intensity;
    }

    void Update()
    {
        if (torchLight != null)
        {
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, transform.position.x);
            torchLight.intensity = baseIntensity + (noise - 0.5f) * flickerAmount;
        }
    }
}
