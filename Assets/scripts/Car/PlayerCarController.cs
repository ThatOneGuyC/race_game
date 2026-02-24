using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Logitech;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using UnityEngine.AI;
using System.Collections;


public class PlayerCarController : BaseCarController
{


    private CarInputActions Controls;
    RacerScript racerScript;
    //LogitechMovement LGM;


    private PlayerInput PlayerInput;
    private string CurrentControlScheme = "Keyboard";

    protected Coroutine DriftBoost;
    


    

    void Awake()
    {
        Controls = new CarInputActions();
        Controls.Enable();
        TurbeMeter = GameObject.Find("turbeFull").GetComponent<Image>();
        AutoAssignWheelsAndMaterials();
    }

    void Start()
    {
        PerusMaxAccerelation = MaxAcceleration;
        SmoothedMaxAcceleration = PerusMaxAccerelation;
        PerusTargetTorque = TargetTorque;
        if (CarRb == null)
            CarRb = GetComponent<Rigidbody>();
        CarRb.centerOfMass = _CenterofMass;
        if (GameManager.instance.sceneSelected != "tutorial")
        {
            CanDrift = true;
            CanUseTurbo = true;
        }
        racerScript = FindAnyObjectByType<RacerScript>();


        //LGM.InitializeLogitechWheel(); 


    }

    private void OnControlsChanged(PlayerInput input)
    {
        CurrentControlScheme = input.currentControlScheme;
    }


    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        SteerInput = ctx.ReadValue<Vector2>().x;
    }
    void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        SteerInput = 0f;
    }

    // void OnApplicationFocus(bool focus)
    // {
    //     if (focus){
    //         if (//LGM.logitechInitialized && LogitechGSDK.LogiIsConnected(0))
    //         {
    //             LogitechGSDK.LogiUpdate();
    //         }
    //     }
    // }


    private void OnEnable()
    {
        Controls.Enable();
        if (PlayerInput != null)
            PlayerInput.onControlsChanged += OnControlsChanged;

        // INPUT SUBSCRIPTIONS: KERRAN
        Controls.CarControls.Move.performed += OnMovePerformed;
        Controls.CarControls.Move.canceled  += OnMoveCanceled;

        Controls.CarControls.Drift.performed   += OnDriftPerformed;
        Controls.CarControls.Drift.canceled    += OnDriftCanceled;
    }

    private void OnDisable()
    {
        Controls.Disable();
        if (PlayerInput != null)
            PlayerInput.onControlsChanged -= OnControlsChanged;

        // UNSUBSCRIBE!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        Controls.CarControls.Move.performed -= OnMovePerformed;
        Controls.CarControls.Move.canceled  -= OnMoveCanceled;
        Controls.CarControls.Drift.performed -= OnDriftPerformed;
        Controls.CarControls.Drift.canceled  -= OnDriftCanceled;
        //LGM.StopAllForceFeedback();
    }

    private void OnDestroy()
    {
        Controls.Disable();
        Controls.Dispose();
        
        //LGM.StopAllForceFeedback();
    }


    void Update()
    {
        GetInputs();
        Animatewheels();
        // detect connection state changes and print once when it changes
    //     bool currentlyConnected = //LGM.logitechInitialized && LogitechGSDK.LogiIsConnected(0);
    //     if (currentlyConnected != //LGM.lastLogiConnected)
    //     {
    //         //LGM.lastLogiConnected = currentlyConnected;
    //         Debug.Log($"[CarController] Logitech connection status: {(currentlyConnected ? "Connected" : "Disconnected")}");
    //     }

    //     if (//LGM.logitechInitialized && LogitechGSDK.LogiIsConnected(0))
    //     {
    //         LogitechGSDK.LogiUpdate();
    //         //LGM.GetLogitechInputs();
    //         //LGM.ApplyForceFeedback(); 
    //     }
    }


    void FixedUpdate()
    {
        float speed = CarRb.linearVelocity.magnitude * 3.6f;
        isOnGrassCachedValid = false;
        ApplySpeedLimit(speed);

        UpdateDriftSpeed();

        ApplyGravity();
        Move();
        Steer();

        Decelerate();
        Applyturnsensitivity(speed);
        OnGrass();
        HandleTurbo();

        WheelEffects(IsDrifting);
    }


    void UpdateDriftSpeed()
    {
        if (!IsDrifting) return;

        if (IsTurboActive)
            Maxspeed = Mathf.Lerp(Maxspeed, BaseSpeed + Turbesped, Time.deltaTime * 0.5f);
        else
            Maxspeed = Mathf.Lerp(Maxspeed, DriftMaxSpeed, Time.deltaTime * 0.03f);

        
        if (Mathf.Abs(SteerInput) > 0.1f)
        {
            CarRb.AddTorque(Vector3.up * Time.deltaTime, ForceMode.Acceleration);
        }
    }




    void GetInputs()
    {
        //lukee inputin valuen ja etenee siittä
        //LGM.logitechInitialized || !LogitechGSDK.LogiIsConnected(0))
        
        SteerInput = Controls.CarControls.Move.ReadValue<Vector2>().x;
        
        
        if (Controls.CarControls.MoveForward.IsPressed())
            MoveInput = Controls.CarControls.MoveForward.ReadValue<float>();
        else if (Controls.CarControls.MoveBackward.IsPressed())
            MoveInput = -Controls.CarControls.MoveBackward.ReadValue<float>();
        else
            MoveInput = 0f;

        if (!Controls.CarControls.Drift.IsPressed())
            StopDrifting();
    }

    void Applyturnsensitivity(float speed)
    {
        TurnSensitivty = Mathf.Lerp(
            TurnSensitivtyAtLowSpeed,
            TurnSensitivtyAtHighSpeed,
            Mathf.Clamp01(speed / Maxspeed));
    }

    protected void HandleTurbo()
    {
        if (!CanUseTurbo) return;
        TURBE();
        TURBEmeter();
    }

    protected void TURBE()
    {
        IsTurboActive = Controls.CarControls.turbo.IsPressed() && TurbeAmount > 0;
        if (IsTurboActive)
        {
            CarRb.AddForce(transform.forward * Turbepush, ForceMode.Acceleration);
            TargetTorque = PerusTargetTorque * 1.5f;                
            TargetTorque = Mathf.Min(TargetTorque, MaxAcceleration); 
        }
    }

    void Move()
    {
        //HandeSteepSlope();
        UpdateTargetTorque();
        AdjustSpeedForGrass();
        AdjustSuspension();
        foreach (var wheel in Wheels)
        {
            if (Controls.CarControls.Brake.IsPressed()) Brakes(wheel);
            else MotorTorgue(wheel);
        }
    }

    private void UpdateTargetTorque()
    {
        float inputValue = CurrentControlScheme == "Gamepad"
            ? Controls.CarControls.ThrottleMod.ReadValue<float>()
            : Mathf.Abs(MoveInput);

        float power = CurrentControlScheme == "Gamepad" ? 0.9f : 1.0f;

        float throttle = Mathf.Pow(inputValue, power);
        
        // Reduce power during drift but don'turbe eliminate it


        float steerFactor = Mathf.Clamp01(Mathf.Abs(SteerInput));
        float driftPowerMultiplier = IsDrifting ? Mathf.Lerp(0.65f, 0.85f, steerFactor) : 1.0f;
        float targetMaxAcc = PerusMaxAccerelation * Mathf.Lerp(0.4f, 1f, throttle) * driftPowerMultiplier;

        SmoothedMaxAcceleration = Mathf.MoveTowards(
            SmoothedMaxAcceleration,
            targetMaxAcc,
            Time.deltaTime * 250f
        );

        float rawTorque = MoveInput * SmoothedMaxAcceleration;
        float forwardVel = Vector3.Dot(CarRb.linearVelocity, transform.forward);
        if (IsDrifting && forwardVel > 0.5f && rawTorque < 0f) rawTorque = 0f;

        TargetTorque = rawTorque;

        // Additional hard reduction while drifting so the car loses speed even when not turning
        if (IsDrifting)
        {
            TargetTorque *= 0.5f; // reduce to 50% while drifting
        }

        if (!IsDrifting)
        {
            float targetMaxSpeed = IsTurboActive ? BaseSpeed + Turbesped : BaseSpeed;
            Maxspeed = Mathf.Lerp(Maxspeed, targetMaxSpeed, Time.deltaTime);
        }
    }



    public float GetDriftSharpness()
    {
        //Checks the drifts sharpness so scoremanager can see how good of a drift you're doing
        if (IsDrifting)
        {
            Vector3 velocity = CarRb.linearVelocity;
            Vector3 forward = transform.forward;
            float angle = Vector3.Angle(forward, velocity);
            return angle;  
        }
        return 0.0f;
    }

    //i hate this so much, its always somewhat broken but for now....... its not broken.
    void OnDriftPerformed(InputAction.CallbackContext ctx)
    {
        if (IsDrifting || GameManager.instance.isPaused || !CanDrift || racerScript.raceFinished) return;

        Activedrift++;
        IsDrifting = true;

        MaxAcceleration = PerusMaxAccerelation * 0.95f;

        foreach (var wheel in Wheels)
        {
            if (wheel.WheelCollider == null) continue;
            WheelFrictionCurve sideways = wheel.WheelCollider.sidewaysFriction;
            sideways.extremumSlip   = 0.9f;
            sideways.asymptoteSlip  = 1.6f;
            sideways.extremumValue  = 1.0f;
            sideways.asymptoteValue = 1.2f;
            sideways.stiffness      = 2.0f;
            wheel.WheelCollider.sidewaysFriction = sideways;
        }

        CarRb.angularDamping = 0.03f;
        AdjustWheelsForDrift();
        WheelEffects(true);
    }

    void OnDriftCanceled(InputAction.CallbackContext ctx)
    {
        StopDrifting();
        OnDriftEndBoost();
        MaxAcceleration = PerusMaxAccerelation;
        TargetTorque = PerusTargetTorque;
        WheelEffects(false);
    }

    override protected bool IsOnGrass()
    {
        if (Wheels.Any(wheel => IsWheelGrounded(wheel) && IsWheelOnGrass(wheel)))
        {
            if (GrassRespawnActive && racerScript != null) racerScript.RespawnAtLastCheckpoint();
            return true;
        }
        return false;
    }

    internal void StopDrifting()
    {
        Activedrift = 0;
   
        IsDrifting = false;
        MaxAcceleration = PerusMaxAccerelation;
        CarRb.angularDamping = 0.1f;
        if (racerScript != null && (racerScript.raceFinished || GameManager.instance.carSpeed < 20.0f))
        {
        }
        AdjustForwardFrictrion();
        AdjustSuspension();

        foreach (var wheel in Wheels)
        {
            if (wheel.WheelCollider == null) continue;

            WheelFrictionCurve sidewaysFriction = wheel.WheelCollider.sidewaysFriction;
            sidewaysFriction.extremumSlip = 0.15f;
            sidewaysFriction.asymptoteSlip = 0.3f;
            sidewaysFriction.extremumValue = 1.0f;
            sidewaysFriction.asymptoteValue = 1f;
            sidewaysFriction.stiffness = 5f;
            wheel.WheelCollider.sidewaysFriction = sidewaysFriction;
        }
    }

    public void OnDriftEndBoost()
    {
        float driftmultiplier = ScoreManager.instance.CurrentDriftMultiplier;

        if (driftmultiplier < 4f) return;

        float turbe = Mathf.InverseLerp(4f, 10f, driftmultiplier);
        float TurbeStrength = Mathf.Lerp(1f, 5f, turbe);

        if (DriftBoost != null)
            StopCoroutine(DriftBoost);

        DriftBoost = StartCoroutine(DriftBoostCoroutine(TurbeStrength));
    }

    private IEnumerator DriftBoostCoroutine(float TurbeStrength)
    {
        float originalspeed = Maxspeed;
        float boostedMax = Mathf.Max(BaseSpeed + Turbesped, originalspeed + TurbeStrength);

        float duration = Mathf.Lerp(1.5f, 3.5f, Mathf.InverseLerp(2f, 5f, TurbeStrength));
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float time = timer / duration;
            float smooth = Mathf.SmoothStep(0f, 1f, time);

            float force = TurbeStrength * (1f - smooth) * Time.deltaTime;
            force = Mathf.Min(force, 0.5f); 

            CarRb.AddForce(transform.forward * force, ForceMode.VelocityChange);

            Maxspeed = Mathf.Lerp(boostedMax, originalspeed, smooth);
            yield return null;
        }

        Maxspeed = originalspeed;
        DriftBoost = null;
    }
}
