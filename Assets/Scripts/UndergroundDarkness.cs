using UnityEngine;
using UnityEngine.Tilemaps;

public class UndergroundDarkness : MonoBehaviour
{
    [Header("Settings")]
    public int surfaceLevel = 8; // Should match TerrainGenerator baseHeight
    public int darknessStartDepth = 5; // How many blocks down darkness starts
    public Color darknessColor = new Color(0.1f, 0.1f, 0.1f, 1f); // Dark tint

    [Header("References")]
    public Tilemap tilemap;
    
    // This script assumes we are using Tilemap Color to simulate darkness if not using URP Lights completely
    // Or we can use a big sprite overlay.
    // For Terraria style, usually we use a lighting engine that propagates light.
    // Since we are asked for "Global light should not penetrate deep underground",
    // and "Use tile-based light occlusion or a custom shader/mask".
    
    // A simple approach without a complex light engine:
    // Use a Tilemap for "Background Wall" or "Darkness Mask".
    // Or just tint the tiles based on depth in Update (expensive) or on Generation.
    
    // Let's assume we hook into TerrainGenerator or run once on Start.
    
    void Start()
    {
        if (!tilemap) tilemap = GetComponent<Tilemap>();
        ApplyDarkness();
    }

    public void ApplyDarkness()
    {
        if (!tilemap) return;

        BoundsInt bounds = tilemap.cellBounds;
        
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                if (y < surfaceLevel - darknessStartDepth)
                {
                    // Check if there is a tile
                    if (tilemap.HasTile(new Vector3Int(x, y, 0)))
                    {
                        // Lock tile color to dark, ignoring global light?
                        // In Unity 2D, SpriteRenderer/TilemapRenderer is affected by Global Light.
                        // To make it dark despite global light, we might need a Shadow Caster or 
                        // a separate material that ignores light, OR we rely on URP 2D Shadows.
                        
                        // If using URP 2D, the Global Light affects everything.
                        // To have underground dark, we need Shadow Casters on the surface blocks.
                        // But generating shadow casters for every block is expensive.
                        
                        // Alternative: Use a "Darkness" overlay tilemap with a semi-transparent black tile
                        // that is placed on top of underground tiles.
                        // This is the "Fog of War" approach.
                    }
                }
            }
        }
    }
}
