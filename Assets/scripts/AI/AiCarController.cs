using UnityEngine;
using System.Linq;
using System;

public class AiCarController : BaseCarController
{
    private const float GROUND_RAY_LENGTH = 0.5f;
    private static readonly Vector3 DEFAULT_CENTER_OF_MASS = new(0, -0.0f, 0);
    [Header("Path Following Settings")]
    [Tooltip("Distance threshold for reaching a waypoint.")]
    [SerializeField] private float waypointThreshold = 10.0f;

    [Tooltip("Minimum % of max speed to slow down to when turning")]
    [UnityEngine.Range(0f, 1f)]
    [SerializeField] private float minSlowdown = 0.5f;

    [Header("Turn Detection Settings")]
    [Tooltip("Tolerance in angle for deviation from the path")]
    [SerializeField] private float curveTolerance = 2.0f;

    [Header("Avoidance Settings")]
    [Tooltip("Extra buffer distance added to the safe radius for avoidance checks.")]
    [SerializeField] private float avoidanceBuffer = 5.0f;
    [Tooltip("How far to offset laterally when dodging another car.")]
    [SerializeField] private float avoidanceLateralOffset = 2.0f;
    [SerializeField] private float maxAvoidanceOffset = 8f;
    public float SafeRadius { get; private set; }

    [Header("Boost Settings")]
    [Tooltip("Multiplier applied to speed and acceleration when boosting.")]
    [SerializeField] private float boostMultiplier = 1.25f;
    [NonSerialized] public AiCarManager aiCarManager;
    private Vector3 targetPoint;
    private int currentWaypointIndex = 0;
    private int waypointSize;
    private BaseCarController.Wheel[] frontWheels = Array.Empty<BaseCarController.Wheel>();
    private float targetTorque;
    private float moveInput = 0f;
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
        this.startIndex = startIndex;
        this.isReversed = isReversed;
        return this;
    }

    override protected void Awake()
    {
        frontWheels = Wheels.Where(w => w.Axel == Axel.Front).ToArray();
        if (CarRb == null) CarRb = GetComponentInChildren<Rigidbody>();
        CarRb.centerOfMass = DEFAULT_CENTER_OF_MASS;
    }

    override protected void Start()
    {
        waypointSign = isReversed ? -1 : 1;
        currentWaypointIndex = startIndex;

        waypointSize = aiCarManager.Waypoints.Count();
        targetPoint = aiCarManager.Waypoints[currentWaypointIndex].position;
        
        base.Start();

        SafeRadius = Mathf.Max(CarExtents.x, CarExtents.z) * 0.5f;
    }

    override protected void FixedUpdate()
    {
        // Gravity
        if (Physics.Raycast(CarRb.position, Vector3.down, GROUND_RAY_LENGTH)) CarRb.AddForce(Physics.gravity.magnitude * Vector3.down, ForceMode.Acceleration);

        // Set new waypoint if close enough to current
        if (Vector3.Distance(CarRb.position, targetPoint) < waypointThreshold || Vector3.Distance(CarRb.position, aiCarManager.Waypoints[currentWaypointIndex].position) < waypointThreshold)
        {
            int newIndex = currentWaypointIndex + waypointSign;
            currentWaypointIndex = (Math.Sign(newIndex) >= 0 ? newIndex : waypointSize - 1) % waypointSize;
            TargetMaxSpeed = Mathf.Clamp(Mathf.Sqrt(Maxspeed * aiCarManager.PointRadi[currentWaypointIndex]) * 1.3f, Maxspeed * minSlowdown, Maxspeed) / 3.6f;
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
        base.FixedUpdate();
    }

    private void ApplyDriveInputs()
    {
        moveInput = 1.0f;
        targetTorque = moveInput * MaxAcceleration;

        foreach (Wheel wheel in Wheels)
        {
            wheel.WheelCollider.motorTorque = targetTorque;
            wheel.WheelCollider.brakeTorque = 0f;
        }
    }

    private void AvoidObstacles()
    {
        Vector3 localPosition = CarRb.transform.InverseTransformPoint(aiCarManager.Waypoints[currentWaypointIndex].position);
        float localX = localPosition.x;

        bool hasHit = false;
        foreach (BaseCarController other in GameManager.instance.spawnedCars)
        {
            if (other == this) continue;

            Vector3 toOther = other.CarRb.position - CarRb.position;
            float distance = Vector3.Distance(other.CarRb.position, CarRb.position);
            float otherSafeRadius = Mathf.Max(other.CarExtents.x, other.CarExtents.z) * 0.5f;
            float minSafeDistance = SafeRadius + otherSafeRadius + avoidanceBuffer;

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
    }
    #endif
}