// Assets/Scripts/Planets/Asteroid.cs (REFACTORED)
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
    [SerializeField] private AtmosphericPhysics atmosphericPhysics;

    [Header("Generated Physics Data")]
    [SerializeField] private float totalMass;
    [SerializeField] private float assignedMass;
    [SerializeField] private float breakThreshold;
    [SerializeField] private float accumulatedForce = 0f;
    [SerializeField] private float atomizeThreshold;
    [SerializeField] private bool isFragment = false;

    // IAtmosphericObject implementation - delegate to AtmosphericPhysics
    public Vector2 GetRelativeVelocity() => atmosphericPhysics?.GetRelativeVelocity() ?? Vector2.zero;
    public Vector2 GetPosition() => transform.position;
    public bool IsInAtmosphere() => atmosphericPhysics?.IsInAtmosphere ?? false;

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
        set { asteroidCollider = value; }
    }

    [Header("Orbital Setup")]
    [SerializeField] private bool startInOrbit = false;
    [SerializeField] private Transform orbitTarget;

    [Header("Fragmentation Settings")]
    [SerializeField] private float toughnessMultiplier = 1f;
    [SerializeField] private float collisionForceMultiplier = 0.33f;
    [SerializeField] private float collisionGracePeriod = 0.2f;

    private float spawnTime;

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

        // Add AtmosphericPhysics component if it doesn't exist
        atmosphericPhysics = GetComponent<AtmosphericPhysics>();
        if (atmosphericPhysics == null)
        {
            atmosphericPhysics = gameObject.AddComponent<AtmosphericPhysics>();
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

            rb.velocity = orbitalDirection * orbitalSpeed;
            Debug.Log($"[{gameObject.name}] Starting - set absolute velocity={rb.velocity.magnitude:F2}");
        }
    }

    void FixedUpdate()
    {
        if (rb == null || atmosphericPhysics == null) return;

        // SIMPLIFIED: Just delegate to AtmosphericPhysics!
        atmosphericPhysics.ApplyAtmosphericPhysics(applyGravity: true);
    }

    void LateUpdate()
    {
        // Decay grounded frames
        atmosphericPhysics?.DecayGroundedFrames();
    }

    public void ApplyGravity(Vector2 gravityAcceleration)
    {
        if (rb == null) return;
        rb.velocity += gravityAcceleration * Time.fixedDeltaTime;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<Planet>(out _))
        {
            atmosphericPhysics?.OnPlanetCollisionStay();
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<Planet>(out _))
        {
            atmosphericPhysics?.OnPlanetCollisionExit();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (Time.time - spawnTime < collisionGracePeriod) return;

        // Mark as grounded
        if (collision.gameObject.TryGetComponent<Planet>(out _))
        {
            atmosphericPhysics?.OnPlanetCollisionEnter();
        }

        if (collision.contacts.Length == 0) return;

        // Calculate relative velocity for collision
        Vector2 myVelocity = rb.velocity;

        // Use AtmosphericPhysics to find nearest planet and calculate atmospheric velocity
        Planet nearestPlanet = atmosphericPhysics.FindNearestPlanet();
        if (nearestPlanet != null && atmosphericPhysics.CheckIfInAtmosphereOf(nearestPlanet))
        {
            Vector2 atmosphericVel = atmosphericPhysics.CalculateAtmosphericVelocityFor(nearestPlanet);
            myVelocity -= atmosphericVel;
        }

        Vector2 otherVelocity = Vector2.zero;
        if (collision.rigidbody != null)
        {
            if (collision.gameObject.TryGetComponent<IAtmosphericObject>(out var otherAtmospheric))
            {
                otherVelocity = collision.rigidbody.velocity;
                if (nearestPlanet != null && atmosphericPhysics.CheckIfInAtmosphereOf(nearestPlanet))
                {
                    Vector2 atmosphericVel = atmosphericPhysics.CalculateAtmosphericVelocityFor(nearestPlanet);
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

        Vector2 bounceDirection = collision.contacts[0].normal;

        if (impactForce < 5f)
        {
            return;
        }

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
            rb.velocity,
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