// Assets/Scripts/Camera/CameraController.cs
using UnityEngine;
using TMPro;

public class CameraController : MonoBehaviour
{
    private enum CameraMode { Free, LockedToPlanet, LockedToVehicle }
    private CameraMode currentMode = CameraMode.Free;
    
    private Planet lockedPlanet;
    private Vehicle lockedVehicle;
    
    [Header("Camera Settings")]
    [SerializeField] private float freeMoveSpeed = 10f;
    [SerializeField] private float freeZoomSpeed = 5f;
    [SerializeField] private float followSmoothness = 5f;
    [SerializeField] private bool matchRotation = false; 
    
    [Header("Zoom Ratios")]
    [SerializeField] private float planetZoomRatio = 200f; // 1 camera size per 200px radius
    [SerializeField] private float vehicleZoomRatio = 50f; // 1 camera size per 50px sprite size
    
    [Header("UI")]
    [SerializeField] private GameObject uiCanvas;
    [SerializeField] private TextMeshProUGUI statusText;
    
    private Camera cam;
    private float targetZoom;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = gameObject.AddComponent<Camera>();
        }
        
        targetZoom = cam.orthographicSize;
        
        // Create UI if not assigned
        if (uiCanvas == null)
        {
            CreateUI();
        }
    }

    private void Update()
    {
        HandleInput();
        UpdateCamera();
        UpdateUI();
    }
    private void HandleInput()
{
    // Lock to planet
    if (Input.GetKeyDown(KeyCode.P))
    {
        Planet nearest = FindNearestPlanet();
        if (nearest != null)
        {
            LockToPlanet(nearest);
        }
    }
    
    // Lock to vehicle
    if (Input.GetKeyDown(KeyCode.V))
    {
        Vehicle nearest = FindNearestVehicleToCursor();
        if (nearest != null)
        {
            LockToVehicle(nearest);
        }
    }
    
    // Unlock (free camera)
    if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F))
    {
        UnlockCamera();
    }
    
    // ADDED: Toggle rotation matching
    if (Input.GetKeyDown(KeyCode.T))
    {
        matchRotation = !matchRotation;
        Debug.Log($"Camera rotation matching: {(matchRotation ? "ON" : "OFF")}");
    }
    
    // Free camera movement (only when not locked)
    if (currentMode == CameraMode.Free)
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 movement = new Vector3(horizontal, vertical, 0) * freeMoveSpeed * Time.deltaTime;
        transform.position += movement;
        
        // Free zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetZoom -= scroll * freeZoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, 1f, 50f);
        }
    }
}

private void UpdateCamera()
{
    switch (currentMode)
    {
        case CameraMode.LockedToPlanet:
            if (lockedPlanet != null)
            {
                // Follow planet
                Vector3 targetPos = new Vector3(
                    lockedPlanet.transform.position.x,
                    lockedPlanet.transform.position.y,
                    transform.position.z
                );
                transform.position = Vector3.Lerp(transform.position, targetPos, followSmoothness * Time.deltaTime);
                
                // ADDED: Match planet rotation if enabled
                if (matchRotation)
                {
                    Quaternion targetRot = Quaternion.Euler(0, 0, lockedPlanet.transform.eulerAngles.z);
                    transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, followSmoothness * Time.deltaTime);
                }
                else
                {
                    // Reset to upright
                    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, followSmoothness * Time.deltaTime);
                }
                
                // Calculate zoom based on planet radius
                float planetRadius = lockedPlanet.Radius;
                targetZoom = planetRadius / planetZoomRatio;
            }
            else
            {
                UnlockCamera();
            }
            break;
            
        case CameraMode.LockedToVehicle:
            if (lockedVehicle != null)
            {
                // Follow vehicle
                Vector3 targetPos = new Vector3(
                    lockedVehicle.transform.position.x,
                    lockedVehicle.transform.position.y,
                    transform.position.z
                );
                transform.position = Vector3.Lerp(transform.position, targetPos, followSmoothness * Time.deltaTime);
                
                // ADDED: Match vehicle rotation if enabled
                if (matchRotation)
                {
                    Quaternion targetRot = Quaternion.Euler(0, 0, lockedVehicle.transform.eulerAngles.z);
                    transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, followSmoothness * Time.deltaTime);
                }
                else
                {
                    // Reset to upright
                    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, followSmoothness * Time.deltaTime);
                }
                
                // Calculate zoom based on vehicle sprite size
                float spriteSize = GetVehicleSpriteSize(lockedVehicle);
                targetZoom = spriteSize / vehicleZoomRatio;
                
                Debug.Log($"Vehicle sprite size: {spriteSize}px, ratio: {vehicleZoomRatio}, targetZoom: {targetZoom}");
            }
            else
            {
                UnlockCamera();
            }
            break;
            
        case CameraMode.Free:
            // ADDED: Reset rotation in free mode
            if (!matchRotation || transform.rotation != Quaternion.identity)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, followSmoothness * Time.deltaTime);
            }
            break;
    }
    
    // Smooth zoom
    cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, followSmoothness * Time.deltaTime);
}

private void UpdateUI()
{
    if (statusText == null) return;
    
    string text = "";
    
    switch (currentMode)
    {
        case CameraMode.Free:
            Planet nearestPlanet = FindNearestPlanet();
            Vehicle nearestVehicle = FindNearestVehicleToCursor();
            
            text = "<b>FREE CAMERA</b>\n";
            
            if (nearestPlanet != null)
            {
                text += $"[P] Follow <color=#FFD700>{nearestPlanet.name}</color>\n";
            }
            
            if (nearestVehicle != null)
            {
                text += $"[V] Follow <color=#00FF00>{nearestVehicle.name}</color>\n";
            }
            
            text += "\n[Arrow Keys] Move\n[Mouse Wheel] Zoom";
            break;
            
        case CameraMode.LockedToPlanet:
            text = $"<b>FOLLOWING PLANET</b>\n";
            text += $"<color=#FFD700>{lockedPlanet.name}</color>\n";
            text += $"\nRadius: {lockedPlanet.Radius:F0}px\n";
            text += $"Zoom: {cam.orthographicSize:F2}\n";
            text += $"Rotation Match: <color={(matchRotation ? "#00FF00>ON" : "#FF0000>OFF")}</color>\n"; // ADDED
            text += "\n[F] Free Camera\n[V] Follow Vehicle\n[T] Toggle Rotation"; // ADDED
            break;
            
        case CameraMode.LockedToVehicle:
            text = $"<b>FOLLOWING VEHICLE</b>\n";
            text += $"<color=#00FF00>{lockedVehicle.name}</color>\n";
            text += $"\nSpeed: {lockedVehicle.GetComponent<Rigidbody2D>()?.velocity.magnitude:F2}\n";
            text += $"Zoom: {cam.orthographicSize:F2}\n";
            text += $"Rotation Match: <color={(matchRotation ? "#00FF00>ON" : "#FF0000>OFF")}</color>\n"; // ADDED
            text += "\n[F] Free Camera\n[P] Follow Planet\n[T] Toggle Rotation"; // ADDED
            break;
    }
    
    statusText.text = text;
}

private float GetVehicleSpriteSize(Vehicle vehicle)
{
    // EDITED: Get the actual sprite bounds size
    SpriteRenderer sr = vehicle.GetComponentInChildren<SpriteRenderer>(); // EDITED
    if (sr != null && sr.sprite != null) // EDITED
    { // EDITED
        // EDITED: Return the max dimension of the sprite in pixels
        float maxPixels = Mathf.Max(sr.sprite.texture.width, sr.sprite.texture.height); // EDITED
        Debug.Log($"Sprite texture size: {sr.sprite.texture.width}x{sr.sprite.texture.height}, max: {maxPixels}"); // EDITED
        return maxPixels; // EDITED
    } // EDITED
    
    Debug.LogWarning($"Could not get sprite size for {vehicle.name}, using default"); // EDITED
    return 50f; // Default fallback
}

    private void LockToPlanet(Planet planet)
    {
        currentMode = CameraMode.LockedToPlanet;
        lockedPlanet = planet;
        lockedVehicle = null;
        
        Debug.Log($"Camera locked to planet: {planet.name}");
    }

    private void LockToVehicle(Vehicle vehicle)
    {
        currentMode = CameraMode.LockedToVehicle;
        lockedVehicle = vehicle;
        lockedPlanet = null;
        
        Debug.Log($"Camera locked to vehicle: {vehicle.name}");
    }

    private void UnlockCamera()
    {
        currentMode = CameraMode.Free;
        lockedPlanet = null;
        lockedVehicle = null;
        
        Debug.Log("Camera unlocked - Free mode");
    }

    private Planet FindNearestPlanet()
    {
        Planet[] planets = FindObjectsOfType<Planet>();
        Planet nearest = null;
        float minDist = float.MaxValue;
        
        foreach (Planet planet in planets)
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

    private Vehicle FindNearestVehicleToCursor()
    {
        Vector3 mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        
        Vehicle[] vehicles = FindObjectsOfType<Vehicle>();
        Vehicle nearest = null;
        float minDist = float.MaxValue;
        
        foreach (Vehicle vehicle in vehicles)
        {
            float dist = Vector2.Distance(mousePos, vehicle.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = vehicle;
            }
        }
        
        return nearest;
    }

    private void CreateUI()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("CameraUI");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        uiCanvas = canvasObj;
        
        // Create background panel
        GameObject panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(canvasObj.transform);
        
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(0, 0);
        panelRect.pivot = new Vector2(0, 0);
        panelRect.anchoredPosition = new Vector2(20, 20);
        panelRect.sizeDelta = new Vector2(300, 200);
        
        UnityEngine.UI.Image panelImage = panelObj.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        
        // Add subtle border
        UnityEngine.UI.Outline outline = panelObj.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);
        outline.effectDistance = new Vector2(2, -2);
        
        // Create text
        GameObject textObj = new GameObject("StatusText");
        textObj.transform.SetParent(panelObj.transform);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        statusText = textObj.AddComponent<TextMeshProUGUI>();
        statusText.fontSize = 16;
        statusText.color = Color.white;
        statusText.alignment = TextAlignmentOptions.TopLeft;
        statusText.margin = new Vector4(10, 10, 10, 10);
        statusText.enableWordWrapping = true;
        
        // Add shadow for readability
        // Note: TMP doesn't use Shadow component, it has built-in shadow settings
        statusText.fontStyle = FontStyles.Normal;
    }
}