// Assets/Editor/ComponentEditor.cs
using UnityEngine;
using UnityEditor;
using System.IO;

public class ComponentEditor : EditorWindow
{
    private int canvasSize = 16;
    private Color[] pixels;
    private Color currentColor = Color.white;
    private Color backgroundColor = Color.clear;
    private string componentName = "NewComponent";
    private ComponentSlotType componentType = ComponentSlotType.Primary;
    private bool isDragging = false;
    
    private Vector2 scrollPos;
    private float pixelDisplaySize = 25f;
    
    // Tool selection
    private enum Tool { Pencil, Eraser, Fill, Eyedropper, SetCenter }
    private Tool currentTool = Tool.Pencil;
    
    // Center point
    private Vector2 centerPoint;
    private bool centerPointSet = false;
    
    // Attachment direction
    private Vector2 attachmentDirection = Vector2.up;
    private bool directionSet = false;
    
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
        new Color(0.5f, 0.5f, 0.5f),
        new Color(0.8f, 0.4f, 0.2f),
        new Color(0.6f, 0.3f, 0.1f),
        new Color(0.7f, 0.7f, 0.7f)
    };

    [MenuItem("Tools/Component Editor")]
    public static void ShowWindow()
    {
        GetWindow<ComponentEditor>("Component Editor");
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
        
        centerPoint = new Vector2(canvasSize / 2f, canvasSize / 2f);
        centerPointSet = false;
        attachmentDirection = Vector2.up;
        directionSet = false;
    }

    private void OnGUI()
    {
        // Start main scroll view for entire window
        Vector2 mainScrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
        
        EditorGUILayout.Space(10);
        
        EditorGUILayout.LabelField("Component Sprite Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        // Two-column layout for settings
        EditorGUILayout.BeginHorizontal();
        
        // LEFT COLUMN
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
        
        // Component info
        componentName = EditorGUILayout.TextField("Name:", componentName);
        componentType = (ComponentSlotType)EditorGUILayout.EnumPopup("Type:", componentType);
        
        EditorGUILayout.Space(10);
        
        // Canvas size
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Canvas Size:", GUILayout.Width(80));
        int newSize = EditorGUILayout.IntSlider(canvasSize, 8, 24);
        if (newSize != canvasSize)
        {
            canvasSize = newSize;
            InitializeCanvas();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Pixel Size:", GUILayout.Width(80));
        pixelDisplaySize = EditorGUILayout.Slider(pixelDisplaySize, 15f, 40f);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // Save buttons
        if (GUILayout.Button("Save Component", GUILayout.Height(35)))
        {
            SaveComponentSprite();
        }
        
        if (GUILayout.Button("Load Component", GUILayout.Height(30)))
        {
            LoadComponentSprite();
        }
        
        EditorGUILayout.EndVertical();
        
        // RIGHT COLUMN
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
        
        // Tool selection
        EditorGUILayout.LabelField("Tools:", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical();
        if (GUILayout.Toggle(currentTool == Tool.Pencil, "Pencil", "Button")) currentTool = Tool.Pencil;
        if (GUILayout.Toggle(currentTool == Tool.Eraser, "Eraser", "Button")) currentTool = Tool.Eraser;
        if (GUILayout.Toggle(currentTool == Tool.Fill, "Fill", "Button")) currentTool = Tool.Fill;
        if (GUILayout.Toggle(currentTool == Tool.Eyedropper, "Eyedropper", "Button")) currentTool = Tool.Eyedropper;
        if (GUILayout.Toggle(currentTool == Tool.SetCenter, "Set Attach Pt", "Button")) currentTool = Tool.SetCenter;
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // Center point info
        if (centerPointSet)
        {
            EditorGUILayout.LabelField($"Attach: ({centerPoint.x:F1}, {centerPoint.y:F1})", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("Attach: Default", EditorStyles.miniLabel);
        }
        
        if (GUILayout.Button("Reset Attach Point"))
        {
            centerPoint = new Vector2(canvasSize / 2f, canvasSize / 2f);
            centerPointSet = false;
            Repaint();
        }
        
        EditorGUILayout.Space(5);
        
        // Attachment direction
        EditorGUILayout.LabelField("Attach Direction:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↑", GUILayout.Width(35), GUILayout.Height(35))) 
        {
            attachmentDirection = Vector2.up;
            directionSet = true;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("←", GUILayout.Width(35), GUILayout.Height(35))) 
        {
            attachmentDirection = Vector2.left;
            directionSet = true;
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("→", GUILayout.Width(35), GUILayout.Height(35))) 
        {
            attachmentDirection = Vector2.right;
            directionSet = true;
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↓", GUILayout.Width(35), GUILayout.Height(35))) 
        {
            attachmentDirection = Vector2.down;
            directionSet = true;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.LabelField($"Dir: {attachmentDirection}", EditorStyles.miniLabel);
        
        EditorGUILayout.Space(5);
        
        // Color palette (compact 2 rows)
        EditorGUILayout.LabelField("Palette:", EditorStyles.boldLabel);
        for (int row = 0; row < 2; row++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int col = 0; col < 6; col++)
            {
                int index = row * 6 + col;
                if (index < palette.Length)
                {
                    GUI.backgroundColor = palette[index];
                    if (GUILayout.Button("", GUILayout.Width(25), GUILayout.Height(25)))
                    {
                        currentColor = palette[index];
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space(5);
        currentColor = EditorGUILayout.ColorField("Color:", currentColor);
        
        EditorGUILayout.Space(5);
        
        // Action buttons
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
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // Canvas (full width, scrollable)
        EditorGUILayout.LabelField("Canvas:", EditorStyles.boldLabel);
        DrawCanvas();
        
        EditorGUILayout.Space(10);
        
        EditorGUILayout.EndScrollView();
        
        scrollPos = mainScrollPos;
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
        
        // Draw center point
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

    private void DrawCenterPoint(Rect canvasRect)
    {
        float screenX = canvasRect.x + centerPoint.x * pixelDisplaySize;
        float screenY = canvasRect.y + (canvasSize - centerPoint.y) * pixelDisplaySize;
        
        Handles.color = centerPointSet ? Color.yellow : Color.green;
        float crossSize = 12f;
        
        // Draw crosshair
        Handles.DrawLine(new Vector3(screenX - crossSize, screenY, 0), new Vector3(screenX + crossSize, screenY, 0));
        Handles.DrawLine(new Vector3(screenX, screenY - crossSize, 0), new Vector3(screenX, screenY + crossSize, 0));
        
        // Draw circles
        Handles.DrawWireDisc(new Vector3(screenX, screenY, 0), Vector3.forward, 6f);
        Handles.DrawWireDisc(new Vector3(screenX, screenY, 0), Vector3.forward, 3f);
        
        // Draw attachment direction arrow
        Handles.color = directionSet ? Color.cyan : Color.blue;
        
        // Convert attachment direction to screen space (flip Y for screen coordinates)
        Vector2 screenDir = new Vector2(attachmentDirection.x, -attachmentDirection.y);
        float arrowLength = 25f;
        Vector2 arrowEnd = new Vector2(screenX, screenY) + screenDir * arrowLength;
        
        // Draw arrow line
        Handles.DrawLine(new Vector3(screenX, screenY, 0), new Vector3(arrowEnd.x, arrowEnd.y, 0));
        
        // Draw arrowhead
        Vector2 perpendicular = new Vector2(-screenDir.y, screenDir.x).normalized * 6f;
        Vector3 arrowPoint1 = new Vector3(arrowEnd.x, arrowEnd.y, 0) - new Vector3(screenDir.x, screenDir.y, 0) * 8f + new Vector3(perpendicular.x, perpendicular.y, 0);
        Vector3 arrowPoint2 = new Vector3(arrowEnd.x, arrowEnd.y, 0) - new Vector3(screenDir.x, screenDir.y, 0) * 8f - new Vector3(perpendicular.x, perpendicular.y, 0);
        Handles.DrawLine(new Vector3(arrowEnd.x, arrowEnd.y, 0), arrowPoint1);
        Handles.DrawLine(new Vector3(arrowEnd.x, arrowEnd.y, 0), arrowPoint2);
        
        // Draw label
        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
        labelStyle.normal.textColor = directionSet ? Color.cyan : Color.blue;
        Handles.Label(new Vector3(arrowEnd.x + 10, arrowEnd.y - 10, 0), "Attach→", labelStyle);
    }

    private void HandleCenterPointPlacement(Event e, Rect canvasRect)
    {
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            float relX = (e.mousePosition.x - canvasRect.x) / pixelDisplaySize;
            float relY = canvasSize - (e.mousePosition.y - canvasRect.y) / pixelDisplaySize;
            
            // Snap to nearest half-pixel
            centerPoint.x = Mathf.Round(relX * 2f) / 2f;
            centerPoint.y = Mathf.Round(relY * 2f) / 2f;
            
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
                currentTool = Tool.Pencil;
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
        
        if (x > 0) FloodFill(x - 1, y, targetColor, replacementColor);
        if (x < canvasSize - 1) FloodFill(x + 1, y, targetColor, replacementColor);
        if (y > 0) FloodFill(x, y - 1, targetColor, replacementColor);
        if (y < canvasSize - 1) FloodFill(x, y + 1, targetColor, replacementColor);
    }

    private void SaveComponentSprite()
    {
        if (string.IsNullOrWhiteSpace(componentName))
        {
            EditorUtility.DisplayDialog("Error", "Please enter a component name.", "OK");
            return;
        }
        
        // Find bounding box
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
        
        // Create texture centered on attachment point
        float centerX = centerPointSet ? centerPoint.x : canvasSize / 2f;
        float centerY = centerPointSet ? centerPoint.y : canvasSize / 2f;
        
        float leftDist = centerX - minX;
        float rightDist = (maxX + 1) - centerX; // Corrected: account for pixel width
        float bottomDist = centerY - minY;
        float topDist = (maxY + 1) - centerY; // Corrected: account for pixel height
        
        Debug.Log($"Bounds: X[{minX}-{maxX}], Y[{minY}-{maxY}]. Center: ({centerX}, {centerY})");
        Debug.Log($"Dists: L={leftDist}, R={rightDist}, B={bottomDist}, T={topDist}");
        
        int newWidth = Mathf.CeilToInt(2f * Mathf.Max(leftDist, rightDist));
        int newHeight = Mathf.CeilToInt(2f * Mathf.Max(bottomDist, topDist));
        
        // Parity correction:
        // If center is on a half-pixel (e.g. X.5), we need ODD width so center remains at .5
        // If center is on integer (e.g. X.0), we need EVEN width so center remains at .0
        
        bool centerXIsHalf = Mathf.Abs(centerX % 1f - 0.5f) < 0.01f;
        if (centerXIsHalf)
        {
            if (newWidth % 2 == 0) newWidth++; // Make Odd
        }
        else
        {
            if (newWidth % 2 != 0) newWidth++; // Make Even
        }

        bool centerYIsHalf = Mathf.Abs(centerY % 1f - 0.5f) < 0.01f;
        if (centerYIsHalf)
        {
            if (newHeight % 2 == 0) newHeight++; // Make Odd
        }
        else
        {
            if (newHeight % 2 != 0) newHeight++; // Make Even
        }
        
        Debug.Log($"New Texture Size: {newWidth}x{newHeight}. CenterXIsHalf: {centerXIsHalf}");
        
        Texture2D texture = new Texture2D(newWidth, newHeight)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        
        Color[] newPixels = new Color[newWidth * newHeight];
        for (int i = 0; i < newPixels.Length; i++)
        {
            newPixels[i] = Color.clear;
        }
        
        int offsetX = Mathf.FloorToInt(newWidth / 2f - centerX);
        int offsetY = Mathf.FloorToInt(newHeight / 2f - centerY);
        
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
        
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                texture.SetPixel(x, y, newPixels[y * newWidth + x]);
            }
        }
        texture.Apply();
        
        // Save to ComponentSprites folder
        string folderPath = "Assets/Resources/ComponentSprites";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        
        string path = $"{folderPath}/{componentName}.png";
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        
        AssetDatabase.Refresh();
        
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
        
        EditorUtility.DisplayDialog("Success", 
            $"Component '{componentName}' saved!\n" +
            $"Type: {componentType}\n" +
            $"Size: {newWidth}x{newHeight}\n" +
            $"Attachment Point: ({centerX:F1}, {centerY:F1})\n" +
            $"Attachment Direction: {attachmentDirection}", 
            "OK");
        
        // Also save component metadata
        SaveComponentMetadata(centerX, centerY);
    }
    
    private void SaveComponentMetadata(float centerX, float centerY)
    {
        string metadataPath = $"Assets/Resources/ComponentMetadata/{componentName}_Metadata.asset";
        string folderPath = "Assets/Resources/ComponentMetadata";
        
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        
        ComponentMetadata metadata = AssetDatabase.LoadAssetAtPath<ComponentMetadata>(metadataPath);
        if (metadata == null)
        {
            metadata = ScriptableObject.CreateInstance<ComponentMetadata>();
            AssetDatabase.CreateAsset(metadata, metadataPath);
        }
        
        metadata.componentName = componentName;
        metadata.componentType = componentType;
        metadata.spriteName = componentName;
        metadata.attachmentPoint = new Vector2(centerX, centerY);
        metadata.attachmentDirection = attachmentDirection;
        
        EditorUtility.SetDirty(metadata);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void LoadComponentSprite()
    {
        string path = EditorUtility.OpenFilePanel("Load Component", "Assets/Resources/ComponentSprites", "png");
        if (string.IsNullOrEmpty(path)) return;
        
        byte[] fileData = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        texture.filterMode = FilterMode.Point;
        texture.LoadImage(fileData);
        
        int maxDimension = Mathf.Max(texture.width, texture.height);
        if (maxDimension > canvasSize)
        {
            canvasSize = Mathf.NextPowerOfTwo(maxDimension);
            if (canvasSize > 24) canvasSize = 24;
        }
        
        InitializeCanvas();
        
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
        
        // Update center point to match the texture center
        // Texture was saved such that its center corresponds to the attach point
        float newCenterX = offsetX + (texture.width / 2f);
        float newCenterY = offsetY + (texture.height / 2f);
        
        centerPoint = new Vector2(newCenterX, newCenterY);
        centerPointSet = true;
        
        componentName = Path.GetFileNameWithoutExtension(path);
        
        // Try to load metadata to get direction
        ComponentMetadata metadata = Resources.Load<ComponentMetadata>($"ComponentMetadata/{componentName}_Metadata");
        if (metadata != null)
        {
            attachmentDirection = metadata.attachmentDirection;
            directionSet = true;
            Debug.Log($"Loaded metadata for {componentName}: Direction {attachmentDirection}");
        }
        
        Repaint();
        EditorUtility.SetDirty(this);
    }
}