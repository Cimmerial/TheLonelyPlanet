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
        EditorGUILayout.HelpBox(
            $"Reference Canvas Center: ({center.x}, {center.y})\n" +
            "Slot positions are stored as OFFSETS relative to center (0,0 at chassis center).\n" +
            "Reference Canvas Size is informational and only used for displaying derived positions.",
            MessageType.Info
        );

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
                
                // Stored value is always a center-relative offset.
                slot.pixelPosition = EditorGUILayout.Vector2Field("Relative Offset (from center)", slot.pixelPosition);

                // Derived reference position (read-only)
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Vector2Field("Reference Canvas Position", slot.pixelPosition + center);
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
