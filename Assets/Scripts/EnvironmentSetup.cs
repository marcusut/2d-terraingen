using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class EnvironmentSetup : MonoBehaviour
{
    [ContextMenu("Setup Day/Night System")]
    public void Setup()
    {
        // 1. Create Managers
        var managers = GameObject.Find("Managers");
        if (!managers) managers = new GameObject("Managers");

        // DayNightCycle
        var dnc = managers.GetComponent<DayNightCycle>();
        if (!dnc) dnc = managers.AddComponent<DayNightCycle>();

        // CloudManager
        var cm = managers.GetComponent<CloudManager>();
        if (!cm) cm = managers.AddComponent<CloudManager>();
        
        // Use reflection to set private field 'dayNightCycle' in CloudManager
        var dncField = typeof(CloudManager).GetField("dayNightCycle", BindingFlags.NonPublic | BindingFlags.Instance);
        if (dncField != null)
        {
            dncField.SetValue(cm, dnc);
        }

        // 2. Create Lights (URP)
        var lightsRoot = GameObject.Find("Lights");
        if (!lightsRoot) lightsRoot = new GameObject("Lights");

        // Global Light
        var globalLightGo = GameObject.Find("Global Light 2D");
        if (!globalLightGo)
        {
            globalLightGo = new GameObject("Global Light 2D") { transform = { parent = lightsRoot.transform } };
            var light2D = globalLightGo.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
            dnc.globalLight = light2D;
        }
        else
        {
            dnc.globalLight = globalLightGo.GetComponent<Light2D>();
        }

        // Sun
        var sunGo = GameObject.Find("Sun");
        if (!sunGo)
        {
            sunGo = new GameObject("Sun") { transform = { parent = lightsRoot.transform } };
            // Add Light2D if URP
            var light2D = sunGo.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Point; // Or Directional if using shadows
            light2D.pointLightOuterRadius = 50f;
            dnc.sunTransform = sunGo.transform;
            dnc.sunlight = light2D;
        }
        else
        {
            dnc.sunTransform = sunGo.transform;
            dnc.sunlight = sunGo.GetComponent<Light2D>();
        }

        // Moon
        var moonGo = GameObject.Find("Moon");
        if (!moonGo)
        {
            moonGo = new GameObject("Moon") { transform = { parent = lightsRoot.transform } };
            var light2D = moonGo.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Point;
            light2D.pointLightOuterRadius = 50f;
            dnc.moonTransform = moonGo.transform;
        }
        else
        {
            dnc.moonTransform = moonGo.transform;
        }

        // 3. Stars
        var starsGo = GameObject.Find("StarField");
        if (!starsGo)
        {
            starsGo = new GameObject("StarField") { transform = { parent = Camera.main ? Camera.main.transform : null } };
            var sf = starsGo.AddComponent<StarField>();
            dnc.starField = sf;
        }
        else
        {
            dnc.starField = starsGo.GetComponent<StarField>();
        }

        // 4. Rainbow
        var rainbowGo = GameObject.Find("Rainbow");
        if (!rainbowGo)
        {
            rainbowGo = new GameObject("Rainbow") { transform = { parent = Camera.main ? Camera.main.transform : null, localPosition = new Vector3(0, 2, 10) } };
            var sr = rainbowGo.AddComponent<SpriteRenderer>();
            // Assign a sprite if available, or user must do it
            var rc = rainbowGo.AddComponent<RainbowController>();
            rc.rainbowSprite = sr;
            rc.dayNightCycle = dnc;
        }

        // 5. Connect Camera
        dnc.mainCamera = Camera.main;
        if (cm.cam == null) cm.cam = Camera.main;

        Debug.Log("Environment Setup Complete. Please assign Sprites and Gradients in the Inspector.");
    }

    [ContextMenu("Setup Default Gradients")]
    public void SetupDefaultGradients()
    {
        var dnc = FindFirstObjectByType<DayNightCycle>();
        if (dnc == null)
        {
            Debug.LogError("DayNightCycle not found!");
            return;
        }

        // Sky Color
        dnc.skyColor = CreateGradient(
            new GradientColorKey[] { 
                new(new Color(0.05f, 0.05f, 0.1f), 0.0f),
                new(new Color(0.05f, 0.05f, 0.15f), 0.2f),
                new(new Color(0.8f, 0.4f, 0.2f), 0.25f),
                new(new Color(0.4f, 0.7f, 1.0f), 0.3f),
                new(new Color(0.5f, 0.8f, 1.0f), 0.5f),
                new(new Color(0.8f, 0.5f, 0.2f), 0.75f),
                new(new Color(0.1f, 0.05f, 0.15f), 0.8f),
                new(new Color(0.05f, 0.05f, 0.1f), 1.0f)
            },
            new GradientAlphaKey[] { new(1f, 0f), new(1f, 1f) }
        );

        // Sun Color: Fade alpha to 0 at night
        dnc.sunColor = CreateGradient(
            new GradientColorKey[] {
                new(Color.black, 0.0f),
                new(Color.black, 0.2f),
                new(new Color(1f, 0.5f, 0f), 0.25f),
                new(Color.white, 0.5f),
                new(new Color(1f, 0.3f, 0f), 0.75f),
                new(Color.black, 0.8f),
                new(Color.black, 1.0f)
            },
            new GradientAlphaKey[] { 
                new(0f, 0.0f), 
                new(0f, 0.2f), 
                new(1f, 0.25f), 
                new(1f, 0.75f), 
                new(0f, 0.8f), 
                new(0f, 1.0f) 
            }
        );

        // Moon Color: Fade alpha to 0 during day
        dnc.moonColor = CreateGradient(
            new GradientColorKey[] {
                new(new Color(0.8f, 0.9f, 1f), 0.0f),
                new(new Color(0.8f, 0.9f, 1f), 0.2f),
                new(Color.black, 0.25f),
                new(Color.black, 0.75f),
                new(new Color(0.8f, 0.9f, 1f), 0.8f),
                new(new Color(0.8f, 0.9f, 1f), 1.0f)
            },
            new GradientAlphaKey[] { 
                new(1f, 0.0f), 
                new(1f, 0.2f), 
                new(0f, 0.25f), 
                new(0f, 0.75f), 
                new(1f, 0.8f), 
                new(1f, 1.0f) 
            }
        );

        // Ambient Light
        dnc.ambientLightColor = CreateGradient(
            new GradientColorKey[] {
                new(new Color(0.1f, 0.1f, 0.2f), 0.0f),
                new(new Color(0.1f, 0.1f, 0.25f), 0.2f),
                new(new Color(0.6f, 0.5f, 0.4f), 0.25f),
                new(Color.white, 0.5f),
                new(new Color(0.6f, 0.4f, 0.3f), 0.75f),
                new(new Color(0.1f, 0.1f, 0.25f), 0.8f),
                new(new Color(0.1f, 0.1f, 0.2f), 1.0f)
            },
            new GradientAlphaKey[] { new(1f, 0f), new(1f, 1f) }
        );
        
        // Cloud Tint
        dnc.cloudTint = CreateGradient(
            new GradientColorKey[] {
                new(new Color(0.3f, 0.3f, 0.5f), 0.0f),
                new(new Color(0.3926471f, 0.33970588f, 0.5132353f), 0.15f),
                new(new Color(1f, 0.6f, 0.6f), 0.25f),
                new(Color.white, 0.4f),
                new(Color.white, 0.6f),
                new(new Color(1f, 0.5f, 0.5f), 0.75f),
                new(new Color(0.39215687f, 0.34117648f, 0.5137255f), 0.85f),
                new(new Color(0.3f, 0.3f, 0.5f), 1.0f)
            },
            new GradientAlphaKey[] { new(1f, 0f), new(1f, 1f) }
        );

        Debug.Log("Default Gradients Applied to DayNightCycle.");
    }

    Gradient CreateGradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
    {
        var g = new Gradient();
        g.SetKeys(colors, alphas);
        return g;
    }
}
