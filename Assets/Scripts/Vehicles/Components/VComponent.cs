// Assets/Scripts/Vehicles/Components/VComponent.cs
using UnityEngine;
using System.Collections;

public abstract class VComponent : MonoBehaviour
{
    [Header("Component Info")]
    [SerializeField] protected string componentName;
    [SerializeField] protected ComponentSlotType componentType;
    [SerializeField] protected Vehicle parentVehicle;
    [SerializeField] protected ComponentMetadata componentMetadata;

    [Header("Component Sprite")]
    [SerializeField] protected SpriteRenderer componentRenderer;
    [SerializeField] protected string componentSpriteName;
    [SerializeField] protected Vector2 centerPointPixel;
    [SerializeField] protected Vector2 attachmentDirection = Vector2.up;

    [Header("Activation")]
    [SerializeField] protected bool isActive = false;
    [SerializeField] protected float activationCost = 0f; // Future: energy/fuel cost
    [SerializeField] protected float flashDuration = 0.1f;

    private Color originalColor;
    private Coroutine flashCoroutine;

    protected virtual void Awake()
    {
        // Find or create sprite renderer as child
        Transform rendererTransform = transform.Find("ComponentRenderer");
        if (rendererTransform != null)
        {
            componentRenderer = rendererTransform.GetComponent<SpriteRenderer>();
        }
        
        if (componentRenderer != null)
        {
            originalColor = componentRenderer.color;
        }
    }

    protected virtual void Start()
    {
        // Load metadata first (contains sprite name and direction)
        LoadMetadata();
        
        // Then load the sprite
        LoadComponentSprite();
    }

    private void LoadMetadata()
    {
        // If metadata is already assigned in inspector, use it
        if (componentMetadata != null)
        {
            componentName = componentMetadata.componentName;
            componentType = componentMetadata.componentType;
            componentSpriteName = componentMetadata.spriteName;
            centerPointPixel = componentMetadata.attachmentPoint;
            attachmentDirection = componentMetadata.attachmentDirection;
            return;
        }

        // Otherwise try to load it based on component name
        if (!string.IsNullOrEmpty(componentName))
        {
            componentMetadata = Resources.Load<ComponentMetadata>($"ComponentMetadata/{componentName}_Metadata");
            if (componentMetadata != null)
            {
                componentSpriteName = componentMetadata.spriteName;
                centerPointPixel = componentMetadata.attachmentPoint;
                attachmentDirection = componentMetadata.attachmentDirection;
                Debug.Log($"Loaded metadata for {componentName}: sprite={componentSpriteName}, direction={attachmentDirection}");
            }
            else
            {
                Debug.LogWarning($"Could not find ComponentMetadata for {componentName}");
            }
        }
    }

    protected virtual void Update()
    {
        if (isActive)
        {
            UsingVComponent();
        }
    }

    // Called to activate the component
    public virtual void ActivateComponent()
    {
        if (!isActive)
        {
            isActive = true;
            StartUseVComponent();
            StartFlash();
        }
    }

    // Called to deactivate the component
    public virtual void DeactivateComponent()
    {
        if (isActive)
        {
            isActive = false;
            StopUseVComponent();
            StopFlash();
        }
    }

    // Toggle activation
    public virtual void ToggleComponent()
    {
        if (isActive)
        {
            DeactivateComponent();
        }
        else
        {
            ActivateComponent();
        }
    }

    protected virtual void StartUseVComponent()
    {
        // Override in derived classes
    }

    protected virtual void StopUseVComponent()
    {
        // Override in derived classes
    }

    protected virtual void UsingVComponent()
    {
        // Called every frame while active - override in derived classes
    }

    // Visual feedback - flash black quickly
    private void StartFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private void StopFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
        if (componentRenderer != null)
        {
            componentRenderer.color = originalColor;
        }
    }

    private IEnumerator FlashRoutine()
    {
        while (isActive)
        {
            // Flash to black
            if (componentRenderer != null)
            {
                componentRenderer.color = Color.black;
            }
            yield return new WaitForSeconds(flashDuration);

            // Back to original
            if (componentRenderer != null)
            {
                componentRenderer.color = originalColor;
            }
            yield return new WaitForSeconds(flashDuration);
        }
    }

    // Setup component with parent vehicle reference and slot data
    public virtual void SetupComponent(Vehicle vehicle, ComponentSlotData slotData)
    {
        parentVehicle = vehicle;
        
        if (slotData != null)
        {
            // Position is already set by parent (slot object position)
            
            // Calculate rotation to align component's attachment direction with slot's direction
            if (slotData.direction != Vector2.zero && attachmentDirection != Vector2.zero)
            {
                // Calculate angle between component's attachment direction and slot direction
                float componentAngle = Mathf.Atan2(attachmentDirection.y, attachmentDirection.x) * Mathf.Rad2Deg;
                float slotAngle = Mathf.Atan2(slotData.direction.y, slotData.direction.x) * Mathf.Rad2Deg;
                
                // Rotate component so its attachment direction aligns with slot direction
                // We want the component's attachment direction to point in the SAME direction as the slot
                float rotationAngle = slotAngle - componentAngle;
                
                transform.localRotation = Quaternion.Euler(0, 0, rotationAngle);
            }
        }
        
        // Load metadata if available
        if (componentMetadata != null)
        {
            componentName = componentMetadata.componentName;
            componentType = componentMetadata.componentType;
            componentSpriteName = componentMetadata.spriteName;
            centerPointPixel = componentMetadata.attachmentPoint;
            attachmentDirection = componentMetadata.attachmentDirection;
        }
    }

    // Load component sprite
    [ContextMenu("Load Component Sprite")]
    public virtual void LoadComponentSprite()
    {
        if (string.IsNullOrEmpty(componentSpriteName)) 
        {
            Debug.LogWarning($"Component sprite name not set for {gameObject.name}");
            return;
        }

        Texture2D componentTexture = Resources.Load<Texture2D>($"ComponentSprites/{componentSpriteName}");
        if (componentTexture == null)
        {
            Debug.LogWarning($"Failed to load component sprite: ComponentSprites/{componentSpriteName}");
            return;
        }

        // Create or get renderer child
        Transform rendererTransform = transform.Find("ComponentRenderer");
        GameObject rendererChild;
        
        if (rendererTransform != null)
        {
            rendererChild = rendererTransform.gameObject;
        }
        else
        {
            rendererChild = new GameObject("ComponentRenderer");
            rendererChild.transform.SetParent(transform);
            rendererChild.transform.localPosition = Vector3.zero;
            rendererChild.transform.localRotation = Quaternion.identity;
        }

        componentRenderer = rendererChild.GetComponent<SpriteRenderer>();
        if (componentRenderer == null)
        {
            componentRenderer = rendererChild.AddComponent<SpriteRenderer>();
        }

        Sprite componentSprite = Sprite.Create(
            componentTexture,
            new Rect(0, 0, componentTexture.width, componentTexture.height),
            new Vector2(0.5f, 0.5f),
            Utility.GLOBAL_PPU
        );

        componentRenderer.sprite = componentSprite;
        componentRenderer.sortingOrder = 2; // Above vehicle
        originalColor = componentRenderer.color;
        
        // Generate collider for the component on the root object
        Utility.GeneratePolygonCollider(gameObject, componentTexture, Utility.GLOBAL_PPU);
        Debug.Log($"Generated PolygonCollider2D for {componentName}");
        
        Debug.Log($"Loaded component sprite: {componentSpriteName} for {componentName}");
    }
    
    [ContextMenu("Reload Metadata")]
    public void ReloadMetadata()
    {
        LoadMetadata();
        LoadComponentSprite();
    }
}