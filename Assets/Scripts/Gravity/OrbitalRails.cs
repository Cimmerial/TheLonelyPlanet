// Assets/Scripts/Gravity/OrbitalRails.cs
using UnityEngine;

/// <summary>
/// Maintains objects on stable orbital paths around a target.
/// Applies corrective forces to keep object on predetermined orbit.
/// Can break orbit if external forces exceed threshold.
/// </summary>
public class OrbitalRails : MonoBehaviour
{
    [Header("Orbital Parameters")]
    [Tooltip("The object to orbit around (Planet or Moon)")]
    public Transform orbitTarget;
    
    [Tooltip("Distance from the target's SURFACE (not center) in pixels")]
    public float orbitalRadiusFromSurface = 100f;
    
    [Tooltip("If true, calculates realistic orbital speed based on target mass. If false, uses custom orbitalSpeed.")]
    public bool useRealisticSpeed = true;
    
    [Tooltip("Orbital speed in degrees/second (360 = 1 orbit per second, -360 = reverse). Only used if useRealisticSpeed is false.")]
    public float orbitalSpeed = 360f;
    
    [Tooltip("Starting angle in degrees (0 = right, 90 = up, etc.)")]
    public float startingAngle = 0f;
    
    [Header("Stability Settings")]
    [Tooltip("Force (in Newtons) required to break orbit. Set to Infinity for unbreakable orbits (moons)")]
    public float forceThreshold = Mathf.Infinity;
    
    [Tooltip("If true, object will re-rail when external forces drop below reRailThreshold")]
    public bool autoReRail = false;
    
    [Tooltip("External force must drop below this to re-rail")]
    public float reRailThreshold = 10f;
    
    [Tooltip("Strength of corrective forces to maintain orbit")]
    public float correctionStrength = 100f;
    
    [Tooltip("How quickly to lerp velocity to ideal orbital velocity (0-1)")]
    [Range(0f, 1f)]
    public float velocityLerpSpeed = 0.1f;
    
    [Tooltip("If true, orbital rails act as strict constraint (ignores external physics). If false, applies corrective forces but allows external forces to affect object.")]
    public bool useStrictRails = true;
    
    [Header("Runtime State (Read Only)")]
    [SerializeField] private bool isRailed = true;
    [SerializeField] private float currentAngle = 0f;
    [SerializeField] private float externalForceThisFrame = 0f;
    
    private Rigidbody2D rb;
    private Planet targetPlanet;
    private Moon targetMoon;
    private float targetRadius; // Actual radius of target body
    private Vector2 lastFrameVelocity;
    private Vector2 totalExternalForce; // Cumulative external force
    
    void Awake()
    {
        currentAngle = startingAngle;
        // Don't check for RB here - it might be added during generation
    }
    
    void OnEnable()
    {
        // Check for RB when enabled (after generation is complete)
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
    }
    
    void Start()
    {
        if (orbitTarget == null)
        {
            Debug.LogError($"[{gameObject.name}] OrbitalRails has no orbit target assigned!");
            enabled = false;
            return;
        }
        
        // Final check for RB
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                Debug.LogError($"[{gameObject.name}] OrbitalRails requires a Rigidbody2D component!");
                enabled = false;
                return;
            }
        }
        
        // Cache target planet or moon and get its radius
        targetPlanet = orbitTarget.GetComponent<Planet>();
        targetMoon = orbitTarget.GetComponent<Moon>();
        
        IBigGravity bigGrav = null;
        if (targetPlanet != null)
        {
            targetRadius = targetPlanet.Radius / Utility.GLOBAL_PPU;
            bigGrav = targetPlanet;
        }
        else if (targetMoon != null)
        {
            targetRadius = targetMoon.Radius / Utility.GLOBAL_PPU;
            bigGrav = targetMoon;
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] Orbit target must have Planet or Moon component!");
            enabled = false;
            return;
        }
        
        // Calculate realistic orbital speed if enabled
        if (useRealisticSpeed && bigGrav != null)
        {
            float totalRadius = GetTotalRadius();
            float orbitalVelocity = Utility.CalculateOrbitalVelocity(bigGrav.GetMass(), totalRadius);
            
            // Convert linear velocity to angular velocity (deg/s)
            // v = ω * r → ω = v / r (in rad/s) → degrees/s = (v / r) * (180/π)
            float angularVelRadians = orbitalVelocity / totalRadius;
            orbitalSpeed = angularVelRadians * Mathf.Rad2Deg;
            
            Debug.Log($"[{gameObject.name}] Realistic orbital speed calculated: {orbitalSpeed:F2} deg/s " +
                      $"(linear velocity: {orbitalVelocity:F2} u/s at radius {totalRadius:F2})");
        }
        
        // Position object at starting orbital position
        PositionAtAngle(currentAngle);
        
        Debug.Log($"[{gameObject.name}] OrbitalRails initialized - Target: {orbitTarget.name}, " +
                  $"Radius: {GetTotalRadius():F2}, Speed: {orbitalSpeed:F2} deg/s, " +
                  $"Threshold: {(forceThreshold == Mathf.Infinity ? "INFINITY" : forceThreshold.ToString("F2"))}N");
    }
    
    void FixedUpdate()
    {
        if (orbitTarget == null || rb == null) return;
        
        // If not railed, just calculate external forces for monitoring and potential re-rail
        if (!isRailed)
        {
            // Calculate what external forces would be
            Vector2 gravity = GravityManager.CalculateGravityAt(rb.position, gameObject);
            externalForceThisFrame = gravity.magnitude * rb.mass;
            
            if (autoReRail && externalForceThisFrame < reRailThreshold)
            {
                ReRail();
            }
            return;
        }
        
        // === ON RAILS ===
        
        if (useStrictRails)
        {
            // STRICT MODE: Kinematic-like behavior, completely ignores physics
            // Just set position and velocity directly
            currentAngle += orbitalSpeed * Time.fixedDeltaTime;
            if (currentAngle > 360f) currentAngle -= 360f;
            if (currentAngle < 0f) currentAngle += 360f;
            
            Vector2 idealPosition = CalculateIdealPosition();
            Vector2 idealVelocity = CalculateIdealVelocity();
            
            rb.position = idealPosition;
            rb.velocity = idealVelocity;
            
            // No external forces tracked - strict rails never break
            externalForceThisFrame = 0f;
        }
        else
        {
            // SOFT MODE: Apply corrective forces but allow external forces to perturb orbit
            currentAngle += orbitalSpeed * Time.fixedDeltaTime;
            if (currentAngle > 360f) currentAngle -= 360f;
            if (currentAngle < 0f) currentAngle += 360f;
            
            Vector2 idealPosition = CalculateIdealPosition();
            Vector2 idealVelocity = CalculateIdealVelocity();
            
            // Calculate all external forces (gravity from all sources)
            Vector2 externalGravity = GravityManager.CalculateGravityAt(rb.position, gameObject);
            Vector2 externalGravityForce = externalGravity * rb.mass;
            
            // Calculate what the orbital force should be (centripetal force to our target only)
            IBigGravity targetGravity = targetPlanet != null ? (IBigGravity)targetPlanet : (IBigGravity)targetMoon;
            Vector2 toTarget = (Vector2)orbitTarget.position - rb.position;
            float distance = toTarget.magnitude;
            float targetGravityMag = (Utility.G * targetGravity.GetMass()) / Mathf.Max(distance * distance, 0.01f) * Utility.GRAVITY_TIMESCALE;
            Vector2 targetGravityForce = toTarget.normalized * targetGravityMag * rb.mass;
            
            // External force = total gravity - target gravity (i.e., moon's gravity, other planets, etc.)
            Vector2 externalForce = externalGravityForce - targetGravityForce;
            externalForceThisFrame = externalForce.magnitude;
            
            // Apply position correction
            Vector2 positionError = idealPosition - rb.position;
            if (positionError.magnitude > 0.01f)
            {
                Vector2 correctionForce = positionError * correctionStrength * rb.mass;
                rb.AddForce(correctionForce, ForceMode2D.Force);
            }
            
            // Lerp velocity toward ideal
            rb.velocity = Vector2.Lerp(rb.velocity, idealVelocity, velocityLerpSpeed);
            
            // Check if external force exceeds threshold
            if (externalForceThisFrame > forceThreshold)
            {
                BreakOrbit();
            }
        }
    }
    
    /// <summary>
    /// Calculate the ideal position on the orbital path
    /// </summary>
    public Vector2 CalculateIdealPosition()
    {
        if (orbitTarget == null) return rb.position;
        
        float totalRadius = GetTotalRadius();
        Vector2 offset = new Vector2(
            Mathf.Cos(currentAngle * Mathf.Deg2Rad),
            Mathf.Sin(currentAngle * Mathf.Deg2Rad)
        ) * totalRadius;
        
        return (Vector2)orbitTarget.position + offset;
    }
    
    /// <summary>
    /// Calculate the ideal velocity (tangential orbital velocity + parent velocity)
    /// </summary>
    public Vector2 CalculateIdealVelocity()
    {
        if (orbitTarget == null) return Vector2.zero;
        
        float totalRadius = GetTotalRadius();
        
        // Calculate current offset from center
        Vector2 offset = (Vector2)transform.position - (Vector2)orbitTarget.position;
        
        // Tangential direction (perpendicular to radius)
        Vector2 tangentDir = new Vector2(-offset.y, offset.x).normalized;
        
        // Orbital velocity = angular velocity (rad/s) * radius
        float angularVelRadians = orbitalSpeed * Mathf.Deg2Rad;
        float orbitalVel = angularVelRadians * totalRadius;
        Vector2 orbitalVelocity = tangentDir * orbitalVel;
        
        // Add parent's velocity if it's moving
        Vector2 parentVelocity = Vector2.zero;
        if (orbitTarget.TryGetComponent<Rigidbody2D>(out var parentRB))
        {
            parentVelocity = parentRB.velocity;
        }
        
        return orbitalVelocity + parentVelocity;
    }
    
    /// <summary>
    /// Get total orbital radius (target radius + orbital radius from surface) in world units
    /// </summary>
    public float GetTotalRadius()
    {
        float orbitalRadiusInUnits = orbitalRadiusFromSurface / Utility.GLOBAL_PPU;
        return targetRadius + orbitalRadiusInUnits;
    }
    
    /// <summary>
    /// Position the object at a specific angle on the orbit
    /// </summary>
    public void PositionAtAngle(float angle)
    {
        if (orbitTarget == null) return;
        
        float totalRadius = GetTotalRadius();
        Vector2 offset = new Vector2(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad)
        ) * totalRadius;
        
        transform.position = orbitTarget.position + (Vector3)offset;
        currentAngle = angle;
    }
    
    /// <summary>
    /// Break out of orbital rails - object becomes fully physics-driven
    /// </summary>
    public void BreakOrbit()
    {
        if (!isRailed) return;
        
        isRailed = false;
        Debug.Log($"[{gameObject.name}] BROKE ORBIT! External force: {externalForceThisFrame:F2}N exceeded threshold: {forceThreshold:F2}N");
    }
    
    /// <summary>
    /// Re-enable orbital rails
    /// </summary>
    public void ReRail()
    {
        if (isRailed) return;
        
        isRailed = true;
        totalExternalForce = Vector2.zero;
        
        // Snap to nearest point on orbit
        Vector2 toObject = (Vector2)transform.position - (Vector2)orbitTarget.position;
        currentAngle = Mathf.Atan2(toObject.y, toObject.x) * Mathf.Rad2Deg;
        
        Debug.Log($"[{gameObject.name}] RE-RAILED at angle {currentAngle:F2}°");
    }
    
    /// <summary>
    /// Manually apply external force (for collision system integration)
    /// </summary>
    public void ApplyExternalForce(Vector2 force)
    {
        totalExternalForce += force;
        externalForceThisFrame += force.magnitude;
        
        // Check if this additional force breaks the orbit
        if (isRailed && externalForceThisFrame > forceThreshold)
        {
            BreakOrbit();
        }
    }
    
    /// <summary>
    /// Apply impact force from collision (magnitude only)
    /// </summary>
    public void ApplyImpactForce(float forceMagnitude)
    {
        externalForceThisFrame += forceMagnitude;
        
        // Check if this impact breaks the orbit
        if (isRailed && externalForceThisFrame > forceThreshold)
        {
            BreakOrbit();
        }
    }
    
    void OnDrawGizmos()
    {
        if (orbitTarget == null) return;
        
        // Draw orbital path
        DrawOrbitalPath();
        
        // Draw current position
        Gizmos.color = isRailed ? Color.yellow : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // Draw line to target
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawLine(transform.position, orbitTarget.position);
    }
    
    void OnDrawGizmosSelected()
    {
        if (orbitTarget == null) return;
        
        // Draw more detailed info when selected
        DrawOrbitalPath();
        
        // Draw velocity arrow
        if (rb != null && Application.isPlaying)
        {
            Vector2 idealVel = CalculateIdealVelocity();
            DrawArrow(transform.position, idealVel * 0.5f, Color.cyan, "Ideal Velocity");
            DrawArrow(transform.position, rb.velocity * 0.5f, Color.green, "Current Velocity");
        }
    }
    
    void DrawOrbitalPath()
    {
        float radius = GetTotalRadius();
        Vector3 center = orbitTarget.position;
        
        int segments = 64;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * 360f * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0
            );
            
            Gizmos.color = isRailed ? new Color(0f, 1f, 0f, 0.5f) : new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
    
    void DrawArrow(Vector3 start, Vector3 direction, Color color, string label = "")
    {
        if (direction.magnitude < 0.01f) return;
        
        Gizmos.color = color;
        Vector3 end = start + direction;
        Gizmos.DrawLine(start, end);
        
        // Arrow head
        Vector3 right = Quaternion.Euler(0, 0, 150) * -direction.normalized * 0.3f;
        Vector3 left = Quaternion.Euler(0, 0, -150) * -direction.normalized * 0.3f;
        Gizmos.DrawLine(end, end + right);
        Gizmos.DrawLine(end, end + left);
        
        #if UNITY_EDITOR
        if (!string.IsNullOrEmpty(label))
        {
            UnityEditor.Handles.Label(end, label);
        }
        #endif
    }
    
    // Public getters for debugging/visualization
    public bool IsRailed => isRailed;
    public float CurrentAngle => currentAngle;
    public float ExternalForceThisFrame => externalForceThisFrame;
}