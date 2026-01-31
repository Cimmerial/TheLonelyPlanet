// Assets/Scripts/Planets/Asteroid.cs (REFACTORED)
using System;
using System.Collections.Generic;
using UnityEngine;

public class Asteroid : MonoBehaviour, IGravityAffectable, IAtmosphericObject, IBreakable
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
    [SerializeField] private float baseMassLossPercentage = 0.1f;
    [SerializeField] private Dictionary<float, ResourceEnum> asteroidComposition = new();
    [SerializeField] private int asteroidResourceCount = 0;

    [Header("Asteroid Resources (Phase 3)")]
    [SerializeField] private AsteroidResourceProfilePreset resourceProfilePreset;
    [SerializeField] private ResourceQualityConfig qualityConfig;
    [SerializeField] private bool logResourceCountsOnGenerate = false;

    [NonSerialized] private byte[] resourceTypeIdPerPixel;
    [NonSerialized] private byte[] qualityBytePerPixel;
    [NonSerialized] private int resourceMapWidth;
    [NonSerialized] private int resourceMapHeight;
    [NonSerialized] private int solidPixelCount;

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

    public void SetResourceMap(byte[] resourceTypeIdPerPixel, byte[] qualityBytePerPixel, int width, int height, int solidPixelCount)
    {
        this.resourceTypeIdPerPixel = resourceTypeIdPerPixel;
        this.qualityBytePerPixel = qualityBytePerPixel;
        this.resourceMapWidth = width;
        this.resourceMapHeight = height;
        this.solidPixelCount = solidPixelCount;

        if (logResourceCountsOnGenerate)
        {
            var counts = GetResourceCounts();
            foreach (var kvp in counts)
            {
                Debug.Log($"[{gameObject.name}] ResourceMap: {kvp.Key}={kvp.Value}");
            }
        }
    }

    public Dictionary<ResourceEnum, int> GetResourceCounts()
    {
        Dictionary<ResourceEnum, int> counts = new();
        if (resourceTypeIdPerPixel == null || asteroidTexture == null) return counts;

        Color32[] pixels = asteroidTexture.GetPixels32();
        int n = Mathf.Min(pixels.Length, resourceTypeIdPerPixel.Length);

        for (int i = 0; i < n; i++)
        {
            if (pixels[i].a == 0) continue;

            ResourceEnum type = (ResourceEnum)resourceTypeIdPerPixel[i];
            if (!counts.TryGetValue(type, out int c)) c = 0;
            counts[type] = c + 1;
        }

        return counts;
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
        // Check both the collision GameObject and its parent for Planet component
        Planet planet = collision.gameObject.GetComponent<Planet>();
        if (planet == null && collision.gameObject.transform.parent != null)
        {
            planet = collision.gameObject.transform.parent.GetComponent<Planet>();
        }

        if (planet != null)
        {
            atmosphericPhysics?.OnPlanetCollisionStay();
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        // Check both the collision GameObject and its parent for Planet component
        Planet planet = collision.gameObject.GetComponent<Planet>();
        if (planet == null && collision.gameObject.transform.parent != null)
        {
            planet = collision.gameObject.transform.parent.GetComponent<Planet>();
        }

        if (planet != null)
        {
            atmosphericPhysics?.OnPlanetCollisionExit();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (Time.time - spawnTime < collisionGracePeriod) return;

        // Check both the collision GameObject and its parent for Planet component
        Planet planet = collision.gameObject.GetComponent<Planet>();
        if (planet == null && collision.gameObject.transform.parent != null)
        {
            planet = collision.gameObject.transform.parent.GetComponent<Planet>();
        }

        // Mark as grounded
        if (planet != null)
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

        DealForce(
            new DealForceData
            {
                forceAmount = impactForce,
                forceDirection = bounceDirection,
                forceReturnEfficiencyPercentage = 0,
            }
        );
    }

    public BrokenResourceData TakeForceDamage(DealForceData data) => DealForce(data);

    public BrokenResourceData DealForce(DealForceData data)
    {
        // ADDED: Notify OrbitalRails if this asteroid is on rails
        float force = data.forceAmount;
        Vector2 direction = data.forceDirection;
        OrbitalRails rails = GetComponent<OrbitalRails>();
        if (rails != null)
        {
            rails.ApplyImpactForce(force);

            // If orbit was broken, we still want to accumulate damage!
            if (!rails.IsRailed)
            {
                Debug.Log($"[{gameObject.name}] Orbit broken by {force}N impact! Asteroid is now free-flying.");
                // Removed early return so damage continues to process
            }
        }

        // Original damage accumulation logic
        if (force >= breakThreshold - accumulatedForce) accumulatedForce += force;
        else accumulatedForce += force * 0.5f;
        
        // Update Name
        string prefix = isFragment ? "FRAG" : "AST";
        gameObject.name = $"{prefix} - {accumulatedForce:F0}/{breakThreshold:F0}";

        // Debug.Log($"Asteroid took {force}N force. Accumulated: {accumulatedForce}/{breakThreshold}N");

        atomizeThreshold -= force;

        if (accumulatedForce >= breakThreshold)
        {
            float excessForce = accumulatedForce - breakThreshold;
            return Fragment(excessForce, data);
        }
        return null;
    }

    private BrokenResourceData Fragment(float excessForce, DealForceData data)
    {
        float currentTotalForce = breakThreshold + excessForce;
        Vector3 direction = data.forceDirection.normalized; // normalized for now

        if (currentTotalForce >= atomizeThreshold)
        {
            Debug.Log($"Asteroid atomized! Total force {currentTotalForce}N exceeded threshold {atomizeThreshold}N");
            Destroy(gameObject);
            return null; // TODO: return resources, hmm or not.
        }

        float forcePercent = (excessForce / breakThreshold) * 100f;

        int baseFragments = UnityEngine.Random.Range(2, 4);
        int bonusFragments = Mathf.FloorToInt(forcePercent / 20f);
        int fragmentCount = baseFragments + bonusFragments;
        float massReductionPercentage = baseMassLossPercentage + (Mathf.FloorToInt(forcePercent / 20f) * 0.01f);

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
            toughnessMultiplier,
            massReductionPercentage
        );

        Destroy(gameObject);

        return new BrokenResourceData
        {
            brokenResources = ResourcesFromFragmentedAsteroid(massReductionPercentage, data),
        };
    }

    private List<ResourceEnum> ResourcesFromFragmentedAsteroid(float percentage, DealForceData data)
    {
        if (data.forceReturnEfficiencyPercentage == 0) return null;

        List<ResourceEnum> fragmentedResources = new List<ResourceEnum>();

        float totalToExtract = asteroidResourceCount * percentage * data.forceReturnEfficiencyPercentage;

        foreach (var entry in asteroidComposition)
        {
            float resourceRatio = entry.Key;
            ResourceEnum resourceEnumData = entry.Value;

            int amountToAdd = Mathf.FloorToInt(totalToExtract * resourceRatio);

            for (int i = 0; i < amountToAdd; i++) fragmentedResources.Add(resourceEnumData);
        }

        return fragmentedResources;
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

    public AsteroidResourceProfilePreset ResourceProfilePreset => resourceProfilePreset;
    public ResourceQualityConfig QualityConfig => qualityConfig;

    public byte[] ResourceTypeIdPerPixel => resourceTypeIdPerPixel;
    public byte[] QualityBytePerPixel => qualityBytePerPixel;
    public int ResourceMapWidth => resourceMapWidth;
    public int ResourceMapHeight => resourceMapHeight;
    public int SolidPixelCount => solidPixelCount;

    public Texture2D AsteroidTexture
    {
        get { return asteroidTexture; }
        set { asteroidTexture = value; }
    }

    public List<float> StartingRotationSpeedBounds => startingRotationSpeedBounds;
    public float ToughnessMultiplier => toughnessMultiplier;
    public float BreakThreshold => breakThreshold;
    public float AccumulatedForce => accumulatedForce;

    /// <summary>
    /// Editor/debug helper. Rerolls this asteroid's generation seed.
    /// </summary>
    public void RandomizeSeed()
    {
        randomSeed = UnityEngine.Random.Range(1, 100000);
    }
}
