using System.Collections.Generic;
using UnityEngine;

public class CloudManager : MonoBehaviour
{
    [Header("References")]
    public Camera cam;                      // Drag Main Camera (auto-fills if left empty)
    public Sprite[] cloudSprites;           // Assign your cloud sprites
    public Transform container;             // Optional parent for clouds (defaults to this)
    [SerializeField] private DayNightCycle dayNightCycle;     // Reference to DayNightCycle for tinting

    [Header("Parallax")]
    [Range(0f, 1f)] public float parallax = 0.2f; // 0 = fixed to world, 1 = moves with camera

    [Header("Spawn & Wrap")]
    public int targetClouds = 12;           // How many clouds to keep around
    public float extraWidth = 20f;          // Margin beyond the screen (units) before wrapping
    public Vector2 yRange = new Vector2(6f, 20f); // Vertical band (world units)

    [Header("Cloud Look")]
    public Vector2 speedRange = new Vector2(0.3f, 1.0f); // World units/sec (use negatives for leftward wind)
    public Vector2 scaleRange = new Vector2(1.5f, 3.5f); // Uniform scale
    public string sortingLayerName = "Clouds";
    public int orderInLayer = -50;

    [Header("Pixel Art Helpers")]
    public bool integerScaleOnly = false;   // If true, scales are 1x, 2x, 3x...
    public int minIntegerScale = 1;
    public int maxIntegerScale = 3;

    class Cloud
    {
        public GameObject go;
        public Transform t;
        public SpriteRenderer sr;
        public float z;
        public float speed;      // wind drift
        public float baseX;      // base position (pre-parallax)
        public float baseY;      // altitude
        public float localDrift; // accumulates with speed over time
    }

    readonly List<Cloud> _clouds = new List<Cloud>();
    float _halfHeight, _halfWidth;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!container) container = transform;
        if (!dayNightCycle) dayNightCycle = FindObjectOfType<DayNightCycle>();
    }

    void Start()
    {
        RecalcViewport();
        // Ensure sorting layer exists (wont create it, just warns)
        if (!SortingLayerExists(sortingLayerName))
            Debug.LogWarning($"CloudManager: Sorting layer '{sortingLayerName}' not found. Using Default.");

        // Prevent infinite loop if no sprites are assigned
        if (cloudSprites == null || cloudSprites.Length == 0)
        {
            Debug.LogWarning("CloudManager: No cloud sprites assigned. Clouds will not be generated.");
            return;
        }

        while (_clouds.Count < targetClouds)
            SpawnOne(randomizeX: true);
    }

    void Update()
    {
        if (!cam || cloudSprites == null || cloudSprites.Length == 0) return;

        RecalcViewport();

        // Camera-centered horizontal band (screen width + margins)
        float leftEdge = cam.transform.position.x - _halfWidth - extraWidth;
        float rightEdge = cam.transform.position.x + _halfWidth + extraWidth;
        float span = rightEdge - leftEdge;     // total wrap width

        float camX = cam.transform.position.x;
        float parallaxX = camX * parallax;

        Color currentTint = dayNightCycle != null ? dayNightCycle.GetCloudTint() : Color.white;

        foreach (var c in _clouds)
        {
            // advance wind drift
            c.localDrift += c.speed * Time.deltaTime;

            // compute world position with parallax + drift
            float worldX = c.baseX + parallaxX + c.localDrift;
            float worldY = c.baseY;

            // wrap BOTH sides to keep a constant cloud field around the camera
            if (worldX > rightEdge)
            {
                c.baseX -= span;                              // jump to left band
                worldX -= span;
                c.baseY = Random.Range(yRange.x, yRange.y); // optional new altitude
                worldY = c.baseY;
                c.localDrift = 0f;
            }
            else if (worldX < leftEdge)
            {
                c.baseX += span;                              // jump to right band
                worldX += span;
                c.baseY = Random.Range(yRange.x, yRange.y);
                worldY = c.baseY;
                c.localDrift = 0f;
            }

            c.t.position = new Vector3(worldX, worldY, c.z);
            
            // Update tint
            if (c.sr) c.sr.color = currentTint;
        }

        // Keep population steady if you tweak targetClouds at runtime
        while (_clouds.Count < targetClouds) SpawnOne(randomizeX: false);
        while (_clouds.Count > targetClouds)
        {
            var last = _clouds[^1];
            if (last != null && last.go) Destroy(last.go);
            _clouds.RemoveAt(_clouds.Count - 1);
        }
    }

    void SpawnOne(bool randomizeX)
    {
        if (cloudSprites == null || cloudSprites.Length == 0) return;

        float camX = cam.transform.position.x;
        RecalcViewport();

        // create
        var go = new GameObject("Cloud");
        go.transform.SetParent(container, worldPositionStays: false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = cloudSprites[Random.Range(0, cloudSprites.Length)];
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = orderInLayer;

        // scale
        float scale = integerScaleOnly
            ? Mathf.Clamp(Random.Range(minIntegerScale, maxIntegerScale + 1), minIntegerScale, maxIntegerScale)
            : Random.Range(scaleRange.x, scaleRange.y);
        go.transform.localScale = Vector3.one * scale;

        // initial placement within the camera band
        float baseY = Random.Range(yRange.x, yRange.y);
        float baseX = randomizeX
            ? Random.Range(-_halfWidth - extraWidth, _halfWidth + extraWidth)
            : -_halfWidth - extraWidth; // spawn just to the left so it drifts in

        float z = 0f;
        float parallaxX = camX * parallax;
        go.transform.position = new Vector3(baseX + parallaxX, baseY, z);

        var c = new Cloud
        {
            go = go,
            t = go.transform,
            sr = sr,
            z = z,
            speed = Random.Range(speedRange.x, speedRange.y),
            baseX = baseX,
            baseY = baseY,
            localDrift = 0f
        };
        _clouds.Add(c);
    }

    void RecalcViewport()
    {
        if (!cam) return;
        _halfHeight = cam.orthographicSize;
        _halfWidth = _halfHeight * cam.aspect;
    }

    static bool SortingLayerExists(string layerName)
    {
        foreach (var sl in SortingLayer.layers)
            if (sl.name == layerName) return true;
        return false;
    }
}
