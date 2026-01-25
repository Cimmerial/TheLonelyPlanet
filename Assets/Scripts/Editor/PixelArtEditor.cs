// Assets/Editor/PixelArtEditor.cs
using UnityEngine;
using UnityEditor;
using System.IO;

public class PixelArtEditor : EditorWindow
{
    private int canvasSize = 10;
    private Color[] pixels;
    private Color currentColor = Color.white;
    private Color backgroundColor = Color.clear;
    private string spriteName = "NewVehicle";
    private bool isDragging = false;
    
    private Vector2 scrollPos;
    private float pixelDisplaySize = 20f;
    
    // Tool selection
    private enum Tool { Pencil, Eraser, Fill, Eyedropper, SetCenter }
    private Tool currentTool = Tool.Pencil;
    
    // ADDED: Center point (in pixel coordinates, supports half-pixels for edges)
    private Vector2 centerPoint;
    private bool centerPointSet = false;
    
    // Color palette
    private Color[] palette = new Color[]
    {
        Color.white,
        Color.black,
        Color.red,
        Color.green,
        Color.blue,
        Color.yellow,
        Color.cyan,
        Color.magenta,
        new Color(0.5f, 0.5f, 0.5f), // Gray
        new Color(0.8f, 0.4f, 0.2f)  // Brown
    };

    [MenuItem("Tools/Pixel Art Editor")]
    public static void ShowWindow()
    {
        GetWindow<PixelArtEditor>("Pixel Art Editor");
    }

    private void OnEnable()
    {
        InitializeCanvas();
    }

    private void InitializeCanvas()
    {
        pixels = new Color[canvasSize * canvasSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = backgroundColor;
        }
        
        // ADDED: Reset center to default (middle of canvas)
        centerPoint = new Vector2(canvasSize / 2f, canvasSize / 2f);
        centerPointSet = false;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        
        // Top toolbar
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Canvas Size:", GUILayout.Width(80));
        int newSize = EditorGUILayout.IntSlider(canvasSize, 8, 32);
        if (newSize != canvasSize)
        {
            canvasSize = newSize;
            InitializeCanvas();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Pixel Size:", GUILayout.Width(80));
        pixelDisplaySize = EditorGUILayout.Slider(pixelDisplaySize, 10f, 40f);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // Tool selection
        EditorGUILayout.LabelField("Tools:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(currentTool == Tool.Pencil, "Pencil", "Button")) currentTool = Tool.Pencil;
        if (GUILayout.Toggle(currentTool == Tool.Eraser, "Eraser", "Button")) currentTool = Tool.Eraser;
        if (GUILayout.Toggle(currentTool == Tool.Fill, "Fill", "Button")) currentTool = Tool.Fill;
        if (GUILayout.Toggle(currentTool == Tool.Eyedropper, "Eyedropper", "Button")) currentTool = Tool.Eyedropper;
        if (GUILayout.Toggle(currentTool == Tool.SetCenter, "Set Center", "Button")) currentTool = Tool.SetCenter;
        EditorGUILayout.EndHorizontal();
        
        // ADDED: Center point info
        if (centerPointSet)
        {
            EditorGUILayout.HelpBox($"Center Point: ({centerPoint.x:F1}, {centerPoint.y:F1})", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("Center Point: Default (middle of canvas)", MessageType.Info);
        }
        
        if (GUILayout.Button("Reset Center to Default"))
        {
            centerPoint = new Vector2(canvasSize / 2f, canvasSize / 2f);
            centerPointSet = false;
            Repaint();
        }
        
        EditorGUILayout.Space(10);
        
        // Color palette
        EditorGUILayout.LabelField("Palette:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        foreach (Color paletteColor in palette)
        {
            GUI.backgroundColor = paletteColor;
            if (GUILayout.Button("", GUILayout.Width(30), GUILayout.Height(30)))
            {
                currentColor = paletteColor;
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Current Color:", GUILayout.Width(90));
        currentColor = EditorGUILayout.ColorField(currentColor, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // Canvas
        EditorGUILayout.LabelField("Canvas:", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        DrawCanvas();
        EditorGUILayout.EndScrollView();
        
        EditorGUILayout.Space(10);
        
        // Action buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Canvas"))
        {
            InitializeCanvas();
        }
        if (GUILayout.Button("Fill with Color"))
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = currentColor;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // Save section
        EditorGUILayout.LabelField("Save Sprite:", EditorStyles.boldLabel);
        spriteName = EditorGUILayout.TextField("Sprite Name:", spriteName);
        
        if (GUILayout.Button("Save Sprite", GUILayout.Height(30)))
        {
            SaveSprite();
        }
        
        if (GUILayout.Button("Load Sprite", GUILayout.Height(30)))
        {
            LoadSprite();
        }
    }

    private void DrawCanvas()
    {
        Event e = Event.current;
        
        Rect canvasRect = GUILayoutUtility.GetRect(
            canvasSize * pixelDisplaySize,
            canvasSize * pixelDisplaySize
        );
        
        // Draw background
        EditorGUI.DrawRect(canvasRect, new Color(0.2f, 0.2f, 0.2f));
        
        // Draw pixels
        for (int y = 0; y < canvasSize; y++)
        {
            for (int x = 0; x < canvasSize; x++)
            {
                Rect pixelRect = new Rect(
                    canvasRect.x + x * pixelDisplaySize,
                    canvasRect.y + (canvasSize - 1 - y) * pixelDisplaySize,
                    pixelDisplaySize,
                    pixelDisplaySize
                );
                
                Color pixelColor = pixels[y * canvasSize + x];
                EditorGUI.DrawRect(pixelRect, pixelColor);
                
                // Draw grid
                Handles.color = new Color(0.3f, 0.3f, 0.3f);
                Handles.DrawSolidRectangleWithOutline(pixelRect, Color.clear, new Color(0.3f, 0.3f, 0.3f));
            }
        }
        
        // ADDED: Draw center point
        DrawCenterPoint(canvasRect);
        
        // Handle mouse input
        if (canvasRect.Contains(e.mousePosition))
        {
            if (currentTool == Tool.SetCenter)
            {
                HandleCenterPointPlacement(e, canvasRect);
            }
            else
            {
                int x = Mathf.FloorToInt((e.mousePosition.x - canvasRect.x) / pixelDisplaySize);
                int y = canvasSize - 1 - Mathf.FloorToInt((e.mousePosition.y - canvasRect.y) / pixelDisplaySize);
                
                if (x >= 0 && x < canvasSize && y >= 0 && y < canvasSize)
                {
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        isDragging = true;
                        ApplyTool(x, y);
                        e.Use();
                    }
                    else if (e.type == EventType.MouseDrag && isDragging)
                    {
                        ApplyTool(x, y);
                        e.Use();
                    }
                    else if (e.type == EventType.MouseUp)
                    {
                        isDragging = false;
                    }
                }
            }
        }
        
        if (e.type == EventType.MouseUp)
        {
            isDragging = false;
        }
    }

    // ADDED: Draw center point crosshair
    private void DrawCenterPoint(Rect canvasRect)
    {
        // Convert center point to screen coordinates
        float screenX = canvasRect.x + centerPoint.x * pixelDisplaySize;
        float screenY = canvasRect.y + (canvasSize - centerPoint.y) * pixelDisplaySize;
        
        // Draw crosshair
        Handles.color = centerPointSet ? Color.yellow : Color.cyan;
        float crossSize = 10f;
        Handles.DrawLine(new Vector3(screenX - crossSize, screenY, 0), new Vector3(screenX + crossSize, screenY, 0));
        Handles.DrawLine(new Vector3(screenX, screenY - crossSize, 0), new Vector3(screenX, screenY + crossSize, 0));
        
        // Draw circle
        Handles.DrawWireDisc(new Vector3(screenX, screenY, 0), Vector3.forward, 5f);
    }

    // ADDED: Handle center point placement with snapping
    private void HandleCenterPointPlacement(Event e, Rect canvasRect)
    {
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            // Get mouse position relative to canvas
            float relX = (e.mousePosition.x - canvasRect.x) / pixelDisplaySize;
            float relY = canvasSize - (e.mousePosition.y - canvasRect.y) / pixelDisplaySize;
            
            // Snap to nearest half-pixel (allows edges and centers)
            centerPoint.x = Mathf.Round(relX * 2f) / 2f;
            centerPoint.y = Mathf.Round(relY * 2f) / 2f;
            
            // Clamp to canvas bounds
            centerPoint.x = Mathf.Clamp(centerPoint.x, 0f, canvasSize);
            centerPoint.y = Mathf.Clamp(centerPoint.y, 0f, canvasSize);
            
            centerPointSet = true;
            e.Use();
            Repaint();
        }
    }

    private void ApplyTool(int x, int y)
    {
        int index = y * canvasSize + x;
        
        switch (currentTool)
        {
            case Tool.Pencil:
                pixels[index] = currentColor;
                Repaint();
                break;
                
            case Tool.Eraser:
                pixels[index] = backgroundColor;
                Repaint();
                break;
                
            case Tool.Fill:
                FloodFill(x, y, pixels[index], currentColor);
                Repaint();
                break;
                
            case Tool.Eyedropper:
                currentColor = pixels[index];
                currentTool = Tool.Pencil; // Auto-switch to pencil
                Repaint();
                break;
        }
    }

    private void FloodFill(int x, int y, Color targetColor, Color replacementColor)
    {
        if (targetColor == replacementColor) return;
        
        int index = y * canvasSize + x;
        if (pixels[index] != targetColor) return;
        
        pixels[index] = replacementColor;
        
        // Recursively fill neighbors
        if (x > 0) FloodFill(x - 1, y, targetColor, replacementColor);
        if (x < canvasSize - 1) FloodFill(x + 1, y, targetColor, replacementColor);
        if (y > 0) FloodFill(x, y - 1, targetColor, replacementColor);
        if (y < canvasSize - 1) FloodFill(x, y + 1, targetColor, replacementColor);
    }

    private void SaveSprite()
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            EditorUtility.DisplayDialog("Error", "Please enter a sprite name.", "OK");
            return;
        }
        
        // EDITED: Calculate bounding box and recenter based on center point
        int minX = canvasSize, maxX = 0, minY = canvasSize, maxY = 0;
        bool hasPixels = false;
        
        for (int y = 0; y < canvasSize; y++)
        {
            for (int x = 0; x < canvasSize; x++)
            {
                if (pixels[y * canvasSize + x].a > 0.1f)
                {
                    hasPixels = true;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        
        if (!hasPixels)
        {
            EditorUtility.DisplayDialog("Error", "Canvas is empty!", "OK");
            return;
        }
        
        // ADDED: Calculate how much to expand around center point
        float centerX = centerPointSet ? centerPoint.x : canvasSize / 2f;
        float centerY = centerPointSet ? centerPoint.y : canvasSize / 2f;
        
        // Distance from center to edges of bounding box
        float leftDist = centerX - minX;
        float rightDist = maxX - centerX;
        float bottomDist = centerY - minY;
        float topDist = maxY - centerY;
        
        // New size is 2 * max distance in each direction
        int newWidth = Mathf.CeilToInt(2f * Mathf.Max(leftDist, rightDist));
        int newHeight = Mathf.CeilToInt(2f * Mathf.Max(bottomDist, topDist));
        
        // Ensure even dimensions (better for centering)
        if (newWidth % 2 != 0) newWidth++;
        if (newHeight % 2 != 0) newHeight++;
        
        // Create new centered texture
        Texture2D texture = new Texture2D(newWidth, newHeight)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        
        // Fill with transparent
        Color[] newPixels = new Color[newWidth * newHeight];
        for (int i = 0; i < newPixels.Length; i++)
        {
            newPixels[i] = Color.clear;
        }
        
        // Calculate offset to center the sprite
        int offsetX = Mathf.FloorToInt(newWidth / 2f - centerX);
        int offsetY = Mathf.FloorToInt(newHeight / 2f - centerY);
        
        // Copy pixels with offset
        for (int y = 0; y < canvasSize; y++)
        {
            for (int x = 0; x < canvasSize; x++)
            {
                int newX = x + offsetX;
                int newY = y + offsetY;
                
                if (newX >= 0 && newX < newWidth && newY >= 0 && newY < newHeight)
                {
                    newPixels[newY * newWidth + newX] = pixels[y * canvasSize + x];
                }
            }
        }
        
        // Apply pixels to texture
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                texture.SetPixel(x, y, newPixels[y * newWidth + x]);
            }
        }
        texture.Apply();
        
        // Save to Resources/VehicleSprites folder
        string folderPath = "Assets/Resources/VehicleSprites";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        
        string path = $"{folderPath}/{spriteName}.png";
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        
        AssetDatabase.Refresh();
        
        // Set texture import settings
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = Utility.GLOBAL_PPU;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
        
        EditorUtility.DisplayDialog("Success", $"Sprite '{spriteName}' saved!\nSize: {newWidth}x{newHeight}\nCentered at: ({centerX:F1}, {centerY:F1})", "OK");
    }

    private void LoadSprite()
    {
        string path = EditorUtility.OpenFilePanel("Load Sprite", "Assets/Resources/VehicleSprites", "png");
        if (string.IsNullOrEmpty(path)) return;
        
        byte[] fileData = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(fileData);
        
        // Resize canvas if needed
        if (texture.width != canvasSize || texture.height != canvasSize)
        {
            canvasSize = Mathf.Max(texture.width, texture.height);
            InitializeCanvas();
        }
        
        // Load pixels (centered if texture is smaller than canvas)
        int offsetX = (canvasSize - texture.width) / 2;
        int offsetY = (canvasSize - texture.height) / 2;
        
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                int canvasX = x + offsetX;
                int canvasY = y + offsetY;
                if (canvasX >= 0 && canvasX < canvasSize && canvasY >= 0 && canvasY < canvasSize)
                {
                    pixels[canvasY * canvasSize + canvasX] = texture.GetPixel(x, y);
                }
            }
        }
        
        spriteName = Path.GetFileNameWithoutExtension(path);
        Repaint();
    }
}