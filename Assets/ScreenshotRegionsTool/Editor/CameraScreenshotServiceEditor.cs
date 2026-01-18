using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraScreenshotService))]
public class CameraScreenshotServiceEditor : Editor
{
    public enum DoubleHandleType
    {
        Min, Max,
    }

    public enum DragMode
    {
        Horizontal, Vertical
    }

    public class HandleData
    {
        public float XMin01;
        public float XMax01;
        public float YMin01;
        public float YMax01;
        public Color Color;
        public int Index;
    }

    private Vector2Int _resolution = new Vector2Int(1920, 1080);
    static Rect _cachedPreviewRect;
    private static bool _allowIntersectionsX = false;
    private static bool _allowIntersectionsY = true;
    private static Dictionary<ScreenShotData, HandleData> _handlesPositions = new Dictionary<ScreenShotData, HandleData>();

    private Texture _stretchXTexture;
    private Texture _stretchYTexture;
    private Texture _sliceXTexture;
    private Texture _sliceYTexture;

    private GUIStyle _titleStyle;
    private GUIStyle _indexStyle;
    private GUIStyle _dimStyle;
    private GUIStyle _arrowLabel;

    private Texture2D _playModePreview;
    private bool _showPlayModePreview;

    private const float HANDLE_SIZE = 6f;

    private static readonly Color[] _sliceColors =
    {
        new Color(0.90f, 0.30f, 0.30f, 1f),
        new Color(0.30f, 0.60f, 0.90f, 1f),
        new Color(0.35f, 0.80f, 0.45f, 1f),
        new Color(0.95f, 0.80f, 0.30f, 1f),
        new Color(0.75f, 0.45f, 0.90f, 1f),
        new Color(0.95f, 0.55f, 0.35f, 1f),
        new Color(0.30f, 0.85f, 0.80f, 1f),
        new Color(0.85f, 0.35f, 0.65f, 1f),
        new Color(0.60f, 0.60f, 0.60f, 1f),
        new Color(0.55f, 0.75f, 0.35f, 1f),
        new Color(0.35f, 0.55f, 0.75f, 1f),
        new Color(0.85f, 0.65f, 0.45f, 1f),
        new Color(0.65f, 0.45f, 0.35f, 1f),
        new Color(0.45f, 0.85f, 0.65f, 1f),
        new Color(0.75f, 0.35f, 0.35f, 1f),
        new Color(0.35f, 0.75f, 0.35f, 1f),
        new Color(0.35f, 0.35f, 0.75f, 1f),
        new Color(0.85f, 0.85f, 0.45f, 1f),
        new Color(0.65f, 0.35f, 0.85f, 1f),
        new Color(0.35f, 0.85f, 0.85f, 1f)
    };

    private void OnEnable()
    {
        _stretchXTexture = GetTexture("Icons/T_Stretch_X.png");
        _stretchYTexture = GetTexture("Icons/T_Stretch_Y.png");
        _sliceXTexture = GetTexture("Icons/T_Slice_X.png");
        _sliceYTexture = GetTexture("Icons/T_Slice_Y.png");

        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            _showPlayModePreview = false;

            if (_playModePreview != null)
            {
                DestroyImmediate(_playModePreview);
                _playModePreview = null;
            }
        }
    }


    private Texture GetTexture(string path)
    {
        string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour((CameraScreenshotService)target));
        string dir = System.IO.Path.GetDirectoryName(scriptPath);
        string iconPath = System.IO.Path.Combine(dir, path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
    }

    /// <summary>
    /// Initiates the styles that will be used.
    /// </summary>
    private void InitStyles()
    {
        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
            };
        }

        if (_indexStyle == null)
        {
            _indexStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                normal = { textColor = Color.white }
            };
        }

        if (_dimStyle == null)
        {
            _dimStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                //fontSize = 11,
                //normal = { textColor = Color.white }
            };
        }

        if (_arrowLabel == null)
        {
            _arrowLabel = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        InitStyles();

        GUILayout.Space(20);
        EditorGUILayout.HelpBox("\nAdd values to the regions list and new regions will appear in the screens graph.\n\nChanging the resolution, adding or removing regions, or using the controls will ruin your layouts. Be careful!\n", MessageType.Info);
        GUILayout.Space(5);
        GUILayout.Label("Editor", _titleStyle, GUILayout.MaxWidth(150));
        GUILayout.Space(30);
        CameraScreenshotService service = (CameraScreenshotService)target;

        // cap the resolution to not have negative values. They act weird
        _resolution = service.CaptureResolution;
        _resolution.x = Mathf.Max(_resolution.x, 1);
        _resolution.y = Mathf.Max(_resolution.y, 1);
        service.SetResolution(_resolution);

        // draw the graph with the resolution and aspect ratio arrows
        Rect graphRect = Draw(_resolution, EditorGUIUtility.currentViewWidth);

        // during the layout phase, rects are reset to their default values, so we cache the size intended during the repaint phase and use it during the layout phase
        if (Event.current.type == EventType.Repaint)
        {
            _cachedPreviewRect = graphRect;
        }
        else
        {
            graphRect = _cachedPreviewRect;
        }

        // again, one of the phases corrupts the variables stored here, so we only get them when it's in the repaint phase
        if (Event.current.type == EventType.Repaint)
        {
            LoadDataFromRegionsList();
        }

        // draw handles for regions
        var list = _handlesPositions.ToList();

        // start with horizontal handles
        for (int i = 0; i < list.Count; i++)
        {
            var v = list[i].Value;

            // cache previous and next handles if applicable
            HandleData prev = i > 0 ? list[i - 1].Value : null;
            HandleData next = i < list.Count - 1 ? list[i + 1].Value : null;

            // the limit for the handles is the edge of the graph if they have no neighbours or if intersection is allow, otherwise, it's the neighbouring handle is located
            float allowedMinX = prev == null || _allowIntersectionsX ? graphRect.xMin : Mathf.Lerp(graphRect.xMin, graphRect.xMax, prev.XMax01);
            float allowedMaxX = next == null || _allowIntersectionsX ? graphRect.xMax : Mathf.Lerp(graphRect.xMin, graphRect.xMax, next.XMin01);

            // get the min and max of the handles in the graph space using the handle data values that range from 0 to 1
            Vector2 minGUI = new Vector2(Mathf.Lerp(graphRect.xMin, graphRect.xMax, v.XMin01), graphRect.y);
            Vector2 maxGUI = new Vector2(Mathf.Lerp(graphRect.xMin, graphRect.xMax, v.XMax01), graphRect.y);

            // draw 2 handles for the min and max values
            DraggableDoubleHandle(ref minGUI, ref maxGUI, DragMode.Horizontal, v.Color, allowedMinX, allowedMaxX);

            // assign the updated positional values from the handles and convert them to 0-1 values
            v.XMin01 = Mathf.InverseLerp(graphRect.xMin, graphRect.xMax, minGUI.x);
            v.XMax01 = Mathf.InverseLerp(graphRect.xMin, graphRect.xMax, maxGUI.x);
        }

        // draw the vertical handles. All plays out same as the horizontal values
        for (int i = 0; i < list.Count; i++)
        {
            var v = list[i].Value;
            HandleData prev = i > 0 ? list[i - 1].Value : null;
            HandleData next = i < list.Count - 1 ? list[i + 1].Value : null;

            float allowedMinY = prev == null || _allowIntersectionsY ? graphRect.yMin : Mathf.Lerp(graphRect.yMin, graphRect.yMax, prev.YMax01);
            float allowedMaxY = next == null || _allowIntersectionsY ? graphRect.yMax : Mathf.Lerp(graphRect.yMin, graphRect.yMax, next.YMin01);

            Vector2 minGUI = new Vector2(graphRect.x, Mathf.Lerp(graphRect.yMin, graphRect.yMax, v.YMin01));
            Vector2 maxGUI = new Vector2(graphRect.x, Mathf.Lerp(graphRect.yMin, graphRect.yMax, v.YMax01));
            DraggableDoubleHandle(ref minGUI, ref maxGUI, DragMode.Vertical, v.Color, allowedMinY, allowedMaxY);

            v.YMin01 = Mathf.InverseLerp(graphRect.yMin, graphRect.yMax, minGUI.y);
            v.YMax01 = Mathf.InverseLerp(graphRect.yMin, graphRect.yMax, maxGUI.y);
        }

        // draw the regions as rects
        foreach (var v in list)
        {
            DrawRegionFromHandles(v.Value, graphRect);
        }


        if (Application.isPlaying)
        {
            GUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("Play Mode Preview", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview Capture"))
            {
                _playModePreview = CaptureCameraPreview((CameraScreenshotService)target);
                _showPlayModePreview = _playModePreview != null;
                Repaint();
            }

            GUI.enabled = _playModePreview != null;
            if (GUILayout.Button("Clear Preview"))
            {
                _playModePreview = null;
                Repaint();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
        }

        GUILayout.Space(10);

        // render the controls
        GUILayout.Label("", GUI.skin.horizontalScrollbar);
        GUILayout.Label("Controls");

        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.BeginHorizontal();
        GUIContent sliceXContent = new GUIContent("Slice equally X", _sliceXTexture);
        if (GUILayout.Button(sliceXContent)) SliceEquallyX(service);
        GUIContent sliceYContent = new GUIContent("Slice equally Y", _sliceYTexture);
        if (GUILayout.Button(sliceYContent)) SliceEquallyY(service);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        _allowIntersectionsX = EditorGUILayout.Toggle("Allow Intersections X", _allowIntersectionsX);
        _allowIntersectionsY = EditorGUILayout.Toggle("Allow Intersections Y", _allowIntersectionsY);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUI.enabled = _allowIntersectionsX;
        GUIContent stretchXContent = new GUIContent("Stretch X", _stretchXTexture);
        if (GUILayout.Button(stretchXContent)) StretchX(service);

        GUI.enabled = _allowIntersectionsY;
        GUIContent stretchYContent = new GUIContent("Stretch X", _stretchYTexture);
        if (GUILayout.Button(stretchYContent)) StretchY(service);
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        ApplyRegions(graphRect);
    }

    private Texture2D CaptureCameraPreview(CameraScreenshotService service)
    {
        var cam = service.TargetCamera;
        if (cam == null)
            return null;

        int w = service.CaptureResolution.x;
        int h = service.CaptureResolution.y;

        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        var prevRT = RenderTexture.active;
        var prevCamRT = cam.targetTexture;

        cam.targetTexture = rt;
        RenderTexture.active = rt;
        cam.Render();

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        cam.targetTexture = prevCamRT;
        RenderTexture.active = prevRT;
        RenderTexture.ReleaseTemporary(rt);

        return tex;
    }


    /// <summary>
    /// Populate handle data from regions.
    /// </summary>
    private void LoadDataFromRegionsList()
    {
        CameraScreenshotService service = (CameraScreenshotService)target;

        if (service.Regions.Length != _handlesPositions.Count)
        {
            _handlesPositions.Clear();

            Vector2Int res = service.CaptureResolution;

            for (int i = 0; i < service.Regions.Length; i++)
            {
                ScreenShotData r = service.Regions[i];

                HandleData data = new HandleData
                {
                    XMin01 = res.x > 0 ? (float)r.PixelsWidth.x / res.x : 0f,
                    XMax01 = res.x > 0 ? (float)r.PixelsWidth.y / res.x : 0f,
                    YMin01 = res.y > 0 ? (float)r.PixelsHeight.x / res.y : 0f,
                    YMax01 = res.y > 0 ? (float)r.PixelsHeight.y / res.y : 0f,
                    Color = i < _sliceColors.Length - 1 ? _sliceColors[i] : Random.ColorHSV(),
                    Index = i + 1
                };

                _handlesPositions.Add(r, data);
            }
        }
    }

    #region Control functions
    private void StretchX(CameraScreenshotService service)
    {
        foreach (var h in _handlesPositions.Values)
        {
            h.XMin01 = 0f;
            h.XMax01 = 1f;
        }

        EditorUtility.SetDirty(service);
    }

    private void StretchY(CameraScreenshotService service)
    {
        foreach (var h in _handlesPositions.Values)
        {
            h.YMin01 = 0f;
            h.YMax01 = 1f;
        }

        EditorUtility.SetDirty(service);
    }

    private void SliceEquallyX(CameraScreenshotService service)
    {
        int count = _handlesPositions.Count;
        if (count == 0) return;

        float step = 1f / count;

        int i = 0;
        foreach (var h in _handlesPositions.Values)
        {
            h.XMin01 = i * step;
            h.XMax01 = (i + 1) * step;
            i++;
        }

        EditorUtility.SetDirty(service);
    }

    private void SliceEquallyY(CameraScreenshotService service)
    {
        int count = _handlesPositions.Count;
        if (count == 0) return;

        float step = 1f / count;

        int i = 0;
        foreach (var h in _handlesPositions.Values)
        {
            h.YMin01 = i * step;
            h.YMax01 = (i + 1) * step;
            i++;
        }

        EditorUtility.SetDirty(service);
    }
    #endregion

    /// <summary>
    /// Updates region values based on the handles' data.
    /// </summary>
    /// <param name="service"></param>
    /// <param name="previewRect"></param>
    private void ApplyRegions(Rect previewRect)
    {
        CameraScreenshotService service = (CameraScreenshotService)target;
        Vector2Int resolution = service.CaptureResolution;
        var list = _handlesPositions.ToList();

        for (int i = 0; i < list.Count; i++)
        {
            ScreenShotData region = list[i].Key;
            HandleData h = list[i].Value;

            int xMin = Mathf.Clamp(Mathf.RoundToInt(h.XMin01 * resolution.x), 0, resolution.x);
            int xMax = Mathf.Clamp(Mathf.RoundToInt(h.XMax01 * resolution.x), 0, resolution.x);
            int yMin = Mathf.Clamp(Mathf.RoundToInt(h.YMin01 * resolution.y), 0, resolution.y);
            int yMax = Mathf.Clamp(Mathf.RoundToInt(h.YMax01 * resolution.y), 0, resolution.y);

            region.PixelsWidth = new Vector2Int(xMin, xMax);
            region.PixelsHeight = new Vector2Int(yMin, yMax);
        }

        EditorUtility.SetDirty(service);
    }

    /// <summary>
    /// Draws a region based on the handle data and the parent graph rect.
    /// </summary>
    /// <param name="h"></param>
    /// <param name="previewRect"></param>
    private void DrawRegionFromHandles(HandleData h, Rect previewRect)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        float xMin = Mathf.Lerp(previewRect.xMin, previewRect.xMax, h.XMin01);
        float xMax = Mathf.Lerp(previewRect.xMin, previewRect.xMax, h.XMax01);
        float yMin = Mathf.Lerp(previewRect.yMin, previewRect.yMax, h.YMin01);
        float yMax = Mathf.Lerp(previewRect.yMin, previewRect.yMax, h.YMax01);

        Rect r = Rect.MinMaxRect(xMin, yMin, xMax, yMax);

        Color c = h.Color;
        c.a = 0.25f;

        EditorGUI.DrawRect(r, c);
        GUI.Label(r, h.Index.ToString(), _indexStyle);

        int widthPx = Mathf.RoundToInt((h.XMax01 - h.XMin01) * _resolution.x);
        int heightPx = Mathf.RoundToInt((h.YMax01 - h.YMin01) * _resolution.y);

        Rect dimRect = r;
        dimRect.center += Vector2.up * (_indexStyle.lineHeight * 0.75f);

        GUI.Label(dimRect, $"{widthPx}px × {heightPx}px", _dimStyle);
    }

    /// <summary>
    /// Draw two handles that define the min and max values of a region.
    /// </summary>
    /// <param name="min">The 2D position of the min handle</param>
    /// <param name="max">The 2D position of the max handle</param>
    /// <param name="mode">Whether it's horizontal or vertical</param>
    /// <param name="color">The color of the handles</param>
    /// <param name="axisMin">The chosen axis' minimum value</param>
    /// <param name="axisMax">The chosen axis' maximum value</param>
    private void DraggableDoubleHandle(ref Vector2 min, ref Vector2 max, DragMode mode, Color color, float axisMin, float axisMax)
    {
        min = DraggableHandle(DoubleHandleType.Min, min, mode, color,
            XMin: mode == DragMode.Horizontal ? axisMin : float.NaN,
            XMax: mode == DragMode.Horizontal ? max.x : float.NaN,
            YMin: mode == DragMode.Vertical ? axisMin : float.NaN,
            YMax: mode == DragMode.Vertical ? max.y : float.NaN
        );

        max = DraggableHandle(DoubleHandleType.Max, max, mode, color,
            XMin: mode == DragMode.Horizontal ? min.x : float.NaN,
            XMax: mode == DragMode.Horizontal ? axisMax : float.NaN,
            YMin: mode == DragMode.Vertical ? min.y : float.NaN,
            YMax: mode == DragMode.Vertical ? axisMax : float.NaN
        );
    }

    private Vector2 DraggableHandle(DoubleHandleType handleType, Vector2 pos, DragMode mode, Color color, float XMin = float.NaN, float XMax = float.NaN, float YMin = float.NaN, float YMax = float.NaN)
    {
        int id = GUIUtility.GetControlID(FocusType.Passive);
        float visualOffset = 10f;
        Vector2 handlePos = pos;

        if (mode == DragMode.Horizontal)
        {
            handlePos.x += handleType == DoubleHandleType.Min ? HANDLE_SIZE : -HANDLE_SIZE;
        }
        else
        {
            handlePos.y += handleType == DoubleHandleType.Min ? HANDLE_SIZE : -HANDLE_SIZE;
        }

        Rect r = new Rect(handlePos - Vector2.one * HANDLE_SIZE, Vector2.one * HANDLE_SIZE * 2);
        r.position += mode == DragMode.Horizontal ? Vector2.down * visualOffset : Vector2.left * visualOffset;

        Event e = Event.current;
        Vector2 originalPos = pos;

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (r.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = id;
                    e.Use();
                }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    pos += e.delta;
                    e.Use();
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
                break;
            case EventType.Repaint:
                //EditorGUI.DrawRect(r, color);
                Vector2 triangleCenter = r.center;
                DrawTriangleHandle(triangleCenter, mode, handleType, HANDLE_SIZE, color);

                break;
        }

        r.position -= mode == DragMode.Horizontal ? Vector2.up * visualOffset : Vector2.left * visualOffset;

        pos.x = mode == DragMode.Horizontal ? pos.x : originalPos.x;
        pos.y = mode == DragMode.Vertical ? pos.y : originalPos.y;

        if (!float.IsNaN(XMin)) pos.x = Mathf.Max(pos.x, XMin);
        if (!float.IsNaN(XMax)) pos.x = Mathf.Min(pos.x, XMax);
        if (!float.IsNaN(YMin)) pos.y = Mathf.Max(pos.y, YMin);
        if (!float.IsNaN(YMax)) pos.y = Mathf.Min(pos.y, YMax);

        return pos;
    }

    private Rect Draw(Vector2 resolution,  float editorWidth)
    {
        float arrowsOffset = 30f;
        editorWidth *= 0.8f; // add padding to the graph. Temp
        float aspect = resolution.x / resolution.y;
        float width = editorWidth;
        float height = width / aspect;

        GUILayout.Space(20);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        Rect r = GUILayoutUtility.GetRect(width, height);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        //EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f));
        bool hasPreview = Application.isPlaying && _showPlayModePreview && _playModePreview != null;
        if (hasPreview)
        {
            GUI.DrawTexture(r, _playModePreview, ScaleMode.StretchToFill);
        }
        else
        {
            EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f));
        }

        // an outline around the graph area
        Handles.DrawLine(new Vector3(r.xMin, r.yMin), new Vector3(r.xMax, r.yMin));
        Handles.DrawLine(new Vector3(r.xMax, r.yMin), new Vector3(r.xMax, r.yMax));
        Handles.DrawLine(new Vector3(r.xMax, r.yMax), new Vector3(r.xMin, r.yMax));
        Handles.DrawLine(new Vector3(r.xMin, r.yMax), new Vector3(r.xMin, r.yMin));

        // horizontal
        DrawArrow2D(new Vector2(r.xMin, r.yMin - arrowsOffset), new Vector2(r.xMax, r.yMin - arrowsOffset), $"{resolution.x} px", 0f);
        // vertical
        DrawArrow2D(new Vector2(r.xMin - arrowsOffset, r.yMin), new Vector2(r.xMin - arrowsOffset, r.yMax), $"{resolution.y} px", -90);

        if (!hasPreview)
        {
            // diagonal
            float ratioW = resolution.x / GCD(resolution.x, resolution.y);
            float ratioH = resolution.y / GCD(resolution.x, resolution.y);
            float angle = Mathf.Atan2(ratioH, ratioW) * Mathf.Rad2Deg;
            DrawArrow2D(r.min, r.max, $"{ratioW} : {ratioH}", angle);
        }

        return r;
    }

    #region Utils
    private void DrawArrow2D(Vector2 a, Vector2 b, string label, float labelAngle)
    {
        float headSize = 6f;
        Handles.DrawLine(a, b);

        Vector2 dir = (a - b).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);

        Vector2 p1 = b + dir * headSize + perp * headSize * 0.5f;
        Vector2 p2 = b + dir * headSize - perp * headSize * 0.5f;
        Handles.DrawLine(b, p1);
        Handles.DrawLine(b, p2);
        dir = (b - a).normalized;
        perp = new Vector2(-dir.y, dir.x);
        p1 = a + dir * headSize + perp * headSize * 0.5f;
        p2 = a + dir * headSize - perp * headSize * 0.5f;
        Handles.DrawLine(a, p1);
        Handles.DrawLine(a, p2);

        Vector2 mid = (a + b) * 0.5f;
        Vector2 size = new Vector2(100, 20);
        Rect rect = new Rect(mid - size * 0.5f, size);

        // label stuff
        Color color = Color.gray * 0.5f;
        color.a = 1f;
        Matrix4x4 old = GUI.matrix;
        Vector2 pivot = rect.center;
        GUIUtility.RotateAroundPivot(labelAngle, pivot);
        EditorGUI.DrawRect(rect, color);
        GUI.Label(rect, label, _arrowLabel);
        GUI.matrix = old;
    }

    private static float GCD(float a, float b)
    {
        while (b != 0)
        {
            float temp = b;
            b = a % b;
            a = temp;
        }

        return a;
    }

    private void DrawTriangleHandle(Vector2 center, DragMode mode, DoubleHandleType type, float size, Color color)
    {
        Vector3[] pts;
        // draw the triangles based on the mode and handle type
        if (mode == DragMode.Horizontal)
        {
            if (type == DoubleHandleType.Min)
            {
                pts = new[]
                {
                new Vector3(center.x + size, center.y),
                new Vector3(center.x - size, center.y - size),
                new Vector3(center.x - size, center.y + size),
            };
            }
            else
            {
                pts = new[]
                {
                new Vector3(center.x - size, center.y),
                new Vector3(center.x + size, center.y - size),
                new Vector3(center.x + size, center.y + size),
            };
            }
        }
        else
        {
            if (type == DoubleHandleType.Min)
            {
                pts = new[]
                {
                new Vector3(center.x, center.y + size),
                new Vector3(center.x - size, center.y - size),
                new Vector3(center.x + size, center.y - size),
            };
            }
            else
            {
                pts = new[]
                {
                new Vector3(center.x, center.y - size),
                new Vector3(center.x - size, center.y + size),
                new Vector3(center.x + size, center.y + size),
            };
            }
        }

        Handles.color = color;
        Handles.DrawAAConvexPolygon(pts);
    }

    #endregion
}