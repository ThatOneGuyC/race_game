using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using UnityEngine.Analytics;
using Unity.Splines.Examples;


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
    [SerializeField] protected float MaxAcceleration = 700.0f;
    [SerializeField] protected float BrakeAcceleration = 500.0f;
    [Header("turn asetukset")]
    [SerializeField] protected float TurnSensitivty  = 1.0f;
    [SerializeField] protected float TurnSensitivtyAtHighSpeed  = 17.5f;
    [SerializeField] protected float TurnSensitivtyAtLowSpeed  = 30.0f;
    [SerializeField] protected float Deceleration  = 1.0f;
    [Min(100.0f)]
    [SerializeField] protected float Maxspeed  = 100.0f;
    [SerializeField] protected float GravityMultiplier  = 1.5f;
    [SerializeField] protected List<Wheel> Wheels;
    WheelHit hit;
    [SerializeField] protected float GrassSpeedMultiplier = 0.5f;
    protected LayerMask Grass;
    public bool GrassRespawnActive = false;
    protected bool isOnGrassCached;
    protected bool isOnGrassCachedValid;
    public float MoveInput;
    public float SteerInput;
    protected Vector3 _CenterofMass;
    protected float TargetTorque  = 0.0f;
    public Rigidbody CarRb { get; protected set; }
    public bool IsTurboActive { get; protected set; } = false;
    protected float Activedrift = 0.0f;
    [SerializeField] protected float Turbesped = 60.0f, BaseSpeed = 180f, Grassmaxspeed = 50.0f, DriftMaxSpeed = 140f;
    [Header("Drift asetukset")]
    //protected float DriftMultiplier = 1.0f;
    public bool IsDrifting { get; protected set; } = false;
    protected Color GrassTrailColor = new Color(0.3f, 0.15f, 0.0f);
    protected Color RoadTrailColor = Color.black;
    protected float PerusMaxAccerelation, PerusTargetTorque, SmoothedMaxAcceleration;
    [Header("turbe asetukset")]
    protected Image TurbeMeter;
    [SerializeField] protected float TurbeAmount = 100.0f, TurbeMax = 100.0f, Turbepush = 15.0f;
    [SerializeField] protected float TurbeReduce = 10.0f;
    [SerializeField] protected float TurbeRegen = 10.0f;

    protected bool IsRegenerating = false;
    protected int TurbeRegenCoroutineAmount = 0;

    [NonSerialized] public bool CanDrift = false;
    [NonSerialized] public bool CanUseTurbo = false;

    public float GetSpeed()
    {
        GameManager.instance.carSpeed = CarRb.linearVelocity.magnitude * 3.6f;
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

        Grass = 1 << 7;

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

    protected bool IsWheelOnGrass(Wheel wheel)
    {
        if (wheel.WheelCollider.GetGroundHit(out hit))
        {
            return (Grass.value & (1 << hit.collider.gameObject.layer)) != 0;
        }
        return false;
    }

    protected void OnGrass()
    {
        int wheelsOnGrass = 0;

        foreach (var wheel in Wheels)
        {
            bool WheelOnGrass = IsWheelGrounded(wheel) && IsWheelOnGrass(wheel);

            if (WheelOnGrass) wheelsOnGrass++;

            var trail = wheel.WheelEffectobj.GetComponentInChildren<TrailRenderer>();

            trail.material.color = WheelOnGrass ? GrassTrailColor : RoadTrailColor;
        }


        ScoreManager.instance.SetOnGrass(wheelsOnGrass >= 2);
    }

    protected virtual bool IsOnGrass()
    {
        return Wheels.Any(wheel => IsWheelGrounded(wheel) && IsWheelOnGrass(wheel));
    }

    protected void AdjustSpeedForGrass()
    {
        if (IsOnGrass())
        {
            TargetTorque *= GrassSpeedMultiplier;

            Maxspeed = Mathf.Lerp(Maxspeed, Maxspeed * GrassSpeedMultiplier,Time.deltaTime);
        }
    }

    protected void ApplySpeedLimit(float speed)
    {
        if (speed <= Maxspeed) return;
        CarRb.linearVelocity = CarRb.linearVelocity.normalized * (Maxspeed / 3.6f);
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
        
            var _steerAngle = SteerInput * TurnSensitivty * (IsDrifting ? 0.8f : 0.35f);
            wheel.WheelCollider.steerAngle = Mathf.Lerp(wheel.WheelCollider.steerAngle, _steerAngle, 0.6f);            
        }
    }

    
    protected void ApplyGravity()
    {
        if (Wheels.All(w => !IsWheelGrounded(w)))
        {
            CarRb.AddForce(Vector3.down * GravityMultiplier * Physics.gravity.magnitude, ForceMode.Acceleration);
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
            var trailRenderer = wheel.WheelEffectobj.GetComponentInChildren<TrailRenderer>();
            bool wheelGrounded = IsWheelGrounded(wheel);
            bool shouldEmit = enable && wheelGrounded;

            if (shouldEmit)
            {
                trailRenderer.emitting = true;
                wheel.SmokeParticle.Play();
            }
            else
            {
                trailRenderer.emitting = false;
                wheel.SmokeParticle.Stop();
            }
        }
    }

    public void ClearWheelTrails()
    {
        foreach (var wheel in Wheels)
        {
            var trail = wheel.WheelEffectobj.GetComponentInChildren<TrailRenderer>();
            // explicitly stop emitting and clear the trail so it can be re-enabled later
            trail.emitting = false;
            trail.Clear();
            trail.enabled = true;
        }
    }

    /// <summary>
    /// käytetään TURBEmeterin päivittämiseen joka frame
    /// </summary>
    protected void TURBEmeter()
    {
        if (IsTurboActive && TurbeAmount != 0) //jos käytät turboa ja sitä o jäljellä
        {
            GameManager.instance.turbeActive = true;

            if (TurbeRegenCoroutineAmount > 0)
            {
                turbeRegenCoroutines("stop");
            }
            IsRegenerating = false;
            TurbeRegenCoroutineAmount = 0;

            TurbeAmount -= TurbeReduce * Time.deltaTime;
        }
        else if (!IsTurboActive && TurbeAmount < TurbeMax) //jos et käytä turboa ja se ei oo täynnä
        {

            GameManager.instance.turbeActive = false;

            if (TurbeRegenCoroutineAmount == 0 && IsRegenerating == false)
            {
                turbeRegenCoroutines("start");
                TurbeRegenCoroutineAmount += 1;
            }
        }

        if (TurbeAmount < 0)
        {
            TurbeAmount = 0;
        }
        if (TurbeAmount > TurbeMax)
        {
            //Debug.Log("I bought a property in Egypt, and what they do is they give you the property");
            TurbeAmount = TurbeMax;

            turbeRegenCoroutines("stop");
            IsRegenerating = false;
            TurbeRegenCoroutineAmount = 0;
        }

        TurbeMeter.fillAmount = TurbeAmount / TurbeMax;
    }

    /// <summary>
    /// käytetään TURBEn regeneroimiseen
    /// ...koska fuck C#
    /// </summary>
    private IEnumerator turbeRegenerate()
    {
        yield return new WaitForSecondsRealtime(2.0f);
        IsRegenerating = true;

        if (IsRegenerating && TurbeRegenCoroutineAmount == 1)
        {
            while (IsRegenerating && TurbeRegenCoroutineAmount == 1)
            {
                yield return StartCoroutine(RegenerateTurbeAmount());
            }
        }
        else
        {
            Debug.Log("stopped regen coroutine");
            yield break;
            //scriptin ei pitäs päästä tähä tilanteeseen missään vaiheessa, mutta se on täällä varmuuden vuoksi
        }
    }

    private IEnumerator RegenerateTurbeAmount()
    {
        TurbeAmount += TurbeRegen * Time.deltaTime;
        yield return null; // Wait for the next frame
    }

    /// <summary>
    /// aloita tai pysäytä TURBEn regenerointi coroutine
    /// </summary>
    /// <param name="option">start / stop</param>
    private void turbeRegenCoroutines(string option)
    {
        switch (option)
        {
            case "start":
                StartCoroutine("turbeRegenerate");
                break;

            case "stop":
                StopCoroutine("turbeRegenerate");
                break;
        }
    }
}
