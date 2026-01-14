using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AppUI.Redux;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.WSA;

public class TerrainGenerator : MonoBehaviour
{
    [Header("References (Collidable ground)")]
    public Tilemap tilemap;              // Ground tilemap (with collider)
    public TileBase[] grassVariants;     // 4 grass tiles
    public TileBase[] grassEdges;
    public TileBase[] dirtVariants;      // 4 dirt tiles
    public TileBase[] dirtEdges;      // 4 dirt tiles
    public TileBase[] dirtCorners;      // 4 dirt tiles
    public TileBase[] rockTiles;

    [Header("Decor (No colliders / pass-through)")]
    public Tilemap decoTilemap;          // Separate tilemap without any collider
    public TileBase trunkTile;           // 16x16 trunk tile
    public TileBase[] leafTiles;         // 1+ leaf variants (optional)

    [Header("Background Walls (No colliders)")]
    public Tilemap wallTilemap;          // Background wall tilemap
    public TileBase wallTile;            // The wall tile (e.g. darker dirt)

    [Header("World Settings")]
    public int chunkWidth = 16;
    public int renderDistanceInChunks = 6; // how many chunks left/right to keep loaded
    public int baseHeight = 8;             // average ground height (in tiles)
    public int heightScale = 6;            // hill height amplitude (in tiles)
    public float noiseFrequency = 0.05f;   // lower = wider hills
    public int fillDepth = 40;             // how far we fill dirt below surface
    public int seed = 12345;               // change to get a different world
    [Range(0f, 1f)] public float rockChance = 0.10f;
    public int minRockSize = 1;
    public int maxRockSize = 3;

    [Header("Tree Generation")]
    [Range(0f, 1f)] public float treeChance = 0.10f; // per-column probability
    public bool requireFlatSpot = true;   // avoid steep edges for tree base
    public int maxSlope = 1;              // max |height| with neighbors to allow a tree
    public int minTrunkHeight = 3;
    public int maxTrunkHeight = 6;
    public int minCanopyRadius = 2;
    public int maxCanopyRadius = 3;

    Transform target; // the player
    readonly HashSet<Vector2Int> activeChunks = new();
    readonly HashSet<Vector2Int> neededThisFrame = new();

    void Start()
    {
        target = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (target == null)
            Debug.LogWarning("TerrainGenerator: No object tagged 'Player' found. Will generate around (0,0).");

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

        // unload chunks
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

        
        int minY = baseHeight - fillDepth - 8;
        int maxY = baseHeight + heightScale + 8;

        
        if (decoTilemap != null)
            ClearDecoRange(startX, endX, minY, maxY);
        
        if (wallTilemap != null)
             ClearWallRange(startX, endX, minY, maxY);

        for (int x = startX; x < endX; x++)
        {
            int surfaceY = GetSurfaceHeight(x);

            // Grass for the surface
            if (tilemap.GetTile(new Vector3Int(x, surfaceY, 0)) == null)
            {
                var grassTile = VariantForPosition(grassVariants, x, surfaceY);
                tilemap.SetTile(new Vector3Int(x, surfaceY, 0), grassTile);
            }


            // Dirt below the surface
            for (int y = surfaceY - 1; y >= surfaceY - fillDepth; y--) 
            {
                if (tilemap.GetTile(new Vector3Int(x, y, 0)) == null)
                {
                    var dirtTile = VariantForPosition(dirtVariants, x, y);
                    tilemap.SetTile(new Vector3Int(x, y, 0), dirtTile);
                }
                
                // Place Wall behind dirt (and slightly above/below if needed)
                // We place walls everywhere underground so if you dig, there is a wall.
                if (wallTilemap != null && wallTile != null)
                {
                    // Start placing walls from a bit below surface to avoid them sticking out on slopes
                    // Or place them everywhere below surfaceY
                    if (y <= surfaceY) 
                    {
                        wallTilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
                    }
                }
            }

            int r = PositiveHash(x, surfaceY, seed);
            int rock_y = surfaceY - (r % fillDepth) + minRockSize;
            TryPlaceRock(tilemap, x, rock_y);

            // Clear above/below
            for (int y = surfaceY + 1; y <= maxY; y++)
                tilemap.SetTile(new Vector3Int(x, y, 0), null);
            for (int y = surfaceY - fillDepth - 1; y >= minY; y--)
                tilemap.SetTile(new Vector3Int(x, y, 0), null);

            
            if (decoTilemap != null)
                TryPlaceTreeAtColumn(x, surfaceY);
        }

        //Pass two to set edges and corners and stuff
        //Also shading
        //Covers a one tile wider range to fix tiles on the borders of chunks
        for (int x = startX-1; x < endX+1; x++)
        {
            int surfaceY = GetSurfaceHeight(x);

            // Grass for the surface
            if (grassVariants.Contains(tilemap.GetTile(new Vector3Int(x, surfaceY, 0))) || grassEdges.Contains(tilemap.GetTile(new Vector3Int(x, surfaceY, 0))))
            {
                TileBase grassEdge = VariantForPosition(grassVariants, x, surfaceY);
                if (tilemap.GetTile(new Vector3Int(x-1, surfaceY, 0)) == null && tilemap.GetTile(new Vector3Int(x + 1, surfaceY, 0)) == null)
                {
                    grassEdge = grassEdges[2];
                }
                else if (tilemap.GetTile(new Vector3Int(x - 1, surfaceY, 0)) == null)
                {
                    grassEdge = grassEdges[0];
                }
                else if (tilemap.GetTile(new Vector3Int(x + 1, surfaceY, 0)) == null)
                {
                    grassEdge = grassEdges[1];
                }
                tilemap.SetTile(new Vector3Int(x, surfaceY, 0), grassEdge);
            }

            // Dirt below the surface
            float depthShadowThing = Math.Max(1, fillDepth - 5);
            for (int y = surfaceY - 1; y >= surfaceY - fillDepth; y--)
            {
                //Currently only side edge tiles for dirt
                if (dirtVariants.Contains(tilemap.GetTile(new Vector3Int(x, y, 0))) ||
                    dirtCorners.Contains(tilemap.GetTile(new Vector3Int(x, y, 0))) ||
                    dirtEdges.Contains(tilemap.GetTile(new Vector3Int(x, y, 0))))
                {
                    var dirtTile = VariantForPosition(dirtVariants, x, y);

                    
                    if (tilemap.GetTile(new Vector3Int(x + 1, y, 0)) == null)
                    {
                        dirtTile = dirtEdges[4];
                    }
                    else if (tilemap.GetTile(new Vector3Int(x - 1, y, 0)) == null)
                    {
                        dirtTile = dirtEdges[3];
                    }
                    tilemap.SetTile(new Vector3Int(x, y, 0), dirtTile);
                }

                //Shading
                TileBase tile = tilemap.GetTile(new Vector3Int(x, y, 0));
                if (tile != null)
                {
                    Color color = Color.white;
                    color *= Math.Max(0, depthShadowThing / (float)Math.Max(1, fillDepth - 5));
                    color.a = 1f;
                    TileChangeData data = new TileChangeData(new Vector3Int(x, y, 0), tile, color, Matrix4x4.identity);
                    tilemap.SetTile(data, true);
                }
                depthShadowThing--;
            }
        }
    }

    void TryPlaceRock(Tilemap tilemap, int x, int y)
    {
        if (rockTiles.Length == 0) return;
        var rockTile = rockTiles[0];

        int r = PositiveHash(x, y, seed);
        if ((r % 10000) / 10000f > rockChance) return;

        int rockSize = (r % ((maxRockSize + 1) - minRockSize)) + minRockSize;

        //generate a rock
        int x_start = x - rockSize;
        int y_start = y - rockSize;
        if (rockSize == 1)
        {
            tilemap.SetTile(new Vector3Int(x_start, y_start, 0), rockTile);
            return;
        }
        //Safety check
        if (rockTiles.Length < 10) return;

        for (int i = x_start; i < x_start + rockSize; i++)
        {
            for (int j = y_start; j < y_start + rockSize; j++)
            {
                rockTile = rockTiles[1];
                //Awful variants code
                if (i == x_start)
                {
                    if (j == y_start)
                    {
                        //botton left corner
                        rockTile = rockTiles[7];
                    }
                    else if (j == y_start + rockSize - 1)
                    {
                        //top left corner
                        rockTile = rockTiles[2];
                    }
                    else
                    {
                        //left side
                        rockTile = rockTiles[5];
                    }
                }
                else if (i == x_start + rockSize - 1)
                {
                    if (j == y_start)
                    {
                        //botton right corner
                        rockTile = rockTiles[9];
                    }
                    else if (j == y_start + rockSize - 1)
                    {
                        //top right corner
                        rockTile = rockTiles[4];
                    }
                    else
                    {
                        //right side
                        rockTile = rockTiles[6];
                    }
                }
                else if (j == y_start)
                {
                    //bottom side
                    rockTile = rockTiles[8];
                }
                else if (j == y_start + rockSize - 1)
                {
                    //top side
                    rockTile = rockTiles[3];
                }
                
                tilemap.SetTile(new Vector3Int(i, j, 0), rockTile);
            }
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
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), null);
                if (decoTilemap != null)
                    decoTilemap.SetTile(new Vector3Int(x, y, 0), null);
                if (wallTilemap != null)
                    wallTilemap.SetTile(new Vector3Int(x, y, 0), null);
            }
        }
    }

    // clear deco once per chunk
    void ClearDecoRange(int startX, int endX, int minY, int maxY)
    {
        for (int x = startX; x < endX; x++)
        {
            if (ContainsTree(x, GetSurfaceHeight(x)))
            {
                ClearLeaves(x, GetSurfaceHeight(x));
            }
            for (int y = minY; y <= maxY; y++)
                //We're removing leaves a bit differently to avoid them getting cut off
                if (!leafTiles.Contains(decoTilemap.GetTile(new Vector3Int(x, y, 0))))
                {
                    decoTilemap.SetTile(new Vector3Int(x, y, 0), null);
                }
        }
    }
    
    void ClearWallRange(int startX, int endX, int minY, int maxY)
    {
        if (wallTilemap == null) return;
        for (int x = startX; x < endX; x++)
            for (int y = minY; y <= maxY; y++)
                wallTilemap.SetTile(new Vector3Int(x, y, 0), null);
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
            h ^= (h << 13);
            h ^= (h >> 17);
            h ^= (h << 5);
            if (h < 0) h = -h;
            return h;
        }
    }

    // Trees
    bool IsFlatEnough(int x, int surfaceY)
    {
        int hL = GetSurfaceHeight(x - 1);
        int hR = GetSurfaceHeight(x + 1);
        return Mathf.Abs(surfaceY - hL) <= maxSlope && Mathf.Abs(hR - surfaceY) <= maxSlope;
    }

    void TryPlaceTreeAtColumn(int x, int surfaceY)
    {
        if (decoTilemap == null || trunkTile == null) return;

        
        int r = PositiveHash(x, surfaceY, seed);
        float u01 = (r % 10000) / 10000f;

        if (u01 > treeChance) return;
        if (requireFlatSpot && !IsFlatEnough(x, surfaceY)) return;

        //Adjacent trees look bad
        if (ContainsTree(x + 1, surfaceY)) return;
        if (ContainsTree(x - 1, surfaceY)) return;

        int trunkH = Mathf.Clamp(
            minTrunkHeight + (r % (maxTrunkHeight - minTrunkHeight + 1)),
            minTrunkHeight, maxTrunkHeight);

        int canopyR = Mathf.Clamp(
            minCanopyRadius + ((r / 7) % (maxCanopyRadius - minCanopyRadius + 1)),
            minCanopyRadius, maxCanopyRadius);

        
        TileBase leaf = (leafTiles != null && leafTiles.Length > 0)
            ? leafTiles[0]
            : null;

        // place trunk
        int baseY = surfaceY + 1;
        for (int y = baseY; y < baseY + trunkH; y++)
            decoTilemap.SetTile(new Vector3Int(x, y, 0), trunkTile);

        // canopy origin at trunk top
        int cx = x;
        int cy = baseY + trunkH;

        
            
        PlaceTriangle(cx, cy, canopyR + 1, leaf); 
    }

    void PlaceTriangle(int cx, int cy, int h, TileBase leaf)
    {
        if (leaf == null) return;
        if (leafTiles.Length < 10) return;

        var leaf_start = leaf;
        
        for (int i = 0; i < h; i++)
        {
            int half = (h - 1) - i;   
            int y = cy + i;           
            for (int dx = -half; dx <= half; dx++)
            {
                leaf = leaf_start;
                if (i == 0)
                {
                    if (dx == -half)
                    {
                        //bottom left
                        //always single
                        leaf = leafTiles[8];
                    }
                    else if (dx == half)
                    {
                        //bottom right
                        //always single
                        leaf = leafTiles[9];
                    }
                    else
                    {
                        //bottom
                        leaf = leafTiles[5];
                    }
                }
                else if (i == h - 1)
                {
                    //top
                    //this is always single
                    leaf = leafTiles[7];
                }
                else if (dx == -half)
                {
                    //left top corner
                    leaf = leafTiles[1];
                }
                else if (dx == half)
                {
                    //right top corner
                    leaf = leafTiles[3];
                }

                //A bit of shading :)
                Color color = Color.white;
                color *= ((float)i / (float)h) * 0.2f + 0.8f;
                color.a = 1f;
                TileChangeData data = new TileChangeData(new Vector3Int(cx + dx, y, 0), leaf, color, Matrix4x4.identity);

                decoTilemap.SetTile(data, true);
            }
        }
    }

    bool ContainsTree(int x, int surfaceY)
    {
        if (decoTilemap == null || trunkTile == null) return false;


        int r = PositiveHash(x, surfaceY, seed);
        float u01 = (r % 10000) / 10000f;

        if (u01 > treeChance) return false;
        if (requireFlatSpot && !IsFlatEnough(x, surfaceY)) return false;

        return true;
    }

    void ClearLeaves(int x, int surfaceY)
    {
        int r = PositiveHash(x, surfaceY, seed);
        int canopyR = Mathf.Clamp(
            minCanopyRadius + ((r / 7) % (maxCanopyRadius - minCanopyRadius + 1)),
            minCanopyRadius, maxCanopyRadius);

        int baseY = surfaceY + 1;

        int trunkH = Mathf.Clamp(
            minTrunkHeight + (r % (maxTrunkHeight - minTrunkHeight + 1)),
            minTrunkHeight, maxTrunkHeight);

        int cx = x;
        int cy = baseY + trunkH;

        int h = canopyR + 1;

        for (int i = 0; i < h; i++)
        {
            int half = (h - 1) - i;
            int y = cy + i;
            for (int dx = -half; dx <= half; dx++)
                decoTilemap.SetTile(new Vector3Int(cx + dx, y, 0), null);
        }
    }
}