using UnityEngine;

public class StarField : MonoBehaviour
{
    public int starCount = 100;
    public Sprite starSprite;
    public Vector2 areaSize = new Vector2(20, 10);
    public float parallaxFactor = 0.05f; // Very distant
    
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
        if (starSprite)
        {
             renderer.material.mainTexture = starSprite.texture;
             // If using sprite sheet, need more setup, assuming simple texture for now
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
            particles[i].startColor = Color.white;
            particles[i].remainingLifetime = float.MaxValue;
        }
        
        particleSys.SetParticles(particles, starCount);
    }

    void LateUpdate()
    {
        if (!cam) return;

        Vector3 delta = cam.position - lastCamPos;
        transform.position += delta * (1f - parallaxFactor); // Move with camera but slightly slower? 
        // Actually for infinite distance (stars), they should move WITH the camera almost 1:1 so they appear static relative to world?
        // No, if they are infinite, they should stay at fixed screen coordinates mostly.
        // If parallaxFactor = 0 (background), they move with camera 100%.
        // If parallaxFactor = 1 (foreground), they don't move with camera.
        
        // Let's just stick them to camera and apply small offset
        transform.position = new Vector3(cam.position.x, cam.position.y, 10f);
        
        // Apply small parallax offset to particles? 
        // Or just move the whole container.
        // If we want them to move slightly as we move:
        // transform.position = cam.position + (cam.position * parallaxFactor); 
        // But we need to wrap them if they go off screen?
        // For stars, usually they are just static on screen or move very slowly.
        
        lastCamPos = cam.position;
    }
    
    // DayNightCycle will handle alpha via material or we can expose a method
    public void SetStarAlpha(float alpha)
    {
        if (particleSys == null) return;
        var main = particleSys.main;
        main.startColor = new Color(1,1,1, alpha);
        
        // Update existing particles
        int count = particleSys.GetParticles(particles);
        for(int i=0; i<count; i++)
        {
            Color c = particles[i].startColor;
            c.a = alpha;
            particles[i].startColor = c;
        }
        particleSys.SetParticles(particles, count);
    }
}
