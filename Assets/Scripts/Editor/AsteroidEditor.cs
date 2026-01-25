// Assets/Scripts/Editor/AsteroidEditor.cs
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Asteroid))]
public class AsteroidEditor : Editor
{
    private float testForce = 10f;
    private Vector2 testDirection = Vector2.right;
    private bool useDirection = false;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Asteroid asteroid = (Asteroid)target;

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Fragmentation Testing", EditorStyles.boldLabel);
        
        testForce = EditorGUILayout.FloatField("Test Force (N)", testForce);
        useDirection = EditorGUILayout.Toggle("Use Direction", useDirection);
        
        if (useDirection)
        {
            testDirection = EditorGUILayout.Vector2Field("Direction", testDirection);
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Deal Force", GUILayout.Height(30)))
        {
            if (Application.isPlaying)
            {
                if (useDirection)
                {
                    asteroid.DealForce(testForce, testDirection);
                }
                else
                {
                    asteroid.DealForce(testForce);
                }
            }
            else
            {
                Debug.LogWarning("Enter Play Mode to test fragmentation!");
            }
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Click 'Deal Force' in Play Mode to test asteroid fragmentation.\n\n" +
            "• With Direction: Fragments blast in a cone\n" +
            "• Without Direction: Fragments explode radially",
            MessageType.Info
        );
    }
}
#endif