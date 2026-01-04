using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LightingManager : MonoBehaviour
{
    [Header("References")]
    public Light2D playerLight;
    public Transform player;

    [Header("Settings")]
    public float lightFlickerSpeed = 5f;
    public float lightFlickerAmount = 0.1f;
    
    private float baseIntensity;

    void Start()
    {
        if (playerLight) baseIntensity = playerLight.intensity;
        if (!player) player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Update()
    {
        if (playerLight)
        {
            // Subtle flicker for player light (torch-like)
            float noise = Mathf.PerlinNoise(Time.time * lightFlickerSpeed, 0f);
            playerLight.intensity = baseIntensity + (noise - 0.5f) * lightFlickerAmount;
            
            if (player)
            {
                playerLight.transform.position = player.position;
            }
        }
    }
}
