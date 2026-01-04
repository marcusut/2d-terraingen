using UnityEngine;

public class ParallaxLayer : MonoBehaviour
{
    [Range(0f, 1f)] public float parallaxFactor = 0.5f; // 0 = static, 1 = moves with camera
    public bool lockY = true;
    
    private Transform cam;
    private Vector3 lastCamPos;

    void Start()
    {
        cam = Camera.main.transform;
        lastCamPos = cam.position;
    }

    void LateUpdate()
    {
        Vector3 deltaMovement = cam.position - lastCamPos;
        
        float parallaxX = deltaMovement.x * parallaxFactor;
        float parallaxY = lockY ? 0 : deltaMovement.y * parallaxFactor;

        transform.position += new Vector3(parallaxX, parallaxY, 0);
        
        lastCamPos = cam.position;
    }
}
