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

    [Header("Asteroid Mining (Phase 5)")]
    [SerializeField] private int applyAfterRemovedPixels = 25;
    [SerializeField] private float applyAfterSeconds = 0.25f;

    [NonSerialized] private byte[] resourceTypeIdPerPixel;
    [NonSerialized] private byte[] qualityBytePerPixel;
    [NonSerialized] private int resourceMapWidth;
    [NonSerialized] private int resourceMapHeight;
    [NonSerialized] private int solidPixelCount;

    [NonSerialized] private int miningTickCounter;
    [NonSerialized] private int removedSinceLastApply;
    [NonSerialized] private float lastApplyTime;

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

        // Reset mining state when regen happens.
        miningTickCounter = 0;
        removedSinceLastApply = 0;
        lastApplyTime = Time.realtimeSinceStartup;

        if (logResourceCountsOnGenerate)
        {
            var counts = GetResourceCounts();
            foreach (var kvp in counts)
            {
                Debug.Log($"[{gameObject.name}] ResourceMap: {kvp.Key}={kvp.Value}");
            }
        }
    }

    /// <summary>
    /// Removes pixels around a world-space point and returns mined units (probabilistic yield).
    /// This is the Phase 5 mining path (no fracture/collider rebuild here).
    /// </summary>
    public List<MinedResourceUnit> MineAtWorldPoint(
        Vector2 worldPoint,
        float brushRadiusWorld,
        int pixelsPerTick,
        float yieldChance01,
        out int pixelsRemoved
    )
    {
        pixelsRemoved = 0;
        List<MinedResourceUnit> mined = new();

        if (asteroidTexture == null) return mined;
        if (resourceTypeIdPerPixel == null || qualityBytePerPixel == null) return mined;

        int w = asteroidTexture.width;
        int h = asteroidTexture.height;

        // World -> local.
        Vector2 localPoint = transform.InverseTransformPoint(worldPoint);

        // Convert brush radius into local units (handles scaling).
        float localRadius = transform.InverseTransformVector(new Vector3(brushRadiusWorld, 0f, 0f)).magnitude;

        float ppu = Utility.GLOBAL_PPU;
        int cx = Mathf.RoundToInt(localPoint.x * ppu + w / 2f);
        int cy = Mathf.RoundToInt(localPoint.y * ppu + h / 2f);
        int rPix = Mathf.CeilToInt(localRadius * ppu);

        if (rPix <= 0 || pixelsPerTick <= 0) return mined;

        int xMin = Mathf.Clamp(cx - rPix, 0, w - 1);
        int xMax = Mathf.Clamp(cx + rPix, 0, w - 1);
        int yMin = Mathf.Clamp(cy - rPix, 0, h - 1);
        int yMax = Mathf.Clamp(cy + rPix, 0, h - 1);

        int r2 = rPix * rPix;

        Color32[] pixels = asteroidTexture.GetPixels32();
        List<int> candidates = new();

        for (int y = yMin; y <= yMax; y++)
        {
            int dy = y - cy;
            int dy2 = dy * dy;
            for (int x = xMin; x <= xMax; x++)
            {
                int dx = x - cx;
                if (dx * dx + dy2 > r2) continue;

                int idx = y * w + x;
                if (idx < 0 || idx >= pixels.Length) continue;

                // Solid pixel check is alpha-based.
                if (pixels[idx].a == 0) continue;

                // Cardinal edge-only check: pixel must have air on top/bottom/left/right.
                if (!IsCardinalEdgePixel(pixels, x, y, w, h)) continue;

                candidates.Add(idx);
            }
        }

        if (candidates.Count == 0) return mined;

        // Deterministic selection order per mining tick.
        int tick = miningTickCounter++;
        int selectSeed = HashSeed(randomSeed, tick, candidates.Count);
        ShuffleInPlace(candidates, new System.Random(selectSeed));

        float yieldChance = Mathf.Clamp01(yieldChance01);

        int toRemove = Mathf.Min(pixelsPerTick, candidates.Count);
        for (int i = 0; i < toRemove; i++)
        {
            int idx = candidates[i];

            // Read mined unit data before clearing.
            ResourceEnum type = (ResourceEnum)resourceTypeIdPerPixel[idx];
            byte q = qualityBytePerPixel[idx];

            // Deterministic yield check per pixel.
            int yieldSeed = HashSeed(randomSeed, tick, idx);
            System.Random yieldRng = new System.Random(yieldSeed);
            if (yieldRng.NextDouble() < yieldChance)
            {
                mined.Add(new MinedResourceUnit(type, q));
            }

            // Remove pixel: clear alpha. (Keep RGB as-is; alpha mask is what matters.)
            Color32 p = pixels[idx];
            p.a = 0;
            pixels[idx] = p;

            pixelsRemoved++;
        }

        removedSinceLastApply += pixelsRemoved;

        // Phase 7: Mining destabilization (Option B - add to accumulatedForce).
        // Each pixel removed contributes damage toward fracture threshold.
        // Formula: each pixel = (breakThreshold / solidPixelCount) × 3
        if (pixelsRemoved > 0 && solidPixelCount > 0)
        {
            float damagePerPixel = (breakThreshold / solidPixelCount) * 3f;
            float miningDamage = pixelsRemoved * damagePerPixel;
            accumulatedForce += miningDamage;

            // Update name to reflect accumulated damage.
            string prefix = isFragment ? "FRAG" : "AST";
            gameObject.name = $"{prefix} - {accumulatedForce:F0}/{breakThreshold:F0}";

            Debug.Log($"[{gameObject.name}] Mining removed {pixelsRemoved} pixels, added {miningDamage:F1}N damage ({damagePerPixel:F2}N/pixel). Accumulated: {accumulatedForce:F0}/{breakThreshold:F0}N");

            // Check if mining pushed us over the fracture threshold.
            if (accumulatedForce >= breakThreshold)
            {
                Debug.Log($"[{gameObject.name}] Mining caused fracture!");
                float excessForce = accumulatedForce - breakThreshold;
                Fragment(excessForce, new DealForceData
                {
                    forceAmount = miningDamage,
                    forceDirection = Vector2.zero, // No directional force from mining.
                    forceReturnEfficiencyPercentage = 0f,
                });
                return mined; // Asteroid is destroyed, exit early.
            }
        }

        // Remove orphaned pixels (pixels with no solid cardinal neighbors).
        RemoveOrphanedPixels(pixels, w, h);

        float now = Time.realtimeSinceStartup;
        bool shouldApply = removedSinceLastApply >= Mathf.Max(1, applyAfterRemovedPixels) || (now - lastApplyTime) >= applyAfterSeconds;

        if (shouldApply)
        {
            asteroidTexture.SetPixels32(pixels);
            asteroidTexture.Apply();
            removedSinceLastApply = 0;
            lastApplyTime = now;

            // Optional: re-bake newly exposed pixels (not needed since we preserve RGB).
            // If you later implement mask-only texture changes, you can rebake here.
        }
        else
        {
            // We still need to store modifications for later Apply().
            asteroidTexture.SetPixels32(pixels);
        }

        return mined;
    }

    /// <summary>
    /// Check if a pixel is on a cardinal edge (has air on top/bottom/left/right).
    /// </summary>
    private static bool IsCardinalEdgePixel(Color32[] pixels, int x, int y, int w, int h)
    {
        // Check only 4-connected (cardinal) neighbors: up, down, left, right.
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { -1, 1, 0, 0 };

        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];

            // Out of bounds counts as transparent (air).
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) return true;

            int nIdx = ny * w + nx;
            if (nIdx < 0 || nIdx >= pixels.Length) return true;

            // Transparent neighbor found (air).
            if (pixels[nIdx].a == 0) return true;
        }

        return false; // No cardinal air neighbors = interior pixel.
    }

    /// <summary>
    /// Remove orphaned pixels (pixels with no solid cardinal neighbors).
    /// </summary>
    private static void RemoveOrphanedPixels(Color32[] pixels, int w, int h)
    {
        // Find all orphaned pixels first (don't modify while iterating).
        List<int> orphans = new();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (pixels[idx].a == 0) continue; // Already transparent.

                // Check if this pixel has ANY solid cardinal neighbor.
                bool hasSolidNeighbor = false;
                int[] dx = { 0, 0, -1, 1 };
                int[] dy = { -1, 1, 0, 0 };

                for (int i = 0; i < 4; i++)
                {
                    int nx = x + dx[i];
                    int ny = y + dy[i];

                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                    int nIdx = ny * w + nx;
                    if (nIdx < 0 || nIdx >= pixels.Length) continue;

                    if (pixels[nIdx].a > 0) // Solid neighbor found.
                    {
                        hasSolidNeighbor = true;
                        break;
                    }
                }

                if (!hasSolidNeighbor)
                {
                    orphans.Add(idx);
                }
            }
        }

        // Remove orphaned pixels.
        foreach (int idx in orphans)
        {
            Color32 p = pixels[idx];
            p.a = 0;
            pixels[idx] = p;
        }
    }

    private static void ShuffleInPlace(List<int> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static int HashSeed(int a, int b, int c)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + a;
            h = h * 31 + b;
            h = h * 31 + c;
            h ^= (h << 13);
            h ^= (h >> 17);
            h ^= (h << 5);
            return h;
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
