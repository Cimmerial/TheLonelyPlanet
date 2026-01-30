// Assets/Scripts/Vehicles/Crawler.cs
using UnityEngine;
using System.Collections.Generic;

public class Crawler : Vehicle
{
    [Header("Crawler Settings")]
    [SerializeField] private float movementSpeed = 2f;
    [SerializeField] private bool usePlayerInput = true;
    
    [Header("Grounding Detection")]
    [SerializeField] private float groundCheckDistance = 2f;

    private float moveInput = 0f;
    private bool lastFlipState = false; // Track sprite flip state

    protected override void Start()
    {
        base.Start();
    }

    protected override void UpdateVehiclePhysics()
    {
        if (IsResettingToZero) return;
        if (!usePlayerInput) return;
        if (!IsPlayerControlled) return;

        moveInput = Input.GetAxis("Horizontal");

        bool isGrounded = CheckGroundedSimple();

        if (isGrounded && Mathf.Abs(moveInput) > 0.01f) 
        {
            ApplyDynamicMovement();
        }
    }

    private bool CheckGroundedSimple()
    {
        Planet planet = atmosphericPhysics?.FindNearestPlanet();
        if (planet == null) return false;

        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        float distanceFromCenter = toPlanet.magnitude;
        float planetRadius = planet.GetRadius();
        float distanceFromSurface = distanceFromCenter - planetRadius;

        float groundCheckWorld = groundCheckDistance / Utility.GLOBAL_PPU;

        bool grounded = distanceFromSurface <= groundCheckWorld;

        if (grounded)
        {
            Debug.DrawLine(transform.position, planet.transform.position, Color.green);
        }

        return grounded;
    }

    private void ApplyDynamicMovement()
    {
        Planet planet = atmosphericPhysics.FindNearestPlanet();
        if (planet == null)
        {
            Debug.LogWarning($"[{gameObject.name}] No planet found for movement!");
            return;
        }

        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        Vector2 tangent = new Vector2(-toPlanet.y, toPlanet.x).normalized;

        bool shouldFlip = false;
        if (vehicleSpriteRenderer != null)
        {
            if (moveInput > 0.01f)
            {
                vehicleSpriteRenderer.flipX = false;
                shouldFlip = false;
            }
            else if (moveInput < -0.01f)
            {
                vehicleSpriteRenderer.flipX = true;
                shouldFlip = true;
            }
            else
            {
                shouldFlip = vehicleSpriteRenderer.flipX;
            }
            
            // If flip state changed, mirror components
            if (shouldFlip != lastFlipState)
            {
                MirrorComponents(shouldFlip);
                lastFlipState = shouldFlip;
            }
        }

        Vector2 moveForce = tangent * moveInput * movementSpeed * rb.mass;
        rb.AddForce(moveForce, ForceMode2D.Force);

        Debug.DrawRay(transform.position, tangent * moveInput * 0.5f, Color.yellow);
    }
    
    private Dictionary<Transform, Vector3> initialSlotPositions = new Dictionary<Transform, Vector3>();
    private bool initializedSlots = false;

    private void InitializeSlotCache()
    {
        if (componentsParent == null || initializedSlots) return;

        initialSlotPositions.Clear();
        foreach (Transform slot in componentsParent)
        {
            initialSlotPositions[slot] = slot.localPosition;
        }
        initializedSlots = true;
    }

    private void MirrorComponents(bool flipped)
    {
        if (componentsParent == null) return;
        
        // Ensure cache is initialized
        if (!initializedSlots) InitializeSlotCache();
        
        // Mirror all component slots horizontally
        foreach (Transform slot in componentsParent)
        {
            if (!initialSlotPositions.ContainsKey(slot)) continue;

            Vector3 originalPos = initialSlotPositions[slot];
            
            float targetX = flipped ? -originalPos.x : originalPos.x;
            
            // Apply new position
            // Force Z to be -0.1f to ensure visibility over chassis (closer to camera)
            slot.localPosition = new Vector3(targetX, originalPos.y, -0.1f);
            
            // Rotate the slot itself to handle directionality
            if (flipped)
            {
                // Rotate 180 degrees around Y axis to mirror
                slot.localRotation = Quaternion.Euler(0, 180, 0);
            }
            else
            {
                // Restore identity rotation
                slot.localRotation = Quaternion.identity;
            }
            
            // We no longer need to flip the sprite renderer manually as the parent rotation handles it
        }
    }

    public void SetMoveInput(float input)
    {
        moveInput = Mathf.Clamp(input, -1f, 1f);
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);
    }

    private void Update()
    {
        // Debug info
        if (Input.GetKeyDown(KeyCode.G))
        {
            Planet planet = atmosphericPhysics?.FindNearestPlanet();
            float currentDist = planet != null ?
                Vector2.Distance(transform.position, planet.transform.position) : -1f;

            Debug.Log($"[{gameObject.name}] " +
                      $"Grounded: {atmosphericPhysics?.IsGrounded}, " +
                      $"RB Type: {rb?.bodyType}, " +
                      $"Velocity: {rb?.velocity.magnitude:F2}, " +
                      $"AngularVel: {rb?.angularVelocity:F2}, " +
                      $"Mass: {rb?.mass:F2}, " +
                      $"Drag: {rb?.drag:F2}, " +
                      $"Distance: {currentDist:F3}, " +
                      $"MoveInput: {moveInput}, " +
                      $"Components: P{primarySlots.Count}/S{secondarySlots.Count}/Sp{specializedSlots.Count}");
        }
    }

    private void OnDrawGizmos()
    {
        if (atmosphericPhysics != null)
        {
            Gizmos.color = atmosphericPhysics.IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.15f);
        }

        Planet planet = atmosphericPhysics?.FindNearestPlanet();
        if (planet != null && atmosphericPhysics != null && atmosphericPhysics.IsGrounded)
        {
            Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
            Vector2 tangent = new Vector2(-toPlanet.y, toPlanet.x).normalized;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + tangent * 0.5f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, (Vector2)transform.position - tangent * 0.5f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + (Vector2)transform.up * 0.3f);
        }
    }
}