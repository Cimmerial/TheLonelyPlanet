// Assets/Scripts/Vehicles/Components/Digger.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class Digger : VComponent
{
    [Header("Digger Settings")]
    [SerializeField] private float digEfficiency = 0.4f;
    [SerializeField] private float digTime = 0.5f;
    [SerializeField] private float digForce = 10f;
    
    [SerializeField] private Collider2D diggingCollider;
    [SerializeField] private LayerMask diggableLayers;
    
    private float digTimeElapsed = 0f;

    protected override void Awake()
    {
        base.Awake();
        
        // Set defaults only if not already set
        if (string.IsNullOrEmpty(componentName))
        {
            componentName = "Digger";
        }
        if (componentType == 0) // Default enum value
        {
            componentType = ComponentSlotType.Primary;
        }
        
        // Get or create the ComponentRenderer
        Transform rendererTransform = transform.Find("ComponentRenderer");
        if (rendererTransform != null)
        {
            componentRenderer = rendererTransform.GetComponent<SpriteRenderer>();
        }
        
        diggingCollider = GetComponent<Collider2D>();
        if (diggingCollider == null)
        {
            // Create a default trigger collider for digging
            PolygonCollider2D polygonCollider = gameObject.AddComponent<PolygonCollider2D>();
            polygonCollider.isTrigger = true;
            
            // Triangle shape pointing in direction (0, 1) by default
            Vector2[] points = new Vector2[]
            {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(0f, 0.5f)
            };
            polygonCollider.points = points;
            diggingCollider = polygonCollider;
        }

        // Ensure it's a trigger for digging interactions
        diggingCollider.isTrigger = true;

        // Default to everything if no layer selected
        if (diggableLayers.value == 0)
        {
            diggableLayers = ~0; // Everything
        }
    }

    protected override void StartUseVComponent()
    {
        base.StartUseVComponent();
        digTimeElapsed = 0f;
        Debug.Log($"[{componentName}] Started digging");
    }

    protected override void StopUseVComponent()
    {
        base.StopUseVComponent();
        Debug.Log($"[{componentName}] Stopped digging");
    }

    protected override void UsingVComponent()
    {
        digTimeElapsed += Time.deltaTime;
        
        if (digTimeElapsed >= digTime)
        {
            digTimeElapsed = 0f;
            PerformDig();
        }
    }

    private void PerformDig()
    {
        // Create a ContactFilter2D to filter by layer
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(diggableLayers);
        filter.useLayerMask = true;
        filter.useTriggers = true;

        // Create a list to store the results
        List<Collider2D> results = new List<Collider2D>();

        // Use OverlapCollider to get everything inside the digging area
        int hitCount = diggingCollider.OverlapCollider(filter, results);

        Debug.Log($"[{componentName}] PerformDig: Layers={diggableLayers.value}, Trigger={diggingCollider.isTrigger}, Hits={hitCount}");

        // Process the hits
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = results[i];
            IBreakable breakable = col.GetComponent<IBreakable>();
            if (breakable == null)
            {
                breakable = col.GetComponentInParent<IBreakable>();
            }

            Debug.Log($"[{componentName}] Hit {i}: {col.name} (Parent: {col.transform.parent?.name}), Found Breakable: {breakable != null}");

            if (breakable != null)
            {
                BrokenResourceData brokenData = breakable.TakeForceDamage(new DealForceData
                {
                    forceAmount = digForce,
                    forceDirection = Vector2.zero,
                    forceReturnEfficiencyPercentage = digEfficiency,
                });

                // TODO: Collect broken resources
                if (brokenData != null && brokenData.brokenResources != null)
                {
                    Debug.Log($"[{componentName}] Collected {brokenData.brokenResources.Count} resources");
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (diggingCollider != null)
        {
            // Draw the digging area
            Gizmos.color = isActive ? Color.red : Color.yellow;
            
            if (diggingCollider is PolygonCollider2D polyCollider)
            {
                Vector2[] points = polyCollider.points;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 worldStart = transform.TransformPoint(points[i]);
                    Vector2 worldEnd = transform.TransformPoint(points[(i + 1) % points.Length]);
                    Gizmos.DrawLine(worldStart, worldEnd);
                }
            }
        }
    }
}