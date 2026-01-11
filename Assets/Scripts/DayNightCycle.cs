using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class DayNightCycle : MonoBehaviour
{
    [Header("Time Settings")]
    [Range(0f, 1f)] public float timeOfDay = 0.5f; // 0.0 = midnight, 0.5 = noon
    public float dayDurationInSeconds = 120f;
    public bool pauseTime;

    [Header("References")]
    public Transform sunTransform;
    public Transform moonTransform;
    public Light2D globalLight; // The ambient global light (No Shadows)
    public Light2D sunlight;    // The Point Light on the Sun Sprite (Glow, No Shadows)
    public Light2D worldLight;  // NEW: Directional Light for World Shadows
    public Camera mainCamera;
    public StarField starField; 

    [Header("Orbit Settings")]
    public float orbitRadius = 10f;
    public float horizonHeight = -2f; 
    [Range(0f, 1f)] public float verticalParallax = 0.9f;
    public float celestialScale = 1f;

    [Header("Colors & Gradients")]
    public Gradient skyColor;
    public Gradient sunColor;
    public Gradient moonColor;
    public Gradient ambientLightColor;
    public Gradient cloudTint;

    public event Action<float> OnTimeChanged; 

    private SpriteRenderer sunSprite;
    private SpriteRenderer moonSprite;

    private void Awake()
    {
        if (skyColor == null) skyColor = new Gradient();
        if (sunColor == null) sunColor = new Gradient();
        if (moonColor == null) moonColor = new Gradient();
        if (ambientLightColor == null) ambientLightColor = new Gradient();
        if (cloudTint == null) cloudTint = new Gradient();

        if (sunTransform != null) sunSprite = sunTransform.GetComponent<SpriteRenderer>();
        if (moonTransform != null) moonSprite = moonTransform.GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (Application.isPlaying && !pauseTime)
        {
            timeOfDay += Time.deltaTime / dayDurationInSeconds;
            if (timeOfDay >= 1f) timeOfDay -= 1f;
        }

        UpdateCelestialBodies();
        UpdateLighting();
        UpdateStars();
        
        OnTimeChanged?.Invoke(timeOfDay);
    }

    private void UpdateCelestialBodies()
    {
        if (mainCamera == null) return;

        float camX = mainCamera.transform.position.x;
        float camY = mainCamera.transform.position.y;
        
        float skyYOffset = camY * verticalParallax;
        float orbitCenterY = skyYOffset + horizonHeight + orbitRadius;

        float sunAngle = (0.5f - timeOfDay) * Mathf.PI * 2f + (Mathf.PI / 2f);

        // Sun Visuals (Parallax)
        if (sunTransform != null)
        {
            float sunX = Mathf.Cos(sunAngle) * orbitRadius;
            float sunY = orbitCenterY + Mathf.Sin(sunAngle) * orbitRadius;
            
            sunTransform.position = new Vector3(camX + sunX, sunY, 0);
            sunTransform.localScale = Vector3.one * celestialScale;
            
            float rotZ = (sunAngle - Mathf.PI) * Mathf.Rad2Deg;
            sunTransform.rotation = Quaternion.Euler(0, 0, rotZ);
        }

        // Moon Visuals (Parallax)
        if (moonTransform != null)
        {
            float moonAngle = sunAngle + Mathf.PI;
            float moonX = Mathf.Cos(moonAngle) * orbitRadius;
            float moonY = orbitCenterY + Mathf.Sin(moonAngle) * orbitRadius;

            moonTransform.position = new Vector3(camX + moonX, moonY, 0);
            moonTransform.localScale = Vector3.one * celestialScale;
            
            float rotZ = (moonAngle - Mathf.PI) * Mathf.Rad2Deg;
            moonTransform.rotation = Quaternion.Euler(0, 0, rotZ);
        }
    }

    private void UpdateLighting()
    {
        // 1. Global Ambient (Base brightness, no shadows)
        if (globalLight != null && ambientLightColor != null)
        {
            globalLight.color = ambientLightColor.Evaluate(timeOfDay);
        }

        // 2. Camera Background
        if (mainCamera != null && skyColor != null)
        {
            mainCamera.backgroundColor = skyColor.Evaluate(timeOfDay);
        }
        
        // 3. Sun Glow (Visual only)
        Color currentSunColor = sunColor != null ? sunColor.Evaluate(timeOfDay) : Color.white;
        if (sunlight != null)
        {
            sunlight.color = currentSunColor;
            float sunHeight = Mathf.Sin((0.5f - timeOfDay) * Mathf.PI * 2f + (Mathf.PI / 2f));
            sunlight.intensity = Mathf.Clamp01(sunHeight);
        }
        if (sunSprite != null)
        {
            sunSprite.color = currentSunColor;
        }

        // 4. World Light (Directional Shadows) - NEW
        if (worldLight != null)
        {
            worldLight.color = currentSunColor;
            
            // Intensity matches Sun height
            float sunHeight = Mathf.Sin((0.5f - timeOfDay) * Mathf.PI * 2f + (Mathf.PI / 2f));
            worldLight.intensity = Mathf.Clamp01(sunHeight);

            // Optional: Rotate slightly to match time of day, but keep mostly downward
            // Noon (0.5) = -90 deg (Down). Sunrise (0.25) = -45 deg? Sunset (0.75) = -135 deg?
            // Let's clamp it so shadows don't get too long/weird.
            // Range: -60 to -120 degrees.
            float angle = Mathf.Lerp(-60f, -120f, (timeOfDay - 0.25f) * 2f); // Map 0.25-0.75 to angles
            if (timeOfDay < 0.25f || timeOfDay > 0.75f) angle = -90f; // Reset at night
            
            worldLight.transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        
        // 5. Moon Glow
        Color currentMoonColor = moonColor != null ? moonColor.Evaluate(timeOfDay) : Color.white;
        if (moonTransform != null)
        {
            var moonLightComp = moonTransform.GetComponent<Light2D>();
            if (moonLightComp != null)
            {
                moonLightComp.color = currentMoonColor;
                float sunHeight = Mathf.Sin((0.5f - timeOfDay) * Mathf.PI * 2f + (Mathf.PI / 2f));
                float moonIntensity = Mathf.Clamp01(-sunHeight);
                moonLightComp.intensity = moonIntensity * 0.3f; 
            }
        }
        if (moonSprite != null)
        {
            moonSprite.color = currentMoonColor;
        }
    }

    private void UpdateStars()
    {
        if (starField == null) return;

        float sunHeight = Mathf.Sin((0.5f - timeOfDay) * Mathf.PI * 2f + (Mathf.PI / 2f));
        float starAlpha = Mathf.Clamp01(-sunHeight * 4f - 0.2f); 

        starField.SetStarAlpha(starAlpha);
    }
    
    public Color GetCloudTint()
    {
        if (cloudTint == null) return Color.white;
        return cloudTint.Evaluate(timeOfDay);
    }
}
