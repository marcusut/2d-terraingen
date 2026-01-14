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
    public Transform sunTransform; // The Visual Sun Sprite
    public Transform moonTransform; // The Visual Moon Sprite
    public Light2D globalLight; // Ambient
    public Light2D sunlight;    // The Actual Light Source (Should be separate object now)
    public Light2D moonlight;   // The Actual Moon Light Source
    public Camera mainCamera;
    public StarField starField; 

    [Header("Visual Orbit (Sprite)")]
    public float visualOrbitRadius = 15f;
    public float visualHorizonHeight = -2f; 
    public float celestialScale = 3f;
    [Range(0f, 1f)] public float verticalParallax = 0.9f;

    [Header("Light Orbit (Shadows)")]
    public float lightOrbitRadius = 100f; // Far away for parallel shadows
    public float lightHorizonHeight = -20f; // Dip lower to ensure total darkness
    public float shadowNoonOffset = 20f; // Horizontal offset to prevent vertical shadows

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
        
        float sunAngle = (0.5f - timeOfDay) * Mathf.PI * 2f + (Mathf.PI / 2f);
        float moonAngle = sunAngle + Mathf.PI;

        // 1. Update Visuals (Close Orbit)
        float visualCenterY = (camY * verticalParallax) + visualHorizonHeight + visualOrbitRadius;
        
        if (sunTransform != null)
        {
            float sunX = Mathf.Cos(sunAngle) * visualOrbitRadius;
            float sunY = visualCenterY + Mathf.Sin(sunAngle) * visualOrbitRadius;
            
            sunTransform.position = new Vector3(camX + sunX, sunY, 0);
            sunTransform.localScale = Vector3.one * celestialScale;
        }

        if (moonTransform != null)
        {
            float moonX = Mathf.Cos(moonAngle) * visualOrbitRadius;
            float moonY = visualCenterY + Mathf.Sin(moonAngle) * visualOrbitRadius;

            moonTransform.position = new Vector3(camX + moonX, moonY, 0);
            moonTransform.localScale = Vector3.one * celestialScale;
        }

        // 2. Update Lights (Far Orbit for Parallel Shadows)
        float lightCenterY = lightHorizonHeight + lightOrbitRadius; // Fixed Y position, no parallax

        if (sunlight != null)
        {
            float lx = Mathf.Cos(sunAngle) * lightOrbitRadius;
            float ly = lightCenterY + Mathf.Sin(sunAngle) * lightOrbitRadius;
            sunlight.transform.position = new Vector3(camX + lx, ly, 0);
        }

        if (moonlight != null)
        {
            float lx = Mathf.Cos(moonAngle) * lightOrbitRadius;
            float ly = lightCenterY + Mathf.Sin(moonAngle) * lightOrbitRadius;
            moonlight.transform.position = new Vector3(camX + lx, ly, 0);
        }
    }

    private void UpdateLighting()
    {
        // Global Ambient
        if (globalLight != null && ambientLightColor != null)
        {
            globalLight.color = ambientLightColor.Evaluate(timeOfDay);
        }

        // Camera Background
        if (mainCamera != null && skyColor != null)
        {
            mainCamera.backgroundColor = skyColor.Evaluate(timeOfDay);
        }
        
        // Sun Light
        Color currentSunColor = sunColor != null ? sunColor.Evaluate(timeOfDay) : Color.white;
        float sunHeight = Mathf.Sin((0.5f - timeOfDay) * Mathf.PI * 2f + (Mathf.PI / 2f));
        float sunIntensity = Mathf.Clamp01(sunHeight);

        if (sunlight != null)
        {
            sunlight.color = currentSunColor;
            sunlight.intensity = sunIntensity;
        }
        if (sunSprite != null)
        {
            sunSprite.color = currentSunColor;
            sunSprite.enabled = sunIntensity > 0.01f; // Make sprite invisible when light is off
        }
        
        // Moon Light
        Color currentMoonColor = moonColor != null ? moonColor.Evaluate(timeOfDay) : Color.white;
        float moonIntensity = Mathf.Clamp01(-sunHeight) * 0.3f;

        if (moonlight != null)
        {
            moonlight.color = currentMoonColor;
            moonlight.intensity = moonIntensity;
        }
        if (moonSprite != null)
        {
            moonSprite.color = currentMoonColor;
            moonSprite.enabled = moonIntensity > 0.01f; // Make sprite invisible when light is off
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
