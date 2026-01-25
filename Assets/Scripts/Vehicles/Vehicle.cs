// Assets/Scripts/Vehicles/Vehicle.cs
using UnityEngine;

/// <summary>
/// Abstract base class for all vehicles
/// </summary>
public abstract class Vehicle : MonoBehaviour, IAtmosphericObject
{
    [Header("Vehicle Properties")]
    [SerializeField] protected string spriteName;
    [SerializeField] protected float massPerPixel = 0.1f;
    
    [Header("Vehicle Components")]
    [SerializeField] protected SpriteRenderer vehicleSpriteRenderer;
    [SerializeField] protected Collider2D vehicleCollider;
    [SerializeField] protected Texture2D vehicleTexture;
    [SerializeField] protected Rigidbody2D rb;
    [SerializeField] protected AtmosphericPhysics atmosphericPhysics;
    
    [Header("Physics Data")]
    [SerializeField] protected float totalMass;
    
    // IAtmosphericObject implementation
    public Vector2 GetRelativeVelocity() => atmosphericPhysics?.GetRelativeVelocity() ?? Vector2.zero;
    public Vector2 GetPosition() => transform.position;
    public bool IsInAtmosphere() => atmosphericPhysics?.IsInAtmosphere ?? false;
    
    protected virtual void Awake()
    {
        // Create Rigidbody2D FIRST (before AtmosphericPhysics needs it)
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        
        // Now safe to add AtmosphericPhysics (it can find the RB)
        atmosphericPhysics = GetComponent<AtmosphericPhysics>();
        if (atmosphericPhysics == null)
        {
            atmosphericPhysics = gameObject.AddComponent<AtmosphericPhysics>();
        }
        
        if (!string.IsNullOrEmpty(spriteName))
        {
            LoadAndSetupVehicle();
        }
    }
    
    protected virtual void Start()
    {
        // Override in subclasses for specific initialization
    }
    
    protected virtual void FixedUpdate()
    {
        // Apply atmospheric physics
        if (atmosphericPhysics != null)
        {
            atmosphericPhysics.ApplyAtmosphericPhysics(applyGravity: true);
        }
        
        // Subclass-specific physics
        UpdateVehiclePhysics();
    }
    
    protected virtual void LateUpdate()
    {
        atmosphericPhysics?.DecayGroundedFrames();
    }
    
    /// <summary>
    /// Override this for vehicle-specific physics (movement, etc.)
    /// </summary>
    protected abstract void UpdateVehiclePhysics();
    
    /// <summary>
    /// Load sprite from Resources and set up vehicle
    /// </summary>
    protected virtual void LoadAndSetupVehicle()
    {
        // Load texture from Resources/VehicleSprites
        vehicleTexture = Resources.Load<Texture2D>($"VehicleSprites/{spriteName}");
        
        if (vehicleTexture == null)
        {
            Debug.LogError($"Failed to load vehicle sprite: {spriteName}");
            return;
        }
        
        // Count filled pixels for mass calculation
        int filledPixels = 0;
        for (int y = 0; y < vehicleTexture.height; y++)
        {
            for (int x = 0; x < vehicleTexture.width; x++)
            {
                if (vehicleTexture.GetPixel(x, y).a > 0.1f)
                {
                    filledPixels++;
                }
            }
        }
        
        totalMass = filledPixels * massPerPixel;
        
        // Create sprite renderer child
        GameObject rendererChild = new GameObject("VehicleRenderer");
        rendererChild.transform.SetParent(transform);
        rendererChild.transform.localPosition = Vector3.zero;
        vehicleSpriteRenderer = rendererChild.AddComponent<SpriteRenderer>();
        vehicleSpriteRenderer.sortingOrder = 1; // Above asteroids
        
        // Create sprite
        Sprite vehicleSprite = Sprite.Create(
            vehicleTexture,
            new Rect(0, 0, vehicleTexture.width, vehicleTexture.height),
            new Vector2(0.5f, 0.5f),
            Utility.GLOBAL_PPU
        );
        vehicleSpriteRenderer.sprite = vehicleSprite;
        
        // Create collider child
        GameObject colliderChild = new GameObject("VehicleCollider");
        colliderChild.transform.SetParent(transform);
        colliderChild.transform.localPosition = Vector3.zero;
        
        vehicleCollider = Utility.GeneratePolygonCollider(
            colliderChild,
            vehicleTexture,
            Utility.GLOBAL_PPU,
            0, // No simplification - tight shrink-wrap collider
            0.1f
        );
        
        // Update rigidbody mass (already created in Awake)
        rb.mass = totalMass;
        
        Debug.Log($"Vehicle '{spriteName}' loaded: mass={totalMass}, pixels={filledPixels}");
    }
    
    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<Planet>(out _))
        {
            atmosphericPhysics?.OnPlanetCollisionEnter();
        }
    }
    
    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<Planet>(out _))
        {
            atmosphericPhysics?.OnPlanetCollisionStay();
        }
    }
    
    protected virtual void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<Planet>(out _))
        {
            atmosphericPhysics?.OnPlanetCollisionExit();
        }
    }
}
