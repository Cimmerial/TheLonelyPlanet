// Assets/Scripts/Vehicles/Vehicle.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Abstract base class for all vehicles
/// </summary>
public abstract class Vehicle : MonoBehaviour, IAtmosphericObject
{
    [System.Serializable]
    public class VehicleCameraSettings
    {
        public enum ZoomMode
        {
            SpriteSize = 0,
            DistanceToNearestGravity = 1,
        }

        [Header("Follow")]
        public bool overrideFollowSmoothness = false;
        public float followSmoothness = 5f;

        [Header("Zoom")]
        public bool overrideZoom = false;
        public ZoomMode zoomMode = ZoomMode.SpriteSize;

        // SpriteSize mode
        public float vehicleZoomRatio = 12.5f;

        // Distance mode (absolute orthographic size values)
        public float minZoom = 6f;
        public float maxZoom = 40f;
        public float distanceAtMinZoom = 5f;
        public float distanceAtMaxZoom = 40f;
    }
    public enum AutoRightingMode
    {
        Off = 0,
        GroundedInAtmosphere = 1,
        InAtmosphere = 2,
    }

    [Header("Vehicle Properties")]
    [SerializeField] protected string spriteName;
    [SerializeField] protected float massPerPixel = 1.0f;

    [Header("Auto Righting")]
    [SerializeField] private AutoRightingMode autoRightingMode = AutoRightingMode.GroundedInAtmosphere;
    [SerializeField] private float autoRightingTriggerDegrees = 5f;
    [Tooltip("Higher values right faster. Uses exponential smoothing in FixedUpdate.")]
    [SerializeField] private float autoRightingSpeed = 8f;
    [SerializeField] private bool autoRightingZeroAngularVelocity = true;

    [Header("Vehicle Chassis Config")]
    [SerializeField] protected VehicleChassisConfig chassisConfig;

    [Header("Vehicle Components")]
    [SerializeField] protected SpriteRenderer vehicleSpriteRenderer;
    [SerializeField] protected Collider2D vehicleCollider;
    [SerializeField] protected Texture2D vehicleTexture;
    [SerializeField] protected Rigidbody2D rb;
    [SerializeField] protected AtmosphericPhysics atmosphericPhysics;

    [Header("Component Management")]
    [SerializeField] protected Transform componentsParent;
    [SerializeField] protected List<Transform> primarySlots = new List<Transform>();
    [SerializeField] protected List<Transform> secondarySlots = new List<Transform>();
    [SerializeField] protected List<Transform> specializedSlots = new List<Transform>();

    [Header("Physics Data")]
    [SerializeField] protected float totalMass;

    [Header("Camera (Optional Overrides)")]
    [SerializeField] private VehicleCameraSettings cameraSettings = new VehicleCameraSettings();

    [Header("Placement Helpers")]
    [SerializeField] private bool placeOnPlanetOnStart = true;
    [SerializeField] private float hoverHeight = 0.0f;

    // IAtmosphericObject implementation
    public Vector2 GetRelativeVelocity() => atmosphericPhysics?.GetRelativeVelocity() ?? Vector2.zero;
    public Vector2 GetPosition() => transform.position;
    public bool IsInAtmosphere() => atmosphericPhysics?.IsInAtmosphere ?? false;

    public VehicleCameraSettings CameraSettings => cameraSettings;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        atmosphericPhysics = GetComponent<AtmosphericPhysics>();
        if (atmosphericPhysics == null)
        {
            atmosphericPhysics = gameObject.AddComponent<AtmosphericPhysics>();
        }

        if (!string.IsNullOrEmpty(spriteName))
        {
            LoadAndSetupVehicle();
        }

        // Setup existing components
        SetupExistingComponents();
    }

    protected virtual void Start()
    {
        if (placeOnPlanetOnStart)
        {
            // In some scene setups, planets/moons may not be ready on the first Start frame.
            // Retry briefly rather than silently failing.
            StartCoroutine(PlaceOnNearestPlanetWithRetries());
        }
    }

    private IEnumerator PlaceOnNearestPlanetWithRetries()
    {
        const int maxAttempts = 30; // ~0.5s at 60fps
        for (int i = 0; i < maxAttempts; i++)
        {
            Planet planet = atmosphericPhysics?.FindNearestPlanet();
            if (planet != null)
            {
                PlaceOnNearestPlanet();
                yield break;
            }

            yield return null;
        }

        // Last attempt (in case FindNearestPlanet was temporarily null)
        PlaceOnNearestPlanet();
    }

    protected virtual void FixedUpdate()
    {
        if (atmosphericPhysics != null)
        {
            atmosphericPhysics.ApplyAtmosphericPhysics(applyGravity: true);
        }

        UpdateVehiclePhysics();
        ApplyAutoRighting();
    }

    protected virtual void LateUpdate()
    {
        atmosphericPhysics?.DecayGroundedFrames();
    }

    protected abstract void UpdateVehiclePhysics();

    /// <summary>
    /// Smoothly rotates the vehicle towards a "correct" orientation relative to the nearest planet.
    /// Default behavior is to align transform.up to the radial (planet center -> vehicle) direction.
    /// </summary>
    protected virtual void ApplyAutoRighting()
    {
        if (autoRightingMode == AutoRightingMode.Off) return;
        if (rb == null || atmosphericPhysics == null) return;

        // Only active in atmosphere (never in space).
        if (!atmosphericPhysics.IsInAtmosphere) return;

        // Optional: only right when grounded.
        if (autoRightingMode == AutoRightingMode.GroundedInAtmosphere && !atmosphericPhysics.IsGrounded)
        {
            return;
        }

        Planet planet = atmosphericPhysics.FindNearestPlanet();
        if (planet == null) return;

        // Safety: ensure we're actually inside this planet's atmosphere.
        if (!atmosphericPhysics.CheckIfInAtmosphereOf(planet)) return;

        float targetAngle = CalculatePlanetRadialUprightAngle(planet);
        float currentAngle = rb.rotation;

        float angleError = Mathf.DeltaAngle(currentAngle, targetAngle);
        if (Mathf.Abs(angleError) <= autoRightingTriggerDegrees) return;

        // Exponential smoothing so tuning is framerate-independent.
        float dt = Time.fixedDeltaTime;
        float t = 1f - Mathf.Exp(-Mathf.Max(0f, autoRightingSpeed) * dt);

        float nextAngle = Mathf.LerpAngle(currentAngle, targetAngle, t);
        rb.MoveRotation(nextAngle);

        if (autoRightingZeroAngularVelocity)
        {
            rb.angularVelocity = 0f;
        }
    }

    protected float CalculatePlanetRadialUprightAngle(Planet planet)
    {
        if (planet == null) return rb != null ? rb.rotation : transform.eulerAngles.z;

        Vector2 fromPlanet = (Vector2)transform.position - (Vector2)planet.transform.position;
        if (fromPlanet.sqrMagnitude < 0.0001f)
        {
            return rb != null ? rb.rotation : transform.eulerAngles.z;
        }

        Vector2 radialUp = fromPlanet.normalized;
        return Mathf.Atan2(radialUp.y, radialUp.x) * Mathf.Rad2Deg - 90f;
    }

    protected virtual void LoadAndSetupVehicle()
    {
        vehicleTexture = Resources.Load<Texture2D>($"VehicleSprites/{spriteName}");

        if (vehicleTexture == null)
        {
            Debug.LogError($"Failed to load vehicle sprite: {spriteName}");
            return;
        }

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
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        totalMass = filledPixels * massPerPixel;

        // Create or reuse sprite renderer child
        Transform rendererTransform = transform.Find("VehicleRenderer");
        GameObject rendererChild;
        
        if (rendererTransform != null)
        {
            rendererChild = rendererTransform.gameObject;
        }
        else
        {
            rendererChild = new GameObject("VehicleRenderer");
            rendererChild.transform.SetParent(transform);
            rendererChild.transform.localPosition = Vector3.zero;
        }
        
        vehicleSpriteRenderer = rendererChild.GetComponent<SpriteRenderer>();
        if (vehicleSpriteRenderer == null)
        {
            vehicleSpriteRenderer = rendererChild.AddComponent<SpriteRenderer>();
        }
        vehicleSpriteRenderer.sortingOrder = 1;

        Sprite vehicleSprite = Sprite.Create(
            vehicleTexture,
            new Rect(0, 0, vehicleTexture.width, vehicleTexture.height),
            new Vector2(0.5f, 0.5f),
            Utility.GLOBAL_PPU
        );
        vehicleSpriteRenderer.sprite = vehicleSprite;

        // Create or reuse collider child
        Transform colliderTransform = transform.Find("VehicleCollider");
        GameObject colliderChild;
        
        if (colliderTransform != null)
        {
            colliderChild = colliderTransform.gameObject;
            // Clean up old box collider if present
            BoxCollider2D oldBox = colliderChild.GetComponent<BoxCollider2D>();
            if (oldBox != null) DestroyImmediate(oldBox);
        }
        else
        {
            colliderChild = new GameObject("VehicleCollider");
            colliderChild.transform.SetParent(transform);
            colliderChild.transform.localPosition = Vector3.zero;
        }

        // Generate accurate polygon collider
        vehicleCollider = Utility.GeneratePolygonCollider(
            colliderChild, 
            vehicleTexture, 
            Utility.GLOBAL_PPU, 
            2, 
            0.1f, 
            Utility.ColliderGenMode.Convex
        );

        // Update rigidbody settings (only if rb exists - might be in edit mode)
        if (rb != null)
        {
            rb.mass = totalMass;
            rb.drag = 0.1f;
            rb.angularDrag = 0.5f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        Debug.Log($"Vehicle '{spriteName}' loaded: mass={totalMass}, pixels={filledPixels}");
        Debug.Log($"Vehicle Dimensions: Texture {vehicleTexture.width}x{vehicleTexture.height}, PPU={Utility.GLOBAL_PPU}");
    }

    // COMPONENT SYSTEM METHODS

    public void ApplyChassisChanges()
    {
        Debug.Log($"Applying chassis changes for {gameObject.name}...");
        if (chassisConfig == null)
        {
            Debug.LogWarning("No chassis config assigned!");
            return;
        }

        // CRITICAL: Always reload vehicle sprite to ensure PPU/Scale changes are applied
        LoadAndSetupVehicle();
        
        if (vehicleTexture == null)
        {
            Debug.LogError("Failed to load vehicle texture! Cannot create component slots.");
            return;
        }

        // Clear existing slot objects
        ClearComponentSlots();

        // Create Components parent if it doesn't exist
        componentsParent = transform.Find("Components");
        if (componentsParent == null)
        {
            GameObject componentsObj = new GameObject("Components");
            componentsObj.transform.SetParent(transform);
            componentsObj.transform.localPosition = Vector3.zero;
            componentsParent = componentsObj.transform;
        }

        // Clear lists
        primarySlots.Clear();
        secondarySlots.Clear();
        specializedSlots.Clear();

        // Create slot objects from config
        foreach (var slotData in chassisConfig.chassisData.componentSlots)
        {
            CreateSlotObject(slotData);
        }

        Debug.Log($"Applied chassis changes: {primarySlots.Count} primary, {secondarySlots.Count} secondary, {specializedSlots.Count} specialized slots");
        
        // Trigger component loading for any existing components
        RefreshAllComponents();
    }
    
    private void RefreshAllComponents()
    {
        if (componentsParent == null) return;
        
        // Find all VComponents under the components parent
        VComponent[] components = componentsParent.GetComponentsInChildren<VComponent>(true);
        foreach (var component in components)
        {
            // Trigger reload
            component.ReloadMetadata();
        }
    }

    private void CreateSlotObject(ComponentSlotData slotData)
    {
        string slotName = $"{slotData.slotType} Slot {slotData.slotIndex}";
        
        // Check if slot already exists
        Transform existingSlot = componentsParent.Find(slotName);
        GameObject slotObj;
        
        if (existingSlot != null)
        {
            slotObj = existingSlot.gameObject;
        }
        else
        {
            slotObj = new GameObject(slotName);
            slotObj.transform.SetParent(componentsParent);
        }

        // Position slot based on pixel position
        // The slot position in the editor is in pixel coordinates relative to the canvas
        // We need to convert this to world coordinates relative to the vehicle center
        
        if (vehicleTexture != null)
        {
            // Vehicle sprite is created with pivot at (0.5, 0.5) of the texture
            // So the center of the texture is at (0, 0) in local space
            float textureCenterX = vehicleTexture.width / 2f;
            float textureCenterY = vehicleTexture.height / 2f;
            
            // Calculate offset from texture center to slot position
            // RELATIVE POSITIONING UPDATE:
            // We now treat slotData.pixelPosition as an offset relative to the vehicle center (0,0)
            // This avoids issues with trimmed sprites having different centers than the original canvas
            
            // Convert directly to world units relative to center
            Vector2 worldOffset = slotData.pixelPosition / Utility.GLOBAL_PPU;
            
            slotObj.transform.localPosition = worldOffset;
            
            Debug.Log($"Slot '{slotName}' CALCULATION: RelativePixelPos {slotData.pixelPosition} / PPU {Utility.GLOBAL_PPU} = WorldOffset {worldOffset}");
        }
        else
        {
            Debug.LogWarning($"Vehicle texture not loaded when creating slot {slotName}");
            slotObj.transform.localPosition = Vector3.zero;
        }
        
        slotObj.transform.localRotation = Quaternion.identity;

        // Add to appropriate list
        switch (slotData.slotType)
        {
            case ComponentSlotType.Primary:
                primarySlots.Add(slotObj.transform);
                break;
            case ComponentSlotType.Secondary:
                secondarySlots.Add(slotObj.transform);
                break;
            case ComponentSlotType.Specialized:
                specializedSlots.Add(slotObj.transform);
                break;
        }

        // Setup any existing component on this slot
        VComponent existingComponent = slotObj.GetComponentInChildren<VComponent>();
        if (existingComponent != null)
        {
            existingComponent.SetupComponent(this, slotData);
        }
    }

    private void ClearComponentSlots()
    {
        if (componentsParent == null) return;

        // Don't destroy components, just clear the lists
        // The slot objects will be reused or recreated
        primarySlots.Clear();
        secondarySlots.Clear();
        specializedSlots.Clear();
    }

    private void SetupExistingComponents()
    {
        // Find components parent
        componentsParent = transform.Find("Components");
        if (componentsParent == null) return;

        // Find all slot transforms and categorize them
        foreach (Transform slotTransform in componentsParent)
        {
            string slotName = slotTransform.name;
            
            if (slotName.StartsWith("Primary"))
            {
                primarySlots.Add(slotTransform);
            }
            else if (slotName.StartsWith("Secondary"))
            {
                secondarySlots.Add(slotTransform);
            }
            else if (slotName.StartsWith("Specialized"))
            {
                specializedSlots.Add(slotTransform);
            }

            // Setup components in this slot
            VComponent component = slotTransform.GetComponentInChildren<VComponent>();
            if (component != null && chassisConfig != null)
            {
                // Find matching slot data
                foreach (var slotData in chassisConfig.chassisData.componentSlots)
                {
                    string expectedName = $"{slotData.slotType} Slot {slotData.slotIndex}";
                    if (slotName == expectedName)
                    {
                        component.SetupComponent(this, slotData);
                        break;
                    }
                }
            }
        }
    }

    // Activate/deactivate all components
    public void ActivateAllComponents()
    {
        ActivateComponentsInSlots(primarySlots);
        ActivateComponentsInSlots(secondarySlots);
        ActivateComponentsInSlots(specializedSlots);
    }

    public void DeactivateAllComponents()
    {
        DeactivateComponentsInSlots(primarySlots);
        DeactivateComponentsInSlots(secondarySlots);
        DeactivateComponentsInSlots(specializedSlots);
    }

    private void ActivateComponentsInSlots(List<Transform> slots)
    {
        foreach (var slot in slots)
        {
            VComponent component = slot.GetComponentInChildren<VComponent>();
            if (component != null)
            {
                component.ActivateComponent();
            }
        }
    }

    private void DeactivateComponentsInSlots(List<Transform> slots)
    {
        foreach (var slot in slots)
        {
            VComponent component = slot.GetComponentInChildren<VComponent>();
            if (component != null)
            {
                component.DeactivateComponent();
            }
        }
    }

    // Collision handling
    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        Planet planet = collision.gameObject.GetComponent<Planet>();
        if (planet == null && collision.gameObject.transform.parent != null)
        {
            planet = collision.gameObject.transform.parent.GetComponent<Planet>();
        }

        if (planet != null)
        {
            atmosphericPhysics?.OnPlanetCollisionEnter();
        }
    }

    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
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

    protected virtual void OnCollisionExit2D(Collision2D collision)
    {
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