using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;


public class BaseCarController : MonoBehaviour
{
    public enum Axel
    {
        Front,
        Rear
    }

    [Serializable]
    public struct Wheel
    {
        public GameObject WheelModel;
        public WheelCollider WheelCollider;

        public GameObject WheelEffectobj;
        public ParticleSystem SmokeParticle;
        public Axel Axel;
    }

    [Header("Auton asetukset")]
    [SerializeField] internal float MaxAcceleration = 700.0f;
    [SerializeField] protected float BrakeAcceleration = 500.0f;
    [Header("turn asetukset")]
    [SerializeField] protected float TurnSensitivity  = 1.0f;
    [SerializeField] protected float TurnSensitivityAtHighSpeed  = 17.5f;
    [SerializeField] protected float TurnSensitivityAtLowSpeed  = 30.0f;
    [SerializeField] protected float Deceleration  = 1.0f;
    [Min(100.0f)]
    [SerializeField] protected float Maxspeed  = 100.0f;
    [SerializeField] protected float GravityMultiplier  = 1.5f;
    [SerializeField] protected List<Wheel> Wheels;
    WheelHit hit;
    [Header("Trail settings")]
    [SerializeField] protected bool EmitTrailsOnlyWhileDrifting = true;
    public float MoveInput;
    public float SteerInput;
    protected Vector3 _CenterofMass;
    internal float TargetTorque  = 0.0f;
    public Rigidbody CarRb { get; protected set; }
    protected float Activedrift = 0.0f;
    [SerializeField] protected float Turbesped = 60.0f, TurbeChargeSped = 80, BaseSpeed = 180f, Grassmaxspeed = 50.0f, DriftMaxSpeed = 140f;
    [Header("Drift asetukset")]
    //protected float DriftMultiplier = 1.0f;
    public bool IsDrifting { get; protected set; } = false;
    protected Color RoadTrailColor = new Color(0.08f, 0.08f, 0.08f);
    internal float PerusMaxAccerelation, PerusTargetTorque, SmoothedMaxAcceleration;
    [Header("turbe asetukset")]
    protected Image TurbeBar;
    public bool IsTurboActive { get; internal set; } = false;
    [SerializeField] internal float TurbeAmount = 100.0f, TurbeMax = 100.0f, Turbepush = 15.0f, turbechargepush = 20;
    [SerializeField] protected float TurbeReduce = 10.0f;
    [SerializeField] protected float TurbeRegen = 10.0f;
    [SerializeField] protected float TurbeWaitTime = 2.0f;
    protected Coroutine TurbeRegeneration = null;

    [NonSerialized] public bool CanDrift = true;
    [NonSerialized] public bool CanUseTurbo = true;
    protected Collider carCollider;
    public float CarWidth { get; protected set; }
    public float CarLength { get; protected set; }

    protected virtual void Start()
    {
        carCollider = GetComponentInChildren<MeshCollider>();
        CarWidth = carCollider.bounds.size.x;
        CarLength = carCollider.bounds.size.z;
        ClearWheelTrails();
    }

    public float GetSpeed()
    {
        return CarRb.linearVelocity.magnitude * 3.6f;
    }

    public float GetMaxSpeed()
    {
        return Maxspeed;
    }

    protected bool IsWheelGrounded(Wheel wheel)
    {
        return wheel.WheelCollider.GetGroundHit(out hit);
    }

    [ContextMenu("Auto Assign Wheels")]
    protected void AutoAssignWheelsAndMaterials()
    {
        Wheels.Clear();

        var Colliders = GetComponentsInChildren<WheelCollider>(true);
        var Meshes = transform.GetComponentsInChildren<Transform>().First(obj => obj.name == "meshes");
        
        var Effects = transform.GetComponentsInChildren<Transform>().First(obj => obj.name == "wheelEffectobj");

        foreach (var WheelCollider in Colliders)
        {
            var wheel = new Wheel
            {
                WheelCollider = WheelCollider
            };

            var Mesh = Meshes.Find(WheelCollider.name);

            wheel.WheelModel = Mesh?.gameObject;

            var Effect = Effects.transform.Find(WheelCollider.name);

            wheel.WheelEffectobj = Effect?.gameObject;
                var trailRenderer = wheel.WheelEffectobj != null ? wheel.WheelEffectobj.GetComponentInChildren<TrailRenderer>(true) : null;
                if (trailRenderer != null && (trailRenderer.sharedMaterial == null || trailRenderer.sharedMaterial.shader == null || !trailRenderer.sharedMaterial.shader.isSupported)) trailRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                    wheel.SmokeParticle =
            wheel.WheelEffectobj != null
                ? wheel.WheelEffectobj.GetComponentInChildren<ParticleSystem>(true)
                : WheelCollider.transform.GetComponentInChildren<ParticleSystem>(true);

            wheel.Axel =
                WheelCollider.name.IndexOf("front", StringComparison.OrdinalIgnoreCase) >= 0
                    ? Axel.Front
                    : Axel.Rear;

            Wheels.Add(wheel);
        }
    }

    protected void ApplySpeedLimit(float speed)
    {
        // 3.6 is to convert from m/s to km/h and vice versa
        if (CarRb.linearVelocity.magnitude  > speed) CarRb.linearVelocity = speed * CarRb.linearVelocity.normalized;
    }



    protected void AdjustSuspension()
    {
        foreach (var wheel in Wheels)
        {
            JointSpring suspensionSpring = wheel.WheelCollider.suspensionSpring;
            suspensionSpring.spring = 8000.0f;
            suspensionSpring.damper = 5000.0f;
            wheel.WheelCollider.suspensionSpring = suspensionSpring;
        }
    }

    protected void AdjustForwardFrictrion()
    {
        foreach (var wheel in Wheels)
        {
            WheelFrictionCurve forwardFriction = wheel.WheelCollider.forwardFriction;
            forwardFriction.extremumSlip = 0.8f;
            forwardFriction.extremumValue = 1;
            forwardFriction.asymptoteSlip = 1.0f;
            forwardFriction.asymptoteValue = 1;
            forwardFriction.stiffness = 7f;
            wheel.WheelCollider.forwardFriction = forwardFriction;
        }
    }

    protected void Brakes(Wheel wheel)
    {
        wheel.WheelCollider.brakeTorque = BrakeAcceleration * 15f;
    }

    protected void MotorTorgue(Wheel wheel)
    {
        wheel.WheelCollider.motorTorque = TargetTorque;
        wheel.WheelCollider.brakeTorque = 0f;
    }

    

    protected void Decelerate()
    {

        if (MoveInput == 0)
        {
            Vector3 velocity = CarRb.linearVelocity;

            velocity -= velocity.normalized * Deceleration * 2.0f * Time.deltaTime;

            if (velocity.magnitude < 0.1f)
            {
                velocity = Vector3.zero;
            }
            CarRb.linearVelocity = velocity;
        }
    }



    protected void Steer()
    {
        foreach (var wheel in Wheels.Where(w => w.Axel == Axel.Front))
        {
        
            var _steerAngle = SteerInput * TurnSensitivity * (IsDrifting ? 0.8f : 0.35f);
            wheel.WheelCollider.steerAngle = Mathf.Lerp(wheel.WheelCollider.steerAngle, _steerAngle, 0.6f);            
        }
    }


    protected void AdjustWheelsForDrift()
    {
        foreach (var wheel in Wheels)
        {
            JointSpring suspensionSpring = wheel.WheelCollider.suspensionSpring;
            suspensionSpring.spring = 500.0f;
            suspensionSpring.damper = 2500.0f;
            wheel.WheelCollider.suspensionSpring = suspensionSpring;

            WheelFrictionCurve forwardFriction = wheel.WheelCollider.forwardFriction;
            forwardFriction.extremumSlip = 0.45f;
            forwardFriction.asymptoteSlip = 0.6f;
            forwardFriction.extremumValue = 1;
            forwardFriction.asymptoteValue = 1;
            forwardFriction.stiffness = 5.5f;
            wheel.WheelCollider.forwardFriction = forwardFriction;

            if (wheel.Axel == Axel.Front)
            {
                WheelFrictionCurve sidewaysFriction = wheel.WheelCollider.sidewaysFriction;
                sidewaysFriction.stiffness = 2f;
                wheel.WheelCollider.sidewaysFriction = sidewaysFriction;
            }
        }        
    }

    public void Animatewheels()
    {
        foreach (var wheel in Wheels)
        {
            wheel.WheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            wheel.WheelModel.transform.SetPositionAndRotation(pos, rot);
        }
    }

    //bobbing effect

    /// <summary>
    /// calls tje wjeeöeffects
    /// </summary>
    protected void WheelEffects(bool enable)
    {
        foreach (var wheel in Wheels.Where(w => w.Axel == Axel.Rear))
        {
            if (wheel.WheelEffectobj == null) continue;

            var trailRenderer = wheel.WheelEffectobj.GetComponentInChildren<TrailRenderer>();
            if (trailRenderer == null) continue;
            bool wheelGrounded = IsWheelGrounded(wheel);

            bool shouldEmit = enable && wheelGrounded;

            trailRenderer.enabled = true;

            if (shouldEmit)
            {
                trailRenderer.emitting = true;
                if (wheel.SmokeParticle != null) wheel.SmokeParticle.Play();
            }
            else
            {
                trailRenderer.emitting = false;
                if (wheel.SmokeParticle != null) wheel.SmokeParticle.Stop();
            }
        }
    }

    public void ClearWheelTrails()
    {
        foreach (var wheel in Wheels)
        {
            if (wheel.WheelEffectobj == null) continue;

            var trail = wheel.WheelEffectobj.GetComponentInChildren<TrailRenderer>();
            if (trail == null) continue;

            trail.emitting = false;
            trail.Clear();
            trail.enabled = true;
        }
    }

    protected void TurbeMeter()
    {
        if (IsTurboActive)
        {
            if (TurbeRegeneration != null) 
            {
                StopCoroutine(TurbeRegeneration);
                TurbeRegeneration = null;
            }
            TurbeAmount = Mathf.Max(TurbeAmount - TurbeReduce * Time.deltaTime, 0f);
        }
        else if (TurbeAmount < TurbeMax && TurbeRegeneration == null) TurbeRegeneration = StartCoroutine(RegenerateTurbe());
        TurbeBar.fillAmount = TurbeAmount / TurbeMax;
    }

    private IEnumerator RegenerateTurbe()
    {
        yield return new WaitForSeconds(TurbeWaitTime);

        while (TurbeAmount < TurbeMax)
        {
            TurbeAmount = Mathf.Min(TurbeAmount + TurbeRegen * Time.deltaTime, TurbeMax);
            yield return null;
        }

        TurbeRegeneration = null;
        yield break;
    }
}