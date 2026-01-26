// Assets/Scripts/Planets/Moon.cs
using UnityEngine;

/// <summary>
/// Moon class - inherits from Planet but orbits around a parent Planet.
/// Uses OrbitalRails to maintain stable orbit.
/// Moons are BigGravity objects that affect small objects but not other BigGravity objects.
/// </summary>
public class Moon : Planet
{
    [Header("Moon Orbital Settings")]
    [Tooltip("The planet this moon orbits around")]
    [SerializeField] private Planet parentPlanet;
    
    [Tooltip("Distance from parent's surface (not center) in pixels")]
    [SerializeField] private float orbitalRadiusFromSurface = 100f;
    
    [Tooltip("Orbital speed in degrees/second (360 = 1 orbit/sec, -360 = reverse)")]
    [SerializeField] private float orbitalSpeed = 360f;
    
    [Tooltip("Starting angle on orbit (0 = right, 90 = up, 180 = left, 270 = down)")]
    [SerializeField] private float startingAngle = 0f;
    
    [Tooltip("Can this moon's orbit be broken by external forces? (Usually false)")]
    [SerializeField] private bool canBreakOrbit = false;
    
    [Tooltip("If canBreakOrbit is true, force in Newtons required to break orbit")]
    [SerializeField] private float orbitBreakThreshold = 10000f;
    
    private OrbitalRails rails;
    
    // Public getter for parent planet (useful for queries)
    public Planet ParentPlanet => parentPlanet;
    
    protected override void Awake()
    {
        // First, validate parent planet
        if (parentPlanet == null)
        {
            Debug.LogError($"[{gameObject.name}] Moon has no parent planet assigned! Moon needs a parent to orbit.");
        }
        
        // Call base Planet.Awake() to generate the moon's visual/collider
        base.Awake();
        
        // Position moon at starting orbital position BEFORE adding OrbitalRails
        if (parentPlanet != null)
        {
            float parentRadius = parentPlanet.Radius / Utility.GLOBAL_PPU;
            float orbitalRadiusInUnits = orbitalRadiusFromSurface / Utility.GLOBAL_PPU;
            float totalRadius = parentRadius + orbitalRadiusInUnits;
            
            Vector2 offset = new Vector2(
                Mathf.Cos(startingAngle * Mathf.Deg2Rad),
                Mathf.Sin(startingAngle * Mathf.Deg2Rad)
            ) * totalRadius;
            
            transform.position = parentPlanet.transform.position + (Vector3)offset;
            
            Debug.Log($"[{gameObject.name}] Moon positioned at {transform.position} " +
                      $"(parent: {parentPlanet.transform.position}, offset: {offset}, totalRadius: {totalRadius:F2} units, " +
                      $"orbitalRadius: {orbitalRadiusFromSurface}px = {orbitalRadiusInUnits:F2} units)");
        }
        
        // IMPORTANT: Add Rigidbody2D to the PARENT Moon GameObject (not the collider child)
        // This is different from Planet - Moons need RB on parent for OrbitalRails
        Rigidbody2D moonRB = gameObject.GetComponent<Rigidbody2D>();
        if (moonRB == null)
        {
            moonRB = gameObject.AddComponent<Rigidbody2D>();
        }
        moonRB.bodyType = RigidbodyType2D.Kinematic;
        moonRB.useFullKinematicContacts = true;
        moonRB.mass = GetMass(); // Will be set after Planet generation
        
        // Add OrbitalRails component to maintain orbit
        rails = gameObject.AddComponent<OrbitalRails>();
        rails.orbitTarget = parentPlanet != null ? parentPlanet.transform : null;
        rails.orbitalRadiusFromSurface = orbitalRadiusFromSurface;
        rails.orbitalSpeed = orbitalSpeed;
        rails.startingAngle = startingAngle;
        rails.forceThreshold = canBreakOrbit ? orbitBreakThreshold : Mathf.Infinity;
        rails.autoReRail = false; // Moons don't auto re-rail
        
        Debug.Log($"[{gameObject.name}] Moon OrbitalRails configured - " +
                  $"Speed: {orbitalSpeed:F2} deg/s, " +
                  $"Radius: {orbitalRadiusFromSurface:F2}, " +
                  $"Breakable: {canBreakOrbit}");
    }
    
    void Start()
    {
        // Don't call base.Start() because it has rotation logic we don't need
        // Moons handle their own rotation via OrbitalRails
        
        Debug.Log($"[{gameObject.name}] Moon initialized:\n" +
                  $"Parent: {(parentPlanet != null ? parentPlanet.name : "NULL")}\n" +
                  $"Orbital Radius: {orbitalRadiusFromSurface:F2}\n" +
                  $"Orbital Speed: {orbitalSpeed:F2} deg/s\n" +
                  $"Starting Angle: {startingAngle:F2}°\n" +
                  $"Mass: {GetMass():F2}\n" +
                  $"Gravity at Surface: {GetSurfaceGravity():F2}\n" +
                  $"Max Influence: {GetMaxInfluenceRadius():F2}");
    }
    
    // Override Update to prevent natural rotation (orbit handles rotation)
    private void Update()
    {
        // Moons don't rotate on their own - they're controlled by OrbitalRails
        // Optionally, we could add tidal locking here (rotate to face parent)
        // For now, just do nothing
    }
    
    // Override FixedUpdate to prevent rotation
    private void FixedUpdate()
    {
        // Don't apply natural rotation like Planet does
        // OrbitalRails handles all movement
    }
    
    // Helper method to get surface gravity for debugging
    private float GetSurfaceGravity()
    {
        float worldRadius = Radius / Utility.GLOBAL_PPU;
        return (Utility.G * GetMass()) / Mathf.Pow(worldRadius, 2);
    }
    
    // Public method to check if orbit is broken
    public bool IsOrbitBroken()
    {
        return rails != null && !rails.IsRailed;
    }
    
    // Public method to manually break orbit (for gameplay events)
    public void BreakOrbit()
    {
        if (rails != null)
        {
            rails.BreakOrbit();
            Debug.Log($"[{gameObject.name}] Moon orbit manually broken!");
        }
    }
    
    // Public method to get current orbital angle
    public float GetOrbitalAngle()
    {
        return rails != null ? rails.CurrentAngle : 0f;
    }
    
    // Optional: Add tidal locking (moon always faces parent)
    // Uncomment this if you want moons to be tidally locked
    /*
    void LateUpdate()
    {
        if (parentPlanet != null)
        {
            Vector2 toParent = (Vector2)parentPlanet.transform.position - (Vector2)transform.position;
            float angleToParent = Mathf.Atan2(toParent.y, toParent.x) * Mathf.Rad2Deg;
            
            // Rotate to face parent (90 offset because sprites typically face right)
            transform.rotation = Quaternion.Euler(0, 0, angleToParent + 90f);
        }
    }
    */
}