using UnityEngine;
using System.IO;
using System;

[Serializable]
public class ScreenShotData
{
    public Vector2Int PixelsWidth;
    public Vector2Int PixelsHeight;
}

public class CameraScreenshotService : MonoBehaviour
{
    [Tooltip("Reference the master camera")]
    [field: SerializeField] public Camera TargetCamera {get; private set;}
    // ideally, we will use Screen.width and Screen.height, but this won't work accurately in the editor as the game window resizes based on our window layout.
    [field: SerializeField] public Vector2Int CaptureResolution { get; private set; } = new Vector2Int(1920, 1080);
    [field: SerializeField] public ScreenShotData[] Regions { get; set; }
    [SerializeField] private string _outputFolder = "Screenshots";

    private Texture2D _fullScreenshot;

    public void CaptureRegion(int index)
    {
        if (index < 0 || index >= Regions.Length)
        {
            return;
        }

        CaptureFullIfNeeded();
        SaveRegion(index);
    }

    [ContextMenu("Capture All")]
    public void CaptureAll()
    {
        for (int i = 0; i < Regions.Length; i++)
        {
            SaveRegion(i);
        }
    }

    public void ClearCache()
    {
        if (_fullScreenshot != null)
        {
            DestroyImmediate(_fullScreenshot);
            _fullScreenshot = null;
        }
    }

    private void CaptureFullIfNeeded()
    {
        ClearCache();

        if (_fullScreenshot != null)
        {
            return;
        }

        int width = CaptureResolution.x;
        int height = CaptureResolution.y;

        RenderTexture rt = new RenderTexture(width, height, 24)
        {
            filterMode = FilterMode.Point
        };

        TargetCamera.targetTexture = rt;

        _fullScreenshot = new Texture2D(width, height, TextureFormat.RGB24, false);

        TargetCamera.Render();
        RenderTexture.active = rt;
        _fullScreenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        _fullScreenshot.Apply();

        TargetCamera.targetTexture = null;
        RenderTexture.active = null;

        DestroyImmediate(rt);
    }

    public void SaveRegion(int index)
    {
        CaptureFullIfNeeded();
        ScreenShotData data = Regions[index];

        int texW = _fullScreenshot.width;
        int texH = _fullScreenshot.height;

        int startX = Mathf.Clamp(data.PixelsWidth.x, 0, texW);
        int endX = Mathf.Clamp(data.PixelsWidth.y, 0, texW);
        int startY = Mathf.Clamp(data.PixelsHeight.x, 0, texH);
        int endY = Mathf.Clamp(data.PixelsHeight.y, 0, texH);

        int w = endX - startX;
        int h = endY - startY;

        if (w <= 0 || h <= 0)
        {
            return;
        }

        Color[] pixels = _fullScreenshot.GetPixels(startX, startY, w, h);

        Texture2D cropped = new Texture2D(w, h, TextureFormat.RGB24, false);
        cropped.SetPixels(pixels);
        cropped.Apply();

        if (!Directory.Exists(_outputFolder))
        {
            Directory.CreateDirectory(_outputFolder);
        }

        File.WriteAllBytes(Path.Combine(_outputFolder, $"screenshot_region_{index}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"), cropped.EncodeToPNG());

        Debug.Log($"Saved to {_outputFolder}");
        DestroyImmediate(cropped);
    }

    public void SetResolution(Vector2Int resolution)
    {
        CaptureResolution = resolution;
    }
}
