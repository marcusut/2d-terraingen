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
    private SpriteMask playerMask;

    void Start()
    {
        if (playerLight) 
        {
            baseIntensity = playerLight.intensity;
            
            // Ensure Player has a SpriteMask for "Fog of War" reveal
            playerMask = playerLight.GetComponent<SpriteMask>();
            if (playerMask == null)
            {
                playerMask = playerLight.gameObject.AddComponent<SpriteMask>();
                // Try to use the same sprite as the light cookie, or a default knob
                // We can't easily load a default sprite from code without Resources, 
                // so we assume the user will assign one or we use a generated one.
                // For now, let's just warn or try to find one.
                Debug.Log("LightingManager: Added SpriteMask to Player Light. Please assign a Sprite to it for Fog of War to work.");
            }
            
            // Configure Mask
            // We want to reveal the "Darkness" layer.
            // Darkness Layer is usually "Visible Outside Mask".
            // So the Mask creates a "hole" in the darkness.
        }

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
            
            // Sync Mask scale with Light radius roughly
            if (playerMask)
            {
                float radius = playerLight.pointLightOuterRadius;
                playerMask.transform.localScale = Vector3.one * (radius * 2f);
            }
        }
    }
}
