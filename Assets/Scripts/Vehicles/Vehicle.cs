using UnityEngine;

/// <summary>
/// Abstract base class for all vehicles
/// </summary>
public abstract class Vehicle : MonoBehaviour, IAtmosphericObject
{
    [Header("Vehicle Properties")]
    [SerializeField] protected string spriteName;
    [SerializeField] protected float massPerPixel = 1.0f; // Reasonable mass (same as asteroids)

    [Header("Vehicle Components")]
    [SerializeField] protected SpriteRenderer vehicleSpriteRenderer;
    [SerializeField] protected Collider2D vehicleCollider;
    [SerializeField] protected Texture2D vehicleTexture;
    [SerializeField] protected Rigidbody2D rb;
    [SerializeField] protected AtmosphericPhysics atmosphericPhysics;

    [Header("Physics Data")]
    [SerializeField] protected float totalMass;

    [Header("Placement Helpers")]
    [SerializeField] private bool placeOnPlanetOnStart = true;
    [SerializeField] private float hoverHeight = 0.0f;

    // IAtmosphericObject implementation
    public Vector2 GetRelativeVelocity() => atmosphericPhysics?.GetRelativeVelocity() ?? Vector2.zero;
    public Vector2 GetPosition() => transform.position;
    public bool IsInAtmosphere() => atmosphericPhysics?.IsInAtmosphere ?? false;


    protected virtual void Awake()
    {
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
        if (placeOnPlanetOnStart) PlaceOnNearestPlanet();
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

    protected virtual void LoadAndSetupVehicle()
    {
        // Load texture from Resources/VehicleSprites
        vehicleTexture = Resources.Load<Texture2D>($"VehicleSprites/{spriteName}");

        if (vehicleTexture == null)
        {
            Debug.LogError($"Failed to load vehicle sprite: {spriteName}");
            return;
        }

        // EDITED: Find bounding box of visible pixels (alpha > 0)
        int minX = vehicleTexture.width;
        int maxX = 0;
        int minY = vehicleTexture.height;
        int maxY = 0;
        int filledPixels = 0;

        for (int y = 0; y < vehicleTexture.height; y++)
        {
            for (int x = 0; x < vehicleTexture.width; x++)
            {
                if (vehicleTexture.GetPixel(x, y).a > 0.1f)
                {
                    filledPixels++;

                    // ADDED: Track bounding box
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
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

        // Create collider on CHILD GameObject (like asteroids)
        GameObject colliderChild = new GameObject("VehicleCollider");
        colliderChild.transform.SetParent(transform);
        colliderChild.transform.localPosition = Vector3.zero;

        BoxCollider2D boxCollider = colliderChild.AddComponent<BoxCollider2D>();

        // EDITED: Calculate size based on bounding box of visible pixels
        float pixelWidth = (maxX - minX + 1);
        float pixelHeight = (maxY - minY + 1);
        float width = pixelWidth / Utility.GLOBAL_PPU;
        float height = pixelHeight / Utility.GLOBAL_PPU;

        // EDITED: Calculate offset (bounding box center relative to sprite center)
        float centerX = vehicleTexture.width / 2f;
        float centerY = vehicleTexture.height / 2f;
        float boundingCenterX = (minX + maxX) / 2f;
        float boundingCenterY = (minY + maxY) / 2f;
        float offsetX = (boundingCenterX - centerX) / Utility.GLOBAL_PPU;
        float offsetY = (boundingCenterY - centerY) / Utility.GLOBAL_PPU;

        boxCollider.size = new Vector2(width, height);
        boxCollider.offset = new Vector2(offsetX, offsetY);

        vehicleCollider = boxCollider;

        // Use same friction material as asteroids
        vehicleCollider.sharedMaterial = Utility.GetFrictionMaterial();

        // Update rigidbody mass (already created in Awake)
        rb.mass = totalMass;

        // Match asteroid physics settings exactly
        rb.drag = 0.1f;
        rb.angularDrag = 0.5f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        Debug.Log($"Vehicle '{spriteName}' loaded: mass={totalMass}, pixels={filledPixels}, collider size={boxCollider.size}, offset={boxCollider.offset}, bounds=[{minX},{minY}] to [{maxX},{maxY}]");
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        // Check both the collision GameObject and its parent for Planet component
        Planet planet = collision.gameObject.GetComponent<Planet>();
        if (planet == null && collision.gameObject.transform.parent != null)
        {
            planet = collision.gameObject.transform.parent.GetComponent<Planet>();
        }

        if (planet != null)
        {
            atmosphericPhysics?.OnPlanetCollisionEnter();
            Debug.Log($"[{gameObject.name}] CollisionEnter with planet - grounded={atmosphericPhysics?.IsGrounded}");
        }
    }

    protected virtual void OnCollisionStay2D(Collision2D collision)
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
            // Don't log - too spammy
        }
    }

    protected virtual void OnCollisionExit2D(Collision2D collision)
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
            Debug.Log($"[{gameObject.name}] CollisionExit with planet - grounded={atmosphericPhysics?.IsGrounded}");
        }
    }

    [ContextMenu("Place On Nearest Planet")]
    public void PlaceOnNearestPlanet()
    {
        Planet planet = atmosphericPhysics?.FindNearestPlanet();
        if (planet == null)
        {
            Debug.LogWarning("No planet found to place vehicle on");
            return;
        }

        Vector2 currentPos = transform.position;
        Vector2 toPlanet = (Vector2)planet.transform.position - currentPos;
        Vector2 directionFromPlanet = -toPlanet.normalized;

        float planetRadius = planet.GetRadius();
        Vector2 surfacePosition = (Vector2)planet.transform.position + directionFromPlanet * planetRadius;
        Vector2 spawnPosition = surfacePosition + directionFromPlanet * hoverHeight;

        transform.position = spawnPosition;

        float angle = Mathf.Atan2(directionFromPlanet.y, directionFromPlanet.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.rotation = angle;

        if (atmosphericPhysics != null)
        {
            Vector2 atmosphericVelocity = atmosphericPhysics.CalculateAtmosphericVelocityFor(planet);
            rb.velocity = atmosphericVelocity;
            rb.angularVelocity = 0f;
            rb.WakeUp();
        }

        Debug.Log($"Placed {gameObject.name} on {planet.name}");
    }
}
