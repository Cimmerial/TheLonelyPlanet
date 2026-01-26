// Assets/Scripts/Debug/GravityGizmos.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Comprehensive gravity and orbital visualization system.
/// Attach to any GameObject in the scene (or create a dedicated "DebugVisualizer" GameObject).
/// Shows orbital paths, gravity fields, velocity vectors, and alignment warnings.
/// </summary>
public class GravityGizmos : MonoBehaviour
{
    [Header("Gizmo Toggles")]
    [SerializeField] private bool showOrbitalPaths = true;
    [SerializeField] private bool showInfluenceRings = true;
    [SerializeField] private bool showGravityArrows = true;
    [SerializeField] private bool showVelocityArrows = true;
    [SerializeField] private bool showIdealVelocityArrows = false;
    [SerializeField] private bool showForceThresholds = true;
    [SerializeField] private bool showAlignmentWarnings = true;
    [SerializeField] private bool showIndividualGravitySources = true;
    [SerializeField] private bool showCurrentVelocityDebug = true;
    [SerializeField] private bool showObjectOutlines = true; // NEW: Show size outlines for all objects
    
    [Header("Orbital Path Settings")]
    [SerializeField] private int orbitalPathSegments = 64;
    [SerializeField] private bool colorPathByGravity = true;
    [SerializeField] private float maxGravityForColor = 50f;
    
    [Header("Influence Ring Settings")]
    [SerializeField] private int ringCount = 10;
    [SerializeField] private bool showRingLabels = true;
    
    [Header("Arrow Settings")]
    [SerializeField] private float arrowScale = 1f;
    [SerializeField] private float velocityArrowScale = 0.1f;
    [SerializeField] private float arrowHeadSize = 0.3f;
    
    [Header("Alignment Warning")]
    [SerializeField] private float alignmentThreshold = 0.95f;
    [SerializeField] private bool showAlignmentGravity = true;
    
    [Header("UI Settings")]
    [SerializeField] private int fontSize = 8; // Smaller default font
    [SerializeField] private float barHeight = 0.2f; // Smaller bar
    [SerializeField] private float barOffsetFromObject = 1f; // Closer to object
    
    void OnDrawGizmos()
    {
        if (showObjectOutlines) DrawObjectOutlines();
        if (showOrbitalPaths) DrawOrbitalPaths();
        if (showInfluenceRings) DrawInfluenceRings();
        if (showGravityArrows) DrawGravityArrows();
        if (showVelocityArrows) DrawVelocityArrows();
        if (showIdealVelocityArrows) DrawIdealVelocityArrows();
        if (showForceThresholds) DrawForceThresholds();
        if (showAlignmentWarnings) DrawAlignmentWarnings();
        if (showIndividualGravitySources) DrawIndividualGravitySources();
        if (showCurrentVelocityDebug) DrawCurrentVelocityDebug();
    }
    
    #region Object Outlines
    
    void DrawObjectOutlines()
    {
        // Draw planets and moons (black crosshair circles)
        foreach (var planet in FindObjectsOfType<Planet>())
        {
            bool isMoon = planet is Moon;
            float radius = planet.Radius / Utility.GLOBAL_PPU;
            
            Gizmos.color = Color.black;
            DrawCrosshairCircle(planet.transform.position, radius, 32);
        }
        
        // Draw asteroids (red circles based on max dimension)
        foreach (var asteroid in FindObjectsOfType<Asteroid>())
        {
            float maxDim = Mathf.Max(asteroid.MaxDimensions.x, asteroid.MaxDimensions.y);
            float radius = maxDim / Utility.GLOBAL_PPU;
            
            Gizmos.color = Color.red;
            DrawCircle(asteroid.transform.position, radius, 16);
        }
        
        // Draw vehicles (white circles based on sprite size)
        // You'll need to add Vehicle script detection here when you have it
        // For now, this is a placeholder
        /*
        foreach (var vehicle in FindObjectsOfType<Vehicle>())
        {
            SpriteRenderer sr = vehicle.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                float maxDim = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
                Gizmos.color = Color.white;
                DrawCircle(vehicle.transform.position, maxDim * 0.5f, 16);
            }
        }
        */
    }
    
    void DrawCrosshairCircle(Vector3 center, float radius, int segments)
    {
        // Draw circle
        DrawCircle(center, radius, segments);
        
        // Draw crosshair lines
        float crosshairLength = radius * 0.3f;
        Gizmos.DrawLine(center + Vector3.left * crosshairLength, center + Vector3.right * crosshairLength);
        Gizmos.DrawLine(center + Vector3.down * crosshairLength, center + Vector3.up * crosshairLength);
    }
    
    #endregion
    
    #region Orbital Paths
    
    void DrawOrbitalPaths()
    {
        OrbitalRails[] allRails = FindObjectsOfType<OrbitalRails>();
        
        foreach (var rails in allRails)
        {
            if (rails.orbitTarget == null) continue;
            
            // Calculate actual total radius (target radius + orbital radius from surface)
            float targetRadius = 0f;
            Planet targetPlanet = rails.orbitTarget.GetComponent<Planet>();
            Moon targetMoon = rails.orbitTarget.GetComponent<Moon>();
            
            if (targetPlanet != null)
            {
                targetRadius = targetPlanet.Radius / Utility.GLOBAL_PPU;
            }
            else if (targetMoon != null)
            {
                targetRadius = targetMoon.Radius / Utility.GLOBAL_PPU;
            }
            
            float orbitalRadiusInUnits = rails.orbitalRadiusFromSurface / Utility.GLOBAL_PPU;
            float totalRadius = targetRadius + orbitalRadiusInUnits;
            
            DrawOrbitalPath(rails, totalRadius);
        }
    }
    
    void DrawOrbitalPath(OrbitalRails rails, float totalRadius)
    {
        Vector3 center = rails.orbitTarget.position;
        
        Vector3 prevPoint = center + new Vector3(totalRadius, 0, 0);
        
        for (int i = 1; i <= orbitalPathSegments; i++)
        {
            float angle = (i / (float)orbitalPathSegments) * 360f * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * totalRadius,
                Mathf.Sin(angle) * totalRadius,
                0
            );
            
            // Color based on gravity strength at this point if enabled
            if (colorPathByGravity)
            {
                float gravityMag = CalculateGravityMagnitudeAt(newPoint, rails.gameObject);
                float normalizedGravity = Mathf.Clamp01(gravityMag / maxGravityForColor);
                Gizmos.color = Color.Lerp(Color.green, Color.red, normalizedGravity);
            }
            else
            {
                Gizmos.color = rails.IsRailed ? new Color(0f, 1f, 0f, 0.5f) : new Color(1f, 0f, 0f, 0.3f);
            }
            
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
        
        // Draw faint line to target
        Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
        Gizmos.DrawLine(rails.transform.position, rails.orbitTarget.position);
    }
    
    #endregion
    
    #region Influence Rings
    
    void DrawInfluenceRings()
    {
        List<IBigGravity> gravitySources = GravityManager.GetAllGravitySources();
        
        foreach (var source in gravitySources)
        {
            if (source is MonoBehaviour mb)
            {
                DrawInfluenceRingsFor(source, mb.transform.position);
            }
        }
    }
    
    void DrawInfluenceRingsFor(IBigGravity source, Vector3 position)
    {
        float maxRadius = source.GetMaxInfluenceRadius();
        
        // Draw rings at deciles (10%, 20%, ... 100%)
        for (int i = 1; i <= ringCount; i++)
        {
            float percentage = i / (float)ringCount;
            float ringRadius = maxRadius * percentage;
            
            // Calculate gravity at this distance - use direct calculation, not cached method
            float mass = source.GetMass();
            float gravityAtRing = (Utility.G * mass) / Mathf.Max(ringRadius * ringRadius, 0.01f) * Utility.GRAVITY_TIMESCALE;
            
            // Color gradient: red (strong) -> blue (weak)
            Color ringColor = Color.Lerp(Color.red, Color.blue, percentage);
            ringColor.a = 0.3f + (1f - percentage) * 0.4f; // Fade outer rings
            
            Gizmos.color = ringColor;
            DrawCircle(position, ringRadius, 32);
            
            // Draw label with gravity strength BELOW the planet (vertical)
            #if UNITY_EDITOR
            if (showRingLabels && i % 2 == 0) // Only label every other ring to reduce clutter
            {
                // Position label below the planet, offset by ring index
                Vector3 labelPos = position + Vector3.down * (i * 0.5f + 2f);
                UnityEditor.Handles.Label(labelPos, 
                    $"{percentage * 100:F0}%: {gravityAtRing:F2} N/kg",
                    new GUIStyle()
                    {
                        normal = new GUIStyleState() { textColor = ringColor },
                        fontSize = fontSize,
                        alignment = TextAnchor.MiddleCenter
                    });
            }
            #endif
        }
    }
    
    #endregion
    
    #region Gravity Arrows
    
    void DrawGravityArrows()
    {
        // Draw gravity arrows for all objects that are affected by gravity
        Rigidbody2D[] allRigidbodies = FindObjectsOfType<Rigidbody2D>();
        
        foreach (var rb in allRigidbodies)
        {
            // Skip kinematic bodies (planets, moons)
            if (rb.bodyType == RigidbodyType2D.Kinematic) continue;
            
            // Skip if this is a BigGravity object (they don't feel gravity)
            if (rb.GetComponent<IBigGravity>() != null) continue;
            
            Vector2 gravity = GravityManager.CalculateGravityAt(rb.position, rb.gameObject);
            
            if (gravity.magnitude > 0.01f)
            {
                DrawArrow(rb.position, gravity * arrowScale, Color.red);
            }
        }
    }
    
    #endregion
    
    #region Velocity Arrows
    
    void DrawVelocityArrows()
    {
        Rigidbody2D[] allRigidbodies = FindObjectsOfType<Rigidbody2D>();
        
        foreach (var rb in allRigidbodies)
        {
            // Skip kinematic bodies
            if (rb.bodyType == RigidbodyType2D.Kinematic) continue;
            
            if (rb.velocity.magnitude > 0.1f)
            {
                DrawArrow(rb.position, rb.velocity * velocityArrowScale * arrowScale, Color.green);
            }
        }
    }
    
    void DrawIdealVelocityArrows()
    {
        OrbitalRails[] allRails = FindObjectsOfType<OrbitalRails>();
        
        foreach (var rails in allRails)
        {
            if (!rails.IsRailed) continue;
            
            Vector2 idealVel = rails.CalculateIdealVelocity();
            
            if (idealVel.magnitude > 0.1f)
            {
                DrawArrow(rails.transform.position, idealVel * velocityArrowScale * arrowScale, Color.cyan);
            }
        }
    }
    
    #endregion
    
    #region Force Thresholds
    
    void DrawForceThresholds()
    {
        OrbitalRails[] allRails = FindObjectsOfType<OrbitalRails>();
        
        foreach (var rails in allRails)
        {
            // Skip if threshold is infinite (can't be broken)
            if (rails.forceThreshold >= Mathf.Infinity) continue;
            
            float forcePercentage = rails.ExternalForceThisFrame / rails.forceThreshold;
            
            // Draw a bar above the object (closer and smaller)
            Vector3 barPosition = rails.transform.position + Vector3.up * barOffsetFromObject;
            float barWidth = 1.5f; // Smaller bar
            
            // Background bar (gray)
            Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            DrawBar(barPosition, barWidth, barHeight);
            
            // Foreground bar (colored by percentage)
            Color barColor = Color.Lerp(Color.green, Color.red, forcePercentage);
            Gizmos.color = barColor;
            float fillWidth = barWidth * Mathf.Clamp01(forcePercentage);
            DrawBar(barPosition - new Vector3((barWidth - fillWidth) * 0.5f, 0, 0), fillWidth, barHeight);
            
            // Label (white font, positioned on the bar)
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(
                barPosition, // Same position as bar, will overlay
                $"{rails.ExternalForceThisFrame:F1}/{rails.forceThreshold:F0}N",
                new GUIStyle()
                {
                    normal = new GUIStyleState() { textColor = Color.white },
                    fontSize = fontSize,
                    alignment = TextAnchor.MiddleCenter
                });
            #endif
        }
    }
    
    void DrawBar(Vector3 center, float width, float height)
    {
        Vector3 bottomLeft = center + new Vector3(-width * 0.5f, -height * 0.5f, 0);
        Vector3 bottomRight = center + new Vector3(width * 0.5f, -height * 0.5f, 0);
        Vector3 topRight = center + new Vector3(width * 0.5f, height * 0.5f, 0);
        Vector3 topLeft = center + new Vector3(-width * 0.5f, height * 0.5f, 0);
        
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }
    
    #endregion
    
    #region Alignment Warnings
    
    void DrawAlignmentWarnings()
    {
        Moon[] moons = FindObjectsOfType<Moon>();
        OrbitalRails[] satellites = FindObjectsOfType<OrbitalRails>()
            .Where(r => r.forceThreshold < Mathf.Infinity) // Only satellites that can break
            .ToArray();
        
        foreach (var moon in moons)
        {
            if (moon.ParentPlanet == null) continue;
            
            foreach (var satellite in satellites)
            {
                // Check if satellite orbits the same planet as the moon
                if (satellite.orbitTarget != moon.ParentPlanet.transform) continue;
                
                // Calculate alignment
                Vector2 moonToSat = (Vector2)satellite.transform.position - (Vector2)moon.transform.position;
                Vector2 satToPlanet = (Vector2)moon.ParentPlanet.transform.position - (Vector2)satellite.transform.position;
                
                // Normalize and check alignment
                if (moonToSat.magnitude < 0.01f || satToPlanet.magnitude < 0.01f) continue;
                
                float alignment = Vector2.Dot(moonToSat.normalized, satToPlanet.normalized);
                
                if (alignment > alignmentThreshold)
                {
                    // ALIGNED! Draw warning
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(moon.transform.position, satellite.transform.position);
                    Gizmos.DrawLine(satellite.transform.position, moon.ParentPlanet.transform.position);
                    
                    // Draw warning sphere at satellite
                    Gizmos.color = new Color(1f, 0f, 1f, 0.5f);
                    Gizmos.DrawWireSphere(satellite.transform.position, 1f);
                    
                    if (showAlignmentGravity)
                    {
                        // Calculate total gravity at satellite position
                        float totalGravity = CalculateGravityMagnitudeAt(satellite.transform.position, satellite.gameObject);
                        
                        #if UNITY_EDITOR
                        UnityEditor.Handles.Label(
                            satellite.transform.position + Vector3.up * 3f,
                            $"⚠ ALIGN ⚠\n{totalGravity:F1} N/kg",
                            new GUIStyle()
                            {
                                normal = new GUIStyleState() { textColor = Color.magenta },
                                fontSize = fontSize + 2,
                                fontStyle = FontStyle.Bold,
                                alignment = TextAnchor.MiddleCenter
                            }
                        );
                        #endif
                    }
                }
            }
        }
    }
    
    #endregion
    
    #region Individual Gravity Sources
    
    void DrawIndividualGravitySources()
    {
        if (!Application.isPlaying) return;
        
        List<IBigGravity> gravitySources = GravityManager.GetAllGravitySources();
        Rigidbody2D[] allRigidbodies = FindObjectsOfType<Rigidbody2D>();
        
        foreach (var rb in allRigidbodies)
        {
            // Skip kinematic bodies and BigGravity objects
            if (rb.bodyType == RigidbodyType2D.Kinematic) continue;
            if (rb.GetComponent<IBigGravity>() != null) continue;
            
            Vector2 objectPos = rb.position;
            
            // Draw arrow to each gravity source affecting this object
            foreach (var source in gravitySources)
            {
                if (source is MonoBehaviour mb)
                {
                    Vector2 direction = (Vector2)mb.transform.position - objectPos;
                    float distance = direction.magnitude;
                    
                    // Skip if outside influence radius
                    if (distance > source.GetMaxInfluenceRadius()) continue;
                    if (distance < 0.01f) continue;
                    
                    // Calculate gravity from this source
                    float mass = source.GetMass();
                    float gravityMag = (Utility.G * mass) / Mathf.Max(distance * distance, 0.01f) * Utility.GRAVITY_TIMESCALE;
                    Vector2 gravityVec = direction.normalized * gravityMag;
                    
                    // Draw thinner arrow to source (color by strength)
                    float normalizedStrength = Mathf.Clamp01(gravityMag / 10f);
                    Color sourceColor = Color.Lerp(new Color(1f, 0.5f, 0f, 0.5f), new Color(1f, 0f, 0f, 0.8f), normalizedStrength);
                    
                    DrawThinArrow(objectPos, gravityVec * arrowScale * 0.5f, sourceColor);
                    
                    #if UNITY_EDITOR
                    // Label with gravity strength (smaller font)
                    Vector3 midPoint = objectPos + gravityVec.normalized * distance * 0.5f;
                    UnityEditor.Handles.Label(midPoint, $"{gravityMag:F2}",
                        new GUIStyle()
                        {
                            normal = new GUIStyleState() { textColor = sourceColor },
                            fontSize = fontSize,
                            alignment = TextAnchor.MiddleCenter
                        });
                    #endif
                }
            }
        }
    }
    
    void DrawThinArrow(Vector3 start, Vector3 direction, Color color)
    {
        if (direction.magnitude < 0.01f) return;
        
        Gizmos.color = color;
        Vector3 end = start + direction;
        Gizmos.DrawLine(start, end);
        
        // Smaller arrow head for individual sources
        float thinArrowHeadSize = arrowHeadSize * 0.6f;
        Vector3 arrowTip = end;
        
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0).normalized;
        Vector3 left = arrowTip - direction.normalized * thinArrowHeadSize + perpendicular * thinArrowHeadSize * 0.5f;
        Vector3 right = arrowTip - direction.normalized * thinArrowHeadSize - perpendicular * thinArrowHeadSize * 0.5f;
        
        Gizmos.DrawLine(arrowTip, left);
        Gizmos.DrawLine(arrowTip, right);
    }
    
    #endregion
    
    #region Current Velocity Debug
    
    void DrawCurrentVelocityDebug()
    {
        if (!Application.isPlaying) return;
        
        Rigidbody2D[] allRigidbodies = FindObjectsOfType<Rigidbody2D>();
        
        foreach (var rb in allRigidbodies)
        {
            // Skip kinematic bodies
            if (rb.bodyType == RigidbodyType2D.Kinematic) continue;
            
            #if UNITY_EDITOR
            Vector3 labelPos = (Vector3)rb.position + Vector3.down * 1.5f;
            string velocityText = $"{rb.velocity.magnitude:F1} u/s";
            
            UnityEditor.Handles.Label(labelPos, velocityText, new GUIStyle()
            {
                normal = new GUIStyleState() { textColor = Color.cyan },
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter
            });
            #endif
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    float CalculateGravityMagnitudeAt(Vector3 position, GameObject obj)
    {
        return GravityManager.CalculateGravityAt(position, obj).magnitude;
    }
    
    void DrawCircle(Vector3 center, float radius, int segments)
    {
        if (radius < 0.01f) return;
        
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
        if (direction.magnitude < 0.01f) return;
        
        Gizmos.color = color;
        Vector3 end = start + direction;
        Gizmos.DrawLine(start, end);
        
        // Arrow head - FIXED: now points in direction of arrow
        Vector3 arrowTip = end;
        Vector3 arrowBase = start + direction.normalized * (direction.magnitude - arrowHeadSize);
        
        // Calculate perpendicular vectors for arrow head
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0).normalized;
        Vector3 left = arrowTip - direction.normalized * arrowHeadSize + perpendicular * arrowHeadSize * 0.5f;
        Vector3 right = arrowTip - direction.normalized * arrowHeadSize - perpendicular * arrowHeadSize * 0.5f;
        
        Gizmos.DrawLine(arrowTip, left);
        Gizmos.DrawLine(arrowTip, right);
    }
    
    #endregion
}