using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using System.Linq;

public class TileMiner : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Tilemap groundTilemap;
    public Tilemap wallTilemap;
    public TileBase[] unbreakableTiles;

    [Header("Mining")]
    public float maxDistance = 6f;

    void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        if (!cam || !groundTilemap) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryMineAtMouse();
        }
    }

    void TryMineAtMouse()
    {
        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
        world.z = 0f;

        if (maxDistance > 0f && Vector2.Distance(transform.position, world) > maxDistance)
            return;

        Vector3Int cell = groundTilemap.WorldToCell(world);

        TileBase t = groundTilemap.GetTile(cell);
        if (t == null) return;
        if (unbreakableTiles.Contains(t)) return;

        groundTilemap.SetTile(cell, null);

        if (wallTilemap != null)
            wallTilemap.SetTile(cell, null);
    }
}
