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
    public static Vector2 CalculateGravityAt(Vector2 position)
    {
        Vector2 totalForce = Vector2.zero;

        foreach (var source in instance.gravitySources)
        {
            if (source is MonoBehaviour mb)
            {
                Vector2 direction = (Vector2)mb.transform.position - position;
                float distance = direction.magnitude;

                if (distance > source.GetMaxInfluenceRadius()) continue;

                if (distance < 0.01f) continue;

                float mass = source.GetMass();
                float force = (Utility.G * mass) / Mathf.Max(distance * distance, 0.01f);

                totalForce += direction.normalized * force;
            }
        }

        return totalForce * Utility.GRAVITY_TIMESCALE;
    }
}