// Assets/Scripts/Editor/AsteroidGenerationTestRigPlayModeCleanup.cs
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures the edit-only AsteroidGenerationTestRig is removed in Play Mode,
/// without permanently modifying the scene asset.
/// </summary>
[InitializeOnLoad]
public static class AsteroidGenerationTestRigPlayModeCleanup
{
    static AsteroidGenerationTestRigPlayModeCleanup()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // IMPORTANT:
        // - ExitingEditMode is still "edit mode"; destroying here persists and removes the rig from the scene.
        // - EnteredPlayMode runs after Play Mode is active; destroying here affects only the play instance.
        if (state != PlayModeStateChange.EnteredPlayMode) return;

        AsteroidGenerationTestRig[] rigs = Object.FindObjectsOfType<AsteroidGenerationTestRig>(true);
        foreach (AsteroidGenerationTestRig rig in rigs)
        {
            if (rig == null) continue;
            Object.Destroy(rig.gameObject);
        }
    }
}
