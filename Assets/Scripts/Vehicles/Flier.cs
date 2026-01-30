// Assets/Scripts/Vehicles/Flier.cs
using UnityEngine;

public class Flier : Vehicle
{
    private void Reset()
    {
        ApplyRecommendedCameraSettings();
    }

    [ContextMenu("Apply Recommended Camera Settings")]
    private void ApplyRecommendedCameraSettings()
    {
        if (CameraSettings == null) return;

        CameraSettings.overrideFollowSmoothness = true;
        CameraSettings.followSmoothness = 10f;

        CameraSettings.overrideZoom = true;
        CameraSettings.zoomMode = VehicleCameraSettings.ZoomMode.DistanceToNearestGravity;
        CameraSettings.minZoom = 6f;
        CameraSettings.maxZoom = 35f;
        CameraSettings.distanceAtMinZoom = 3f;
        CameraSettings.distanceAtMaxZoom = 30f;
    }
    public enum FlightMode
    {
        Space = 0,
        Atmospheric = 1,
    }

    [Header("Flier Input")]
    [SerializeField] private bool usePlayerInput = true;
    [SerializeField] private KeyCode toggleModeKey = KeyCode.M;

    // Input.GetKeyDown can be missed when read from FixedUpdate, so capture it in Update.
    private bool toggleRequested;

    [Header("Flight Mode")]
    [SerializeField] private FlightMode currentMode = FlightMode.Atmospheric;
    [SerializeField] private bool allowModeToggleInAtmosphere = true;

    [Tooltip("After leaving atmosphere, wait this many seconds then force Space mode and disable toggling until re-entry.")]
    [SerializeField] private float spaceLockDelaySeconds = 5f;

    [Header("Stabilization (Space)")]
    [Tooltip("0 = fully affected by gravity. 1 = ignores gravity in space.")]
    [Range(0f, 1f)]
    [SerializeField] private float stabilization = 0f;

    [Header("Atmospheric Travel (Planet-relative)")]
    [Tooltip("Force (Newtons) applied away from the nearest planet when holding W in Atmospheric mode.")]
    [SerializeField] private float atmosphereThrustUp = 500f;

    [Tooltip("Force (Newtons) applied towards the nearest planet when holding S in Atmospheric mode.")]
    [SerializeField] private float atmosphereThrustDown = 500f;

    [Tooltip("Force (Newtons) applied tangentially when holding A/D in Atmospheric mode.")]
    [SerializeField] private float atmosphereThrustSide = 400f;

    [SerializeField] private float atmosphereMaxSpeed = 10f;

    [Tooltip("When moving sideways (A/D) without W/S, apply a spring force to maintain a target altitude.")]
    [SerializeField] private float atmosphereAltitudeHoldStrength = 200f;

    [Tooltip("How aggressively the ship aligns 'up' away from the planet in Atmospheric mode.")]
    [SerializeField] private float atmosphereUprightSpeed = 10f;

    [SerializeField] private float atmosphereUprightTriggerDegrees = 1f;

    [Header("Space Thrusters (Mode 1)")]
    [Tooltip("Force (Newtons) applied along ship up when holding W in Space mode.")]
    [SerializeField] private float spaceThrustUp = 500f;

    [Tooltip("Force (Newtons) applied along ship down when holding S in Space mode. Default 0 for 'no S'.")]
    [SerializeField] private float spaceThrustDown = 0f;

    [Tooltip("Reserved for future use (components). Not mapped in default Space controls.")]
    [SerializeField] private float spaceThrustLeft = 0f;

    [Tooltip("Reserved for future use (components). Not mapped in default Space controls.")]
    [SerializeField] private float spaceThrustRight = 0f;

    [Tooltip("Max speed in Space mode. Set to 0 to disable speed clamping.")]
    [SerializeField] private float spaceMaxSpeed = 0f;

    [Header("Space Turning (A/D)")]
    [Tooltip("Torque applied (N·m) while holding A/D in Space mode.")]
    [SerializeField] private float spaceTurnTorque = 150f;

    [SerializeField] private float spaceMaxAngularVelocity = 180f;

    [Header("Space Environment Scaling")]
    [Tooltip("Multiplier applied to Space-mode thrusters/turning when NOT in atmosphere (true space).\nUse this to keep the same thruster values usable for takeoff/atmosphere while making space less twitchy.")]
    [SerializeField] private float spaceControlScaleOutOfAtmosphere = 0.25f;

    [Header("Space Damping")]
    [Tooltip("Linear drag used when NOT in atmosphere. Set to 0 for no auto slow-down in vacuum.")]
    [SerializeField] private float spaceLinearDrag = 0f;

    [Tooltip("Angular drag used when NOT in atmosphere.")]
    [SerializeField] private float spaceAngularDrag = 0f;

    // Internal state
    private bool lastInAtmosphere;
    private float forceSpaceModeAtTime = -1f;
    private bool spaceModeLocked;

    private float targetRadius = -1f;

    private float defaultLinearDrag;
    private float defaultAngularDrag;

    public FlightMode CurrentFlightMode => currentMode;

    public bool CanToggleMode
    {
        get
        {
            if (!allowModeToggleInAtmosphere) return false;
            if (atmosphericPhysics == null) return false;
            if (!atmosphericPhysics.IsInAtmosphere) return false;
            if (spaceModeLocked) return false;
            return true;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        if (rb != null)
        {
            defaultLinearDrag = rb.drag;
            defaultAngularDrag = rb.angularDrag;
        }

        lastInAtmosphere = atmosphericPhysics != null && atmosphericPhysics.IsInAtmosphere;
    }

    private void Update()
    {
        if (!usePlayerInput) return;
        if (!IsPlayerControlled) return;

        // Capture toggle input here so it doesn't get missed between physics ticks.
        if (CanToggleMode && Input.GetKeyDown(toggleModeKey))
        {
            toggleRequested = true;
        }
    }

    protected override void FixedUpdate()
    {
        // Run AtmosphericPhysics for drag/surface coupling, but apply gravity ourselves (so we can scale it).
        atmosphericPhysics?.ApplyAtmosphericPhysics(applyGravity: false);

        ApplyStabilizedGravity();

        UpdateModeState();
        UpdateSpaceDamping();
        UpdateVehiclePhysics();
        ApplyAutoRighting();
    }

    private void UpdateModeState()
    {
        if (atmosphericPhysics == null) return;

        bool inAtmosphere = atmosphericPhysics.IsInAtmosphere;

        // Apply pending toggle (captured in Update())
        if (toggleRequested)
        {
            toggleRequested = false;

            if (CanToggleMode)
            {
                currentMode = (currentMode == FlightMode.Atmospheric) ? FlightMode.Space : FlightMode.Atmospheric;
                targetRadius = -1f;
            }
        }

        // Atmosphere re-entry unlocks mode switching.
        if (inAtmosphere && !lastInAtmosphere)
        {
            spaceModeLocked = false;
            forceSpaceModeAtTime = -1f;
        }

        // Atmosphere exit starts a timer; once elapsed, force space mode and lock.
        if (!inAtmosphere && lastInAtmosphere)
        {
            forceSpaceModeAtTime = Time.time + Mathf.Max(0f, spaceLockDelaySeconds);
            spaceModeLocked = false;
        }

        if (!inAtmosphere && forceSpaceModeAtTime > 0f && Time.time >= forceSpaceModeAtTime)
        {
            currentMode = FlightMode.Space;
            spaceModeLocked = true;
        }

        lastInAtmosphere = inAtmosphere;
    }

    private void ApplyStabilizedGravity()
    {
        if (rb == null || atmosphericPhysics == null) return;

        bool inAtmosphere = atmosphericPhysics.IsInAtmosphere;
        bool grounded = atmosphericPhysics.IsGrounded;

        // Match AtmosphericPhysics behavior: in-atmosphere gravity applies only when not grounded.
        bool shouldApplyGravity = !inAtmosphere || !grounded;
        if (!shouldApplyGravity) return;

        float gravityMultiplier = inAtmosphere ? 1f : (1f - Mathf.Clamp01(stabilization));
        if (gravityMultiplier <= 0f) return;

        Vector2 gravity = GravityManager.CalculateGravityAt(transform.position, gameObject);
        rb.AddForce(gravity * gravityMultiplier * rb.mass, ForceMode2D.Force);
    }

    protected override void UpdateVehiclePhysics()
    {
        if (rb == null || atmosphericPhysics == null) return;
        if (!usePlayerInput) return;
        if (!IsPlayerControlled) return;

        switch (currentMode)
        {
            case FlightMode.Space:
                ApplySpaceControls();
                break;
            case FlightMode.Atmospheric:
                ApplyAtmosphericControls();
                break;
        }

        ClampMotion();
    }

    private void ApplySpaceControls()
    {
        if (!usePlayerInput) return;

        Vector2 up = transform.up;

        // Make Space mode less twitchy in true space without forcing you to retune for atmosphere/takeoff.
        float scale = atmosphericPhysics.IsInAtmosphere ? 1f : Mathf.Clamp01(spaceControlScaleOutOfAtmosphere);

        if (Input.GetKey(KeyCode.W) && spaceThrustUp > 0f)
        {
            rb.AddForce(up * (spaceThrustUp * scale), ForceMode2D.Force);
        }

        if (Input.GetKey(KeyCode.S) && spaceThrustDown > 0f)
        {
            rb.AddForce(-up * (spaceThrustDown * scale), ForceMode2D.Force);
        }

        // Turning only (Mode 1)
        float turn = 0f;
        if (Input.GetKey(KeyCode.A)) turn += 1f;
        if (Input.GetKey(KeyCode.D)) turn -= 1f;

        if (Mathf.Abs(turn) > 0.01f && spaceTurnTorque > 0f)
        {
            rb.AddTorque(turn * (spaceTurnTorque * scale), ForceMode2D.Force);
        }

        if (spaceMaxAngularVelocity > 0f)
        {
            rb.angularVelocity = Mathf.Clamp(rb.angularVelocity, -spaceMaxAngularVelocity, spaceMaxAngularVelocity);
        }
    }

    private void ApplyAtmosphericControls()
    {
        if (!usePlayerInput) return;

        Planet planet = atmosphericPhysics.FindNearestPlanet();
        if (planet == null) return;
        if (!atmosphericPhysics.CheckIfInAtmosphereOf(planet)) return;

        Vector2 fromPlanet = (Vector2)transform.position - (Vector2)planet.transform.position;
        if (fromPlanet.sqrMagnitude < 0.0001f) return;

        Vector2 radialOut = fromPlanet.normalized;
        Vector2 tangent = new Vector2(-radialOut.y, radialOut.x);

        // Inputs
        float radialInput = 0f;
        if (Input.GetKey(KeyCode.W)) radialInput += 1f;
        if (Input.GetKey(KeyCode.S)) radialInput -= 1f;

        float tangentialInput = 0f;
        // A = left (counter-clockwise at the top of the planet), D = right.
        if (Input.GetKey(KeyCode.A)) tangentialInput += 1f;
        if (Input.GetKey(KeyCode.D)) tangentialInput -= 1f;

        // Set altitude target when entering mode / first frame.
        float currentRadius = fromPlanet.magnitude;
        if (targetRadius < 0f)
        {
            targetRadius = currentRadius;
        }

        // Radial thrust: W away from planet, S towards planet.
        if (radialInput > 0.01f && atmosphereThrustUp > 0f)
        {
            rb.AddForce(radialOut * atmosphereThrustUp, ForceMode2D.Force);
            targetRadius = currentRadius;
        }
        else if (radialInput < -0.01f && atmosphereThrustDown > 0f)
        {
            rb.AddForce(-radialOut * atmosphereThrustDown, ForceMode2D.Force);
            targetRadius = currentRadius;
        }

        // Tangential thrust: A/D around the planet.
        if (Mathf.Abs(tangentialInput) > 0.01f && atmosphereThrustSide > 0f)
        {
            rb.AddForce(tangent * tangentialInput * atmosphereThrustSide, ForceMode2D.Force);

            // Altitude hold only when the player isn't actively changing altitude.
            if (Mathf.Abs(radialInput) < 0.01f && atmosphereAltitudeHoldStrength > 0f)
            {
                float radiusError = targetRadius - currentRadius;
                rb.AddForce(radialOut * radiusError * atmosphereAltitudeHoldStrength, ForceMode2D.Force);
            }
        }
    }

    protected override void ApplyAutoRighting()
    {
        // In Atmospheric mode we keep the ship aligned relative to the nearest planet.
        if (rb == null || atmosphericPhysics == null) return;
        if (currentMode != FlightMode.Atmospheric) return;

        Planet planet = atmosphericPhysics.FindNearestPlanet();
        if (planet == null) return;
        if (!atmosphericPhysics.CheckIfInAtmosphereOf(planet)) return;

        float targetAngle = CalculatePlanetRadialUprightAngle(planet);
        float currentAngle = rb.rotation;

        float angleError = Mathf.DeltaAngle(currentAngle, targetAngle);
        if (Mathf.Abs(angleError) <= atmosphereUprightTriggerDegrees) return;

        float dt = Time.fixedDeltaTime;
        float t = 1f - Mathf.Exp(-Mathf.Max(0f, atmosphereUprightSpeed) * dt);

        float nextAngle = Mathf.LerpAngle(currentAngle, targetAngle, t);
        rb.MoveRotation(nextAngle);
        rb.angularVelocity = 0f;
    }

    private void UpdateSpaceDamping()
    {
        if (rb == null || atmosphericPhysics == null) return;

        // If we're not in atmosphere, do not use Rigidbody drag unless explicitly set.
        if (!atmosphericPhysics.IsInAtmosphere)
        {
            rb.drag = Mathf.Max(0f, spaceLinearDrag);
            rb.angularDrag = Mathf.Max(0f, spaceAngularDrag);
        }
        else
        {
            rb.drag = defaultLinearDrag;
            rb.angularDrag = defaultAngularDrag;
        }
    }

    private void ClampMotion()
    {
        float maxSpeed = currentMode == FlightMode.Space ? spaceMaxSpeed : atmosphereMaxSpeed;
        if (maxSpeed > 0f)
        {
            rb.velocity = Vector2.ClampMagnitude(rb.velocity, maxSpeed);
        }
    }
}
