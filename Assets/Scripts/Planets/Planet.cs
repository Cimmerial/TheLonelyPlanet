// Assets/Scripts/Planets/Planet.cs
using UnityEngine;

public class Planet : MonoBehaviour, IBigGravity
{
    [Header("Planet Properties")]
    [SerializeField] private float radius = 100f;
    [SerializeField] private float atmosphereRadius = 20f;
    [SerializeField] private float naturalRotationSpeed = 1f;
    [SerializeField] private float massPerPixel = 1f;
    
    [Header("Planet Shape Generation")]
    [SerializeField] private float shapeVariation = 0f; // 0 = perfect sphere, 0.1-0.3 = realistic imperfection
    [SerializeField] private float noiseScale = 2f; // Frequency of surface variation
    [SerializeField] private int randomSeed = 0; // 0 = random, else specific seed
    [SerializeField] private int colliderSimplification = 3; // Only used if shapeVariation > 0

    [Header("Planet Components")]
    [SerializeField] private SpriteRenderer planetSpriteRenderer;
    [SerializeField] private SpriteRenderer atmosphereSpriteRenderer;
    [SerializeField] private Collider2D planetCollider;
    [SerializeField] private Rigidbody2D planetRB;
    
    [Header("Generated Physics Data")]
    [SerializeField] private float totalMass;
    [SerializeField] private float surfaceGravity;
    [SerializeField] private float maxInfluenceRadius;

    public void SetPhysicsData(float mass, float gravity, float influence)
    {
        totalMass = mass;
        surfaceGravity = gravity;
        maxInfluenceRadius = influence;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxInfluenceRadius);
    }

    protected virtual void Awake()
    {
        // If seed is 0, generate random seed
        if (randomSeed == 0)
        {
            randomSeed = UnityEngine.Random.Range(1, 100000);
        }
        
        GeneratePlanet();
    }


    void OnEnable() => GravityManager.Register(this);
    void OnDisable() => GravityManager.Unregister(this);

    protected virtual void FixedUpdate()
    {
        // Keep rotation on the Rigidbody2D (if present) so physics/collisions remain consistent.
        if (planetRB != null)
        {
            float nextRotation = planetRB.rotation + naturalRotationSpeed * Time.fixedDeltaTime;
            planetRB.MoveRotation(nextRotation);
        }
        else
        {
            // Fallback (should be rare): rotate transform directly.
            transform.Rotate(Vector3.forward, naturalRotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void GeneratePlanet()
    {
        PlanetGenerator generator = new();
        generator.GeneratePlanet(this);
    }

    public float GetMass() => totalMass;
    public Vector3 GetPosition() => transform.position;
    public float GetMaxInfluenceRadius() => maxInfluenceRadius;

    public float Radius => radius;
    public float AtmosphereHeight => atmosphereRadius;
    public float ShapeVariation => shapeVariation;
    public float NoiseScale => noiseScale;
    public int RandomSeed => randomSeed;
    public int ColliderSimplification => colliderSimplification;
    
    public SpriteRenderer PlanetSpriteRenderer
    {
        get { return planetSpriteRenderer; }
        set { planetSpriteRenderer = value; }
    }
    public SpriteRenderer AtmosphereSpriteRenderer
    {
        get { return atmosphereSpriteRenderer; }
        set { atmosphereSpriteRenderer = value; }
    }
    public Collider2D PlanetCollider
    {
        get { return planetCollider; }
        set { planetCollider = value; }
    }
    public float MassPerPixel
    {
        get { return massPerPixel; }
        set { massPerPixel = value; }
    }
    public Rigidbody2D PlanetRB
    {
        get { return planetRB; }
        set { planetRB = value; }
    }

    public float GetAngularVelocity()
    {
        // Convert degrees/sec to radians/sec
        return naturalRotationSpeed * Mathf.Deg2Rad;
    }
    
    public float GetAtmosphereRadius()
    {
        return (radius + atmosphereRadius) / Utility.GLOBAL_PPU;
    }
    
    public float GetRadius()
    {
        return radius / Utility.GLOBAL_PPU;
    }
    void Start()
{
    Debug.Log($"[{gameObject.name}] Planet initialized:\n" +
              $"Angular Velocity (rad/s): {GetAngularVelocity():F4}\n" +
              $"Angular Velocity (deg/s): {naturalRotationSpeed:F2}\n" +
              $"Radius: {GetRadius():F2}\n" +
              $"Atmosphere Radius: {GetAtmosphereRadius():F2}\n" +
              $"Mass: {GetMass():F2}");
}
}