using UnityEngine;
using UnityEngine.Tilemaps;

public class TorchPhysics : MonoBehaviour
{
    public Tilemap groundTilemap;
    public Vector3Int cellPosition;
    
    private bool isFalling = false;
    private Vector3 velocity;

    void Update()
    {
        if (isFalling)
        {
            // Simple falling logic
            velocity += Physics.gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(0, 0, 360f * Time.deltaTime); // Spin while falling

            if (transform.position.y < -100) // Destroy if fallen off world
            {
                Destroy(gameObject);
            }
            return;
        }

        if (groundTilemap == null) return;

        // Check if support block is gone
        Vector3Int below = cellPosition + Vector3Int.down;
        if (!groundTilemap.HasTile(below))
        {
            isFalling = true;
            // Add a random horizontal pop
            velocity = new Vector3(Random.Range(-2f, 2f), 2f, 0f);
        }
    }
}
