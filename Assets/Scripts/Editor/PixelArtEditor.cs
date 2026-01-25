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
    private enum Tool { Pencil, Eraser, Fill, Eyedropper }
    private Tool currentTool = Tool.Pencil;
    
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
        EditorGUILayout.EndHorizontal();
        
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
        
        // Handle mouse input
        if (canvasRect.Contains(e.mousePosition))
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
        
        if (e.type == EventType.MouseUp)
        {
            isDragging = false;
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
        
        // Create texture
        Texture2D texture = new Texture2D(canvasSize, canvasSize)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        
        for (int y = 0; y < canvasSize; y++)
        {
            for (int x = 0; x < canvasSize; x++)
            {
                texture.SetPixel(x, y, pixels[y * canvasSize + x]);
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
            importer.isReadable = true; // CRITICAL: Allow reading pixels at runtime
            importer.SaveAndReimport();
        }
        
        EditorUtility.DisplayDialog("Success", $"Sprite '{spriteName}' saved to {path}", "OK");
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
            canvasSize = texture.width;
            InitializeCanvas();
        }
        
        // Load pixels
        for (int y = 0; y < canvasSize; y++)
        {
            for (int x = 0; x < canvasSize; x++)
            {
                pixels[y * canvasSize + x] = texture.GetPixel(x, y);
            }
        }
        
        spriteName = Path.GetFileNameWithoutExtension(path);
        Repaint();
    }
}