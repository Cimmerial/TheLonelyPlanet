// Assets/Editor/VehicleEditor.cs
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Vehicle), true)]
public class VehicleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        
        Vehicle vehicle = (Vehicle)target;
        
        // Add custom button
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("Apply Vehicle Chassis Changes", GUILayout.Height(35)))
        {
            vehicle.ApplyChassisChanges();
            EditorUtility.SetDirty(vehicle);
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space(5);
        
        // Show slot counts
        SerializedProperty chassisConfig = serializedObject.FindProperty("chassisConfig");
        if (chassisConfig.objectReferenceValue != null)
        {
            VehicleChassisConfig config = chassisConfig.objectReferenceValue as VehicleChassisConfig;
            if (config != null)
            {
                EditorGUILayout.HelpBox(
                    $"Chassis Config: {config.chassisData.spriteName}\n" +
                    $"Primary Slots: {config.chassisData.GetSlotCount(ComponentSlotType.Primary)}\n" +
                    $"Secondary Slots: {config.chassisData.GetSlotCount(ComponentSlotType.Secondary)}\n" +
                    $"Specialized Slots: {config.chassisData.GetSlotCount(ComponentSlotType.Specialized)}",
                    MessageType.Info
                );
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a Chassis Config to use component slots", MessageType.Warning);
        }
    }
}