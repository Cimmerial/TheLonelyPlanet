using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class Digger : VComponent
{
    [SerializeField] private float digEfficiency = .4f;
    [SerializeField] private float digTime = .5f;
    [SerializeField] private float digForce = 10f;
    [SerializeField] private bool digging = false;

    [SerializeField] private Collider2D diggingCollider;
    [SerializeField] private LayerMask diggableLayers;

    [SerializeField] private float digTimeElapsed = 0f;

    protected override void Awake()
    {
        base.Awake();
        diggingCollider = GetComponent<Collider2D>();
    }

    protected override void Update()
    {
        // base.Update();
        StartUseVComponent();
    }

    protected override void StartUseVComponent() => digging = true;

    protected override void StopUseVComponent() => digging = false;

    protected override void UsingVComponent()
    {
        if (digging)
        {
            digTimeElapsed += Time.deltaTime * digTime;
            if (digTimeElapsed >= digTime)
            {
                digTimeElapsed = 0f;
                PerformDig();
            }
        }
    }

    private void PerformDig()
    {
        // 1. Create a ContactFilter2D to filter by layer
        ContactFilter2D filter = new ();
        filter.SetLayerMask(diggableLayers);
        filter.useLayerMask = true;
        filter.useTriggers = true; // Set to true since the target or this might be triggers

        // 2. Create a list to store the results
        List<Collider2D> results = new List<Collider2D>();

        // 3. Use OverlapCollider to get everything inside the actual TRIANGLE shape
        int hitCount = diggingCollider.OverlapCollider(filter, results);

        // 4. Process the hits
        for (int i = 0; i < hitCount; i++)
        {
            if (results[i].TryGetComponent(out IBreakable breakable))
            {
                breakable.TakeForceDamage( new DealForceData
                {
                    forceAmount = digForce,
                    forceDirection = Vector2.zero,
                    forceReturnEfficiencyPercentage = digEfficiency,
                });
            }
        }
    }
}