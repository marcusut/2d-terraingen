using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    public Transform target;                 // drag your Player here
    public Vector3 offset = new Vector3(0, 2, -10);
    public float smooth = 10f;

    void Start()
    {
        if (target) transform.position = target.position + offset; // snap on start
    }

    void LateUpdate()
    {
        if (!target) return;
        transform.position = Vector3.Lerp(transform.position, target.position + offset, smooth * Time.deltaTime);
    }
}
