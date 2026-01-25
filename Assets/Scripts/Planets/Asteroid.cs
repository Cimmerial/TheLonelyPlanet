// Assets/Scripts/Planets/Asteroid.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class Asteroid : MonoBehaviour, IGravityAffectable, IAtmosphericObject
{
    [Header("Asteroid Generation")]
    [SerializeField] private Vector2 maxDimensions = new Vector2(20f, 20f);
    [SerializeField] private float shapeVariation = 0.3f;
    [SerializeField] private float noiseScale = 2f;
    [SerializeField] private int randomSeed = 0;
    [SerializeField] private Color asteroidColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private float massPerPixel = 0.1f;
    [SerializeField] private int colliderSimplification = 3;
    [SerializeField] private List<float> startingRotationSpeedBounds = new() { -90, 90 };
    [SerializeField] private float rotationSpeed = 0f;

    [Header("Asteroid Components")]
    [SerializeField] private SpriteRenderer asteroidSpriteRenderer;
    [SerializeField] private Collider2D asteroidCollider;
    [SerializeField] private Texture2D asteroidTexture;

    [Header("Generated Physics Data")]
    [SerializeField] private float totalMass;
    [SerializeField] private float assignedMass;
    [SerializeField] private float breakThreshold;
    [SerializeField] private float accumulatedForce = 0f;
    [SerializeField] private float atomizeThreshold;
    [SerializeField] private bool isFragment = false;
    // [SerializeField] private Vector2 relativeVelocity;

    // public Vector2 GetRelativeVelocity() => relativeVelocity;
    public Vector2 GetRelativeVelocity()
    {
        // Calculate on-the-fly
        Vector2 myVelocity = rb != null ? rb.velocity : Vector2.zero;
        Planet nearestPlanet = FindNearestPlanet();
        if (nearestPlanet != null && CheckIfInAtmosphereOf(nearestPlanet))
        {
            Vector2 atmosphericVel = CalculateAtmosphericVelocityFor(nearestPlanet);
            myVelocity -= atmosphericVel;
        }
        return myVelocity;
    }

    // public void SetRelativeVelocity(Vector2 velocity) => relativeVelocity = velocity;
    public Vector2 GetPosition() => transform.position;
    public bool IsInAtmosphere() => CheckIfInAtmosphere();

    private Rigidbody2D _cachedRB;
    private Rigidbody2D rb
    {
        get
        {
            if (_cachedRB == null)
            {
                _cachedRB = GetComponent<Rigidbody2D>();
            }
            return _cachedRB;
        }
    }

    public Collider2D AsteroidCollider
    {
        get { return asteroidCollider; }
        set
        {
            asteroidCollider = value;
        }
    }

    [Header("Orbital Setup")]
    [SerializeField] private bool startInOrbit = false;
    [SerializeField] private Transform orbitTarget;

    [Header("Fragmentation Settings")]
    [SerializeField] private float toughnessMultiplier = 1f;
    [SerializeField] private float collisionForceMultiplier = 0.33f;
    [SerializeField] private float collisionGracePeriod = 0.2f;

    private float spawnTime;
    private bool isGrounded = false;
    private int groundedFrames = 0;

    public void SetPhysicsData(float mass)
    {
        totalMass = mass;

        float toughnessVariation = UnityEngine.Random.Range(-1f, 1f);
        float finalToughness = toughnessMultiplier * (1f + toughnessVariation);
        breakThreshold = mass * finalToughness;

        atomizeThreshold = breakThreshold * 4.0f;

        spawnTime = Time.time;
    }

    public void SetMass(float mass)
    {
        assignedMass = mass;
        if (rb != null)
        {
            rb.mass = mass;
        }
    }

    void Awake()
    {
        if (randomSeed == 0)
        {
            randomSeed = UnityEngine.Random.Range(1, 100000);
        }

        Transform existingRenderer = transform.Find("AsteroidRenderer");
        Transform existingCollider = transform.Find("AsteroidCollider");

        if (existingRenderer == null && existingCollider == null)
        {
            GenerateAsteroid();
        }
        else
        {
            isFragment = true;
            if (existingRenderer != null)
            {
                asteroidSpriteRenderer = existingRenderer.GetComponent<SpriteRenderer>();
            }
            if (existingCollider != null)
            {
                asteroidCollider = existingCollider.GetComponent<Collider2D>();
            }
        }

        rotationSpeed = UnityEngine.Random.Range(
            startingRotationSpeedBounds[0],
            startingRotationSpeedBounds[1]
        );
    }

    void Start()
    {
        if (!isFragment && rb != null)
        {
            rb.angularVelocity = rotationSpeed;

            if (startInOrbit && orbitTarget != null)
            {
                SetupOrbit(orbitTarget);
            }
        }
    }

    private void GenerateAsteroid()
    {
        AsteroidGenerator generator = new();
        generator.GenerateAsteroid(this);
    }

    void SetupOrbit(Transform target)
    {
        if (rb == null) return;

        if (target.TryGetComponent<IBigGravity>(out var bigGrav))
        {
            Vector2 toAsteroid = (Vector2)transform.position - (Vector2)target.position;
            float distance = toAsteroid.magnitude;

            float orbitalSpeed = Utility.CalculateOrbitalVelocity(bigGrav.GetMass(), distance);
            Vector2 orbitalDirection = Utility.GetOrbitalDirection(toAsteroid);

            Debug.Log($"[{gameObject.name}] SETUP ORBIT: distance={distance:F2}, orbitalSpeed={orbitalSpeed:F2}, direction={orbitalDirection}");

            // Always set absolute velocity in space
            rb.velocity = orbitalDirection * orbitalSpeed;
            Debug.Log($"[{gameObject.name}] Starting - set absolute velocity={rb.velocity.magnitude:F2}");
        }
    }

    // Add these fields at the top of the class
    private float atmosphereEntryTime = -1f;
    private float atmosphereDragDelay = 10f; // Seconds before atmospheric drag kicks in

    void FixedUpdate()
    {
        if (rb == null) return;

        Planet nearestPlanet = FindNearestPlanet();
        bool isInAtmosphere = nearestPlanet != null && CheckIfInAtmosphereOf(nearestPlanet);
        bool grounded = IsGrounded();

        // Track when we entered atmosphere
        if (isInAtmosphere && atmosphereEntryTime < 0f)
        {
            atmosphereEntryTime = Time.time;
            Debug.Log($"[{gameObject.name}] Entered atmosphere at {atmosphereEntryTime:F2}");
        }
        else if (!isInAtmosphere)
        {
            atmosphereEntryTime = -1f; // Reset when leaving atmosphere
        }

        // Calculate how long we've been in atmosphere
        float timeInAtmosphere = isInAtmosphere ? (Time.time - atmosphereEntryTime) : 0f;

        // Calculate drag ramp-up factor (0 to 1)
        float dragRampUp = grounded ? 1f : Mathf.Clamp01(timeInAtmosphere / atmosphereDragDelay);

        if (isInAtmosphere && nearestPlanet != null)
        {
            Vector2 targetAtmosphericVelocity = CalculateAtmosphericVelocityFor(nearestPlanet);

            if (grounded)
            {
                // GROUNDED: Very strong coupling to surface (instant)
                Vector2 currentVelocity = rb.velocity;
                Vector2 velocityDifference = targetAtmosphericVelocity - currentVelocity;

                float surfaceDragStrength = 50f;
                rb.AddForce(velocityDifference * surfaceDragStrength * rb.mass, ForceMode2D.Force);

                // Tidal locking
                ApplyTidalLocking(nearestPlanet);

                // Strong damping to settle
                if (rb.velocity.magnitude < 0.05f)
                {
                    rb.velocity *= 0.9f;
                    rb.angularVelocity *= 0.9f;
                }
            }
            else
            {
                // IN ATMOSPHERE BUT NOT GROUNDED: Gradual drag ramp-up
                Vector2 currentVelocity = rb.velocity;
                Vector2 velocityDifference = targetAtmosphericVelocity - currentVelocity;

                // Apply drag strength based on ramp-up (starts at 0, reaches full strength after delay)
                float atmosphericDragStrength = 10f * dragRampUp;
                rb.AddForce(velocityDifference * atmosphericDragStrength * rb.mass, ForceMode2D.Force);

                // Gravity
                Vector2 gravity = GravityManager.CalculateGravityAt(transform.position);
                rb.AddForce(gravity * rb.mass, ForceMode2D.Force);
            }
        }
        else
        {
            // IN SPACE: Only gravity
            Vector2 gravity = GravityManager.CalculateGravityAt(transform.position);
            rb.AddForce(gravity * rb.mass, ForceMode2D.Force);
        }

        // DebugVelocityPeriodically();
    }

    private void ApplyTidalLocking(Planet planet)
    {
        if (planet == null) return;

        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        float angleToCenter = Mathf.Atan2(toPlanet.y, toPlanet.x) * Mathf.Rad2Deg;
        float targetRotation = angleToCenter + 90f;

        float currentRotation = rb.rotation;
        float rotationDifference = Mathf.DeltaAngle(currentRotation, targetRotation);

        // Much stronger torque
        float torqueStrength = 100f; // Was 10f
        float torque = rotationDifference * torqueStrength;
        rb.AddTorque(torque, ForceMode2D.Force);

        // Strong damping when close
        if (Mathf.Abs(rotationDifference) < 10f)
        {
            rb.angularVelocity *= 0.7f;
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<Planet>(out _))
        {
            groundedFrames = 0;
            // When leaving surface, restore some random tumbling
            if (rb != null)
            {
                rb.angularVelocity = UnityEngine.Random.Range(-30f, 30f);
                Debug.Log($"[{gameObject.name}] Left surface - resuming tumble");
            }
        }
    }

    private float lastDebugTime = 0f;

    void DebugVelocityPeriodically()
    {
        if (Time.time - lastDebugTime >= 1f)
        {
            lastDebugTime = Time.time;

            Planet nearestPlanet = FindNearestPlanet();
            Vector2 atmosphericVel = CalculateAtmosphericVelocity();
            Vector2 relVel = GetRelativeVelocity(); // Calculate on-the-fly

            string planetInfo = "No planet";
            string distanceInfo = "N/A";
            if (nearestPlanet != null)
            {
                Vector2 toPlanet = (Vector2)nearestPlanet.transform.position - (Vector2)transform.position;
                float distance = toPlanet.magnitude;
                float atmosphereRadius = nearestPlanet.GetAtmosphereRadius();
                planetInfo = nearestPlanet.name;
                distanceInfo = $"{distance:F2} / {atmosphereRadius:F2}";
            }

            Debug.Log($"[{gameObject.name}] === 1s DEBUG ===\n" +
                      $"Absolute Velocity: {rb.velocity} (mag: {rb.velocity.magnitude:F2})\n" +
                      $"Relative Velocity: {relVel} (mag: {relVel.magnitude:F2})\n" +
                      $"Atmospheric Velocity: {atmosphericVel} (mag: {atmosphericVel.magnitude:F2})\n" +
                      $"In Atmosphere: {IsInAtmosphere()}\n" +
                      $"Grounded: {IsGrounded()} (frames: {groundedFrames})\n" +
                      $"Nearest Planet: {planetInfo}\n" +
                      $"Distance / Atmo Radius: {distanceInfo}\n" +
                      $"Angular Velocity: {rb.angularVelocity:F2}");
        }
    }

    Vector2 CalculateAtmosphericVelocityFor(Planet planet)
    {
        if (planet == null) return Vector2.zero;

        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        float distance = toPlanet.magnitude;

        // Check if in atmosphere
        float atmosphereRadius = planet.GetAtmosphereRadius();
        if (distance > atmosphereRadius) return Vector2.zero;

        // Calculate tangential velocity at this distance
        // REVERSED: Swap the x and y components to flip direction
        Vector2 tangentialDir = new Vector2(toPlanet.y, -toPlanet.x).normalized; // Was: (-toPlanet.y, toPlanet.x)
        float angularVel = planet.GetAngularVelocity();
        float tangentialSpeed = angularVel * distance;

        return tangentialDir * tangentialSpeed;
    }

    void LateUpdate()
    {
        // Decay grounded frames
        if (groundedFrames > 0)
        {
            groundedFrames--;
        }
    }

    private bool IsGrounded()
    {
        // Check if we have recent collision contacts
        return groundedFrames > 0;
    }

    private bool CheckIfInAtmosphereOf(Planet planet)
    {
        if (planet == null) return false;

        Vector2 toPlanet = (Vector2)planet.transform.position - (Vector2)transform.position;
        float distance = toPlanet.magnitude;
        float atmosphereRadius = planet.GetAtmosphereRadius();

        return distance <= atmosphereRadius;
    }

    private bool CheckIfInAtmosphere()
    {
        Planet nearestPlanet = FindNearestPlanet();
        return CheckIfInAtmosphereOf(nearestPlanet);
    }

    Vector2 CalculateAtmosphericVelocity()
    {
        // Find nearest planet
        Planet nearestPlanet = FindNearestPlanet();
        if (nearestPlanet == null) return Vector2.zero;

        Vector2 toPlanet = (Vector2)nearestPlanet.transform.position - (Vector2)transform.position;
        float distance = toPlanet.magnitude;

        // Check if in atmosphere
        float atmosphereRadius = nearestPlanet.GetAtmosphereRadius();
        if (distance > atmosphereRadius) return Vector2.zero;

        // Calculate tangential velocity at this distance
        Vector2 tangentialDir = new Vector2(-toPlanet.y, toPlanet.x).normalized;
        float angularVel = nearestPlanet.GetAngularVelocity();
        float tangentialSpeed = angularVel * distance;

        return tangentialDir * tangentialSpeed;
    }

    Planet FindNearestPlanet()
    {
        // You could optimize this with a manager or spatial partitioning
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

        return nearest;
    }

    public void ApplyGravity(Vector2 gravityAcceleration)
    {
        if (rb == null) return;
        rb.velocity += gravityAcceleration * Time.fixedDeltaTime;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        // Mark as grounded if touching a planet
        if (collision.gameObject.TryGetComponent<Planet>(out _))
        {
            groundedFrames = 3; // Stay grounded for a few frames
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (Time.time - spawnTime < collisionGracePeriod) return;

        // Mark as grounded if touching a planet
        if (collision.gameObject.TryGetComponent<Planet>(out _))
        {
            groundedFrames = 3;
            Debug.Log($"[{gameObject.name}] COLLISION ENTER with planet - grounded");
        }

        // SAFETY CHECK: Make sure contacts exist
        if (collision.contacts.Length == 0) return;

        // Calculate ACTUAL relative velocity for this collision
        Vector2 myVelocity = rb.velocity;

        // Subtract atmospheric velocity to get TRUE relative velocity
        Planet nearestPlanet = FindNearestPlanet();
        if (nearestPlanet != null && CheckIfInAtmosphereOf(nearestPlanet))
        {
            Vector2 atmosphericVel = CalculateAtmosphericVelocityFor(nearestPlanet);
            myVelocity -= atmosphericVel; // This is our velocity relative to the atmosphere
        }

        // Calculate impact in RELATIVE velocity
        Vector2 otherVelocity = Vector2.zero;
        if (collision.rigidbody != null)
        {
            if (collision.gameObject.TryGetComponent<IAtmosphericObject>(out var otherAtmospheric))
            {
                // Other object is also atmospheric - need its relative velocity too
                otherVelocity = collision.rigidbody.velocity;
                if (nearestPlanet != null && CheckIfInAtmosphereOf(nearestPlanet))
                {
                    Vector2 atmosphericVel = CalculateAtmosphericVelocityFor(nearestPlanet);
                    otherVelocity -= atmosphericVel;
                }
            }
            else
            {
                otherVelocity = collision.rigidbody.velocity;
            }
        }

        Vector2 relativeImpact = myVelocity - otherVelocity;
        Vector2 impulse = relativeImpact * rb.mass;
        float impactForce = impulse.magnitude * collisionForceMultiplier;

        // Debug.Log($"[{gameObject.name}] COLLISION: relativeImpact={relativeImpact.magnitude:F2}, impactForce={impactForce:F2}");

        // Cache contact info before any operations that might clear it
        Vector2 bounceDirection = collision.contacts[0].normal;

        // Only deal damage if impact is significant
        if (impactForce < 5f)
        {
            Debug.Log($"[{gameObject.name}] Small impact, no damage");
            return;
        }

        // Debug.Log($"[{gameObject.name}] Collision impact force: {impactForce}N (relative velocity: {relativeImpact.magnitude})");
        DealForce(impactForce, bounceDirection);
    }

    public void DealForce(float force, Vector2? direction = null)
    {
        if (force >= breakThreshold - accumulatedForce)
        {
            accumulatedForce += force;
        }
        else
        {
            accumulatedForce += force * 0.5f;
        }

        Debug.Log($"Asteroid took {force}N force. Accumulated: {accumulatedForce}/{breakThreshold}N");

        atomizeThreshold -= force;

        if (accumulatedForce >= breakThreshold)
        {
            float excessForce = accumulatedForce - breakThreshold;
            Fragment(excessForce, direction);
        }
    }

    private void Fragment(float excessForce, Vector2? direction)
    {
        float currentTotalForce = breakThreshold + excessForce;

        if (currentTotalForce >= atomizeThreshold)
        {
            Debug.Log($"Asteroid atomized! Total force {currentTotalForce}N exceeded threshold {atomizeThreshold}N");
            Destroy(gameObject);
            return;
        }

        float forcePercent = (excessForce / breakThreshold) * 100f;

        int baseFragments = UnityEngine.Random.Range(2, 4);
        int bonusFragments = Mathf.FloorToInt(forcePercent / 20f);
        int fragmentCount = baseFragments + bonusFragments;

        Debug.Log($"Breaking into {fragmentCount} fragments (Excess: {forcePercent:F1}%)");

        AsteroidFragmentGenerator fragmentGenerator = new();
        fragmentGenerator.GenerateFragments(
            asteroidTexture,
            fragmentCount,
            transform.position,
            rb.velocity, // Pass ABSOLUTE velocity (fragments will calculate their own relative velocity)
            rb.angularVelocity,
            excessForce,
            direction,
            asteroidColor,
            massPerPixel,
            colliderSimplification,
            startingRotationSpeedBounds,
            toughnessMultiplier
        );

        Destroy(gameObject);
    }

    void OnDrawGizmos()
    {
        // Draw a small sphere at the asteroid's position
        // Green if in atmosphere, Red if in space
        if (IsInAtmosphere())
        {
            Gizmos.color = Color.green;
        }
        else
        {
            Gizmos.color = Color.red;
        }

        Gizmos.DrawWireSphere(transform.position, 0.1f);
    }

    // Properties
    public Vector2 MaxDimensions => maxDimensions;
    public float ShapeVariation => shapeVariation;
    public float NoiseScale => noiseScale;
    public int RandomSeed => randomSeed;
    public Color AsteroidColor => asteroidColor;
    public float MassPerPixel => massPerPixel;
    public int ColliderSimplification => colliderSimplification;

    public SpriteRenderer AsteroidSpriteRenderer
    {
        get { return asteroidSpriteRenderer; }
        set { asteroidSpriteRenderer = value; }
    }

    public Texture2D AsteroidTexture
    {
        get { return asteroidTexture; }
        set { asteroidTexture = value; }
    }

    public List<float> StartingRotationSpeedBounds => startingRotationSpeedBounds;
    public float ToughnessMultiplier => toughnessMultiplier;
}