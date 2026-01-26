// Assets/Scripts/Gravity/GravityManager.cs (UPDATED)
using UnityEngine;
using System.Collections.Generic;

public class GravityManager : MonoBehaviour
{
    private static GravityManager instance;
    [SerializeField] private List<IBigGravity> gravitySources = new();

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    public static void Register(IBigGravity source)
    {
        if (instance == null)
        {
            Debug.LogError("GravityManager instance not found! Make sure GravityManager is in the scene.");
            return;
        }
        
        instance.gravitySources.Add(source);
        Debug.Log($"Registered gravity source. Total sources: {instance.gravitySources.Count}");
    }
    
    public static void Unregister(IBigGravity source)
    {
        if (instance == null) return;
        
        instance.gravitySources.Remove(source);
        Debug.Log($"Unregistered gravity source. Total sources: {instance.gravitySources.Count}");
    }
    
    /// <summary>
    /// Calculate gravity at a position, optionally excluding BigGravity objects from affecting each other.
    /// </summary>
    /// <param name="position">Position to calculate gravity at</param>
    /// <param name="affectedObject">The object being affected (optional). If this is a BigGravity object, it won't be affected by other BigGravity objects.</param>
    /// <returns>Total gravity acceleration vector</returns>
    public static Vector2 CalculateGravityAt(Vector2 position, GameObject affectedObject = null)
    {
        if (instance == null) return Vector2.zero;
        
        Vector2 totalForce = Vector2.zero;
        
        // Check if the affected object is a BigGravity object
        bool affectedIsBigGravity = affectedObject != null && affectedObject.GetComponent<IBigGravity>() != null;

        foreach (var source in instance.gravitySources)
        {
            if (source is MonoBehaviour mb)
            {
                // CRITICAL: BigGravity objects don't affect other BigGravity objects
                // This prevents moons from being pulled by planets, planets from affecting each other, etc.
                // They use OrbitalRails instead for their motion.
                if (affectedIsBigGravity)
                {
                    continue;
                }
                
                Vector2 direction = (Vector2)mb.transform.position - position;
                float distance = direction.magnitude;

                // Skip if outside influence radius
                if (distance > source.GetMaxInfluenceRadius()) continue;

                // Avoid division by zero
                if (distance < 0.01f) continue;

                float mass = source.GetMass();
                float force = (Utility.G * mass) / Mathf.Max(distance * distance, 0.01f);

                totalForce += direction.normalized * force;
            }
        }

        return totalForce * Utility.GRAVITY_TIMESCALE;
    }
    
    /// <summary>
    /// Calculate gravity magnitude at a specific distance from a BigGravity source.
    /// Useful for visualization and orbit calculations.
    /// </summary>
    public static float CalculateGravityMagnitudeAt(IBigGravity source, float distance)
    {
        if (source == null || distance < 0.01f) return 0f;
        
        float mass = source.GetMass();
        return (Utility.G * mass) / Mathf.Max(distance * distance, 0.01f) * Utility.GRAVITY_TIMESCALE;
    }
    
    /// <summary>
    /// Get all registered gravity sources (useful for visualization)
    /// </summary>
    public static List<IBigGravity> GetAllGravitySources()
    {
        return instance != null ? new List<IBigGravity>(instance.gravitySources) : new List<IBigGravity>();
    }
}