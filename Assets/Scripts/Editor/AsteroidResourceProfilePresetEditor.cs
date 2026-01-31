using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AsteroidResourceProfilePreset))]
public class AsteroidResourceProfilePresetEditor : Editor
{
    private SerializedProperty _profileSalt;
    private SerializedProperty _entries;

    private void OnEnable()
    {
        _profileSalt = serializedObject.FindProperty("profileSalt");
        _entries = serializedObject.FindProperty("entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_profileSalt);

        EditorGUILayout.Space();
        DrawFractionBar();

        EditorGUILayout.Space();
        DrawEntriesList();

        EditorGUILayout.Space();
        DrawActions();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFractionBar()
    {
        AsteroidResourceProfilePreset preset = (AsteroidResourceProfilePreset)target;

        Rect rect = GUILayoutUtility.GetRect(10, 18, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.25f));

        if (preset.entries == null || preset.entries.Count == 0)
        {
            EditorGUI.LabelField(rect, "No entries");
            return;
        }

        float sum = preset.SumFractions();
        if (sum <= 0f)
        {
            EditorGUI.LabelField(rect, "Fractions sum to 0");
            return;
        }

        float x = rect.x;
        for (int i = 0; i < preset.entries.Count; i++)
        {
            AsteroidResourceProfileEntry entry = preset.entries[i];
            if (entry == null) continue;

            float w = rect.width * (Mathf.Max(0f, entry.fraction01) / sum);
            if (w <= 0f) continue;

            Color c = GetResourceColor(entry.resourceType);
            EditorGUI.DrawRect(new Rect(x, rect.y, w, rect.height), c);
            x += w;
        }

        // Outline
        Handles.color = new Color(0f, 0f, 0f, 0.6f);
        Handles.DrawAAPolyLine(1f,
            new Vector3(rect.x, rect.y),
            new Vector3(rect.xMax, rect.y),
            new Vector3(rect.xMax, rect.yMax),
            new Vector3(rect.x, rect.yMax),
            new Vector3(rect.x, rect.y)
        );

        EditorGUI.LabelField(rect, $"Fraction Sum: {preset.SumFractions():0.###}", EditorStyles.whiteMiniLabel);
    }

    private static Color GetResourceColor(ResourceEnum resourceType)
    {
        // Best-effort: use Resource SO color if present; otherwise fallback.
        Resource resource = ResourceUtilities.GetResource(resourceType);
        if (resource != null) return resource.color;
        return new Color(0.6f, 0.6f, 0.6f, 1f);
    }

    private void DrawEntriesList()
    {
        EditorGUILayout.LabelField($"Entries ({_entries.arraySize})", EditorStyles.boldLabel);

        for (int i = 0; i < _entries.arraySize; i++)
        {
            SerializedProperty element = _entries.GetArrayElementAtIndex(i);
            SerializedProperty resourceType = element.FindPropertyRelative("resourceType");
            SerializedProperty fraction01 = element.FindPropertyRelative("fraction01");
            SerializedProperty curve = element.FindPropertyRelative("qualityDistributionCurve");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(resourceType);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                _entries.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(fraction01);
            EditorGUILayout.PropertyField(curve);

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Add Entry"))
        {
            int idx = _entries.arraySize;
            _entries.arraySize++;

            SerializedProperty element = _entries.GetArrayElementAtIndex(idx);
            element.FindPropertyRelative("resourceType").enumValueIndex = 0;
            element.FindPropertyRelative("fraction01").floatValue = 1f;

            // Leave curve null; OnValidate will supply a sane default.
            element.FindPropertyRelative("qualityDistributionCurve").animationCurveValue = null;
        }
    }

    private void DrawActions()
    {
        AsteroidResourceProfilePreset preset = (AsteroidResourceProfilePreset)target;

        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Normalize Fractions"))
        {
            Undo.RecordObject(preset, "Normalize Fractions");
            preset.NormalizeFractions();
            EditorUtility.SetDirty(preset);
        }

        if (GUILayout.Button("Even Split"))
        {
            Undo.RecordObject(preset, "Even Split Fractions");
            preset.EvenSplitFractions();
            EditorUtility.SetDirty(preset);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Note: generation is deterministic. If you keep the same seed + same preset, you'll get the same map.",
            MessageType.Info
        );
    }
}
