// Assets/Scripts/Physics/AtmosphericPhysics.cs
using UnityEngine;

/// <summary>
/// Reusable component that handles atmospheric physics for any object.
/// Eliminates code duplication between Asteroid, Vehicle, and other atmospheric objects.
/// </summary>
public class AtmosphericPhysics : MonoBehaviour
{
    [Header("Atmospheric Settings")]
    [SerializeField] private float atmosphereDragDelay = 10f; // Seconds before drag kicks in
    [SerializeField] private float atmosphericDragStrength = 10f;
    [SerializeField] private float surfaceDragStrength = 50f;
    
    [Header("Grounding Settings")]
    [SerializeField] private int groundedFrameThreshold = 5; // Increased from 3 to 5 for stability
    
    [Header("Tidal Locking (for grounded objects)")]
    [SerializeField] private bool enableTidalLocking = false; // Disabled by default - vehicles control their own orientation
    [SerializeField] private float tidalLockingTorque = 100f;
    
    // Private state
    private Rigidbody2D rb;
    private float atmosphereEntryTime = -1f;
    private int groundedFrames = 0;
    private Planet cachedNearestPlanet;
    private float lastPlanetSearchTime;
    private const float PLANET_SEARCH_INTERVAL = 0.5f; // Cache planet search
    
    public bool IsGrounded => groundedFrames > 0;
    public bool IsInAtmosphere => CheckIfInAtmosphere();
    
    private void Awake()
    {
        // Don't require RB immediately - it might be added later (e.g., during generation)
        rb = GetComponent<Rigidbody2D>();
    }
    
    private void Start()
    {
        // Verify RB exists after initialization
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                Debug.LogError($"[{gameObject.name}] AtmosphericPhysics requires a Rigidbody2D!");
            }
        }
    }
    
    /// <summary>
    /// Get the velocity relative to the atmosphere (or surface if grounded)
    /// </summary>
    public Vector2 GetRelativeVelocity()
    {
        if (rb == null) return Vector2.zero;
        
        Vector2 myVelocity = rb.velocity;
        Planet nearestPlanet = FindNearestPlanet();
        
        if (nearestPlanet != null && CheckIfInAtmosphereOf(nearestPlanet))
        {
            Vector2 atmosphericVel = CalculateAtmosphericVelocityFor(nearestPlanet);
            myVelocity -= atmosphericVel;
        }
        
        return myVelocity;
    }
    
    /// <summary>
    /// Apply atmospheric physics in FixedUpdate
    /// </summary>
    public void ApplyAtmosphericPhysics(bool applyGravity = true)
    {
        if (rb == null) return;
        
        // Don't apply physics to kinematic bodies - they handle their own movement
        if (rb.bodyType == RigidbodyType2D.Kinematic)
        {
            return;
        }
        
        Planet nearestPlanet = FindNearestPlanet();
        bool isInAtmosphere = nearestPlanet != null && CheckIfInAtmosphereOf(nearestPlanet);
        bool grounded = IsGrounded;
        
        // Track atmosphere entry time
        if (isInAtmosphere && atmosphereEntryTime < 0f)
        {
            atmosphereEntryTime = Time.time;
        }
        else if (!isInAtmosphere)
        {
            atmosphereEntryTime = -1f;
        }
        
        float timeInAtmosphere = isInAtmosphere ? (Time.time - atmosphereEntryTime) : 0f;
        float dragRampUp = grounded ? 1f : Mathf.Clamp01(timeInAtmosphere / atmosphereDragDelay);
        
        if (isInAtmosphere && nearestPlanet != null)
        {
            Vector2 targetAtmosphericVelocity = CalculateAtmosphericVelocityFor(nearestPlanet);
            
            if (grounded)
            {
                // GROUNDED: Strong coupling to surface
                Vector2 velocityDifference = targetAtmosphericVelocity - rb.velocity;
                rb.AddForce(velocityDifference * surfaceDragStrength * rb.mass, ForceMode2D.Force);
                
                // Tidal locking
                if (enableTidalLocking)
                {
                    ApplyTidalLocking(nearestPlanet);
                }
                
                // Strong damping to settle and prevent bouncing
                if (rb.velocity.magnitude < 1f)
                {
                    rb.velocity *= 0.95f;
                }
                rb.angularVelocity *= 0.9f;
            }
            else
            {
                // IN ATMOSPHERE: Gradual drag ramp-up
                Vector2 velocityDifference = targetAtmosphericVelocity - rb.velocity;
                float effectiveDrag = atmosphericDragStrength * dragRampUp;
                rb.AddForce(velocityDifference * effectiveDrag * rb.mass, ForceMode2D.Force);
                
                // Apply gravity
                if (applyGravity)
                {
                    Vector2 gravity = GravityManager.CalculateGravityAt(transform.position);
                    rb.AddForce(gravity * rb.mass, ForceMode2D.Force);
                }
            }
        }
        else if (applyGravity)
        {
            // IN SPACE: Only gravity
            Vector2 gravity = GravityManager.CalculateGravityAt(transform.position);
            rb.AddForce(gravity * rb.mass, ForceMode2D.Force);
        }
    }
    
    /// <summary>
    /// Calculate atmospheric velocity at this position for a given planet
    /// </summary>
    public Vector2 CalculateAtmosphericVelocityFor(Planet planet)
    {
        if (planet == null) return Vector2.zero;
        
        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        float distance = toPlanet.magnitude;
        float atmosphereRadius = planet.GetAtmosphereRadius();
        
        if (distance > atmosphereRadius) return Vector2.zero;
        
        // Tangential velocity
        Vector2 tangentialDir = new Vector2(toPlanet.y, -toPlanet.x).normalized;
        float angularVel = planet.GetAngularVelocity();
        float tangentialSpeed = angularVel * distance;
        
        return tangentialDir * tangentialSpeed;
    }
    
    /// <summary>
    /// Find the nearest planet (cached for performance)
    /// </summary>
    public Planet FindNearestPlanet()
    {
        // Cache planet search to avoid constant FindObjectsOfType
        if (Time.time - lastPlanetSearchTime < PLANET_SEARCH_INTERVAL && cachedNearestPlanet != null)
        {
            return cachedNearestPlanet;
        }
        
        lastPlanetSearchTime = Time.time;
        
        Planet[] planets = FindObjectsOfType<Planet>();
        Planet nearest = null;
        float minDist = float.MaxValue;
        
        foreach (var planet in planets)
        {
            float dist = Vector2.Distance(transform.position, planet.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = planet;
            }
        }
        
        cachedNearestPlanet = nearest;
        return nearest;
    }
    
    /// <summary>
    /// Check if this object is in a planet's atmosphere
    /// </summary>
    public bool CheckIfInAtmosphereOf(Planet planet)
    {
        if (planet == null) return false;
        
        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        float distance = toPlanet.magnitude;
        float atmosphereRadius = planet.GetAtmosphereRadius();
        
        return distance <= atmosphereRadius;
    }
    
    /// <summary>
    /// Check if in any planet's atmosphere
    /// </summary>
    public bool CheckIfInAtmosphere()
    {
        Planet nearestPlanet = FindNearestPlanet();
        return CheckIfInAtmosphereOf(nearestPlanet);
    }
    
    /// <summary>
    /// Apply tidal locking torque to face planet
    /// </summary>
    private void ApplyTidalLocking(Planet planet)
    {
        if (planet == null) return;
        
        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        float angleToCenter = Mathf.Atan2(toPlanet.y, toPlanet.x) * Mathf.Rad2Deg;
        float targetRotation = angleToCenter + 90f;
        
        float currentRotation = rb.rotation;
        float rotationDifference = Mathf.DeltaAngle(currentRotation, targetRotation);
        
        float torque = rotationDifference * tidalLockingTorque;
        rb.AddTorque(torque, ForceMode2D.Force);
        
        // Damping when close
        if (Mathf.Abs(rotationDifference) < 10f)
        {
            rb.angularVelocity *= 0.7f;
        }
    }
    
    /// <summary>
    /// Call this from OnCollisionEnter2D when hitting a planet
    /// </summary>
    public void OnPlanetCollisionEnter()
    {
        groundedFrames = groundedFrameThreshold;
        Debug.Log($"[{gameObject.name}] GROUNDED - frames={groundedFrames}"); // ED
        // Debug.Log($"[{gameObject.name}] OnPlanetCollisionEnter - groundedFrames set to {groundedFrames}");
    }
    
    /// <summary>
    /// Call this from OnCollisionStay2D when staying on a planet
    /// </summary>
    public void OnPlanetCollisionStay()
    {
        groundedFrames = groundedFrameThreshold;
        // Commented out to reduce spam: Debug.Log($"[{gameObject.name}] OnPlanetCollisionStay - groundedFrames refreshed");
    }
    
    /// <summary>
    /// Call this from OnCollisionExit2D when leaving a planet
    /// </summary>
    public void OnPlanetCollisionExit()
    {
        groundedFrames = 0;
        Debug.Log($"[{gameObject.name}] UNGROUNDED"); // EDITED
        // Debug.Log($"[{gameObject.name}] OnPlanetCollisionExit - groundedFrames set to 0");
        
        // Restore some tumbling when leaving surface
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
        {
            rb.angularVelocity = Random.Range(-30f, 30f);
        }
    }
    
    /// <summary>
    /// Decay grounded frames each LateUpdate
    /// </summary>
    public void DecayGroundedFrames()
    {
        if (groundedFrames > 0)
        {
            groundedFrames--;
        }
    }
}