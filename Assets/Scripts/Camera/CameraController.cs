// Assets/Scripts/Camera/CameraController.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
    [SerializeField] private float planetZoomRatio = 50f; // 1 camera size per 50px radius (Adjusted for 50PPU)
    [SerializeField] private float vehicleZoomRatio = 12.5f; // 1 camera size per 12.5px sprite size (Adjusted for 50PPU)
    
    [Header("UI")]
    [SerializeField] private GameObject uiCanvas;
    [SerializeField] private TextMeshProUGUI statusText;

    // Top-right vehicle controls
    [SerializeField] private GameObject vehicleUiPanel;
    [SerializeField] private TextMeshProUGUI vehicleUiText;
    [SerializeField] private Button resetToZeroButton;
    [SerializeField] private Slider resetToZeroProgress;

    // Bottom-right force diagram (vehicle only)
    [SerializeField] private GameObject forceDiagramPanel;
    [SerializeField] private RectTransform forceDiagramArea;
    [SerializeField] private TextMeshProUGUI forceDiagramText;
    [SerializeField] private RectTransform velArrow;
    [SerializeField] private RectTransform relVelArrow;
    [SerializeField] private RectTransform gravityArrow;

    // Top-left minimap
    private enum MinimapCenterMode { Camera = 0, ControlledVehicle = 1 }

    [SerializeField] private GameObject minimapPanel;
    [SerializeField] private RectTransform minimapArea;
    [SerializeField] private RectTransform minimapCameraRect;
    [SerializeField] private Button minimapZoomInButton;
    [SerializeField] private Button minimapZoomOutButton;

    [Header("Minimap Settings")]
    [SerializeField] private bool showMinimap = true;
    [SerializeField] private MinimapCenterMode minimapCenterMode = MinimapCenterMode.Camera;
    [SerializeField] private bool minimapShowPlanets = true;
    [SerializeField] private bool minimapShowMoons = true;
    [SerializeField] private bool minimapShowAsteroids = true;
    [SerializeField] private bool minimapShowVehicles = true;

    [Tooltip("World units per minimap pixel (smaller = zoomed in).")]
    [SerializeField] private float minimapUnitsPerPixel = 0.1f;

    [Tooltip("How often we refresh the object lists (seconds).")]
    [SerializeField] private float minimapRefreshInterval = 0.5f;

    [Tooltip("Minimum blip size (pixels).")]
    [SerializeField] private float minimapMinBlipSizePx = 4f;

    [Tooltip("Extra multiplier for planet/moon blip sizes.")]
    [SerializeField] private float minimapPlanetSizeMultiplier = 1.0f;

    [Tooltip("Extra multiplier for vehicle blip sizes.")]
    [SerializeField] private float minimapVehicleSizeMultiplier = 1.0f;

    [SerializeField] private Color minimapPlanetColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color minimapMoonColor = new Color(0.4f, 0.8f, 1f, 1f);
    [SerializeField] private Color minimapAsteroidColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color minimapVehicleColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color minimapControlledVehicleColor = new Color(1f, 0.3f, 0.3f, 1f);

    private float nextMinimapRefreshTime;
    private Planet[] cachedPlanets;
    private Moon[] cachedMoons;
    private Asteroid[] cachedAsteroids;
    private Vehicle[] cachedVehicles;

    private Sprite minimapCircleSprite;

    private readonly System.Collections.Generic.List<RectTransform> planetBlips = new();
    private readonly System.Collections.Generic.List<RectTransform> moonBlips = new();
    private readonly System.Collections.Generic.List<RectTransform> asteroidBlips = new();
    private readonly System.Collections.Generic.List<RectTransform> vehicleBlips = new();
    
    private Camera cam;
    private float targetZoom;

    [Header("Scroll Zoom")]
    [SerializeField] private bool allowScrollZoomWhenLocked = true;
    [Tooltip("When you scroll while locked to a planet/vehicle, we temporarily stop auto-zooming and respect your manual zoom.")]
    [SerializeField] private float lockedScrollZoomOverrideSeconds = 2f;

    private float manualZoomUntilTime;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = gameObject.AddComponent<Camera>();
        }

        targetZoom = cam.orthographicSize;

        EnsureEventSystemExists();

        // Create UI if not assigned, otherwise ensure required sub-panels exist.
        if (uiCanvas == null)
        {
            CreateUI();
        }
        else
        {
            EnsureCanvasHasRaycaster(uiCanvas);
            EnsureVehicleControlsUI(uiCanvas);
            EnsureForceDiagramUI(uiCanvas);
            EnsureMinimapUI(uiCanvas);
        }
    }

    private void Update()
    {
        HandleInput();

        // Component activation is bound to the currently controlled vehicle only.
        if (currentMode == CameraMode.LockedToVehicle && lockedVehicle != null)
        {
            if (Input.GetKey(KeyCode.E))
            {
                lockedVehicle.ActivateAllComponents();
            }
            else
            {
                lockedVehicle.DeactivateAllComponents();
            }

            // Reset-to-zero hotkey (UI button can be flaky without correct EventSystem setup).
            if (Input.GetKeyDown(KeyCode.R))
            {
                Debug.Log($"[CameraController] R pressed -> ResetToZero for '{lockedVehicle.name}'");
                OnResetToZeroClicked();
            }
        }

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
        
        // Toggle rotation matching
        if (Input.GetKeyDown(KeyCode.T))
        {
            matchRotation = !matchRotation;
            Debug.Log($"Camera rotation matching: {(matchRotation ? "ON" : "OFF")}");
        }
        
        // Free camera movement (only when not locked)
        // NOTE: Use arrow keys only (not WASD) so player controls don't fight the camera.
        if (currentMode == CameraMode.Free)
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
            if (Input.GetKey(KeyCode.UpArrow)) vertical += 1f;

            Vector3 movement = new Vector3(horizontal, vertical, 0) * freeMoveSpeed * Time.deltaTime;
            transform.position += movement;
        }

        // Scroll zoom (allowed in locked views too)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        bool canScrollZoom = currentMode == CameraMode.Free || allowScrollZoomWhenLocked;
        if (canScrollZoom && Mathf.Abs(scroll) > 0.01f)
        {
            targetZoom -= scroll * freeZoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, 1f, 80f);

            if (currentMode != CameraMode.Free)
            {
                manualZoomUntilTime = Time.time + Mathf.Max(0f, lockedScrollZoomOverrideSeconds);
            }
        }
    }

    private void UpdateCamera()
    {
        float effectiveFollowSmoothness = followSmoothness;

        bool allowAutoZoom = Time.time >= manualZoomUntilTime;

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
                    transform.position = Vector3.Lerp(transform.position, targetPos, effectiveFollowSmoothness * Time.deltaTime);

                    // Match planet rotation if enabled
                    if (matchRotation)
                    {
                        Quaternion targetRot = Quaternion.Euler(0, 0, lockedPlanet.transform.eulerAngles.z);
                        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, effectiveFollowSmoothness * Time.deltaTime);
                    }
                    else
                    {
                        // Reset to upright
                        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, effectiveFollowSmoothness * Time.deltaTime);
                    }

                    // Calculate zoom based on planet radius (unless the user recently scrolled)
                    if (allowAutoZoom)
                    {
                        float planetRadius = lockedPlanet.Radius;
                        targetZoom = planetRadius / planetZoomRatio;
                    }
                }
                else
                {
                    UnlockCamera();
                }
                break;

            case CameraMode.LockedToVehicle:
                if (lockedVehicle != null)
                {
                    var vehicleCamera = lockedVehicle.CameraSettings;
                    if (vehicleCamera != null && vehicleCamera.overrideFollowSmoothness)
                    {
                        effectiveFollowSmoothness = vehicleCamera.followSmoothness;
                    }

                    // Follow vehicle
                    Vector3 targetPos = new Vector3(
                        lockedVehicle.transform.position.x,
                        lockedVehicle.transform.position.y,
                        transform.position.z
                    );
                    transform.position = Vector3.Lerp(transform.position, targetPos, effectiveFollowSmoothness * Time.deltaTime);

                    // Match vehicle rotation if enabled
                    if (matchRotation)
                    {
                        Quaternion targetRot = Quaternion.Euler(0, 0, lockedVehicle.transform.eulerAngles.z);
                        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, effectiveFollowSmoothness * Time.deltaTime);
                    }
                    else
                    {
                        // Reset to upright
                        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, effectiveFollowSmoothness * Time.deltaTime);
                    }

                    // Zoom (unless the user recently scrolled)
                    if (allowAutoZoom)
                    {
                        if (vehicleCamera != null && vehicleCamera.overrideZoom)
                        {
                            switch (vehicleCamera.zoomMode)
                            {
                                case Vehicle.VehicleCameraSettings.ZoomMode.DistanceToNearestGravity:
                                    float dist = FindNearestGravityDistance(lockedVehicle.transform.position);
                                    float t = Mathf.InverseLerp(vehicleCamera.distanceAtMinZoom, vehicleCamera.distanceAtMaxZoom, dist);
                                    targetZoom = Mathf.Lerp(vehicleCamera.minZoom, vehicleCamera.maxZoom, t);
                                    break;

                                case Vehicle.VehicleCameraSettings.ZoomMode.SpriteSize:
                                default:
                                    float spriteSize = GetVehicleSpriteSize(lockedVehicle);
                                    float ratio = Mathf.Max(0.0001f, vehicleCamera.vehicleZoomRatio);
                                    targetZoom = spriteSize / ratio;
                                    break;
                            }
                        }
                        else
                        {
                            // Default: Calculate zoom based on vehicle sprite size
                            float spriteSize = GetVehicleSpriteSize(lockedVehicle);
                            targetZoom = spriteSize / vehicleZoomRatio;
                        }
                    }
                }
                else
                {
                    UnlockCamera();
                }
                break;

            case CameraMode.Free:
                // Reset rotation in free mode
                if (!matchRotation || transform.rotation != Quaternion.identity)
                {
                    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, followSmoothness * Time.deltaTime);
                }
                break;
        }

        // Smooth zoom
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, effectiveFollowSmoothness * Time.deltaTime);
    }

    private float FindNearestGravityDistance(Vector3 position)
    {
        float minDist = float.MaxValue;
        var sources = GravityManager.GetAllGravitySources();

        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i] is MonoBehaviour mb)
            {
                float dist = Vector2.Distance(position, mb.transform.position);
                if (dist < minDist) minDist = dist;
            }
        }

        // If no sources, just return a large distance.
        return minDist == float.MaxValue ? 9999f : minDist;
    }

    private void UpdateUI()
    {
        if (statusText == null) return;

        UpdateVehiclePanelUI();
        UpdateForceDiagramUI();
        UpdateMinimapUI();
        
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

                    if (nearestVehicle is Flier)
                    {
                        text += "[M] Toggle Flight Mode (in atmosphere)\n";
                    }
                }
                
                text += "\n[Arrow Keys] Move\n[Mouse Wheel] Zoom";
                break;
                
            case CameraMode.LockedToPlanet:
                text = $"<b>FOLLOWING PLANET</b>\n";
                text += $"<color=#FFD700>{lockedPlanet.name}</color>\n";
                text += $"\nRadius: {lockedPlanet.Radius:F0}px\n";
                text += $"Zoom: {cam.orthographicSize:F2}\n";
                text += $"Rotation Match: <color={(matchRotation ? "#00FF00>ON" : "#FF0000>OFF")}</color>\n";
                text += "\n[F] Free Camera\n[V] Follow Vehicle\n[T] Toggle Rotation\n[Mouse Wheel] Zoom";
                break;
                
            case CameraMode.LockedToVehicle:
                text = $"<b>FOLLOWING VEHICLE</b>\n";
                text += $"<color=#00FF00>{lockedVehicle.name}</color>\n";
                text += $"\nSpeed: {lockedVehicle.GetComponent<Rigidbody2D>()?.velocity.magnitude:F2}\n";

                if (lockedVehicle is Flier flier)
                {
                    text += $"Flight Mode: <color=#00FFFF>{flier.CurrentFlightMode}</color>\n";
                    text += "[M] Toggle Flight Mode (in atmosphere)\n";
                }

                text += $"Zoom: {cam.orthographicSize:F2}\n";
                text += $"Rotation Match: <color={(matchRotation ? "#00FF00>ON" : "#FF0000>OFF")}</color>\n";
                text += "\n[F] Free Camera\n[P] Follow Planet\n[T] Toggle Rotation\n[Mouse Wheel] Zoom";
                break;
        }
        
        statusText.text = text;
    }

    private void UpdateVehiclePanelUI()
    {
        if (vehicleUiPanel == null) return;

        bool show = currentMode == CameraMode.LockedToVehicle && lockedVehicle != null;
        vehicleUiPanel.SetActive(show);
        if (!show) return;

        // Defensive: scene might have had uiCanvas assigned but missing the runtime-created subwidgets.
        if (resetToZeroButton == null || resetToZeroProgress == null || vehicleUiText == null)
        {
            EnsureVehicleControlsUI(uiCanvas);
        }

        if (vehicleUiText != null)
        {
            string t = $"<b>{lockedVehicle.name}</b>\n";

            bool canReset = lockedVehicle.CanResetToZeroInSpace();
            if (!canReset)
            {
                t += "Reset To 0: <color=#FF6666>SPACE ONLY</color>\n";
            }
            else
            {
                if (lockedVehicle.IsResettingToZero)
                {
                    float remaining = Mathf.Max(0f, lockedVehicle.ResetToZeroEstimatedSeconds - lockedVehicle.ResetToZeroElapsedSeconds);
                    t += $"Resetting... <color=#00FFAA>{remaining:F1}s</color>\n";
                }
                else
                {
                    t += "Reset To 0 ready\n";
                }
            }

            t += "[E] Use Components\n[R] Reset To 0";

            vehicleUiText.text = t;
        }

        if (resetToZeroButton != null)
        {
            bool canClick = lockedVehicle.CanResetToZeroInSpace() && !lockedVehicle.IsResettingToZero;
            resetToZeroButton.interactable = canClick;
        }

        if (resetToZeroProgress != null)
        {
            resetToZeroProgress.value = lockedVehicle.IsResettingToZero ? lockedVehicle.ResetToZeroProgress01 : 0f;
        }
    }

    private float GetVehicleSpriteSize(Vehicle vehicle)
    {
        // Try to find VehicleRenderer child first
        Transform rendererTransform = vehicle.transform.Find("VehicleRenderer");
        SpriteRenderer sr = null;
        
        if (rendererTransform != null)
        {
            sr = rendererTransform.GetComponent<SpriteRenderer>();
        }
        
        // Fallback: search in children
        if (sr == null)
        {
            sr = vehicle.GetComponentInChildren<SpriteRenderer>();
        }
        
        if (sr != null && sr.sprite != null && sr.sprite.texture != null)
        {
            // Return the max dimension of the sprite in pixels
            float maxPixels = Mathf.Max(sr.sprite.texture.width, sr.sprite.texture.height);
            return maxPixels;
        }
        
        Debug.LogWarning($"Could not get sprite size for {vehicle.name}, using default");
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

        Vehicle.SetControlledVehicle(vehicle);
        
        Debug.Log($"Camera locked to vehicle: {vehicle.name}");
    }

    private void UnlockCamera()
    {
        currentMode = CameraMode.Free;
        lockedPlanet = null;
        lockedVehicle = null;

        Vehicle.SetControlledVehicle(null);
        
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

    private void EnsureEventSystemExists()
    {
        EventSystem existing = FindObjectOfType<EventSystem>();
        if (existing != null)
        {
            // Some scenes may have an EventSystem without an input module.
            if (existing.GetComponent<BaseInputModule>() == null)
            {
                existing.gameObject.AddComponent<StandaloneInputModule>();
            }
            return;
        }

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private void EnsureCanvasHasRaycaster(GameObject canvasRoot)
    {
        if (canvasRoot == null) return;

        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = canvasRoot.GetComponentInChildren<Canvas>();
        }

        if (canvas == null) return;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void OnResetToZeroClicked()
    {
        if (lockedVehicle == null)
        {
            Debug.LogWarning("[CameraController] ResetToZero clicked but no locked vehicle.");
            return;
        }

        bool canReset = lockedVehicle.CanResetToZeroInSpace();
        Debug.Log($"[CameraController] ResetToZero clicked for '{lockedVehicle.name}'. CanReset={canReset}");

        lockedVehicle.ResetToZeroInSpace();
    }

    private void EnsureVehicleControlsUI(GameObject canvasRoot)
    {
        if (canvasRoot == null) return;

        // If something partially exists (e.g. scene had uiCanvas assigned but our runtime refs are missing), rebuild.
        if (vehicleUiPanel != null)
        {
            if (resetToZeroButton != null && resetToZeroProgress != null && vehicleUiText != null) return;

            Destroy(vehicleUiPanel);
            vehicleUiPanel = null;
            vehicleUiText = null;
            resetToZeroButton = null;
            resetToZeroProgress = null;
        }

        // ===== Top-right vehicle control panel =====
        vehicleUiPanel = new GameObject("VehiclePanel");
        vehicleUiPanel.transform.SetParent(canvasRoot.transform);

        RectTransform vpRect = vehicleUiPanel.AddComponent<RectTransform>();
        vpRect.anchorMin = new Vector2(1, 1);
        vpRect.anchorMax = new Vector2(1, 1);
        vpRect.pivot = new Vector2(1, 1);
        vpRect.anchoredPosition = new Vector2(-20, -20);
        vpRect.sizeDelta = new Vector2(260, 110);

        var vpImage = vehicleUiPanel.AddComponent<Image>();
        vpImage.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        // Important: background should NOT intercept clicks.
        vpImage.raycastTarget = false;

        var vpOutline = vehicleUiPanel.AddComponent<Outline>();
        vpOutline.effectColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);
        vpOutline.effectDistance = new Vector2(2, -2);

        // Text
        var vTextObj = new GameObject("VehicleText");
        vTextObj.transform.SetParent(vehicleUiPanel.transform);
        var vTextRect = vTextObj.AddComponent<RectTransform>();
        vTextRect.anchorMin = new Vector2(0, 0);
        vTextRect.anchorMax = new Vector2(1, 1);
        vTextRect.offsetMin = new Vector2(10, 40);
        vTextRect.offsetMax = new Vector2(-10, -10);

        vehicleUiText = vTextObj.AddComponent<TextMeshProUGUI>();
        vehicleUiText.fontSize = 14;
        vehicleUiText.color = Color.white;
        vehicleUiText.alignment = TextAlignmentOptions.TopLeft;
        vehicleUiText.enableWordWrapping = true;
        vehicleUiText.raycastTarget = false;

        // Progress slider
        var sliderObj = new GameObject("ResetProgress");
        sliderObj.transform.SetParent(vehicleUiPanel.transform);
        resetToZeroProgress = sliderObj.AddComponent<Slider>();

        var sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0, 0);
        sliderRect.anchorMax = new Vector2(1, 0);
        sliderRect.pivot = new Vector2(0.5f, 0);
        sliderRect.anchoredPosition = new Vector2(0, 10);
        sliderRect.offsetMin = new Vector2(10, 10);
        sliderRect.offsetMax = new Vector2(-70, 28);

        // Slider visuals
        var sliderBg = new GameObject("Background");
        sliderBg.transform.SetParent(sliderObj.transform);
        var bgImg = sliderBg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
        bgImg.raycastTarget = false;
        var bgRect = sliderBg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform);
        var fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0, 0);
        fillAreaRect.anchorMax = new Vector2(1, 1);
        fillAreaRect.offsetMin = new Vector2(2, 2);
        fillAreaRect.offsetMax = new Vector2(-2, -2);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.8f, 0.9f, 1f);
        fillImg.raycastTarget = false;
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        resetToZeroProgress.fillRect = fillRect;
        resetToZeroProgress.targetGraphic = fillImg;
        resetToZeroProgress.direction = Slider.Direction.LeftToRight;
        resetToZeroProgress.minValue = 0f;
        resetToZeroProgress.maxValue = 1f;
        resetToZeroProgress.value = 0f;

        // Button
        var btnObj = new GameObject("ResetButton");
        btnObj.transform.SetParent(vehicleUiPanel.transform);
        resetToZeroButton = btnObj.AddComponent<Button>();
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.15f, 0.2f, 1f);
        btnImg.raycastTarget = true;
        resetToZeroButton.targetGraphic = btnImg;

        var btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1, 0);
        btnRect.anchorMax = new Vector2(1, 0);
        btnRect.pivot = new Vector2(1, 0);
        btnRect.anchoredPosition = new Vector2(-10, 10);
        btnRect.sizeDelta = new Vector2(50, 18);

        var btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(btnObj.transform);
        var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "0";
        btnText.fontSize = 14;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;
        btnText.raycastTarget = false;
        var btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        resetToZeroButton.onClick.RemoveAllListeners();
        resetToZeroButton.onClick.AddListener(OnResetToZeroClicked);

        vehicleUiPanel.SetActive(false);
    }

    private RectTransform CreateVectorArrow(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(40, 4);

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        // Simple arrowhead
        GameObject head = new GameObject("Head");
        head.transform.SetParent(go.transform);
        RectTransform headRt = head.AddComponent<RectTransform>();
        headRt.anchorMin = new Vector2(1, 0.5f);
        headRt.anchorMax = new Vector2(1, 0.5f);
        headRt.pivot = new Vector2(0.5f, 0.5f);
        headRt.anchoredPosition = Vector2.zero;
        headRt.sizeDelta = new Vector2(8, 8);
        var headImg = head.AddComponent<Image>();
        headImg.color = color;
        headImg.raycastTarget = false;

        return rt;
    }

    private void SetArrow(RectTransform arrow, Vector2 vec, float pixelsPerUnit, float maxPixels)
    {
        if (arrow == null) return;

        float mag = vec.magnitude;
        float len = Mathf.Clamp(mag * pixelsPerUnit, 0f, maxPixels);

        // Hide near-zero
        arrow.gameObject.SetActive(len > 0.5f);
        if (len <= 0.5f) return;

        float angle = Mathf.Atan2(vec.y, vec.x) * Mathf.Rad2Deg;
        arrow.localRotation = Quaternion.Euler(0, 0, angle);
        arrow.sizeDelta = new Vector2(len, arrow.sizeDelta.y);
    }

    private void EnsureForceDiagramUI(GameObject canvasRoot)
    {
        if (canvasRoot == null) return;

        if (forceDiagramPanel != null)
        {
            if (forceDiagramArea != null && forceDiagramText != null && velArrow != null && gravityArrow != null) return;

            Destroy(forceDiagramPanel);
            forceDiagramPanel = null;
            forceDiagramArea = null;
            forceDiagramText = null;
            velArrow = null;
            relVelArrow = null;
            gravityArrow = null;
        }

        forceDiagramPanel = new GameObject("ForceDiagramPanel");
        forceDiagramPanel.transform.SetParent(canvasRoot.transform);

        RectTransform panelRect = forceDiagramPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1, 0);
        panelRect.anchorMax = new Vector2(1, 0);
        panelRect.pivot = new Vector2(1, 0);
        panelRect.anchoredPosition = new Vector2(-20, 20);
        panelRect.sizeDelta = new Vector2(320, 220);

        var bg = forceDiagramPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        bg.raycastTarget = false;

        var outline = forceDiagramPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);
        outline.effectDistance = new Vector2(2, -2);

        // Text (right side)
        GameObject txtObj = new GameObject("ForceText");
        txtObj.transform.SetParent(forceDiagramPanel.transform);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = new Vector2(0, 0);
        txtRt.anchorMax = new Vector2(1, 1);
        txtRt.offsetMin = new Vector2(170, 10);
        txtRt.offsetMax = new Vector2(-10, -10);

        forceDiagramText = txtObj.AddComponent<TextMeshProUGUI>();
        forceDiagramText.fontSize = 12;
        forceDiagramText.color = Color.white;
        forceDiagramText.alignment = TextAlignmentOptions.TopLeft;
        forceDiagramText.enableWordWrapping = true;
        forceDiagramText.raycastTarget = false;

        // Diagram area (left side)
        GameObject areaObj = new GameObject("DiagramArea");
        areaObj.transform.SetParent(forceDiagramPanel.transform);
        forceDiagramArea = areaObj.AddComponent<RectTransform>();
        forceDiagramArea.anchorMin = new Vector2(0, 0);
        forceDiagramArea.anchorMax = new Vector2(0, 0);
        forceDiagramArea.pivot = new Vector2(0, 0);
        forceDiagramArea.anchoredPosition = new Vector2(10, 10);
        forceDiagramArea.sizeDelta = new Vector2(150, 150);

        var areaBg = areaObj.AddComponent<Image>();
        areaBg.color = new Color(0.08f, 0.08f, 0.11f, 0.9f);
        areaBg.raycastTarget = false;

        // Crosshair
        GameObject cross = new GameObject("Cross");
        cross.transform.SetParent(areaObj.transform);
        RectTransform crossRt = cross.AddComponent<RectTransform>();
        crossRt.anchorMin = new Vector2(0.5f, 0.5f);
        crossRt.anchorMax = new Vector2(0.5f, 0.5f);
        crossRt.pivot = new Vector2(0.5f, 0.5f);
        crossRt.anchoredPosition = Vector2.zero;
        crossRt.sizeDelta = new Vector2(6, 6);
        var crossImg = cross.AddComponent<Image>();
        crossImg.color = new Color(1f, 1f, 1f, 0.6f);
        crossImg.raycastTarget = false;

        // Arrows
        velArrow = CreateVectorArrow("Velocity", areaObj.transform, new Color(0.2f, 0.8f, 1f, 1f));
        relVelArrow = CreateVectorArrow("RelativeVelocity", areaObj.transform, new Color(0.6f, 1f, 0.4f, 1f));
        gravityArrow = CreateVectorArrow("Gravity", areaObj.transform, new Color(1f, 0.8f, 0.2f, 1f));

        forceDiagramPanel.SetActive(false);
    }

    private void UpdateForceDiagramUI()
    {
        if (forceDiagramPanel == null) return;

        bool show = currentMode == CameraMode.LockedToVehicle && lockedVehicle != null;
        forceDiagramPanel.SetActive(show);
        if (!show) return;

        // Defensive: if canvas was assigned, the panel might not exist yet.
        if (forceDiagramArea == null || forceDiagramText == null || velArrow == null || gravityArrow == null)
        {
            EnsureForceDiagramUI(uiCanvas);
        }

        Rigidbody2D rb = lockedVehicle.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            if (forceDiagramText != null) forceDiagramText.text = "No Rigidbody2D";
            return;
        }

        Vector2 v = rb.velocity;
        Vector2 relV = lockedVehicle.GetRelativeVelocity();
        Vector2 g = GravityManager.CalculateGravityAt(lockedVehicle.transform.position, lockedVehicle.gameObject);

        // Scale vectors into UI pixels.
        const float pixelsPerUnit = 12f;
        const float maxPixels = 60f;
        SetArrow(velArrow, v, pixelsPerUnit, maxPixels);
        SetArrow(relVelArrow, relV, pixelsPerUnit, maxPixels);
        SetArrow(gravityArrow, g, pixelsPerUnit, maxPixels);

        if (forceDiagramText != null)
        {
            forceDiagramText.text =
                $"v: ({v.x:F2}, {v.y:F2}) | |v|={v.magnitude:F2}\n" +
                $"rel v: ({relV.x:F2}, {relV.y:F2})\n" +
                $"g: ({g.x:F3}, {g.y:F3})\n" +
                $"rot: {rb.rotation:F1}°\n" +
                $"ω: {rb.angularVelocity:F1}°/s\n" +
                "\n(Blue=v, Green=rel v, Yellow=g)";
        }
    }

    private void EnsureMinimapUI(GameObject canvasRoot)
    {
        if (canvasRoot == null) return;

        if (minimapPanel != null)
        {
            if (minimapArea != null && minimapCameraRect != null && minimapZoomInButton != null && minimapZoomOutButton != null) return;

            Destroy(minimapPanel);
            minimapPanel = null;
            minimapArea = null;
            minimapCameraRect = null;
            minimapZoomInButton = null;
            minimapZoomOutButton = null;
            planetBlips.Clear();
            moonBlips.Clear();
            asteroidBlips.Clear();
            vehicleBlips.Clear();
        }

        minimapCircleSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");

        minimapPanel = new GameObject("MinimapPanel");
        minimapPanel.transform.SetParent(canvasRoot.transform);

        RectTransform panelRect = minimapPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(20, -20);
        panelRect.sizeDelta = new Vector2(260, 260);

        var bg = minimapPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        bg.raycastTarget = false;

        var outline = minimapPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);
        outline.effectDistance = new Vector2(2, -2);

        // Area
        GameObject areaObj = new GameObject("Area");
        areaObj.transform.SetParent(minimapPanel.transform);
        minimapArea = areaObj.AddComponent<RectTransform>();
        minimapArea.anchorMin = new Vector2(0, 0);
        minimapArea.anchorMax = new Vector2(1, 1);
        minimapArea.offsetMin = new Vector2(10, 10);
        minimapArea.offsetMax = new Vector2(-10, -40);

        var areaBg = areaObj.AddComponent<Image>();
        areaBg.color = new Color(0.08f, 0.08f, 0.11f, 0.9f);
        areaBg.raycastTarget = false;

        minimapPanel.SetActive(showMinimap);

        // Camera rect
        GameObject camRectObj = new GameObject("CameraRect");
        camRectObj.transform.SetParent(areaObj.transform);
        minimapCameraRect = camRectObj.AddComponent<RectTransform>();
        minimapCameraRect.anchorMin = new Vector2(0.5f, 0.5f);
        minimapCameraRect.anchorMax = new Vector2(0.5f, 0.5f);
        minimapCameraRect.pivot = new Vector2(0.5f, 0.5f);
        minimapCameraRect.anchoredPosition = Vector2.zero;
        minimapCameraRect.sizeDelta = new Vector2(40, 30);

        var camRectImg = camRectObj.AddComponent<Image>();
        camRectImg.color = new Color(1f, 1f, 1f, 0.1f);
        camRectImg.raycastTarget = false;

        var camRectOutline = camRectObj.AddComponent<Outline>();
        camRectOutline.effectColor = new Color(1f, 1f, 1f, 0.8f);
        camRectOutline.effectDistance = new Vector2(1, -1);

        // Buttons
        minimapZoomInButton = CreateMiniButton(minimapPanel.transform, "ZoomIn", "+", new Vector2(-60, 10));
        minimapZoomOutButton = CreateMiniButton(minimapPanel.transform, "ZoomOut", "-", new Vector2(-20, 10));

        minimapZoomInButton.onClick.RemoveAllListeners();
        minimapZoomInButton.onClick.AddListener(() => minimapUnitsPerPixel = Mathf.Max(0.01f, minimapUnitsPerPixel * 0.8f));

        minimapZoomOutButton.onClick.RemoveAllListeners();
        minimapZoomOutButton.onClick.AddListener(() => minimapUnitsPerPixel = Mathf.Min(10f, minimapUnitsPerPixel * 1.25f));
    }

    private Button CreateMiniButton(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(30, 22);

        var img = btnObj.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.2f, 1f);
        img.raycastTarget = true;

        var btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        var tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }

    private RectTransform CreateBlip(string name, Transform parent, Color color, bool circular)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(6, 6);

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        if (circular && minimapCircleSprite != null)
        {
            img.sprite = minimapCircleSprite;
        }

        return rt;
    }

    private void RefreshMinimapObjects()
    {
        if (minimapShowPlanets)
        {
            // IMPORTANT: Moon inherits Planet, so FindObjectsOfType<Planet>() would include moons.
            Planet[] allPlanets = FindObjectsOfType<Planet>();
            var list = new System.Collections.Generic.List<Planet>(allPlanets.Length);
            for (int i = 0; i < allPlanets.Length; i++)
            {
                Planet p = allPlanets[i];
                if (p == null) continue;
                if (p is Moon) continue;
                list.Add(p);
            }
            cachedPlanets = list.ToArray();
        }
        else
        {
            cachedPlanets = System.Array.Empty<Planet>();
        }

        cachedMoons = minimapShowMoons ? FindObjectsOfType<Moon>() : System.Array.Empty<Moon>();
        cachedAsteroids = minimapShowAsteroids ? FindObjectsOfType<Asteroid>() : System.Array.Empty<Asteroid>();
        cachedVehicles = minimapShowVehicles ? FindObjectsOfType<Vehicle>() : System.Array.Empty<Vehicle>();

        EnsureBlipCount(planetBlips, cachedPlanets != null ? cachedPlanets.Length : 0, minimapArea, minimapPlanetColor, "Planet", circular: true);
        EnsureBlipCount(moonBlips, cachedMoons != null ? cachedMoons.Length : 0, minimapArea, minimapMoonColor, "Moon", circular: true);
        EnsureBlipCount(asteroidBlips, cachedAsteroids != null ? cachedAsteroids.Length : 0, minimapArea, minimapAsteroidColor, "Asteroid", circular: false);
        EnsureBlipCount(vehicleBlips, cachedVehicles != null ? cachedVehicles.Length : 0, minimapArea, minimapVehicleColor, "Vehicle", circular: true);
    }

    private void EnsureBlipCount(System.Collections.Generic.List<RectTransform> list, int count, RectTransform parent, Color color, string prefix, bool circular)
    {
        if (parent == null) return;

        while (list.Count > count)
        {
            if (list[list.Count - 1] != null) Destroy(list[list.Count - 1].gameObject);
            list.RemoveAt(list.Count - 1);
        }

        while (list.Count < count)
        {
            list.Add(CreateBlip($"{prefix}Blip{list.Count}", parent, color, circular));
        }
    }

    private void UpdateMinimapUI()
    {
        if (minimapPanel == null || minimapArea == null) return;

        minimapPanel.SetActive(showMinimap);
        if (!showMinimap) return;

        if (Time.time >= nextMinimapRefreshTime)
        {
            nextMinimapRefreshTime = Time.time + Mathf.Max(0.1f, minimapRefreshInterval);
            RefreshMinimapObjects();
        }

        Vector2 origin = minimapCenterMode == MinimapCenterMode.ControlledVehicle && lockedVehicle != null
            ? (Vector2)lockedVehicle.transform.position
            : (Vector2)transform.position;

        Vector2 areaSize = minimapArea.rect.size;
        Vector2 half = areaSize * 0.5f;

        // Place blips (objects may be destroyed between refreshes, so skip nulls and hide unused blips)
        int planetIndex = 0;
        for (int i = 0; cachedPlanets != null && i < cachedPlanets.Length; i++)
        {
            Planet pObj = cachedPlanets[i];
            if (pObj == null) continue;
            if (planetIndex >= planetBlips.Count) break;

            Vector2 p = (Vector2)pObj.transform.position;
            Vector2 delta = (p - origin) / Mathf.Max(0.0001f, minimapUnitsPerPixel);

            // Size: world radius -> minimap pixels
            float worldRadius = Mathf.Max(0f, pObj.GetRadius());
            float sizePx = (worldRadius * 2f / Mathf.Max(0.0001f, minimapUnitsPerPixel)) * Mathf.Max(0.01f, minimapPlanetSizeMultiplier);
            sizePx = Mathf.Clamp(sizePx, minimapMinBlipSizePx, Mathf.Min(areaSize.x, areaSize.y));

            RectTransform blip = planetBlips[planetIndex];
            if (blip != null)
            {
                blip.gameObject.SetActive(true);
                blip.sizeDelta = new Vector2(sizePx, sizePx);
                blip.anchoredPosition = new Vector2(
                    Mathf.Clamp(delta.x, -half.x, half.x),
                    Mathf.Clamp(delta.y, -half.y, half.y)
                );
            }

            planetIndex++;
        }
        for (int i = planetIndex; i < planetBlips.Count; i++)
        {
            if (planetBlips[i] != null) planetBlips[i].gameObject.SetActive(false);
        }

        int moonIndex = 0;
        for (int i = 0; cachedMoons != null && i < cachedMoons.Length; i++)
        {
            Moon mObj = cachedMoons[i];
            if (mObj == null) continue;
            if (moonIndex >= moonBlips.Count) break;

            Vector2 p = (Vector2)mObj.transform.position;
            Vector2 delta = (p - origin) / Mathf.Max(0.0001f, minimapUnitsPerPixel);

            float worldRadius = Mathf.Max(0f, mObj.GetRadius());
            float sizePx = (worldRadius * 2f / Mathf.Max(0.0001f, minimapUnitsPerPixel)) * Mathf.Max(0.01f, minimapPlanetSizeMultiplier);
            sizePx = Mathf.Clamp(sizePx, minimapMinBlipSizePx, Mathf.Min(areaSize.x, areaSize.y));

            RectTransform blip = moonBlips[moonIndex];
            if (blip != null)
            {
                blip.gameObject.SetActive(true);
                blip.sizeDelta = new Vector2(sizePx, sizePx);
                blip.anchoredPosition = new Vector2(
                    Mathf.Clamp(delta.x, -half.x, half.x),
                    Mathf.Clamp(delta.y, -half.y, half.y)
                );
            }

            moonIndex++;
        }
        for (int i = moonIndex; i < moonBlips.Count; i++)
        {
            if (moonBlips[i] != null) moonBlips[i].gameObject.SetActive(false);
        }

        int asteroidIndex = 0;
        for (int i = 0; cachedAsteroids != null && i < cachedAsteroids.Length; i++)
        {
            Asteroid aObj = cachedAsteroids[i];
            if (aObj == null) continue;
            if (asteroidIndex >= asteroidBlips.Count) break;

            Vector2 p = (Vector2)aObj.transform.position;
            Vector2 delta = (p - origin) / Mathf.Max(0.0001f, minimapUnitsPerPixel);

            RectTransform blip = asteroidBlips[asteroidIndex];
            if (blip != null)
            {
                blip.gameObject.SetActive(true);
                float sizePx = Mathf.Max(minimapMinBlipSizePx, 3f);
                blip.sizeDelta = new Vector2(sizePx, sizePx);
                blip.anchoredPosition = new Vector2(
                    Mathf.Clamp(delta.x, -half.x, half.x),
                    Mathf.Clamp(delta.y, -half.y, half.y)
                );
            }

            asteroidIndex++;
        }
        for (int i = asteroidIndex; i < asteroidBlips.Count; i++)
        {
            if (asteroidBlips[i] != null) asteroidBlips[i].gameObject.SetActive(false);
        }

        int vehicleIndex = 0;
        for (int i = 0; cachedVehicles != null && i < cachedVehicles.Length; i++)
        {
            Vehicle vObj = cachedVehicles[i];
            if (vObj == null) continue;
            if (vehicleIndex >= vehicleBlips.Count) break;

            Vector2 p = (Vector2)vObj.transform.position;
            Vector2 delta = (p - origin) / Mathf.Max(0.0001f, minimapUnitsPerPixel);

            float worldSize = GetVehicleWorldSize(vObj);
            float sizePx = (worldSize / Mathf.Max(0.0001f, minimapUnitsPerPixel)) * Mathf.Max(0.01f, minimapVehicleSizeMultiplier);
            sizePx = Mathf.Clamp(sizePx, minimapMinBlipSizePx, 30f);

            RectTransform blip = vehicleBlips[vehicleIndex];
            if (blip != null)
            {
                blip.gameObject.SetActive(true);
                blip.sizeDelta = new Vector2(sizePx, sizePx);
                blip.anchoredPosition = new Vector2(
                    Mathf.Clamp(delta.x, -half.x, half.x),
                    Mathf.Clamp(delta.y, -half.y, half.y)
                );

                // Controlled vehicle highlight
                var img = blip.GetComponent<Image>();
                if (img != null)
                {
                    img.color = vObj == Vehicle.ControlledVehicle ? minimapControlledVehicleColor : minimapVehicleColor;
                }
            }

            vehicleIndex++;
        }
        for (int i = vehicleIndex; i < vehicleBlips.Count; i++)
        {
            if (vehicleBlips[i] != null) vehicleBlips[i].gameObject.SetActive(false);
        }

        // Camera view box
        if (minimapCameraRect != null && cam != null)
        {
            Vector2 camPos = (Vector2)transform.position;
            Vector2 camDelta = (camPos - origin) / Mathf.Max(0.0001f, minimapUnitsPerPixel);
            minimapCameraRect.anchoredPosition = new Vector2(
                Mathf.Clamp(camDelta.x, -half.x, half.x),
                Mathf.Clamp(camDelta.y, -half.y, half.y)
            );

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector2 viewSizeWorld = new Vector2(halfW * 2f, halfH * 2f);
            Vector2 viewSizePx = viewSizeWorld / Mathf.Max(0.0001f, minimapUnitsPerPixel);
            viewSizePx.x = Mathf.Clamp(viewSizePx.x, 6f, areaSize.x);
            viewSizePx.y = Mathf.Clamp(viewSizePx.y, 6f, areaSize.y);
            minimapCameraRect.sizeDelta = viewSizePx;
        }
    }

    private float GetVehicleWorldSize(Vehicle vehicle)
    {
        if (vehicle == null) return 1f;
        SpriteRenderer sr = vehicle.transform.Find("VehicleRenderer")?.GetComponent<SpriteRenderer>();
        if (sr == null) sr = vehicle.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Vector3 size = sr.bounds.size;
            return Mathf.Max(size.x, size.y);
        }
        return 1f;
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

        EnsureEventSystemExists();

        uiCanvas = canvasObj;

        // Create background panel
        GameObject panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(canvasObj.transform);

        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(0, 0);
        panelRect.pivot = new Vector2(0, 0);
        panelRect.anchoredPosition = new Vector2(20, 20);
        panelRect.sizeDelta = new Vector2(300, 280);
        
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
        statusText.fontStyle = FontStyles.Normal;

        EnsureVehicleControlsUI(canvasObj);
        EnsureForceDiagramUI(canvasObj);
        EnsureMinimapUI(canvasObj);
    }
}