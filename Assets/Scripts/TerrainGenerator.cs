using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TerrainGenerator : MonoBehaviour
{
    [Header("References")]
    public Tilemap tilemap;              // Assign the Ground tilemap
    public TileBase[] grassVariants;     // 4 grass tiles
    public TileBase[] dirtVariants;      // 4 dirt tiles

    [Header("World Settings")]
    public int chunkWidth = 16;
    public int renderDistanceInChunks = 6; // how many chunks left/right to keep loaded
    public int baseHeight = 8;            // average ground height (in tiles)
    public int heightScale = 6;           // hill height amplitude (in tiles)
    public float noiseFrequency = 0.05f;  // lower = wider hills
    public int fillDepth = 40;            // how far we fill dirt below surface
    public int seed = 12345;              // change to get a different world

    Transform target; // the player
    readonly HashSet<Vector2Int> activeChunks = new();
    readonly HashSet<Vector2Int> neededThisFrame = new();

    void Start()
    {
        target = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (target == null)
            Debug.LogWarning("TerrainGenerator: No object tagged 'Player' found. Will generate around (0,0).");

        // initial populate
        UpdateWorld(force: true);
    }

    void Update()
    {
        UpdateWorld(force: false);
    }

    void UpdateWorld(bool force)
    {
        var centerChunk = WorldToChunk(target ? target.position : Vector3.zero);
        neededThisFrame.Clear();

        for (int dx = -renderDistanceInChunks; dx <= renderDistanceInChunks; dx++)
        {
            var c = new Vector2Int(centerChunk.x + dx, 0);
            neededThisFrame.Add(c);

            if (force || !activeChunks.Contains(c))
            {
                GenerateChunk(c);
                activeChunks.Add(c);
            }
        }

        // unload chunks we no longer need
        var toRemove = new List<Vector2Int>();
        foreach (var c in activeChunks)
            if (!neededThisFrame.Contains(c))
                toRemove.Add(c);

        foreach (var c in toRemove)
        {
            ClearChunk(c);
            activeChunks.Remove(c);
        }
    }

    Vector2Int WorldToChunk(Vector3 worldPos)
    {
        int wx = Mathf.FloorToInt(worldPos.x);
        int cx = Mathf.FloorToInt((float)wx / chunkWidth);
        return new Vector2Int(cx, 0);
    }

    void GenerateChunk(Vector2Int chunk)
    {
        int startX = chunk.x * chunkWidth;
        int endX = startX + chunkWidth;

        // Reasonable vertical bounds to draw/clear
        int minY = baseHeight - fillDepth - 8;
        int maxY = baseHeight + heightScale + 8;

        for (int x = startX; x < endX; x++)
        {
            int surfaceY = GetSurfaceHeight(x);

            // Grass at the surface
            var grassTile = VariantForPosition(grassVariants, x, surfaceY);
            tilemap.SetTile(new Vector3Int(x, surfaceY, 0), grassTile);

            // Dirt below
            for (int y = surfaceY - 1; y >= surfaceY - fillDepth; y--)
            {
                var dirtTile = VariantForPosition(dirtVariants, x, y);
                tilemap.SetTile(new Vector3Int(x, y, 0), dirtTile);
            }

            // (Optional) Clear above/below just in case of previous tiles
            for (int y = surfaceY + 1; y <= maxY; y++)
                tilemap.SetTile(new Vector3Int(x, y, 0), null);
            for (int y = surfaceY - fillDepth - 1; y >= minY; y--)
                tilemap.SetTile(new Vector3Int(x, y, 0), null);
        }
    }

    void ClearChunk(Vector2Int chunk)
    {
        int startX = chunk.x * chunkWidth;
        int endX = startX + chunkWidth;

        int minY = baseHeight - fillDepth - 8;
        int maxY = baseHeight + heightScale + 8;

        for (int x = startX; x < endX; x++)
        {
            for (int y = minY; y <= maxY; y++)
                tilemap.SetTile(new Vector3Int(x, y, 0), null);
        }
    }

    int GetSurfaceHeight(int x)
    {
        float n = Mathf.PerlinNoise((x + seed) * noiseFrequency, 0f); // 0..1
        int h = baseHeight + Mathf.RoundToInt((n - 0.5f) * 2f * heightScale);
        return h;
    }

    TileBase VariantForPosition(TileBase[] variants, int x, int y)
    {
        if (variants == null || variants.Length == 0) return null;
        int idx = PositiveHash(x, y, seed) % variants.Length;
        return variants[idx];
    }

    // stable pseudo-random index per tile coordinate
    int PositiveHash(int x, int y, int s)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + x;
            h = h * 31 + y;
            h = h * 31 + s;
            // scramble a bit
            h ^= (h << 13);
            h ^= (h >> 17);
            h ^= (h << 5);
            if (h < 0) h = -h;
            return h;
        }
    }
}
