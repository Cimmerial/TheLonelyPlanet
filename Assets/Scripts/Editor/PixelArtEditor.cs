// Assets/Editor/PixelArtEditor.cs
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

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
    
    // Editor mode selection
    private enum EditorMode { Drawing, ComponentSlots }
    private EditorMode currentMode = EditorMode.Drawing;
    
    // Tool selection for drawing mode
    private enum Tool { Pencil, Eraser, Fill, Eyedropper, SetCenter }
    private Tool currentTool = Tool.Pencil;
    
    // Center point
    private Vector2 centerPoint;
    private bool centerPointSet = false;
    
    // Component slots
    private List<ComponentSlotData> componentSlots = new List<ComponentSlotData>();
    private ComponentSlotType currentSlotType = ComponentSlotType.Primary;
    private Vector2 currentSlotDirection = Vector2.up;
    private int selectedSlotIndex = -1;
    
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
        new Color(0.8f, 0.4f, 0.2f)
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
        
        centerPoint = new Vector2(canvasSize / 2f, canvasSize / 2f);
        centerPointSet = false;
        componentSlots.Clear();
    }

    private void OnGUI()
    {
        // Start main scroll view for entire window
        Vector2 mainScrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
        
        EditorGUILayout.Space(10);
        
        // Mode selection
        EditorGUILayout.LabelField("Editor Mode:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(currentMode == EditorMode.Drawing, "Drawing", "Button", GUILayout.Height(30))) 
            currentMode = EditorMode.Drawing;
        if (GUILayout.Toggle(currentMode == EditorMode.ComponentSlots, "Component Slots", "Button", GUILayout.Height(30))) 
            currentMode = EditorMode.ComponentSlots;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // Two-column layout for settings
        EditorGUILayout.BeginHorizontal();
        
        // LEFT COLUMN
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
        
        // Canvas size
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
        
        // Save section
        EditorGUILayout.LabelField("Save:", EditorStyles.boldLabel);
        spriteName = EditorGUILayout.TextField("Name:", spriteName);
        
        if (GUILayout.Button("Save Sprite", GUILayout.Height(30)))
        {
            SaveSprite();
        }
        if (GUILayout.Button("Save Chassis Config", GUILayout.Height(30)))
        {
            SaveChassisConfig();
        }
        if (GUILayout.Button("Load Sprite", GUILayout.Height(30)))
        {
            LoadSprite();
        }
        
        EditorGUILayout.EndVertical();
        
        // RIGHT COLUMN
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
        
        // Mode-specific UI
        if (currentMode == EditorMode.Drawing)
        {
            DrawDrawingModeUI();
        }
        else
        {
            DrawComponentSlotsModeUI();
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

    private void DrawDrawingModeUI()
    {
        // Tool selection
        EditorGUILayout.LabelField("Tools:", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical();
        if (GUILayout.Toggle(currentTool == Tool.Pencil, "Pencil", "Button")) currentTool = Tool.Pencil;
        if (GUILayout.Toggle(currentTool == Tool.Eraser, "Eraser", "Button")) currentTool = Tool.Eraser;
        if (GUILayout.Toggle(currentTool == Tool.Fill, "Fill", "Button")) currentTool = Tool.Fill;
        if (GUILayout.Toggle(currentTool == Tool.Eyedropper, "Eyedropper", "Button")) currentTool = Tool.Eyedropper;
        if (GUILayout.Toggle(currentTool == Tool.SetCenter, "Set Center", "Button")) currentTool = Tool.SetCenter;
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        
        // Center point info
        if (centerPointSet)
        {
            EditorGUILayout.LabelField($"Center: ({centerPoint.x:F1}, {centerPoint.y:F1})", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("Center: Default", EditorStyles.miniLabel);
        }
        
        if (GUILayout.Button("Reset Center"))
        {
            centerPoint = new Vector2(canvasSize / 2f, canvasSize / 2f);
            centerPointSet = false;
            Repaint();
        }
        
        EditorGUILayout.Space(5);
        
        // Color palette (compact)
        EditorGUILayout.LabelField("Palette:", EditorStyles.boldLabel);
        for (int row = 0; row < 2; row++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int col = 0; col < 5; col++)
            {
                int index = row * 5 + col;
                if (index < palette.Length)
                {
                    GUI.backgroundColor = palette[index];
                    if (GUILayout.Button("", GUILayout.Width(30), GUILayout.Height(30)))
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
    }

    private void DrawComponentSlotsModeUI()
    {
        EditorGUILayout.LabelField("Component Slots:", EditorStyles.boldLabel);
        
        // Slot type selection
        currentSlotType = (ComponentSlotType)EditorGUILayout.EnumPopup("Type:", currentSlotType);
        
        EditorGUILayout.Space(5);
        
        // Direction arrows (compact)
        EditorGUILayout.LabelField("Direction:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↑", GUILayout.Width(35), GUILayout.Height(35))) currentSlotDirection = Vector2.up;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("←", GUILayout.Width(35), GUILayout.Height(35))) currentSlotDirection = Vector2.left;
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("→", GUILayout.Width(35), GUILayout.Height(35))) currentSlotDirection = Vector2.right;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↓", GUILayout.Width(35), GUILayout.Height(35))) currentSlotDirection = Vector2.down;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"Dir: {currentSlotDirection}", EditorStyles.miniLabel);
        
        EditorGUILayout.Space(5);
        
        // Slots list (scrollable if many)
        EditorGUILayout.LabelField($"Slots ({componentSlots.Count}):", EditorStyles.boldLabel);
        
        Vector2 slotsScrollPos = EditorGUILayout.BeginScrollView(new Vector2(), GUILayout.Height(150));
        for (int i = 0; i < componentSlots.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            
            bool isSelected = (selectedSlotIndex == i);
            GUI.backgroundColor = isSelected ? Color.yellow : Color.white;
            
            string slotInfo = $"{componentSlots[i].slotType} {componentSlots[i].slotIndex}";
            
            if (GUILayout.Button(slotInfo, GUILayout.Height(20)))
            {
                selectedSlotIndex = (selectedSlotIndex == i) ? -1 : i;
            }
            
            GUI.backgroundColor = Color.white;
            
            if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(20)))
            {
                componentSlots.RemoveAt(i);
                selectedSlotIndex = -1;
                ReindexSlots();
                Repaint();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        
        if (GUILayout.Button("Clear All"))
        {
            componentSlots.Clear();
            selectedSlotIndex = -1;
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Click canvas to place slot", EditorStyles.miniLabel);
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
        
        // Draw component slots
        if (currentMode == EditorMode.ComponentSlots)
        {
            DrawComponentSlots(canvasRect);
        }
        
        // Handle mouse input
        if (canvasRect.Contains(e.mousePosition))
        {
            if (currentMode == EditorMode.ComponentSlots)
            {
                HandleComponentSlotPlacement(e, canvasRect);
            }
            else if (currentTool == Tool.SetCenter)
            {
                HandleCenterPointPlacement(e, canvasRect);
            }
            else
            {
                HandleDrawingInput(e, canvasRect);
            }
        }
        
        if (e.type == EventType.MouseUp)
        {
            isDragging = false;
        }
    }

    private void HandleDrawingInput(Event e, Rect canvasRect)
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

    private void HandleComponentSlotPlacement(Event e, Rect canvasRect)
    {
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            float relX = (e.mousePosition.x - canvasRect.x) / pixelDisplaySize;
            float relY = canvasSize - (e.mousePosition.y - canvasRect.y) / pixelDisplaySize;
            
            // Snap to nearest half-pixel
            Vector2 slotPos = new Vector2(
                Mathf.Round(relX * 2f) / 2f,
                Mathf.Round(relY * 2f) / 2f
            );
            
            slotPos.x = Mathf.Clamp(slotPos.x, 0f, canvasSize);
            slotPos.y = Mathf.Clamp(slotPos.y, 0f, canvasSize);
            
            // Count existing slots of this type
            int slotIndex = 1;
            foreach (var slot in componentSlots)
            {
                if (slot.slotType == currentSlotType) slotIndex++;
            }
            
            ComponentSlotData newSlot = new ComponentSlotData(
                currentSlotType,
                slotPos,
                currentSlotDirection,
                slotIndex
            );
            
            componentSlots.Add(newSlot);
            ReindexSlots();
            
            e.Use();
            Repaint();
        }
    }

    private void DrawCenterPoint(Rect canvasRect)
    {
        float screenX = canvasRect.x + centerPoint.x * pixelDisplaySize;
        float screenY = canvasRect.y + (canvasSize - centerPoint.y) * pixelDisplaySize;
        
        Handles.color = centerPointSet ? Color.yellow : Color.cyan;
        float crossSize = 10f;
        Handles.DrawLine(new Vector3(screenX - crossSize, screenY, 0), new Vector3(screenX + crossSize, screenY, 0));
        Handles.DrawLine(new Vector3(screenX, screenY - crossSize, 0), new Vector3(screenX, screenY + crossSize, 0));
        Handles.DrawWireDisc(new Vector3(screenX, screenY, 0), Vector3.forward, 5f);
    }

    private void DrawComponentSlots(Rect canvasRect)
    {
        for (int i = 0; i < componentSlots.Count; i++)
        {
            ComponentSlotData slot = componentSlots[i];
            
            float screenX = canvasRect.x + slot.pixelPosition.x * pixelDisplaySize;
            float screenY = canvasRect.y + (canvasSize - slot.pixelPosition.y) * pixelDisplaySize;
            
            // Choose color based on type
            Color slotColor = Color.white;
            switch (slot.slotType)
            {
                case ComponentSlotType.Primary: slotColor = Color.red; break;
                case ComponentSlotType.Secondary: slotColor = Color.green; break;
                case ComponentSlotType.Specialized: slotColor = Color.magenta; break;
            }
            
            if (selectedSlotIndex == i)
            {
                slotColor = Color.yellow;
            }
            
            Handles.color = slotColor;
            
            // Draw circle for slot
            Handles.DrawWireDisc(new Vector3(screenX, screenY, 0), Vector3.forward, 8f);
            Handles.DrawSolidDisc(new Vector3(screenX, screenY, 0), Vector3.forward, 3f);
            
            // Draw direction arrow
            Vector2 directionScreen = new Vector2(slot.direction.x, -slot.direction.y) * 15f;
            Vector3 arrowEnd = new Vector3(screenX + directionScreen.x, screenY + directionScreen.y, 0);
            Handles.DrawLine(new Vector3(screenX, screenY, 0), arrowEnd);
            
            // Draw arrowhead
            Vector2 perpendicular = new Vector2(-directionScreen.y, directionScreen.x).normalized * 5f;
            Vector3 arrowPoint1 = arrowEnd - new Vector3(directionScreen.x, directionScreen.y, 0) * 0.3f + new Vector3(perpendicular.x, perpendicular.y, 0);
            Vector3 arrowPoint2 = arrowEnd - new Vector3(directionScreen.x, directionScreen.y, 0) * 0.3f - new Vector3(perpendicular.x, perpendicular.y, 0);
            Handles.DrawLine(arrowEnd, arrowPoint1);
            Handles.DrawLine(arrowEnd, arrowPoint2);
            
            // Draw label
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = slotColor;
            labelStyle.fontSize = 10;
            string slotLabel = slot.slotType.ToString()[0].ToString() + slot.slotIndex;
            Handles.Label(new Vector3(screenX + 12, screenY - 12, 0), slotLabel, labelStyle);
        }
    }

    private void HandleCenterPointPlacement(Event e, Rect canvasRect)
    {
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            float relX = (e.mousePosition.x - canvasRect.x) / pixelDisplaySize;
            float relY = canvasSize - (e.mousePosition.y - canvasRect.y) / pixelDisplaySize;
            
            centerPoint.x = Mathf.Round(relX * 2f) / 2f;
            centerPoint.y = Mathf.Round(relY * 2f) / 2f;
            
            centerPoint.x = Mathf.Clamp(centerPoint.x, 0f, canvasSize);
            centerPoint.y = Mathf.Clamp(centerPoint.y, 0f, canvasSize);
            
            centerPointSet = true;
            e.Use();
            Repaint();
        }
    }

    private void ReindexSlots()
    {
        int primaryIndex = 1;
        int secondaryIndex = 1;
        int specializedIndex = 1;

        foreach (var slot in componentSlots)
        {
            switch (slot.slotType)
            {
                case ComponentSlotType.Primary:
                    slot.slotIndex = primaryIndex++;
                    break;
                case ComponentSlotType.Secondary:
                    slot.slotIndex = secondaryIndex++;
                    break;
                case ComponentSlotType.Specialized:
                    slot.slotIndex = specializedIndex++;
                    break;
            }
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

    private void SaveSprite()
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            EditorUtility.DisplayDialog("Error", "Please enter a sprite name.", "OK");
            return;
        }
        
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
        
        float centerX = centerPointSet ? centerPoint.x : canvasSize / 2f;
        float centerY = centerPointSet ? centerPoint.y : canvasSize / 2f;
        
        float leftDist = centerX - minX;
        float rightDist = maxX - centerX;
        float bottomDist = centerY - minY;
        float topDist = maxY - centerY;
        
        int newWidth = Mathf.CeilToInt(2f * Mathf.Max(leftDist, rightDist));
        int newHeight = Mathf.CeilToInt(2f * Mathf.Max(bottomDist, topDist));
        
        if (newWidth % 2 != 0) newWidth++;
        if (newHeight % 2 != 0) newHeight++;
        
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
        
        string folderPath = "Assets/Resources/VehicleSprites";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        
        string path = $"{folderPath}/{spriteName}.png";
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
        
        EditorUtility.DisplayDialog("Success", $"Sprite '{spriteName}' saved!\nSize: {newWidth}x{newHeight}", "OK");
    }

    private void SaveChassisConfig()
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            EditorUtility.DisplayDialog("Error", "Please enter a sprite name.", "OK");
            return;
        }
        
        // Adjust component slot positions for the exported texture offset
        float centerX = centerPointSet ? centerPoint.x : canvasSize / 2f;
        float centerY = centerPointSet ? centerPoint.y : canvasSize / 2f;
        
        // Create/load chassis config
        string configPath = $"Assets/Resources/ChassisConfigs/{spriteName}_Chassis.asset";
        string folderPath = "Assets/Resources/ChassisConfigs";
        
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        
        VehicleChassisConfig config = AssetDatabase.LoadAssetAtPath<VehicleChassisConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<VehicleChassisConfig>();
            AssetDatabase.CreateAsset(config, configPath);
        }
        
        config.chassisData.spriteName = spriteName;
        config.chassisData.componentSlots.Clear();
        config.chassisData.componentSlots.AddRange(componentSlots);
        
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Success", 
            $"Chassis config saved to {configPath}\n" +
            $"Slots: {componentSlots.Count} " +
            $"(P:{GetSlotCount(ComponentSlotType.Primary)} " +
            $"S:{GetSlotCount(ComponentSlotType.Secondary)} " +
            $"Sp:{GetSlotCount(ComponentSlotType.Specialized)})", 
            "OK");
    }

    private int GetSlotCount(ComponentSlotType type)
    {
        int count = 0;
        foreach (var slot in componentSlots)
        {
            if (slot.slotType == type) count++;
        }
        return count;
    }

    private void LoadSprite()
    {
        string path = EditorUtility.OpenFilePanel("Load Sprite", "Assets/Resources/VehicleSprites", "png");
        if (string.IsNullOrEmpty(path)) return;
        
        byte[] fileData = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        texture.filterMode = FilterMode.Point;
        texture.LoadImage(fileData);
        
        int maxDimension = Mathf.Max(texture.width, texture.height);
        if (maxDimension > canvasSize)
        {
            canvasSize = Mathf.NextPowerOfTwo(maxDimension);
            if (canvasSize > 32) canvasSize = 32;
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
        
        spriteName = Path.GetFileNameWithoutExtension(path);
        
        // Try to load associated chassis config
        string configPath = $"Assets/Resources/ChassisConfigs/{spriteName}_Chassis.asset";
        VehicleChassisConfig config = AssetDatabase.LoadAssetAtPath<VehicleChassisConfig>(configPath);
        if (config != null)
        {
            componentSlots.Clear();
            componentSlots.AddRange(config.chassisData.componentSlots);
            Debug.Log($"Loaded {componentSlots.Count} component slots from chassis config");
        }
        
        Repaint();
        EditorUtility.SetDirty(this);
    }
}