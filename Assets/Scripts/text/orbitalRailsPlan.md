Perfect! Let me formulate a comprehensive plan:

## Final System Design

### Core Principles
1. **BigGravity objects (Planets, Moons) are immovable** - RigidbodyType2D.Kinematic, never affected by gravity
2. **Moons use OrbitalRails with Infinity threshold** - Perfect stable orbits, never break
3. **Satellites use OrbitalRails with tunable threshold** - Can be disrupted but stay on course normally
4. **Small objects (Asteroids, Ships) are physics-driven** - Affected by all BigGravity sources
5. **Extensive debug visualization** - See gravity fields, thresholds, velocities

---

## Implementation Plan

### Phase 1: Create OrbitalRails Component
**New File:** `Assets/Scripts/Gravity/OrbitalRails.cs`

**Features:**
- `orbitTarget` (Transform)
- `orbitalRadiusFromSurface` (float) - Distance from target's surface
- `orbitalSpeed` (float) - degrees/second
- `forceThreshold` (float) - N before breaking orbit (Infinity = never break)
- `isRailed` (bool) - Current state
- `autoReRail` (bool) - Snap back if forces drop
- `reRailThreshold` (float) - Force must drop below this to re-rail

**Methods:**
- `FixedUpdate()` - Apply corrective forces
- `BreakOrbit()` - Disable rails
- `ReRail()` - Re-enable rails
- `CalculateIdealPosition()` - Where object should be
- `CalculateIdealVelocity()` - Orbital + parent velocity
- `OnDrawGizmos()` - Draw orbital path

---

### Phase 2: Create Moon Class
**New File:** `Assets/Scripts/Planets/Moon.cs`

```csharp
public class Moon : Planet
{
    [Header("Moon Orbit Settings")]
    [SerializeField] private Planet parentPlanet;
    [SerializeField] private float orbitalRadiusFromSurface = 50f;
    [SerializeField] private float orbitalSpeed = 360f; // deg/s (360 = 1 orbit/sec)
    [SerializeField] private float startingAngle = 0f; // Where to spawn on orbit
    
    private OrbitalRails rails;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Position moon at starting orbital position
        if (parentPlanet != null)
        {
            float totalRadius = parentPlanet.Radius / Utility.GLOBAL_PPU + orbitalRadiusFromSurface;
            Vector2 offset = new Vector2(
                Mathf.Cos(startingAngle * Mathf.Deg2Rad),
                Mathf.Sin(startingAngle * Mathf.Deg2Rad)
            ) * totalRadius;
            transform.position = parentPlanet.transform.position + (Vector3)offset;
        }
        
        // Add orbital rails
        rails = gameObject.AddComponent<OrbitalRails>();
        rails.orbitTarget = parentPlanet.transform;
        rails.orbitalRadiusFromSurface = orbitalRadiusFromSurface;
        rails.orbitalSpeed = orbitalSpeed;
        rails.forceThreshold = Mathf.Infinity; // Moons never break orbit
        rails.currentAngle = startingAngle;
    }
    
    // Override to ensure moons don't participate in mutual gravity
    void OnEnable()
    {
        // Moons still register as gravity sources for small objects
        GravityManager.Register(this);
    }
}
```

---

### Phase 3: Update GravityManager
**Modify:** `Assets/Scripts/Gravity/GravityManager.cs`

**Add parameter to CalculateGravityAt:**
```csharp
public static Vector2 CalculateGravityAt(Vector2 position, GameObject affectedObject = null)
{
    Vector2 totalForce = Vector2.zero;

    foreach (var source in instance.gravitySources)
    {
        if (source is MonoBehaviour mb)
        {
            // BigGravity objects don't affect each other
            if (affectedObject != null && affectedObject.GetComponent<IBigGravity>() != null)
            {
                continue;
            }
            
            // ... rest of gravity calculation
        }
    }
    
    return totalForce * Utility.GRAVITY_TIMESCALE;
}
```

**Update all calls to CalculateGravityAt to pass GameObject:**
- In `Asteroid.cs`: `GravityManager.CalculateGravityAt(transform.position, gameObject)`
- In `AtmosphericPhysics.cs`: `GravityManager.CalculateGravityAt(transform.position, gameObject)`

---

### Phase 4: Advanced Gizmo System
**New File:** `Assets/Scripts/Debug/GravityGizmos.cs`

**Features:**

#### 1. Orbital Path Visualization
```csharp
- Draw circular orbit path
- Show gravity strength along path (color gradient)
- Mark peaks and troughs
- Show "safe zone" vs "danger zone" based on threshold
```

#### 2. BigGravity Influence Rings
```csharp
- Draw influence radius sphere
- Draw concentric rings at every 10% (deciles) of max radius
- Label each ring with gravity strength at that distance
- Color code: strong (red) → weak (blue)
```

#### 3. Gravity Field Arrows (for IGravityAffectable objects)
```csharp
- Red Arrow: Sum of all gravity forces (direction + magnitude)
- Green Arrow: Current velocity (direction + magnitude)
- Yellow Arrow: Ideal orbital velocity (if on rails)
- Cyan Arrow: Corrective force (if on rails)
```

#### 4. Force Threshold Indicator
```csharp
- Show current external force vs threshold
- Progress bar above object
- Color: Green (safe) → Yellow (warning) → Red (breaking)
```

#### 5. Moon Alignment Warning
```csharp
- When Moon-Satellite-Planet aligned, highlight in gizmo
- Show combined gravity at satellite position
- Compare to threshold
```

**Implementation:**
```csharp
public class GravityGizmos : MonoBehaviour
{
    [Header("Gizmo Settings")]
    public bool showOrbitalPaths = true;
    public bool showInfluenceRings = true;
    public bool showGravityArrows = true;
    public bool showVelocityArrows = true;
    public bool showForceThresholds = true;
    public bool showAlignmentWarnings = true;
    
    [Header("Visualization")]
    public int orbitalPathSegments = 64;
    public float arrowScale = 1f;
    
    void OnDrawGizmos()
    {
        if (showOrbitalPaths) DrawOrbitalPaths();
        if (showInfluenceRings) DrawInfluenceRings();
        if (showGravityArrows) DrawGravityArrows();
        if (showVelocityArrows) DrawVelocityArrows();
        if (showForceThresholds) DrawForceThresholds();
        if (showAlignmentWarnings) DrawAlignmentWarnings();
    }
    
    void DrawOrbitalPaths()
    {
        // For each OrbitalRails in scene
        foreach (var rails in FindObjectsOfType<OrbitalRails>())
        {
            DrawOrbitalPath(rails);
        }
    }
    
    void DrawOrbitalPath(OrbitalRails rails)
    {
        if (rails.orbitTarget == null) return;
        
        Vector3 center = rails.orbitTarget.position;
        float radius = rails.GetTotalRadius();
        
        // Draw orbit circle
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= orbitalPathSegments; i++)
        {
            float angle = (i / (float)orbitalPathSegments) * 360f * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0
            );
            
            // Calculate gravity at this point
            float gravityMag = CalculateGravityMagnitudeAt(newPoint, rails.gameObject);
            Color gravityColor = Color.Lerp(Color.green, Color.red, 
                Mathf.Clamp01(gravityMag / 50f)); // Adjust scale as needed
            
            Gizmos.color = gravityColor;
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
        
        // Mark current position
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(rails.transform.position, 0.5f);
    }
    
    void DrawInfluenceRings()
    {
        foreach (var bigGrav in FindObjectsOfType<MonoBehaviour>().OfType<IBigGravity>())
        {
            if (bigGrav is MonoBehaviour mb)
            {
                DrawInfluenceRingsFor(bigGrav, mb.transform.position);
            }
        }
    }
    
    void DrawInfluenceRingsFor(IBigGravity source, Vector3 position)
    {
        float maxRadius = source.GetMaxInfluenceRadius();
        
        // Draw 10 rings (0%, 10%, 20%, ... 100%)
        for (int i = 1; i <= 10; i++)
        {
            float ringRadius = maxRadius * (i / 10f);
            float gravityAtRing = CalculateGravityAtDistance(source, ringRadius);
            
            Color ringColor = Color.Lerp(Color.red, Color.blue, i / 10f);
            Gizmos.color = ringColor;
            DrawCircle(position, ringRadius, 32);
            
            // Label with gravity strength
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(
                position + new Vector3(ringRadius, 0, 0),
                $"{gravityAtRing:F2} N/kg"
            );
            #endif
        }
    }
    
    void DrawGravityArrows()
    {
        foreach (var affectable in FindObjectsOfType<MonoBehaviour>().OfType<IGravityAffectable>())
        {
            if (affectable is MonoBehaviour mb)
            {
                Vector2 gravity = GravityManager.CalculateGravityAt(mb.transform.position, mb.gameObject);
                DrawArrow(mb.transform.position, gravity * arrowScale, Color.red);
            }
        }
    }
    
    void DrawVelocityArrows()
    {
        foreach (var rb in FindObjectsOfType<Rigidbody2D>())
        {
            if (rb.bodyType != RigidbodyType2D.Kinematic)
            {
                DrawArrow(rb.position, rb.velocity * arrowScale * 0.1f, Color.green);
            }
        }
    }
    
    void DrawForceThresholds()
    {
        foreach (var rails in FindObjectsOfType<OrbitalRails>())
        {
            if (rails.forceThreshold < Mathf.Infinity)
            {
                // Draw threshold bar above object
                Vector3 pos = rails.transform.position + Vector3.up * 2f;
                float percentage = rails.accumulatedExternalForce.magnitude / rails.forceThreshold;
                
                Color barColor = Color.Lerp(Color.green, Color.red, percentage);
                // Draw bar using Gizmos or Handles
            }
        }
    }
    
    void DrawAlignmentWarnings()
    {
        // Find all Moon-Satellite-Planet alignments
        var moons = FindObjectsOfType<Moon>();
        var satellites = FindObjectsOfType<OrbitalRails>().Where(r => r.forceThreshold < Mathf.Infinity);
        
        foreach (var moon in moons)
        {
            foreach (var sat in satellites)
            {
                // Check if aligned
                Vector2 moonToSat = (Vector2)sat.transform.position - (Vector2)moon.transform.position;
                Vector2 satToPlanet = (Vector2)moon.parentPlanet.transform.position - (Vector2)sat.transform.position;
                
                float alignment = Vector2.Dot(moonToSat.normalized, satToPlanet.normalized);
                
                if (alignment > 0.95f) // Nearly aligned
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(moon.transform.position, sat.transform.position);
                    Gizmos.DrawLine(sat.transform.position, moon.parentPlanet.transform.position);
                    
                    // Calculate total gravity at satellite
                    float totalGravity = CalculateGravityMagnitudeAt(sat.transform.position, sat.gameObject);
                    
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(
                        sat.transform.position,
                        $"ALIGNED! Gravity: {totalGravity:F2} N/kg"
                    );
                    #endif
                }
            }
        }
    }
    
    // Helper methods
    float CalculateGravityMagnitudeAt(Vector3 position, GameObject obj)
    {
        return GravityManager.CalculateGravityAt(position, obj).magnitude;
    }
    
    float CalculateGravityAtDistance(IBigGravity source, float distance)
    {
        float mass = source.GetMass();
        return (Utility.G * mass) / Mathf.Max(distance * distance, 0.01f);
    }
    
    void DrawCircle(Vector3 center, float radius, int segments)
    {
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * 360f * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0
            );
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
    
    void DrawArrow(Vector3 start, Vector3 direction, Color color)
    {
        Gizmos.color = color;
        Vector3 end = start + direction;
        Gizmos.DrawLine(start, end);
        
        // Arrow head
        Vector3 right = Quaternion.Euler(0, 0, 150) * -direction.normalized * 0.3f;
        Vector3 left = Quaternion.Euler(0, 0, -150) * -direction.normalized * 0.3f;
        Gizmos.DrawLine(end, end + right);
        Gizmos.DrawLine(end, end + left);
    }
}
```

---

### Phase 5: Update Existing Classes

#### Update `Asteroid.cs`:
- Change `ApplyGravity` to use new signature:
```csharp
Vector2 gravity = GravityManager.CalculateGravityAt(transform.position, gameObject);
```

#### Update `AtmosphericPhysics.cs`:
- Same change in `ApplyAtmosphericPhysics`

#### Update `Planet.cs`:
- Ensure `PlanetRB.bodyType = RigidbodyType2D.Kinematic`
- Maybe add comment: "Planets are immovable - use OrbitalRails for orbital motion"

---

## File Structure Summary

**New Files:**
1. `Assets/Scripts/Gravity/OrbitalRails.cs` - Core orbital system
2. `Assets/Scripts/Planets/Moon.cs` - Moon class
3. `Assets/Scripts/Debug/GravityGizmos.cs` - Visualization system

**Modified Files:**
1. `Assets/Scripts/Gravity/GravityManager.cs` - Add GameObject parameter
2. `Assets/Scripts/Planets/Asteroid.cs` - Update gravity call
3. `Assets/Scripts/Gravity/AtmosphericPhysics.cs` - Update gravity call
4. `Assets/Scripts/Planets/Planet.cs` - Ensure kinematic (already is)

---

## Testing Checklist

1. ✅ Create Planet → Create Moon orbiting it → Moon orbits smoothly
2. ✅ Add Satellite with threshold → Stays on orbit
3. ✅ Shoot Satellite → Breaks orbit when threshold exceeded
4. ✅ Create Moon-Satellite-Planet alignment → Check gizmo warnings
5. ✅ Enable all gizmos → Verify visualization clarity
6. ✅ Parent planet "moves" slowly → Moon follows parent
7. ✅ Asteroid passes near Moon → Asteroid affected, Moon unaffected
8. ✅ Moon of Moon → Nested orbits work

---

**Ready to implement?** Should I start with OrbitalRails.cs first?