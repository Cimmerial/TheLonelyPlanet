
using UnityEngine;

/// <summary>
/// Ground vehicle that moves along planet surfaces
/// </summary>
public class Crawler : Vehicle
{
    [Header("Crawler Settings")]
    [SerializeField] private float movementForce = 100f;
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private bool usePlayerInput = true;
    
    [Header("Placement Helpers")]
    [SerializeField] private bool placeOnPlanetOnStart = true;
    [SerializeField] private float hoverHeight = 0.2f; // Height above surface to spawn
    
    private float moveInput = 0f;
    
    protected override void Start()
    {
        base.Start();
        
        if (placeOnPlanetOnStart)
        {
            PlaceOnNearestPlanet();
        }
    }
    
    protected override void UpdateVehiclePhysics()
    {
        if (!usePlayerInput) return;
        
        // Get input
        moveInput = Input.GetAxis("Horizontal");
        
        // Always maintain orientation, but only move when grounded
        MaintainOrientation();
        
        if (atmosphericPhysics != null && atmosphericPhysics.IsGrounded && Mathf.Abs(moveInput) > 0.01f)
        {
            ApplyMovement();
        }
    }
    
    private void ApplyMovement()
    {
        // Get the planet we're on
        Planet planet = atmosphericPhysics.FindNearestPlanet();
        if (planet == null) return;
        
        // Calculate movement direction
        // Right input = clockwise around planet
        // Left input = counterclockwise around planet
        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        Vector2 tangent = new Vector2(-toPlanet.y, toPlanet.x).normalized;
        
        // Apply movement force (moveInput is already -1 to 1)
        Vector2 moveDirection = tangent * moveInput;
        
        // Check speed limit (relative to surface)
        Vector2 relativeVelocity = atmosphericPhysics.GetRelativeVelocity();
        float currentSpeed = Vector2.Dot(relativeVelocity, tangent.normalized);
        
        // Only apply force if under max speed in that direction
        if ((moveInput > 0 && currentSpeed < maxSpeed) || (moveInput < 0 && currentSpeed > -maxSpeed))
        {
            rb.AddForce(moveDirection * movementForce, ForceMode2D.Force);
        }
        
        // Debug visualization
        Debug.DrawRay(transform.position, moveDirection * 0.5f, Color.yellow);
    }
    
    private void MaintainOrientation()
    {
        // Keep crawler upright relative to nearest planet
        Planet planet = atmosphericPhysics.FindNearestPlanet();
        if (planet == null) return;
        
        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        float targetAngle = Mathf.Atan2(toPlanet.y, toPlanet.x) * Mathf.Rad2Deg + 90f;
        
        float currentAngle = rb.rotation;
        float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);
        
        // Strong corrective torque to maintain upright orientation
        float torqueStrength = atmosphericPhysics.IsGrounded ? 200f : 100f;
        float torque = angleDiff * torqueStrength;
        rb.AddTorque(torque, ForceMode2D.Force);
        
        // Strong damping to prevent wobble
        rb.angularVelocity *= 0.7f;
    }
    
    /// <summary>
    /// Place vehicle on the nearest planet surface with correct orientation
    /// </summary>
    [ContextMenu("Place On Nearest Planet")]
    public void PlaceOnNearestPlanet()
    {
        Planet planet = atmosphericPhysics?.FindNearestPlanet();
        if (planet == null)
        {
            Debug.LogWarning("No planet found to place vehicle on");
            return;
        }
        
        // Calculate position just above planet surface
        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        Vector2 directionFromPlanet = -toPlanet.normalized;
        
        float planetRadius = planet.GetRadius();
        Vector2 surfacePosition = (Vector2)planet.transform.position + directionFromPlanet * planetRadius;
        Vector2 spawnPosition = surfacePosition + directionFromPlanet * hoverHeight;
        
        transform.position = spawnPosition;
        
        // Set correct rotation (perpendicular to surface, pointing away from planet)
        float angle = Mathf.Atan2(directionFromPlanet.y, directionFromPlanet.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        
        // Reset velocities
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        
        Debug.Log($"Placed {gameObject.name} on {planet.name} at {spawnPosition}");
    }
    
    /// <summary>
    /// Set movement programmatically (for AI or other control)
    /// </summary>
    public void SetMoveInput(float input)
    {
        moveInput = Mathf.Clamp(input, -1f, 1f);
    }
    
    private void OnDrawGizmos()
    {
        if (atmosphericPhysics == null) return;
        
        // Draw grounded status
        Gizmos.color = atmosphericPhysics.IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
        
        // Draw movement direction when grounded
        if (atmosphericPhysics.IsGrounded)
        {
            Planet planet = atmosphericPhysics.FindNearestPlanet();
            if (planet != null)
            {
                Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
                Vector2 tangent = new Vector2(-toPlanet.y, toPlanet.x).normalized;
                
                // Draw tangent direction (movement direction)
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, (Vector2)transform.position + tangent * 0.5f);
                
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, (Vector2)transform.position - tangent * 0.5f);
                
                // Draw up direction (should point away from planet)
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, (Vector2)transform.position + (Vector2)transform.up * 0.3f);
            }
        }
    }
}