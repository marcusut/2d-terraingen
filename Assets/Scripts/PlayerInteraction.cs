using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using System.Linq;

public class PlayerInteraction : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Tilemap groundTilemap;
    public Tilemap wallTilemap;
    public TileBase[] unbreakableTiles;
    public GameObject torchPrefab;

    [Header("Settings")]
    public float maxDistance = 6f;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (groundTilemap == null) groundTilemap = GameObject.Find("Ground")?.GetComponent<Tilemap>();
        if (wallTilemap == null) wallTilemap = GameObject.Find("WallLayer")?.GetComponent<Tilemap>();
    }

    void Update()
    {
        if (!cam || !groundTilemap) return;

        // MB1: Mine (Tiles or Torches)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryMineAtMouse();
        }

        // MB2: Place Torch
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryPlaceTorch();
        }
    }

    void TryMineAtMouse()
    {
        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
        world.z = 0f;

        if (maxDistance > 0f && Vector2.Distance(transform.position, world) > maxDistance)
            return;

        // 1. Check for Torch first (since it sits on top)
        Collider2D hit = Physics2D.OverlapPoint(world);
        if (hit != null)
        {
            Torch torch = hit.GetComponent<Torch>();
            if (torch == null) torch = hit.GetComponentInParent<Torch>();
            
            if (torch != null)
            {
                Destroy(torch.gameObject);
                return; // Don't mine the block underneath if we just broke a torch
            }
        }

        // 2. Check for Tile
        Vector3Int cell = groundTilemap.WorldToCell(world);
        TileBase t = groundTilemap.GetTile(cell);
        
        if (t != null)
        {
            if (unbreakableTiles != null && unbreakableTiles.Contains(t)) return;
            groundTilemap.SetTile(cell, null);
        }
    }

    void TryPlaceTorch()
    {
        if (torchPrefab == null) return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
        world.z = 0f;

        if (maxDistance > 0f && Vector2.Distance(transform.position, world) > maxDistance)
            return;

        Vector3Int cell = groundTilemap.WorldToCell(world);

        // Requirement 1: Must be empty space (no ground tile)
        bool isOccupied = groundTilemap.HasTile(cell);
        if (isOccupied) return;

        // Requirement 2: Must have support underneath
        Vector3Int below = cell + Vector3Int.down;
        bool hasSupport = groundTilemap.HasTile(below);

        if (hasSupport)
        {
            Vector3 placePos = groundTilemap.GetCellCenterWorld(cell);
            
            // Requirement 3: Check for existing torch using OverlapPoint
            Collider2D hit = Physics2D.OverlapPoint(placePos);
            if (hit != null)
            {
                if (hit.GetComponent<Torch>() != null || hit.GetComponentInParent<Torch>() != null)
                {
                    return; // Already a torch here
                }
            }

            GameObject torch = Instantiate(torchPrefab, placePos, Quaternion.identity);
            
            var tp = torch.GetComponent<TorchPhysics>();
            if (tp == null) tp = torch.AddComponent<TorchPhysics>();
            tp.groundTilemap = groundTilemap;
            tp.cellPosition = cell;
        }
    }
}
