// Assets/Scripts/Editor/AsteroidGenerationTestRigEditor.cs
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(AsteroidGenerationTestRig))]
public class AsteroidGenerationTestRigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        AsteroidGenerationTestRig rig = (AsteroidGenerationTestRig)target;

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Generate All (Edit Mode)"))
            {
                rig.GenerateAll();

                // Persist hierarchy changes.
                EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            }

            if (GUILayout.Button("Randomize Seeds + Generate All (Edit Mode)"))
            {
                rig.RandomizeSeedsAndGenerateAll();

                // Persist hierarchy changes.
                EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            }
        }
    }
}
