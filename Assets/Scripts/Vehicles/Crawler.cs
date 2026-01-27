// Assets/Scripts/Vehicles/Crawler.cs
using UnityEngine;

/// <summary>
/// Ground vehicle that moves along planet surfaces
/// Uses Dynamic rigidbody like asteroids - no kinematic switching
/// </summary>
public class Crawler : Vehicle
{
    [Header("Crawler Settings")]
    [SerializeField] private float movementSpeed = 2f;
    [SerializeField] private bool usePlayerInput = true;
    [Header("Grounding Detection")]
    [SerializeField] private float groundCheckDistance = 2f;

    private float moveInput = 0f;

    protected override void Start()
    {
        base.Start();
    }

    protected override void UpdateVehiclePhysics()
    {
        if (!usePlayerInput) return;

        moveInput = Input.GetAxis("Horizontal");

        // EDITED: Use raycast-based grounding check
        bool isGrounded = CheckGroundedSimple(); // EDITED

        // Only move when grounded
        if (isGrounded && Mathf.Abs(moveInput) > 0.01f) ApplyDynamicMovement();
    }

    // ADDED: Simple raycast-based grounding check
    private bool CheckGroundedSimple()
    {
        Planet planet = atmosphericPhysics?.FindNearestPlanet();
        if (planet == null) return false;

        // Calculate distance from planet surface
        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        float distanceFromCenter = toPlanet.magnitude;
        float planetRadius = planet.GetRadius();
        float distanceFromSurface = distanceFromCenter - planetRadius;

        // Convert pixel threshold to world units
        float groundCheckWorld = groundCheckDistance / Utility.GLOBAL_PPU;

        bool grounded = distanceFromSurface <= groundCheckWorld;

        // Debug visualization
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

        // Calculate movement direction (tangent to planet surface)
        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        Vector2 tangent = new Vector2(-toPlanet.y, toPlanet.x).normalized;

        // Flip sprite based on movement direction
        if (vehicleSpriteRenderer != null)
        {
            if (moveInput > 0.01f)
            {
                vehicleSpriteRenderer.flipX = false;
            }
            else if (moveInput < -0.01f)
            {
                vehicleSpriteRenderer.flipX = true;
            }
        }

        // Apply force along the surface tangent (amplify/diminish planet rotation)
        Vector2 moveForce = tangent * moveInput * movementSpeed * rb.mass;
        rb.AddForce(moveForce, ForceMode2D.Force);

        // Debug.Log($"[{gameObject.name}] Applied force: {moveForce}, tangent: {tangent}, moveInput: {moveInput}");

        // Debug visualization
        Debug.DrawRay(transform.position, tangent * moveInput * 0.5f, Color.yellow);
    }



    public void SetMoveInput(float input)
    {
        moveInput = Mathf.Clamp(input, -1f, 1f);
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);
        // check building/resource collisions for pickup/dropoff
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
                      $"MoveInput: {moveInput}");
        }
    }

    private void OnDrawGizmos()
    {
        // Draw grounded status
        if (atmosphericPhysics != null)
        {
            Gizmos.color = atmosphericPhysics.IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.15f);
        }

        // Draw movement direction when grounded
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