using UnityEngine;
using System.Linq;
using System;
using UnityEngine.SocialPlatforms;
using NUnit.Framework;

public class AiCarController : BaseCarController
{
    #pragma warning disable 0414
    // --- Constants ---
    private const float GROUND_RAY_LENGTH = 0.5f;
    private static readonly Vector3 DEFAULT_CENTER_OF_MASS = new(0, -0.0f, 0);
        // --- Path Following ---
    [Header("Path Following Settings")]
    [Tooltip("Distance threshold for reaching a waypoint.")]
    [SerializeField] private float waypointThreshold = 10.0f;

    // --- Steering ---
    [Tooltip("How many waypoints to look ahead for at max when finding the nearest one.")]
    [SerializeField] private int lookAheadIndex = 5;

    // --- Corner Slowdown ---
    [Tooltip("Minimum % of max speed to slow down to when turning")]
    [UnityEngine.Range(0f, 1f)]
    [SerializeField] private float minSlowdown = 0.5f;

    // --- Turn Detection ---
    [Header("Turn Detection Settings")]
    [Tooltip("Tolerance in angle for deviation from the path")]
    [SerializeField] private float curveTolerance = 2.0f;

    // --- Avoidance ---
    [Header("Avoidance Settings")]
    [Tooltip("Extra buffer distance added to the safe radius for avoidance checks.")]
    [SerializeField] private float avoidanceBuffer = 5.0f;
    [Tooltip("How far to offset laterally when dodging another car.")]
    [SerializeField] private float avoidanceLateralOffset = 2.0f;
    [SerializeField] private float maxAvoidanceOffset = 8f;
    [SerializeField] private int objectAvoidanceBeams = 10;
    [SerializeField] private float avoidanceBeamLenght = 10.0f;
    [SerializeField] private float beamAngle = 45f;
    public float safeRadius { get; private set; }

    // --- Boost ---
    [Header("Boost Settings")]
    [Tooltip("Multiplier applied to speed and acceleration when boosting.")]
    [SerializeField] private float boostMultiplier = 1.25f;
    private bool isBoosting = false;
    [NonSerialized] public AiCarManager aiCarManager;
    private Vector3 targetPoint;
    private int currentWaypointIndex = 0;
    private int waypointSize;
    private BaseCarController.Wheel[] frontWheels = Array.Empty<BaseCarController.Wheel>();
    private float targetTorque;
    private float moveInput = 0f;
    private LayerMask objectLayerMask;
    private float steerInput;
    private float avoidance;
    // Used for calculating the speed on curves
    private float speedLimit;
    private int waypointSign = 1;
    private int startIndex = 0;
    private bool isReversed;
    public AiCarController Initialize(
        AiCarManager aiCarManager, 
        AiCarManager.DifficultyStats difficultyStats,
        int startIndex,
        bool isReversed
        )
    {
        this.aiCarManager = aiCarManager;
        Maxspeed = difficultyStats.maxSpeed;
        MaxAcceleration = difficultyStats.maxAcceleration;
        avoidance = difficultyStats.avoidance;
        this.startIndex = startIndex;
        this.isReversed = isReversed;
        return this;
    }

    private void Awake()
    {
        
        frontWheels = Wheels.Where(w => w.Axel == Axel.Front).ToArray();
        if (CarRb == null) CarRb = GetComponentInChildren<Rigidbody>();
        CarRb.centerOfMass = DEFAULT_CENTER_OF_MASS;

        carCollider = GetComponentInChildren<Collider>();
        if (carCollider != null)
        {
            CarWidth = carCollider.bounds.size.x;
            CarLength = carCollider.bounds.size.z;
        }
    }

    override protected void Start()
    {
        objectLayerMask = LayerMask.NameToLayer("roadObjects");
        waypointSign = isReversed ? -1 : 1;
        currentWaypointIndex = startIndex;

        waypointSize = aiCarManager.Waypoints.Count();
        targetPoint = aiCarManager.Waypoints[currentWaypointIndex].position;
        speedLimit = Mathf.Clamp(Mathf.Sqrt(Maxspeed * aiCarManager.PointRadi[currentWaypointIndex]), Maxspeed * minSlowdown, Maxspeed) / 3.6f;
        
        base.Start();

        safeRadius = Mathf.Max(CarWidth, CarLength) * 0.5f;
    }

    private void FixedUpdate()
    {
        // Gravity
        if (Physics.Raycast(CarRb.position, Vector3.down, GROUND_RAY_LENGTH)) CarRb.AddForce(GravityMultiplier * Physics.gravity.magnitude * Vector3.down, ForceMode.Acceleration);

        // Set new waypoint if close enough to current
        if (Vector3.Distance(CarRb.position, targetPoint) < waypointThreshold || Vector3.Distance(CarRb.position, aiCarManager.Waypoints[currentWaypointIndex].position) < waypointThreshold)
        {
            int newIndex = currentWaypointIndex + waypointSign;
            currentWaypointIndex = (Math.Sign(newIndex) >= 0 ? newIndex : waypointSize - 1) % waypointSize;
            speedLimit = Mathf.Clamp(Mathf.Sqrt(Maxspeed * aiCarManager.PointRadi[currentWaypointIndex]) * 1.3f, Maxspeed * minSlowdown, Maxspeed) / 3.6f;
            targetPoint = aiCarManager.Waypoints[currentWaypointIndex].position;
        }

        // Prevent car from jiggling when already pointing at the target
        if (Vector3.Angle(CarRb.rotation.eulerAngles.normalized, targetPoint.normalized) > curveTolerance)
        {
            // Rotate car itself
            CarRb.rotation = Quaternion.Lerp(
                CarRb.rotation,
                Quaternion.LookRotation(
                    new Vector3(targetPoint.x - CarRb.position.x, 0, targetPoint.z - CarRb.position.z)
                ),
                TurnSensitivity
            );

            // Turn wheels
            foreach (Wheel wheel in frontWheels) wheel.WheelCollider.steerAngle = CarRb.rotation.y;
        }

        AvoidObstacles();
        ApplyDriveInputs();
        ApplySpeedLimit(speedLimit);
    }

    private void ApplyDriveInputs()
    {
        moveInput = 1.0f;
        targetTorque = moveInput * MaxAcceleration;

        if (Mathf.Abs(steerInput) > 0.5f)
        {
            targetTorque *= 0.5f;
        }

        // Apply boost if active
        if (isBoosting)
        {
            speedLimit = (Maxspeed * boostMultiplier) + 20f;
            targetTorque *= boostMultiplier;
        }

        foreach (Wheel wheel in Wheels)
        {
            wheel.WheelCollider.motorTorque = targetTorque;
            //Debug.Log(wheel.WheelCollider.motorTorque);
            wheel.WheelCollider.brakeTorque = 0f;
        }
    }

    private void AvoidObstacles()
    {
        Vector3 localPosition = CarRb.transform.InverseTransformPoint(aiCarManager.Waypoints[currentWaypointIndex].position);
        float localX = localPosition.x;

        // HashSet<GameObject> hitObjects = new();
        // bool hasHit = false;
        // for (int i = objectAvoidanceBeams / -2; i <= objectAvoidanceBeams / 2; i++)
        // {
        //     // For some reason the object layer mask doesnt work
        //     if (Physics.Raycast(origin:CarRb.position + CarRb.transform.up, direction:Quaternion.AngleAxis(beamAngle / objectAvoidanceBeams * (i + objectAvoidanceBeams / 2f) - beamAngle / 2f, CarRb.transform.up) * CarRb.transform.forward, maxDistance:avoidanceBeamLenght, hitInfo:out RaycastHit hit))
        //     {
        //         GameObject go = hit.transform.gameObject;
        //         if (go == null || hitObjects.Contains(go)) continue;

        //         BaseCarController carController = go.GetComponentInChildren<BaseCarController>();
        //         if (carController != null || go.layer == objectLayerMask)
        //         {
        //             int sign = i < 1 ? -1 : 1; // Not sign because 0 would scuff it up
        //             // Debug.Log($"doing {(Math.Abs(i) - objectAvoidanceBeams / 2f) * sign}, hit with beam #{i + objectAvoidanceBeams / 2}, local pos is {localPosition.x} and target pos is {localTarget.x}, distance is {Vector3.Distance(aiCarManager.Waypoints[currentWaypointIndex].position, localPosition)}");
        //             localPosition.x += (Math.Abs(i) - objectAvoidanceBeams / 2f + (hasHit ? avoidanceBuffer : 0)) * sign;
        //             hitObjects.Add(go);
        //             hasHit = true;
        //         }
        //     }
        // }
        // if (!hasHit) return;

        bool hasHit = false;
        foreach (BaseCarController other in GameManager.instance.spawnedCars)
        {
            if (other == this) continue;

            Vector3 toOther = other.CarRb.position - CarRb.position;
            float distance = Vector3.Distance(other.CarRb.position, CarRb.position);
            float otherSafeRadius = Mathf.Max(other.CarWidth, other.CarLength) * 0.5f;
            float minSafeDistance = safeRadius + otherSafeRadius + avoidanceBuffer;

            if (distance < minSafeDistance && Vector3.Dot(CarRb.transform.forward, toOther.normalized) > 0.3f)
            {
                Vector3 myFuturePos = CarRb.position + CarRb.linearVelocity;
                Vector3 otherFuturePos = other.CarRb.position + other.CarRb.linearVelocity;
                float futureDist = (myFuturePos - otherFuturePos).magnitude;

                if (futureDist < minSafeDistance)
                {
                    int steerDirection = Vector3.Cross(CarRb.transform.forward, toOther).y > 0 ? -1 : 1;
                    localPosition.x += (avoidanceLateralOffset + (hasHit ? avoidanceBuffer : 0)) * steerDirection;
                    hasHit = true;

                    if (distance < minSafeDistance * 0.7f && CarRb.linearVelocity.magnitude > other.CarRb.linearVelocity.magnitude) moveInput = 0.7f; 
                }
            }
        }
        if (!hasHit) return;
        
        if (Mathf.Abs(Mathf.Abs(localPosition.x) - Mathf.Abs(localX)) > maxAvoidanceOffset) localPosition.x = maxAvoidanceOffset * Mathf.Sign(localPosition.x);
        targetPoint = CarRb.transform.TransformPoint(localPosition);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(targetPoint, 0.5f);
        Gizmos.DrawLine(CarRb.position, targetPoint);
        Gizmos.DrawSphere(CarRb.transform.forward + CarRb.position, 0.5f);
        
        // for (int i = 1; i <= objectAvoidanceBeams; i++)
        // {
        //     Gizmos.DrawRay(CarRb.position + CarRb.transform.up, Quaternion.AngleAxis(beamAngle / objectAvoidanceBeams * i - beamAngle / 2f, CarRb.transform.up) * CarRb.transform.forward * avoidanceBeamLenght);
        // } 
    }
    #endif
}