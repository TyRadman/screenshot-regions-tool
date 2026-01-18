using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraScreenshotService))]
public class CameraScreenshotServiceEditor : Editor
{
    private Vector2Int _resolution = new Vector2Int(1920, 1080);
    static Rect _cachedPreviewRect;
    private bool _allowIntersectionsX = false;
    private bool _allowIntersectionsY = true;
    private Dictionary<ScreenShotData, HandleData> _handlesPositions = new Dictionary<ScreenShotData, HandleData>();

    private Texture _stretchXTexture;
    private Texture _stretchYTexture;
    private Texture _sliceXTexture;
    private Texture _sliceYTexture;

    private static readonly Color[] _sliceColors =
    {
        new Color(0.90f, 0.30f, 0.30f, 1f), // red
        new Color(0.30f, 0.60f, 0.90f, 1f), // blue
        new Color(0.35f, 0.80f, 0.45f, 1f), // green
        new Color(0.95f, 0.80f, 0.30f, 1f), // yellow
        new Color(0.75f, 0.45f, 0.90f, 1f), // purple
        new Color(0.95f, 0.55f, 0.35f, 1f), // orange
        new Color(0.30f, 0.85f, 0.80f, 1f), // cyan
        new Color(0.85f, 0.35f, 0.65f, 1f), // magenta
        new Color(0.60f, 0.60f, 0.60f, 1f), // gray
        new Color(0.55f, 0.75f, 0.35f, 1f), // lime
        new Color(0.35f, 0.55f, 0.75f, 1f), // steel blue
        new Color(0.85f, 0.65f, 0.45f, 1f), // sand
        new Color(0.65f, 0.45f, 0.35f, 1f), // brown
        new Color(0.45f, 0.85f, 0.65f, 1f), // mint
        new Color(0.75f, 0.35f, 0.35f, 1f), // dark red
        new Color(0.35f, 0.75f, 0.35f, 1f), // dark green
        new Color(0.35f, 0.35f, 0.75f, 1f), // dark blue
        new Color(0.85f, 0.85f, 0.45f, 1f), // olive
        new Color(0.65f, 0.35f, 0.85f, 1f), // violet
        new Color(0.35f, 0.85f, 0.85f, 1f)  // teal
    };

    private void OnEnable()
    {
        _stretchXTexture = GetTexture("Icons/T_Stretch_X.png");
        _stretchYTexture = GetTexture("Icons/T_Stretch_Y.png");
        _sliceXTexture = GetTexture("Icons/T_Slice_X.png");
        _sliceYTexture = GetTexture("Icons/T_Slice_Y.png");
    }

    private Texture GetTexture(string path)
    {
        string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour((CameraScreenshotService)target));
        string dir = System.IO.Path.GetDirectoryName(scriptPath);
        string iconPath = System.IO.Path.Combine(dir, path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
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

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GUILayout.Label("", GUI.skin.horizontalScrollbar);
        GUILayout.Label("Editor");
        GUILayout.Space(10);
        CameraScreenshotService service = (CameraScreenshotService)target;

        _resolution = service.CaptureResolution;

        _resolution.x = Mathf.Max(_resolution.x, 1);
        _resolution.y = Mathf.Max(_resolution.y, 1);

        Rect last = ResolutionPreviewDrawer.Draw(_resolution, EditorGUIUtility.currentViewWidth);

        if (Event.current.type == EventType.Repaint)
        {
            _cachedPreviewRect = last;
        }
        else
        {
            last = _cachedPreviewRect;
        }


        if (Event.current.type == EventType.Repaint)
        {
            LoadDataFromRegionsList();
        }

        float minY = last.y;
        float maxY = last.y + last.size.y;
        float minX = last.x;
        float maxX = last.x + last.size.x;

        var list = _handlesPositions.ToList();

        for (int i = 0; i < list.Count; i++)
        {
            var v = list[i].Value;
            HandleData prev = i > 0 ? list[i - 1].Value : null;
            HandleData next = i < list.Count - 1 ? list[i + 1].Value : null;

            float allowedMinX = prev == null || _allowIntersectionsX? minX : prev.XMax01 * last.width + last.x;
            float allowedMaxX = next == null || _allowIntersectionsX? maxX : next.XMin01 * last.width + last.x;

            Vector2 minGUI = X01ToGUI(v.XMin01, last, last.y);
            Vector2 maxGUI = X01ToGUI(v.XMax01, last, last.y);
            DraggableDoubleHandle(ref minGUI, ref maxGUI, DragMode.Horizontal, v.Color, v.Color, allowedMinX, allowedMaxX);

            v.XMin01 = GUIToX01(minGUI.x, last);
            v.XMax01 = GUIToX01(maxGUI.x, last);
        }
        
        for (int i = 0; i < list.Count; i++)
        {
            var v = list[i].Value;
            HandleData prev = i > 0 ? list[i - 1].Value : null;
            HandleData next = i < list.Count - 1 ? list[i + 1].Value : null;

            float allowedMinY = prev == null || _allowIntersectionsY ? minY : prev.YMax01 * last.height + last.y;
            float allowedMaxY = next == null || _allowIntersectionsY ? maxY : next.YMin01 * last.height + last.y;

            Vector2 minGUI = Y01ToGUI(v.YMin01, last, last.x);
            Vector2 maxGUI = Y01ToGUI(v.YMax01, last, last.x);
            DraggableDoubleHandle(ref minGUI, ref maxGUI, DragMode.Vertical, v.Color, v.Color, allowedMinY, allowedMaxY);


            v.YMin01 = GUIToY01(minGUI.y, last);
            v.YMax01 = GUIToY01(maxGUI.y, last);
        }

        foreach (var v in list)
        {
            DrawSliceFromHandles(v.Value, last);
        }

        service.SetResolution(_resolution);

        GUILayout.Space(10);

        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

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

        ApplyRegions(service, last);
    }

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
                    Color = i < _sliceColors.Length - 1 ? _sliceColors[i] : Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f),
                    Index = i + 1
                };

                _handlesPositions.Add(r, data);
            }
        }
    }

    void StretchX(CameraScreenshotService service)
    {
        foreach (var h in _handlesPositions.Values)
        {
            h.XMin01 = 0f;
            h.XMax01 = 1f;
        }

        EditorUtility.SetDirty(service);
    }
    void StretchY(CameraScreenshotService service)
    {
        foreach (var h in _handlesPositions.Values)
        {
            h.YMin01 = 0f;
            h.YMax01 = 1f;
        }

        EditorUtility.SetDirty(service);
    }

    void SliceEquallyX(CameraScreenshotService service)
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

    void SliceEquallyY(CameraScreenshotService service)
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

    void ApplyRegions(CameraScreenshotService service, Rect previewRect)
    {
        Vector2Int resolution = service.CaptureResolution;

        var list = _handlesPositions.ToList();

        for (int i = 0; i < list.Count; i++)
        {
            ScreenShotData region = list[i].Key;
            HandleData h = list[i].Value;

            int xMin = Mathf.RoundToInt(h.XMin01 * resolution.x);
            int xMax = Mathf.RoundToInt(h.XMax01 * resolution.x);
            int yMin = Mathf.RoundToInt(h.YMin01 * resolution.y);
            int yMax = Mathf.RoundToInt(h.YMax01 * resolution.y);

            // Safety clamp
            xMin = Mathf.Clamp(xMin, 0, resolution.x);
            xMax = Mathf.Clamp(xMax, 0, resolution.x);
            yMin = Mathf.Clamp(yMin, 0, resolution.y);
            yMax = Mathf.Clamp(yMax, 0, resolution.y);

            region.PixelsWidth = new Vector2Int(xMin, xMax);
            region.PixelsHeight = new Vector2Int(yMin, yMax);
        }

        EditorUtility.SetDirty(service);
    }


    static Vector2 X01ToGUI(float x01, Rect r, float y)
    {
        return new Vector2(
            Mathf.Lerp(r.xMin, r.xMax, x01),
            y
        );
    }

    static Vector2 Y01ToGUI(float y01, Rect r, float x)
    {
        return new Vector2(
            x,
            Mathf.Lerp(r.yMin, r.yMax, y01)
        );
    }

    static float GUIToX01(float x, Rect r)
    {
        return Mathf.InverseLerp(r.xMin, r.xMax, x);
    }

    static float GUIToY01(float y, Rect r)
    {
        return Mathf.InverseLerp(r.yMin, r.yMax, y);
    }

    static void DrawSliceFromHandles(HandleData h, Rect previewRect)
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

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 18;
        style.normal.textColor = Color.white;

        GUI.Label(r, h.Index.ToString(), style);
    }

    static void DraggableDoubleHandle(
            ref Vector2 min,
            ref Vector2 max,
            DragMode mode,
            Color minColor,
            Color maxColor,
            float axisMin,
            float axisMax,
            float size = 6f)
    {
        // MIN handle: cannot go past MAX
        min = DraggableHandle(
            min,
            mode,
            minColor,
            XMin: mode == DragMode.Horizontal ? axisMin : float.NaN,
            XMax: mode == DragMode.Horizontal ? max.x : float.NaN,
            YMin: mode == DragMode.Vertical ? axisMin : float.NaN,
            YMax: mode == DragMode.Vertical ? max.y : float.NaN,
            size: size
        );

        // MAX handle: cannot go below MIN
        max = DraggableHandle(
            max,
            mode,
            maxColor,
            XMin: mode == DragMode.Horizontal ? min.x : float.NaN,
            XMax: mode == DragMode.Horizontal ? axisMax : float.NaN,
            YMin: mode == DragMode.Vertical ? min.y : float.NaN,
            YMax: mode == DragMode.Vertical ? axisMax : float.NaN,
            size: size
        );
    }


    public enum DragMode
    {
        Horizontal, Vertical, Both
    }

    static Vector2 DraggableHandle(Vector2 pos, DragMode mode, Color color, float XMin = float.NaN, float XMax = float.NaN, float YMin = float.NaN, float YMax = float.NaN, float size = 6f)
    {
        int id = GUIUtility.GetControlID(FocusType.Passive);

        float visualOffset = 10f;
        Rect r = new Rect(pos - Vector2.one * size, Vector2.one * size * 2);
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
                EditorGUI.DrawRect(r, color);
                break;
        }

        r.position -= mode == DragMode.Horizontal ? Vector2.up * visualOffset : Vector2.left * visualOffset;

        if (mode != DragMode.Both)
        {
            pos.x = mode == DragMode.Horizontal ? pos.x : originalPos.x;
            pos.y = mode == DragMode.Vertical ? pos.y : originalPos.y;
        }

        if (!float.IsNaN(XMin))
        {
            pos.x = Mathf.Max(pos.x, XMin);
        }

        if (!float.IsNaN(XMax))
        {
            pos.x = Mathf.Min(pos.x, XMax);
        }

        if (!float.IsNaN(YMin))
        {
            pos.y = Mathf.Max(pos.y, YMin);
        }

        if (!float.IsNaN(YMax))
        {
            pos.y = Mathf.Min(pos.y, YMax);
        }

        return pos;
    }

}

public class ResolutionPreviewDrawer
{
    public static Rect Draw(Vector2 resolution,  float editorWidth)
    {
        float arrowsOffset = 30f;
        editorWidth *= 0.8f;
        float aspect = resolution.x / resolution.y;
        float width = editorWidth;
        float height = width / aspect;

        GUILayout.Space(20);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        Rect r = GUILayoutUtility.GetRect(width, height);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f));

        Handles.BeginGUI();

        Handles.color = Color.white;
        Handles.DrawAAPolyLine(
            2,
            new Vector3(r.xMin, r.yMin),
            new Vector3(r.xMax, r.yMin),
            new Vector3(r.xMax, r.yMax),
            new Vector3(r.xMin, r.yMax),
            new Vector3(r.xMin, r.yMin)
        );

        // Horizontal
        DrawArrow2D(
            new Vector2(r.xMin, r.yMin - arrowsOffset),
            new Vector2(r.xMax, r.yMin - arrowsOffset),
            $"{resolution.x} px", 0f
        );

        // Vertical
        DrawArrow2D(
            new Vector2(r.xMin - arrowsOffset, r.yMin),
            new Vector2(r.xMin - arrowsOffset, r.yMax),
            $"{resolution.y} px", -90
        );

        float ratioW = resolution.x / GCD(resolution.x, resolution.y);
        float ratioH = resolution.y / GCD(resolution.x, resolution.y); 
        float angle = Mathf.Atan2(ratioH, ratioW) * Mathf.Rad2Deg;

        // Diagonal
        DrawArrow2D(
            r.min,
            r.max,
            $"{ratioW} : {ratioH}", angle
        );

        Handles.EndGUI();

        return r;
    }

    static float GCD(float a, float b)
    {
        while (b != 0)
        {
            float temp = b;
            b = a % b;
            a = temp;
        }

        return a;
    }

    static void DrawArrow2D(Vector2 a, Vector2 b, string label, float labelAngle)
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

        GUIStyle style = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter
        };

        DrawRotatedLabel(rect, label, labelAngle, style);

    }

    static void DrawRotatedLabel(Rect rect, string text, float angle, GUIStyle style)
    {
        Color color = Color.gray * 0.5f;
        color.a = 1f;

        Matrix4x4 old = GUI.matrix;

        Vector2 pivot = rect.center;
        GUIUtility.RotateAroundPivot(angle, pivot);

        EditorGUI.DrawRect(rect, color);
        GUI.Label(rect, text, style);

        GUI.matrix = old;
    }
}

