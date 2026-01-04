using UnityEngine;

public class RainbowController : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer rainbowSprite;
    public DayNightCycle dayNightCycle;
    public Camera mainCamera;

    [Header("Settings")]
    public float fadeSpeed = 0.5f;
    public float minSunHeightForRainbow = -0.5f; // Rainbows appear when sun is low
    public float maxSunHeightForRainbow = 0.5f;
    public float probability = 0.1f; // Chance to appear when conditions are met

    private float targetAlpha = 0f;
    private float currentAlpha = 0f;
    private bool isRaining = false; // Hook this up to a weather system later

    void Start()
    {
        if (rainbowSprite)
        {
            Color c = rainbowSprite.color;
            c.a = 0f;
            rainbowSprite.color = c;
        }
        
        if (!dayNightCycle) dayNightCycle = FindObjectOfType<DayNightCycle>();
        if (!mainCamera) mainCamera = Camera.main;
    }

    void Update()
    {
        if (!rainbowSprite || !dayNightCycle) return;

        // Determine if rainbow should be visible
        // For now, let's just use sun position. In a real game, check for rain too.
        // float sunHeight = Mathf.Sin((dayNightCycle.timeOfDay - 0.25f) * Mathf.PI * 2f);
        
        // Simplified logic: Show rainbow occasionally if sun is low and "raining" (simulated)
        // For this example, we'll just toggle it based on time of day to show it works.
        // Real logic: if (isRaining && sunHeight > min && sunHeight < max) ...

        // Let's make it appear at sunrise/sunset for demo purposes
        float time = dayNightCycle.timeOfDay;
        bool conditionsMet = (time > 0.2f && time < 0.3f) || (time > 0.7f && time < 0.8f); 
        
        targetAlpha = conditionsMet ? 0.8f : 0f;

        // Smooth fade
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
        
        Color c = rainbowSprite.color;
        c.a = currentAlpha;
        rainbowSprite.color = c;

        // Position relative to camera (parallax)
        if (mainCamera && currentAlpha > 0.01f)
        {
            Vector3 camPos = mainCamera.transform.position;
            // Keep rainbow at a fixed distance relative to camera, maybe with slight parallax
            transform.position = new Vector3(camPos.x, transform.position.y, transform.position.z);
        }
    }
    
    // Call this from WeatherSystem
    public void SetRaining(bool raining)
    {
        isRaining = raining;
    }
}
