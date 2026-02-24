using UnityEngine;
using System.Linq;
using System;

public class AiCarController : BaseCarController
{
    #pragma warning disable 0414
    // --- Constants ---
    private const float GROUND_RAY_LENGTH = 0.5f;
    private const float STEERING_DEAD_ZONE = 0.05f;
    private const float NODE_GIZMO_RADIUS = 0.5f;
    private static readonly Vector3 DEFAULT_CENTER_OF_MASS = new(0, -0.0f, 0);
        // --- Path Following ---
    [Header("Path Following Settings")]
    [Tooltip("Distance threshold for reaching a waypoint.")]
    [SerializeField] private float waypointThreshold = 10.0f;
    [Tooltip("Angle threshold for switching between straight lines and curves.")]
    [SerializeField] private float angleThreshold = 35.0f;

    // --- Steering ---
    [Header("Steering Settings")]
    [Tooltip("Left turn radius (how far the front left wheel can rotate).")]
    [SerializeField] private float leftTurnRadius = 10.0f;
    [Tooltip("Right turn radius (how far the front right wheel can rotate).")]
    [SerializeField] private float rightTurnRadius = 30.0f;

    [SerializeField] private int lookAheadIndex = 5;

    // --- Corner Slowdown ---
    [Header("AI Turn Slowdown Settings")]
    [Tooltip("Degrees: Only slow down for turns sharper than this.")]
    [SerializeField] private float slowdownThreshold = 30f;
    [Tooltip("Degrees: Max slowdown at this angle or above.")]
    [SerializeField] private float maxSlowdownAngle = 90f;
    [Tooltip("Minimum speed factor at max angle (e.g. 0.35 = 35% of Maxspeed).")]
    [SerializeField] private float minSlowdown = 0.35f;

    // --- Turn Detection ---
    [Header("Turn Detection Settings")]
    [Tooltip("Radius of the detection sphere for upcoming turns.")]
    [SerializeField] private float detectionRadius = 7.0f;
    [Tooltip("Tolerance for deviation from the Bezier curve.")]
    [SerializeField] private float curveTolerance = 2.0f;

    // --- Avoidance ---
    [Header("Avoidance Settings")]
    [Tooltip("Extra buffer distance added to the safe radius for avoidance checks.")]
    [SerializeField] private float avoidanceBuffer = 5.0f;
    [Tooltip("How far to offset laterally when dodging another car.")]
    [SerializeField] private float avoidanceLateralOffset = 2.0f;
    private float avoidanceOffset = 0f;
    public float safeRadius { get; private set; }

    // --- Boost ---
    [Header("Boost Settings")]
    [Tooltip("Multiplier applied to speed and acceleration when boosting.")]
    [SerializeField] private float boostMultiplier = 1.25f;
    private bool isBoosting = false;


    // --- References ---
    [Header("References")]
    [Tooltip("Rigidbody component of the car.")]
    public Rigidbody carRb { get; private set; }
    [Tooltip("Reference to the player car.")]
    private PlayerCarController playerCar;
    public AiCarManager aiCarManager;
    private Collider carCollider;
    public float CarWidth { get; private set; }
    public float CarLength { get; private set; }

    
    private float playerCarWidth;
    private float playerCarLength;
    private Transform[] waypoints;
    private Vector3 targetPoint;
    private int currentWaypointIndex = 0;
    private BaseCarController.Wheel[] frontWheels = Array.Empty<BaseCarController.Wheel>();
    private float targetTorque;
    private float moveInput = 0f;
    private LayerMask objectLayerMask;
    private float steerInput;
    private float avoidance;
    public AiCarController Initialize(
        AiCarManager aiCarManager, 
        Collider playerCollider, 
        AiCarManager.DifficultyStats difficultyStats
        )
    {
        this.aiCarManager = aiCarManager;
        Collider pc = playerCollider.GetComponent<Collider>();
        playerCarWidth = pc.bounds.size.x;
        playerCarLength = pc.bounds.size.z;
        Maxspeed = difficultyStats.maxSpeed;
        MaxAcceleration = difficultyStats.maxAccel;
        avoidance = difficultyStats.avoidance;
        return this;
    }

    private void Awake()
    {
        playerCar = GameManager.instance.CurrentCar.GetComponentInChildren<PlayerCarController>();
        
        frontWheels = Wheels.Where(w => w.Axel == Axel.Front).ToArray();
        if (carRb == null) carRb = GetComponentInChildren<Rigidbody>();
        carRb.centerOfMass = DEFAULT_CENTER_OF_MASS;

        carCollider = GetComponentInChildren<Collider>();
        if (carCollider != null)
        {
            CarWidth = carCollider.bounds.size.x;
            CarLength = carCollider.bounds.size.z;
            safeRadius = Mathf.Max(CarWidth, CarLength) * 0.5f;
        }
    }

    private void Start()
    {
        Grass = LayerMask.NameToLayer("Grass");
        objectLayerMask = LayerMask.NameToLayer("roadObjects");
    }

    private void FixedUpdate()
    {
        // Airborne?
        if (Physics.Raycast(carRb.position, Vector3.down, GROUND_RAY_LENGTH))
        {
            // Apply gravity
            carRb.AddForce(GravityMultiplier * Physics.gravity.magnitude * Vector3.down, ForceMode.Acceleration);
        }

        // Set new waypoint if close enough to current
        if (Vector3.Distance(carRb.position, aiCarManager.Waypoints[currentWaypointIndex]) < waypointThreshold)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % aiCarManager.Waypoints.Count();
        }

        targetPoint = aiCarManager.Waypoints[currentWaypointIndex];

        AvoidObstacles();

        carRb.rotation = Quaternion.Lerp(
            carRb.rotation,
            Quaternion.LookRotation(new Vector3(targetPoint.x - carRb.position.x, 0, targetPoint.z - carRb.position.z)),
            TurnSensitivty * Time.fixedDeltaTime
        );

        foreach (Wheel wheel in frontWheels) wheel.WheelCollider.steerAngle = carRb.rotation.y;

        ApplyDriveInputs();
    }

    private void ApplyDriveInputs()
    {
        moveInput = 1.0f;
        targetTorque = moveInput * MaxAcceleration;

        if (Mathf.Abs(steerInput) > 0.5f)
        {
            targetTorque *= 0.5f;
        }

        if (IsOnGrass())
        {
            targetTorque *= GrassSpeedMultiplier;
        }

        // Apply boost if active
        float speedLimit = Maxspeed;
        if (isBoosting)
        {
            speedLimit = (Maxspeed * boostMultiplier) + 20f; // Add flat +20
            targetTorque *= boostMultiplier;
        }

        foreach (Wheel wheel in Wheels)
        {
            wheel.WheelCollider.motorTorque = targetTorque;
            wheel.WheelCollider.brakeTorque = 0f;
        }

        // Apply speed limit
        ApplySpeedLimit(speedLimit);
    }

    private void AvoidObstacles()
    {
        float avoidanceOffset = 0f;
        
        RaycastHit[] hits = Physics.BoxCastAll(center:carRb.transform.forward * CarLength + carRb.position, halfExtents:new Vector3(CarLength * 4, 2, CarWidth), direction:carRb.transform.forward, orientation:carRb.transform.rotation,  maxDistance:CarLength * 2, layerMask:objectLayerMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject.layer != objectLayerMask) continue;

            float steerDirection = Vector3.Cross(carRb.transform.forward, hit.point).y > 0 ? -1f : 1f;
            avoidanceOffset += steerDirection * avoidanceLateralOffset * avoidance;
        }

        foreach (var other in aiCarManager.AiCars)
        {
            if (other == this) continue;

            Vector3 toOther = other.carRb.position - carRb.position;
            float distance = toOther.magnitude;
            float otherSafeRadius = Mathf.Max(other.CarWidth, other.CarLength) * 0.5f;
            float minSafeDistance = safeRadius + otherSafeRadius + avoidanceBuffer;

            if (distance < minSafeDistance && Vector3.Dot(carRb.transform.forward, toOther.normalized) > 0.5f)
            {
                Vector3 myFuturePos = carRb.position + carRb.linearVelocity * 0.5f;
                Vector3 otherFuturePos = other.carRb.position + other.carRb.linearVelocity * 0.5f;
                float futureDist = (myFuturePos - otherFuturePos).magnitude;

                if (futureDist < minSafeDistance)
                {
                    float steerDirection = Vector3.Cross(carRb.transform.forward, toOther).y > 0 ? -1f : 1f;
                    avoidanceOffset += steerDirection * avoidanceLateralOffset * avoidance;

                    if (distance < minSafeDistance * 0.5f && carRb.linearVelocity.magnitude > other.carRb.linearVelocity.magnitude)
                    {
                        moveInput = 0.7f;
                    }
                }
            }
        }

        if (playerCar != null && playerCar.CarRb != null && playerCar != this)
        {
            Vector3 toPlayer = playerCar.transform.position - carRb.transform.position;
            float distance = toPlayer.magnitude;
            float playerSafeRadius = Mathf.Max(playerCarWidth, playerCarLength) * 0.5f;
            float minSafeDistance = safeRadius + playerSafeRadius + avoidanceBuffer;

            if (distance < minSafeDistance && Vector3.Dot(carRb.transform.forward, toPlayer.normalized) > 0.5f)
            {
                Vector3 myFuturePos = carRb.transform.position + carRb.linearVelocity * 0.5f;
                Vector3 playerFuturePos = playerCar.transform.position + playerCar.CarRb.linearVelocity * 0.5f;
                float futureDist = (myFuturePos - playerFuturePos).magnitude;

                if (futureDist < minSafeDistance)
                {
                    float steerDirection = Vector3.Cross(carRb.transform.forward, toPlayer).y > 0 ? -1f : 1f;
                    avoidanceOffset += steerDirection * avoidanceLateralOffset;

                    if (distance < minSafeDistance * 0.5f && carRb.linearVelocity.magnitude > playerCar.CarRb.linearVelocity.magnitude) moveInput = 0.7f;
                }
            }
        }

        Vector3 localPosition = carRb.gameObject.transform.InverseTransformPoint(targetPoint);
        localPosition.x += avoidanceOffset;


        targetPoint = carRb.gameObject.transform.TransformPoint(localPosition);

    }
}