// Assets/Scripts/Debug/AsteroidGenerationTestRig.cs
using UnityEngine;

/// <summary>
/// Edit-mode harness for quickly regenerating one or more Asteroids under this GameObject.
/// Intended for iteration/debugging only; should not exist in play mode.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class AsteroidGenerationTestRig : MonoBehaviour
{
    [SerializeField] private bool includeInactiveChildren = true;
    [SerializeField] private bool destroyThisRigOnPlay = true;

    private void OnEnable()
    {
        // Safety net: if this rig somehow makes it into play mode, remove it.
        if (Application.isPlaying && destroyThisRigOnPlay)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Regenerates all Asteroid components under this GameObject without re-parenting under the global ASTEROIDS container.
    /// Note: if your asteroids keep the same seed, the output will look identical (deterministic).
    /// </summary>
    public void GenerateAll()
    {
        if (Application.isPlaying) return;

        Asteroid[] asteroids = GetComponentsInChildren<Asteroid>(includeInactiveChildren);
        AsteroidGenerator generator = new();

        foreach (Asteroid asteroid in asteroids)
        {
            if (asteroid == null) continue;

            // In edit-mode test rig generation we do NOT want to create or use a global container.
            generator.GenerateAsteroid(asteroid, parentOverride: null, ensureAsteroidsContainer: false);
        }
    }

    /// <summary>
    /// Convenience: re-roll each asteroid seed, then regenerate.
    /// </summary>
    public void RandomizeSeedsAndGenerateAll()
    {
        if (Application.isPlaying) return;

        Asteroid[] asteroids = GetComponentsInChildren<Asteroid>(includeInactiveChildren);
        foreach (Asteroid asteroid in asteroids)
        {
            if (asteroid == null) continue;
            asteroid.RandomizeSeed();
        }

        GenerateAll();
    }

    [ContextMenu("Generate All (Edit Mode)")]
    private void ContextMenuGenerateAll() => GenerateAll();

    [ContextMenu("Randomize Seeds + Generate All (Edit Mode)")]
    private void ContextMenuRandomizeSeedsAndGenerateAll() => RandomizeSeedsAndGenerateAll();
}
