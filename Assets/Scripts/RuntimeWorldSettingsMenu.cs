using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

public class RuntimeWorldSettingsMenu : MonoBehaviour
{
    [Header("References")]
    public TerrainGenerator generator;

    [Header("Behavior")]
    public bool pauseGameWhenOpen = true;

    // UI state
    bool _open;
    Rect _windowRect = new Rect(20, 20, 430, 620);
    Vector2 _scroll;

    // Editable fields cache
    readonly List<FieldInfo> _fields = new List<FieldInfo>();
    readonly Dictionary<FieldInfo, object> _pending = new Dictionary<FieldInfo, object>();

    bool _prevCursorVisible;
    CursorLockMode _prevCursorLock;
    float _prevTimeScale = 1f;

    void Awake()
    {
        if (!generator) generator = FindObjectOfType<TerrainGenerator>();

        CacheEditableFields();
        LoadFromGenerator();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Toggle();
        }
    }

    void Toggle()
    {
        _open = !_open;

        if (_open)
        {
            
            _prevCursorVisible = Cursor.visible;
            _prevCursorLock = Cursor.lockState;
            _prevTimeScale = Time.timeScale;

            if (pauseGameWhenOpen)
                Time.timeScale = 0f;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            if (pauseGameWhenOpen)
                Time.timeScale = _prevTimeScale;

            Cursor.visible = _prevCursorVisible;
            Cursor.lockState = _prevCursorLock;
        }
    }


    void OnGUI()
    {
        if (!_open) return;
        if (!generator)
        {
            GUI.Label(new Rect(20, 20, 500, 30), "RuntimeWorldSettingsMenu: No TerrainGenerator found.");
            return;
        }

        _windowRect = GUI.Window(123456, _windowRect, DrawWindow, "World Settings (ESC)");
    }

    void DrawWindow(int id)
    {
        GUILayout.BeginVertical();

        GUILayout.Space(4);
        GUILayout.Label("Edit values, then press Apply + Regenerate.");

        GUILayout.Space(6);

        // Buttons row
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reload Current", GUILayout.Height(26)))
            LoadFromGenerator();

        if (GUILayout.Button("Random Seed", GUILayout.Height(26)))
            RandomizeSeed();

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply", GUILayout.Height(28)))
            ApplyToGenerator(regenerate: false);

        if (GUILayout.Button("Apply + Regenerate", GUILayout.Height(28)))
            ApplyToGenerator(regenerate: true);

        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // Scroll view of fields
        _scroll = GUILayout.BeginScrollView(_scroll, false, true);

        string lastHeader = null;

        foreach (var f in _fields)
        {
            // show headers based on [Header] attributes
            var header = f.GetCustomAttribute<HeaderAttribute>()?.header;
            if (!string.IsNullOrEmpty(header) && header != lastHeader)
            {
                GUILayout.Space(8);
                GUILayout.Label(header, EditorLikeBold());
                lastHeader = header;
            }

            DrawFieldControl(f);
        }

        GUILayout.EndScrollView();

        GUILayout.Space(6);

        if (GUILayout.Button("Close (ESC)", GUILayout.Height(26)))
            Toggle();

        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    void CacheEditableFields()
    {
        _fields.Clear();
        if (!generator) return;

        // Public instance fields only (your parameters are public already)
        var all = generator.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);

        foreach (var f in all)
        {
            // Only allow editing of simple types
            if (f.FieldType != typeof(int) && f.FieldType != typeof(float) && f.FieldType != typeof(bool))
                continue;

            // Exclude things you probably don't want to tweak live (optional)
            // (You can remove these filters if you want)
            if (f.Name == "chunkWidth" || f.Name == "renderDistanceInChunks")
            {
                // These are editable, but changing them can feel weird; keep them if you want.
                // Comment this block out to include them.
            }

            _fields.Add(f);
        }

        // Nice stable order (by declared name)
        _fields.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
    }

    void LoadFromGenerator()
    {
        if (!generator) return;

        _pending.Clear();
        foreach (var f in _fields)
            _pending[f] = f.GetValue(generator);
    }

    void ApplyToGenerator(bool regenerate)
    {
        if (!generator) return;

        foreach (var kv in _pending)
        {
            try { kv.Key.SetValue(generator, kv.Value); }
            catch { /* ignore */ }
        }

        if (regenerate)
            generator.ResetWorld();
    }

    void RandomizeSeed()
    {
        // Ensure seed exists and is editable
        var seedField = _fields.FirstOrDefault(x => x.Name == "seed" && x.FieldType == typeof(int));
        if (seedField == null) return;

        int s = UnityEngine.Random.Range(int.MinValue / 2, int.MaxValue / 2);
        _pending[seedField] = s;
    }

    void DrawFieldControl(FieldInfo f)
    {
        string label = Nicify(f.Name);

        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(210));

        if (!_pending.TryGetValue(f, out var obj))
            obj = f.GetValue(generator);

        var range = f.GetCustomAttribute<RangeAttribute>();

        if (f.FieldType == typeof(bool))
        {
            bool v = (bool)obj;
            bool nv = GUILayout.Toggle(v, "");
            _pending[f] = nv;
        }
        else if (f.FieldType == typeof(int))
        {
            int v = (int)obj;

            if (range != null)
            {
                float sv = GUILayout.HorizontalSlider(v, range.min, range.max, GUILayout.Width(140));
                int nv = Mathf.RoundToInt(sv);
                GUILayout.Label(nv.ToString(), GUILayout.Width(50));
                _pending[f] = nv;
            }
            else
            {
                string s = GUILayout.TextField(v.ToString(), GUILayout.Width(190));
                if (int.TryParse(s, out int nv)) _pending[f] = nv;
            }
        }
        else if (f.FieldType == typeof(float))
        {
            float v = (float)obj;

            if (range != null)
            {
                float nv = GUILayout.HorizontalSlider(v, range.min, range.max, GUILayout.Width(140));
                GUILayout.Label(nv.ToString("0.###"), GUILayout.Width(50));
                _pending[f] = nv;
            }
            else
            {
                string s = GUILayout.TextField(v.ToString("0.###"), GUILayout.Width(190));
                if (float.TryParse(s, out float nv)) _pending[f] = nv;
            }
        }

        GUILayout.EndHorizontal();
    }

    // Simple bold style without UnityEditor
    GUIStyle EditorLikeBold()
    {
        var st = new GUIStyle(GUI.skin.label);
        st.fontStyle = FontStyle.Bold;
        return st;
    }

    static string Nicify(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var chars = new List<char>(s.Length + 8);
        chars.Add(char.ToUpperInvariant(s[0]));
        for (int i = 1; i < s.Length; i++)
        {
            char c = s[i];
            char p = s[i - 1];
            if (char.IsUpper(c) && !char.IsUpper(p))
                chars.Add(' ');
            chars.Add(c);
        }
        return new string(chars.ToArray());
    }
}
