using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TerrainGenerator : MonoBehaviour
{
    [Header("References (Collidable ground)")]
    public Tilemap tilemap;              // Ground tilemap (with collider)
    public TileBase[] grassVariants;     // 4 grass tiles
    public TileBase[] grassEdges;
    public TileBase[] dirtVariants;      // 4 dirt tiles
    public TileBase[] dirtEdges;         // dirt edge tiles (make sure indexes exist!)
    public TileBase[] rockTiles;
    public TileBase worldBottomTile;

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

    [Header("Cave Generation")]
    public bool generateCaves = true;
    public float caveFrequency = 0.08f;                 // lower = bigger features
    [Range(0f, 1f)] public float caveThreshold = 0.58f; // higher = fewer caves
    public int caveSurfaceBuffer = 3;                   // keeps a roof for noise-caves (entrances ignore this)
    public bool useDomainWarp = true;
    public float caveWarpFrequency = 0.03f;
    public float caveWarpAmplitude = 6f;
    [Range(1, 6)] public int caveOctaves = 3;

    [Header("Cave Entrances (natural, diagonal/sideways)")]
    [Range(0f, 1f)] public float caveEntranceChance = 0.03f; // per-column chance
    public bool entranceRequireFlatSpot = true;
    [Range(4, 80)] public int entranceMinSteps = 14;          // length of entrance "worm"
    [Range(4, 120)] public int entranceMaxSteps = 32;
    [Range(0, 30)] public int entranceMaxHorizontalDrift = 8; // how far sideways it can wander from the opening
    [Range(1, 3)] public int entranceRadius = 1;              // tunnel thickness along the path
    [Range(1, 5)] public int entranceMouthRadius = 2;         // bigger opening at the surface
    [Range(0, 12)] public int entranceMouthSteps = 4;         // how many steps use mouth radius

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

        // Precompute entrance carve cells for this chunk (+margin), so entrances can wander diagonally/sideways
        // without chunk seams.
        HashSet<Vector2Int> entranceCarve = BuildEntranceCarveSet(startX, endX, minY, maxY);

        for (int x = startX; x < endX; x++)
        {
            int surfaceY = GetSurfaceHeight(x);

            // Surface: place grass unless entrance carved this cell
            if (entranceCarve.Contains(new Vector2Int(x, surfaceY)))
            {
                tilemap.SetTile(new Vector3Int(x, surfaceY, 0), null);
                if (wallTilemap != null && wallTile != null)
                    wallTilemap.SetTile(new Vector3Int(x, surfaceY, 0), wallTile);
            }
            else
            {
                if (tilemap.GetTile(new Vector3Int(x, surfaceY, 0)) == null)
                {
                    var grassTile = VariantForPosition(grassVariants, x, surfaceY);
                    tilemap.SetTile(new Vector3Int(x, surfaceY, 0), grassTile);
                }
            }

            // Dirt below the surface, with cave carving + entrance carving
            for (int y = surfaceY - 1; y >= surfaceY - fillDepth; y--)
            {
                bool isEntrance = entranceCarve.Contains(new Vector2Int(x, y));
                bool isCave = isEntrance || IsCaveCell(x, y, surfaceY);

                if (!isCave)
                {
                    if (tilemap.GetTile(new Vector3Int(x, y, 0)) == null)
                    {
                        var dirtTile = VariantForPosition(dirtVariants, x, y);
                        tilemap.SetTile(new Vector3Int(x, y, 0), dirtTile);
                    }
                }
                else
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), null);
                }

                // Wall behind everything underground (including caves/entrances)
                if (wallTilemap != null && wallTile != null)
                    wallTilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
            }

            // Rocks (avoid placing inside carved-out empty space)
            if (x >= startX + maxRockSize)
            {
                int r = PositiveHash(x, surfaceY, seed);
                int rock_y = surfaceY - (r % fillDepth) + minRockSize;
                TryPlaceRock(tilemap, x, rock_y);
            }

            // Clear above/below
            for (int y = surfaceY + 1; y <= maxY; y++)
                tilemap.SetTile(new Vector3Int(x, y, 0), null);
            for (int y = surfaceY - fillDepth - 1; y >= minY; y--)
                tilemap.SetTile(new Vector3Int(x, y, 0), null);

            // Don't place trees at (or right next to) entrances
            if (decoTilemap != null)
            {
                bool nearEntrance =
                    entranceCarve.Contains(new Vector2Int(x, surfaceY)) ||
                    entranceCarve.Contains(new Vector2Int(x - 1, GetSurfaceHeight(x - 1))) ||
                    entranceCarve.Contains(new Vector2Int(x + 1, GetSurfaceHeight(x + 1)));

                if (!nearEntrance)
                    TryPlaceTreeAtColumn(x, surfaceY);
            }

            // Fill the bottom of the world with the world bottom tile
            if (worldBottomTile != null)
            {
                for (int y = surfaceY - fillDepth - 1; y >= surfaceY - fillDepth - 3; y--)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), worldBottomTile);
                }
            }
        }

        // Pass two to set edges / shading (covers 1 tile wider for chunk borders)
        for (int x = startX - 1; x < endX + 1; x++)
        {
            int surfaceY = GetSurfaceHeight(x);

            // Grass edges
            if (grassVariants.Contains(tilemap.GetTile(new Vector3Int(x, surfaceY, 0))) ||
                grassEdges.Contains(tilemap.GetTile(new Vector3Int(x, surfaceY, 0))))
            {
                TileBase grassEdge = VariantForPosition(grassVariants, x, surfaceY);

                if (tilemap.GetTile(new Vector3Int(x - 1, surfaceY, 0)) == null &&
                    tilemap.GetTile(new Vector3Int(x + 1, surfaceY, 0)) == null)
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

            // Dirt edges + shading
            float depthShadowThing = Math.Max(1, fillDepth - 5);
            for (int y = surfaceY - 1; y >= surfaceY - fillDepth; y--)
            {
                if (dirtVariants.Contains(tilemap.GetTile(new Vector3Int(x, y, 0))) ||
                    dirtEdges.Contains(tilemap.GetTile(new Vector3Int(x, y, 0))))
                {
                    var dirtTile = VariantForPosition(dirtVariants, x, y);
                    if (dirtEdges.Length >= 15)
                    {
                        int dirtIndex = 0;
                        //We add a number to the state if we have a tile in that direction
                        if (tilemap.GetTile(new Vector3Int(x - 1, y, 0)) != null)
                        {
                            dirtIndex += 1; //Tile on the left
                        }
                        if (tilemap.GetTile(new Vector3Int(x + 1, y, 0)) != null)
                        {
                            dirtIndex += 2; //Tile on the right
                        }
                        if (tilemap.GetTile(new Vector3Int(x, y + 1, 0)) != null)
                        {
                            dirtIndex += 4; //Tile on top
                        }
                        if (tilemap.GetTile(new Vector3Int(x, y - 1, 0)) != null)
                        {
                            dirtIndex += 8; //Tile on bottom
                        }
                        
                        if (dirtIndex != 15)
                        {
                            dirtTile = dirtEdges[dirtIndex];
                        }
                    }

                    tilemap.SetTile(new Vector3Int(x, y, 0), dirtTile);
                }

                // Shading
                TileBase tile = tilemap.GetTile(new Vector3Int(x, y, 0));
                if (tile != null)
                {
                    Color color = Color.white;
                    color *= Math.Max(0.35f, depthShadowThing / (float)Math.Max(1, fillDepth - 5));
                    color.a = 1f;
                    TileChangeData data = new TileChangeData(new Vector3Int(x, y, 0), tile, color, Matrix4x4.identity);
                    tilemap.SetTile(data, true);
                }
                if (wallTilemap != null)
                {
                    tile = wallTilemap.GetTile(new Vector3Int(x, y, 0));
                    if (tile != null)
                    {
                        Color color = Color.white;
                        color *= Math.Max(0.5f, depthShadowThing / (float)Math.Max(1, fillDepth - 5));
                        color.a = 1f;
                        TileChangeData data = new TileChangeData(new Vector3Int(x, y, 0), tile, color, Matrix4x4.identity);
                        wallTilemap.SetTile(data, true);
                    }
                }
                depthShadowThing--;
            }
        }
    }

    // --- ENTRANCES (worm-carved) ---

    HashSet<Vector2Int> BuildEntranceCarveSet(int startX, int endX, int minY, int maxY)
    {
        var set = new HashSet<Vector2Int>();
        if (!generateCaves || caveEntranceChance <= 0f) return set;

        // Scan a wider X-range so entrances that start outside the chunk but drift into it
        // still get carved consistently.
        int margin = entranceMaxHorizontalDrift + entranceMouthRadius + 2;
        int scanStart = startX - margin;
        int scanEnd = endX + margin;

        for (int x0 = scanStart; x0 < scanEnd; x0++)
        {
            int surfaceY0 = GetSurfaceHeight(x0);
            if (!HasCaveEntrance(x0, surfaceY0)) continue;

            foreach (var cell in GenerateEntrancePathCells(x0, surfaceY0))
            {
                // Keep only cells in the vertical band we care about for this chunk rendering.
                if (cell.y < minY || cell.y > maxY) continue;

                // We still add cells even if x is outside chunk range; checking later is cheap.
                set.Add(cell);
            }
        }

        return set;
    }

    IEnumerable<Vector2Int> GenerateEntrancePathCells(int startX, int surfaceY)
    {
        // Deterministic RNG based on opening position
        int rngSeed = PositiveHash(startX, surfaceY, seed ^ 0x4D2C1B7F);
        var rng = new DeterministicRng(rngSeed);

        int steps = Mathf.Clamp(
            rng.RangeInclusive(entranceMinSteps, entranceMaxSteps),
            1, 200);

        int x = startX;
        int y = surfaceY;

        // Direction bias: generally downwards, with occasional sideways.
        // We cap horizontal drift to keep it local (and chunk-safe with our margin).
        for (int i = 0; i < steps; i++)
        {
            int radius = (i < entranceMouthSteps) ? entranceMouthRadius : entranceRadius;
            foreach (var c in CarveDiamondCells(x, y, radius))
                yield return c;

            // Decide next move
            // 0..1
            float u = rng.Next01();

            int nx = x;
            int ny = y;

            // Strong downward bias, but can zig-zag and sometimes go sideways
            if (u < 0.50f)
            {
                // down
                ny -= 1;
            }
            else if (u < 0.70f)
            {
                // down-left
                nx -= 1; ny -= 1;
            }
            else if (u < 0.90f)
            {
                // down-right
                nx += 1; ny -= 1;
            }
            else if (u < 0.95f)
            {
                // left
                nx -= 1;
            }
            else
            {
                // right
                nx += 1;
            }

            // Enforce drift limit from entrance mouth
            nx = Mathf.Clamp(nx, startX - entranceMaxHorizontalDrift, startX + entranceMaxHorizontalDrift);

            // Keep moving down overall (avoid long flat tunnels right at the surface)
            // If we didn't go down for a couple steps, force a down step occasionally.
            if (ny == y && i < 6 && (i % 2 == 1))
                ny -= 1;

            x = nx;
            y = ny;

            // stop if we've gone deep enough into the ground fill
            if ((surfaceY - y) > fillDepth) break;
        }
    }

    IEnumerable<Vector2Int> CarveDiamondCells(int cx, int cy, int r)
    {
        // Manhattan "circle" (diamond) - looks good in tiles and is cheap.
        for (int dx = -r; dx <= r; dx++)
        {
            int rem = r - Mathf.Abs(dx);
            for (int dy = -rem; dy <= rem; dy++)
                yield return new Vector2Int(cx + dx, cy + dy);
        }
    }

    // Deterministic: per-column decision to start an entrance
    bool HasCaveEntrance(int x, int surfaceY)
    {
        int r = PositiveHash(x, surfaceY, seed ^ 0x6C8E9CF5);
        float u01 = (r % 10000) / 10000f;

        if (u01 > caveEntranceChance) return false;
        if (entranceRequireFlatSpot && !IsFlatEnough(x, surfaceY)) return false;

        // avoid entrances immediately adjacent (keeps it nicer)
        int sL = GetSurfaceHeight(x - 1);
        int sR = GetSurfaceHeight(x + 1);

        int rL = PositiveHash(x - 1, sL, seed ^ 0x6C8E9CF5);
        int rR = PositiveHash(x + 1, sR, seed ^ 0x6C8E9CF5);

        float uL = (rL % 10000) / 10000f;
        float uR = (rR % 10000) / 10000f;

        if (uL <= caveEntranceChance) return false;
        if (uR <= caveEntranceChance) return false;

        return true;
    }

    // --- NOISE CAVES ---

    bool IsCaveCell(int x, int y, int surfaceY)
    {
        if (!generateCaves) return false;

        int depth = surfaceY - y; // 1 = just below surface
        if (depth < caveSurfaceBuffer) return false;
        if (depth > fillDepth) return false;

        float depth01 = Mathf.InverseLerp(caveSurfaceBuffer, fillDepth, depth);
        float threshold = Mathf.Lerp(caveThreshold + 0.08f, caveThreshold - 0.05f, depth01);

        float wx = 0f, wy = 0f;
        if (useDomainWarp)
        {
            wx = (Mathf.PerlinNoise((x + seed) * caveWarpFrequency, (y + seed) * caveWarpFrequency) - 0.5f) * caveWarpAmplitude;
            wy = (Mathf.PerlinNoise((x - seed) * caveWarpFrequency, (y - seed) * caveWarpFrequency) - 0.5f) * caveWarpAmplitude;
        }

        float n = FractalPerlin01((x + seed + wx) * caveFrequency, (y + seed + wy) * caveFrequency, Mathf.Max(1, caveOctaves));
        return n > threshold;
    }

    float FractalPerlin01(float x, float y, int octaves)
    {
        float sum = 0f;
        float amp = 1f;
        float freq = 1f;
        float norm = 0f;

        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }

        return sum / Mathf.Max(0.0001f, norm);
    }

    // Small deterministic RNG for entrance worms
    struct DeterministicRng
    {
        uint state;

        public DeterministicRng(int seed)
        {
            state = (uint)seed;
            if (state == 0) state = 1;
        }

        uint NextUInt()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        public float Next01()
        {
            // 0..1 (exclusive of 1)
            return (NextUInt() & 0x00FFFFFF) / 16777216f;
        }

        public int RangeInclusive(int min, int max)
        {
            if (max < min) (min, max) = (max, min);
            int span = (max - min) + 1;
            return min + (int)(Next01() * span);
        }
    }

    // --- ROCKS ---

    void TryPlaceRock(Tilemap tilemap, int x, int y)
    {
        if (rockTiles == null || rockTiles.Length == 0) return;

        // Don't place rocks into empty space (e.g., caves)
        if (tilemap.GetTile(new Vector3Int(x, y, 0)) == null) return;

        var rockTile = rockTiles[0];

        int r = PositiveHash(x, y, seed);
        if ((r % 10000) / 10000f > rockChance) return;

        int rockSize = (r % ((maxRockSize + 1) - minRockSize)) + minRockSize;

        int x_start = x - rockSize;
        int y_start = y - rockSize;

        if (rockSize == 1)
        {
            tilemap.SetTile(new Vector3Int(x_start, y_start, 0), rockTile);
            return;
        }

        if (rockTiles.Length < 10) return;

        for (int i = x_start; i < x_start + rockSize; i++)
        {
            for (int j = y_start; j < y_start + rockSize; j++)
            {
                rockTile = rockTiles[1];

                if (i == x_start)
                {
                    if (j == y_start) rockTile = rockTiles[7];                  // bottom left
                    else if (j == y_start + rockSize - 1) rockTile = rockTiles[2]; // top left
                    else rockTile = rockTiles[5];                                // left
                }
                else if (i == x_start + rockSize - 1)
                {
                    if (j == y_start) rockTile = rockTiles[9];                  // bottom right
                    else if (j == y_start + rockSize - 1) rockTile = rockTiles[4]; // top right
                    else rockTile = rockTiles[6];                                // right
                }
                else if (j == y_start) rockTile = rockTiles[8];                  // bottom
                else if (j == y_start + rockSize - 1) rockTile = rockTiles[3];   // top

                tilemap.SetTile(new Vector3Int(i, j, 0), rockTile);
            }
        }
    }

    // --- CLEAR / CHUNK MGMT ---

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

    void ClearDecoRange(int startX, int endX, int minY, int maxY)
    {
        for (int x = startX; x < endX; x++)
        {
            if (ContainsTree(x, GetSurfaceHeight(x)))
            {
                ClearLeaves(x, GetSurfaceHeight(x));
            }

            for (int y = minY; y <= maxY; y++)
            {
                if (leafTiles == null || !leafTiles.Contains(decoTilemap.GetTile(new Vector3Int(x, y, 0))))
                {
                    decoTilemap.SetTile(new Vector3Int(x, y, 0), null);
                }
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

    // --- HEIGHT / VARIANTS / HASH ---

    int GetSurfaceHeight(int x)
    {
        float n = Mathf.PerlinNoise((x + seed) * noiseFrequency, 0f);
        int h = baseHeight + Mathf.RoundToInt((n - 0.5f) * 2f * heightScale);
        return h;
    }

    TileBase VariantForPosition(TileBase[] variants, int x, int y)
    {
        if (variants == null || variants.Length == 0) return null;
        int idx = PositiveHash(x, y, seed) % variants.Length;
        return variants[idx];
    }

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

    // --- TREES ---

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

        if (ContainsTree(x + 1, surfaceY)) return;
        if (ContainsTree(x - 1, surfaceY)) return;

        int trunkH = Mathf.Clamp(
            minTrunkHeight + (r % (maxTrunkHeight - minTrunkHeight + 1)),
            minTrunkHeight, maxTrunkHeight);

        int canopyR = Mathf.Clamp(
            minCanopyRadius + ((r / 7) % (maxCanopyRadius - minCanopyRadius + 1)),
            minCanopyRadius, maxCanopyRadius);

        TileBase leaf = (leafTiles != null && leafTiles.Length > 0) ? leafTiles[0] : null;

        int baseY = surfaceY + 1;
        for (int y = baseY; y < baseY + trunkH; y++)
            decoTilemap.SetTile(new Vector3Int(x, y, 0), trunkTile);

        int cx = x;
        int cy = baseY + trunkH;

        PlaceTriangle(cx, cy, canopyR + 1, leaf);
    }

    void PlaceTriangle(int cx, int cy, int h, TileBase leaf)
    {
        if (leaf == null) return;
        if (leafTiles == null || leafTiles.Length < 10) return;

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
                    if (dx == -half) leaf = leafTiles[8];
                    else if (dx == half) leaf = leafTiles[9];
                    else leaf = leafTiles[5];
                }
                else if (i == h - 1)
                {
                    leaf = leafTiles[7];
                }
                else if (dx == -half)
                {
                    leaf = leafTiles[1];
                }
                else if (dx == half)
                {
                    leaf = leafTiles[3];
                }

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
