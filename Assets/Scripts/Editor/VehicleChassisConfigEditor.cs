using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VehicleChassisConfig))]
public class VehicleChassisConfigEditor : Editor
{
    private bool showSlots = true;

    public override void OnInspectorGUI()
    {
        VehicleChassisConfig config = (VehicleChassisConfig)target;
        
        // Undo support
        Undo.RecordObject(config, "Modify Chassis Config");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Chassis Configuration", EditorStyles.boldLabel);
        
        // Basic Info
        config.chassisData.spriteName = EditorGUILayout.TextField("Sprite Name", config.chassisData.spriteName);
        config.chassisData.canvasSize = EditorGUILayout.Vector2Field("Canvas Size (Pixels)", config.chassisData.canvasSize);
        
        EditorGUILayout.Space();
        
        // Helper Calculation Info
        Vector2 center = config.chassisData.canvasSize / 2f;
        EditorGUILayout.HelpBox($"Canvas Center: ({center.x}, {center.y})\n" +
                                "All 'Canvas Positions' entered below will be converted to offsets relative to this center.", MessageType.Info);

        // MIGRATION TOOL
        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("Migrate Legacy Data (Absolute -> Relative)"))
        {
            if (EditorUtility.DisplayDialog("Migrate Data?", 
                "This will subtract the Canvas Center from ALL current slot positions. \n\n" +
                "Use this ONLY if your current data is in 'Absolute Canvas Coordinates' (e.g. 7, 5.5) and you want to convert it to Relative Offsets.", 
                "Yes, Migrate", "Cancel"))
            {
                foreach (var slot in config.chassisData.componentSlots)
                {
                    // Convert Absolute (7, 5.5) -> Relative (2, 0.5)
                    slot.pixelPosition -= center;
                }
                EditorUtility.SetDirty(config);
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space();

        // Slot List
        showSlots = EditorGUILayout.Foldout(showSlots, $"Component Slots ({config.chassisData.componentSlots.Count})", true);
        
        if (showSlots)
        {
            EditorGUI.indentLevel++;
            
            for (int i = 0; i < config.chassisData.componentSlots.Count; i++)
            {
                ComponentSlotData slot = config.chassisData.componentSlots[i];
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Header
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Slot {i + 1}: {slot.slotType} {slot.slotIndex}", EditorStyles.boldLabel);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    config.RemoveComponentSlot(i);
                    break; // Exit loop to avoid modification error
                }
                EditorGUILayout.EndHorizontal();
                
                // Properties
                slot.slotType = (ComponentSlotType)EditorGUILayout.EnumPopup("Slot Type", slot.slotType);
                slot.direction = EditorGUILayout.Vector2Field("Direction (Normal)", slot.direction);
                if (slot.direction == Vector2.zero) slot.direction = Vector2.up; // Prevent zero direction
                slot.direction.Normalize();
                
                // POSITIONING MAGIC
                // Calculate current Canvas Position derived from stored Relative Offset
                // Relative = Canvas - Center  =>  Canvas = Relative + Center
                Vector2 currentCanvasPos = slot.pixelPosition + center;
                
                Vector2 newCanvasPos = EditorGUILayout.Vector2Field("Canvas Position (e.g. 7, 5.5)", currentCanvasPos);
                
                // If changed, update the Relative Offset (pixelPosition)
                if (newCanvasPos != currentCanvasPos)
                {
                    slot.pixelPosition = newCanvasPos - center;
                }
                
                // Show the actual stored value read-only
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Vector2Field("Stored Relative Offset", slot.pixelPosition);
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            
            EditorGUI.indentLevel--;
            
            // Add Button
            EditorGUILayout.Space();
            if (GUILayout.Button("Add New Slot"))
            {
                config.AddComponentSlot(ComponentSlotType.Primary, Vector2.zero, Vector2.up);
            }
        }
        
        if (GUI.changed)
        {
            EditorUtility.SetDirty(config);
        }
    }
}
