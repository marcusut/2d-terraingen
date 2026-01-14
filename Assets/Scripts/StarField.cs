using UnityEngine;

public class StarField : MonoBehaviour
{
    public int starCount = 100;
    public Sprite starSprite;
    public Vector2 areaSize = new Vector2(20, 10);
    public float parallaxFactor = 0.05f; // Very distant
    
    [Header("Sorting")]
    public string sortingLayerName = "Background";
    public int sortingOrder = -60; // Behind clouds (-50)

    private Transform cam;
    private Vector3 lastCamPos;
    private ParticleSystem particleSys;
    private ParticleSystem.Particle[] particles;

    void Start()
    {
        cam = Camera.main.transform;
        lastCamPos = cam.position;
        
        // Setup Particle System
        particleSys = gameObject.AddComponent<ParticleSystem>();
        var main = particleSys.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        
        var emission = particleSys.emission;
        emission.enabled = false;
        
        var shape = particleSys.shape;
        shape.enabled = false;

        var renderer = particleSys.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        
        // Set Sorting Layer
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;

        if (starSprite)
        {
             renderer.material.mainTexture = starSprite.texture;
        }

        // Spawn initial particles
        particles = new ParticleSystem.Particle[starCount];
        for(int i=0; i<starCount; i++)
        {
            particles[i].position = new Vector3(
                Random.Range(-areaSize.x, areaSize.x),
                Random.Range(-areaSize.y, areaSize.y),
                0
            );
            particles[i].startSize = Random.Range(0.1f, 0.3f);
            particles[i].startColor = new Color(1f, 1f, 1f, 0f); // Start invisible
            particles[i].remainingLifetime = float.MaxValue;
        }
        
        particleSys.SetParticles(particles, starCount);
    }

    void LateUpdate()
    {
        if (!cam) return;

        // Simple parallax: Move slightly opposite to camera movement to fake distance
        // Since it's a child of the camera, it moves WITH the camera by default (0 parallax).
        // To make it look like it's moving slower (parallax), we need to offset it.
        
        // Actually, for stars (infinite distance), they should stay fixed on screen or move very little.
        // Being a child of the camera keeps them fixed on screen (0 movement relative to camera).
        // To add parallax, we move them slightly against the camera movement?
        // No, if they are child of camera, they are locked. 
        // If we want them to move, we shift local position?
        
        // Let's keep it simple: Fixed to camera is fine for stars (infinite distance).
        // If we want slight movement:
        // transform.localPosition += (lastCamPos - cam.position) * parallaxFactor;
        
        // But we need to wrap them if they drift too far. 
        // For now, let's just keep them static relative to camera (infinite distance).
        
        lastCamPos = cam.position;
    }
    
    public void SetStarAlpha(float alpha)
    {
        if (particleSys == null) return;
        
        if (particles == null || particles.Length < starCount)
            particles = new ParticleSystem.Particle[starCount];

        int count = particleSys.GetParticles(particles);
        bool changed = false;
        
        for(int i=0; i<count; i++)
        {
            Color c = particles[i].startColor;
            if (Mathf.Abs(c.a - alpha) > 0.01f)
            {
                c.a = alpha;
                particles[i].startColor = c;
                changed = true;
            }
        }
        
        if (changed)
            particleSys.SetParticles(particles, count);
    }
}
