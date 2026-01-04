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
    public Light2D globalLight; // The ambient global light
    public Light2D sunlight;    // The directional sun light (casts shadows)
    public Camera mainCamera;
    public StarField starField; // Reference to StarField script

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
        // Ensure gradients are not null to prevent crashes if not assigned
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
        
        // The center of the orbit is offset by the horizon height and parallax
        float skyYOffset = camY * verticalParallax;
        float orbitCenterY = skyYOffset + horizonHeight + orbitRadius;

        float sunAngle = (0.5f - timeOfDay) * Mathf.PI * 2f + (Mathf.PI / 2f);

        if (sunTransform != null)
        {
            float sunX = Mathf.Cos(sunAngle) * orbitRadius;
            float sunY = orbitCenterY + Mathf.Sin(sunAngle) * orbitRadius;
            
            sunTransform.position = new Vector3(camX + sunX, sunY, 0);
            sunTransform.localScale = Vector3.one * celestialScale;
            
            float rotZ = (sunAngle - Mathf.PI) * Mathf.Rad2Deg;
            sunTransform.rotation = Quaternion.Euler(0, 0, rotZ);
        }

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
        
        // Sun
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
        
        // Moon
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
