using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Logitech;
using System.Linq;
using System.Collections;
using UnityEngine.Rendering.Universal;



public class PlayerCarController : BaseCarController
{
    internal CarInputActions Controls;
    RacerScript racerScript;
    LogitechMovement LGM;


    private PlayerInput PlayerInput;
    private string CurrentControlScheme = "Keyboard";
    [Header("Turbo Type")]
    internal int turbeChargeAmount = 3;
    


    internal Coroutine TurbeBoost;
    internal float LastNonWheelInputTime = 0f;
    internal float LastWheelInputTime = 0f;


    

    new void  Awake()
    {
        Controls = new CarInputActions();
        Controls.Enable();
        PlayerInput = GetComponent<PlayerInput>();
        TurbeBar = GameManager.instance.CarUI.transform.Find("TurbeDisplay").GetComponentInChildren<Image>();
        AutoAssignWheelsAndMaterials();
    }

    override protected void Start()
    {

        BaseMaxAccerelation = MaxAcceleration;
        SmoothedMaxAcceleration = BaseMaxAccerelation;

        if (LGM == null)
        {
            LGM = FindFirstObjectByType<LogitechMovement>();
        }
        if (CarRb == null)
            CarRb = GetComponent<Rigidbody>();
        CarRb.centerOfMass = _CenterofMass;
        racerScript = FindAnyObjectByType<RacerScript>();


        if (LGM != null)
        {
            LGM.InitializeLogitechWheel(); 
        }


        base.Start();
    }


    private void OnControlsChanged(PlayerInput input)
    {
        CurrentControlScheme = input.currentControlScheme;
        if (LGM != null)
            LGM.ReenableFromControlScheme(CurrentControlScheme);
    }

    void OnAnyActionTriggered(InputAction.CallbackContext ctx)
    {
        var control = ctx.action?.activeControl;
        if (control == null)
            return;

        var device = control.device;
        if (device is Keyboard || device is Mouse)
            CurrentControlScheme = "Keyboard";
        else if (device is Gamepad)
            CurrentControlScheme = "Gamepad";
        if (LGM != null)
        {
            LGM.useLogitechWheel = false;
            LGM.allowAutoEnable = true;
            LGM.StopAllForceFeedback();
        }
    }


    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        SteerInput = ctx.ReadValue<Vector2>().x;
    }
    void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        SteerInput = 0f;
    }

    void OnApplicationFocus(bool focus)
    {
        if (focus)
        {
            if (LGM != null && LGM.logitechInitialized && LogitechGSDK.LogiIsConnected(0))
            {
                LogitechGSDK.LogiUpdate();
            }
        }
    }


    private void OnEnable()
    {
        Controls.Enable();
        if (PlayerInput == null)
            PlayerInput = GetComponent<PlayerInput>();

        if (PlayerInput != null)
            PlayerInput.onControlsChanged += OnControlsChanged;

        Controls.CarControls.Get().actionTriggered += OnAnyActionTriggered;

        // INPUT SUBSCRIPTIONS: KERRAN
        Controls.CarControls.Move.performed += OnMovePerformed;
        Controls.CarControls.Move.canceled  += OnMoveCanceled;

        Controls.CarControls.Drift.performed   += OnDriftPerformed;
        Controls.CarControls.Drift.canceled    += OnDriftCanceled;

        Controls.CarControls.MoveForward.performed += OnBrakePerformed;
        // Controls.CarControls.MoveForward.canceled  += OnBrakeCanceled;

         
        
    }

    private void OnDisable()
    {
        Controls.Disable();
        if (PlayerInput != null)
            PlayerInput.onControlsChanged -= OnControlsChanged;

        Controls.CarControls.Get().actionTriggered -= OnAnyActionTriggered;

        // UNSUBSCRIBE!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        Controls.CarControls.Move.performed -= OnMovePerformed;
        Controls.CarControls.Move.canceled  -= OnMoveCanceled;
        Controls.CarControls.Drift.performed -= OnDriftPerformed;
        Controls.CarControls.Drift.canceled  -= OnDriftCanceled;
        Controls.CarControls.MoveForward.performed -= OnBrakePerformed;
        // Controls.CarControls.MoveForward.canceled  -= OnBrakeCanceled;
        if (LGM != null)
            LGM.StopAllForceFeedback();
    }

    private void OnDestroy()
    {
        Controls.Disable();
        Controls.Dispose();

        if (LGM != null)
            LGM.StopAllForceFeedback();
    }

    //move the movement into the update 
    void Update()
    {
        GetInputs();
        Animatewheels();
        // detect connection state changes and print once when it changes
        bool currentlyConnected = (LGM != null) && LGM.logitechInitialized && LogitechGSDK.LogiIsConnected(0);
        if (LGM != null && currentlyConnected != LGM.lastLogiConnected)
        {
            LGM.lastLogiConnected = currentlyConnected;
            Debug.Log($"[CarController] Logitech connection status: {(currentlyConnected ? "Connected" : "Disconnected")}");
        }

        if (LGM != null && LGM.useLogitechWheel && LGM.logitechInitialized && LogitechGSDK.LogiIsConnected(0))
        {
            LogitechGSDK.LogiUpdate();
            LGM.GetLogitechInputs();
            LGM.ApplyForceFeedback(); 
        }
    }


    new void FixedUpdate()
    {
        float speed = CarRb.linearVelocity.magnitude;
        UpdateDriftSpeed();
        ApplySpeedLimit();
        Move();
        Steer();
        Decelerate();
        Applyturnsensitivity(speed);
        //OnGrass();
        HandleTurbo();
        WheelEffects(IsDrifting);
    }


    void UpdateDriftSpeed()
    {
        if (!IsDrifting) return;

        if (IsTurboActive)
            Maxspeed = Mathf.Lerp(Maxspeed, BaseSpeed + Turbesped, Time.deltaTime * 0.5f);
        else
            Maxspeed = Mathf.Lerp(Maxspeed, DriftMaxSpeed, Time.deltaTime * 0.1f);

        
        if (Mathf.Abs(SteerInput) > 0.1f)
        {
            CarRb.AddTorque(Vector3.up * Time.deltaTime, ForceMode.Acceleration);
        }
    }




    void GetInputs()
    {
        //reads inputs and assigns them to values 
        // read non-wheel input (keyboard / gamepad) and mark last-non-wheel time when active
        SteerInput = Controls.CarControls.Move.ReadValue<Vector2>().x;
        float nonWheelMove = Mathf.Abs(SteerInput) + Mathf.Abs(Controls.CarControls.MoveForward.ReadValue<float>()) + Mathf.Abs(Controls.CarControls.MoveBackward.ReadValue<float>());
        
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
        TurnSensitivity = Mathf.Lerp(
            TurnSensitivityAtLowSpeed,
            TurnSensitivityAtHighSpeed,
            Mathf.Clamp01(speed / Maxspeed));
    }

    protected void HandleTurbo()
    {
        if (!CanUseTurbo) return;
        Turbe.TURBO(this);
        TurbeMeter();
    }



    void Move()
    {
        CarMovement();
        AdjustSuspension();
        foreach (var wheel in Wheels)
        {
            if (Controls.CarControls.Brake.IsPressed()) wheel.Brakes(BrakeAcceleration);
            else wheel.MotorTorque(TargetTorque);
        }
    }

    /// <summary>
    /// moves the car using CarRb.linearvelocity and forcemode.accerelation
    /// </summary>
    private void CarMovement()
    {
    {
        float inputValue = Mathf.Abs(MoveInput);
        if (CurrentControlScheme == "Gamepad")
        {
            Vector2 moveVector = Controls.CarControls.Move.ReadValue<Vector2>();
            inputValue = Mathf.Max(inputValue, Mathf.Abs(moveVector.y));
        }

        float power = CurrentControlScheme == "Gamepad" ? 0.9f : 1.0f;

        float throttle = Mathf.Pow(inputValue, power);
        
        // Reduce power during drift but don'turbe eliminate it


        float steerFactor = Mathf.Clamp01(Mathf.Abs(SteerInput));
        float driftPowerMultiplier = IsDrifting ? Mathf.Lerp(0.65f, 0.85f, steerFactor) : 1.0f;
        float targetMaxAcc = BaseMaxAccerelation * Mathf.Lerp(0.4f, 1f, throttle) * driftPowerMultiplier;

        SmoothedMaxAcceleration = Mathf.MoveTowards(
            SmoothedMaxAcceleration,
            targetMaxAcc,
            Time.deltaTime * 250f
        );

        float rawTorque = MoveInput * SmoothedMaxAcceleration;
        float forwardVel = Vector3.Dot(CarRb.linearVelocity, transform.forward);
        if (IsDrifting && forwardVel > 0.5f && rawTorque < 0f) rawTorque = 0f;

        TargetTorque = rawTorque;

        if (IsDrifting)
        {
            TargetTorque *= Mathf.Lerp(0.5f, 0.7f, steerFactor); 
        }

        if (!IsDrifting)
        {
            float targetMaxSpeed = IsTurboActive ? BaseSpeed + Turbesped : BaseSpeed;
            Maxspeed = Mathf.Lerp(Maxspeed, targetMaxSpeed, Time.deltaTime);
        }
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


    //this entire thing will be reworked into a completely different drift
    //i hate this so much, its always somewhat broken but for now....... its not broken.
    void OnDriftPerformed(InputAction.CallbackContext ctx)
    {
        if (IsDrifting || !CanDrift || racerScript.raceFinished) return;

        Activedrift++;
        IsDrifting = true;

        MaxAcceleration = BaseMaxAccerelation * 0.95f;

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

    protected new void AdjustWheelsForDrift()
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


    protected new void AdjustSuspension()
    {
        foreach (var wheel in Wheels)
        {
            JointSpring suspensionSpring = wheel.WheelCollider.suspensionSpring;
            suspensionSpring.spring = 8000.0f;
            suspensionSpring.damper = 5000.0f;
            wheel.WheelCollider.suspensionSpring = suspensionSpring;
        }
    }

    protected  void AdjustForwardFrictrion()
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

    void OnDriftCanceled(InputAction.CallbackContext ctx)
    {
        StopDrifting();
        OnDriftEndBoostTheCar();
        MaxAcceleration = BaseMaxAccerelation;
        WheelEffects(false);
    }



    internal void StopDrifting()
    {
        if (IsDrifting)
        {
            Activedrift = 0;
            IsDrifting = false;
            MaxAcceleration = BaseMaxAccerelation;
        }
        float DeltaTime = Time.deltaTime * 2.5f;

        CarRb.angularDamping = Mathf.Lerp(CarRb.angularDamping, 0.1f, DeltaTime);
        
        foreach (var wheel in Wheels)
        {
            if (wheel.WheelCollider == null) continue;
            WheelFrictionCurve sideways = wheel.WheelCollider.sidewaysFriction;
            sideways.stiffness = Mathf.Lerp(sideways.stiffness, 5f, DeltaTime);
            sideways.extremumSlip  = Mathf.Lerp(sideways.extremumSlip, 0.15f, DeltaTime);
            sideways.asymptoteSlip = Mathf.Lerp(sideways.asymptoteSlip, 0.1f, DeltaTime);
            wheel.WheelCollider.sidewaysFriction = sideways;
        }
    }

    //this entire patch thats inside these comments will be reworked

    
    public void OnDriftEndBoostTheCar()
    {
        float driftmultiplier = ScoreManager.instance.CurrentDriftMultiplier;

        if (driftmultiplier < 6) return;

        float turbe = Mathf.InverseLerp(6f, 10f, driftmultiplier);
        float TurbeStrength = Mathf.Lerp(1f, 3f, turbe);
        float Duration = 3.5f;

        if (TurbeBoost != null)
            StopCoroutine(TurbeBoost);

        TurbeBoost = StartCoroutine(BoostCoroutine(TurbeStrength, Duration));
    }
    //refactor this aswell bcs its shit right now
    internal IEnumerator BoostCoroutine(float turboStrength, float durationOverride = -1f)
    {

        float GetCurrentBaseSpeed() => IsDrifting
            ? (IsTurboActive ? BaseSpeed + Turbesped : DriftMaxSpeed)
            : (IsTurboActive ? BaseSpeed + Turbesped : BaseSpeed);

        float originalSpeed = GetCurrentBaseSpeed();
        float boostedMax = Mathf.Max(BaseSpeed + Turbesped, originalSpeed + turboStrength);


        float duration = durationOverride > 0f
            ? durationOverride
            : Mathf.Lerp(2.5f, 4.5f, Mathf.InverseLerp(2f, 5f, turboStrength));

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float smooth = Mathf.SmoothStep(0f, 1f, timer / duration);

            float expo = 1f - Mathf.Exp(-12f * timer / duration);
            CarRb.AddForce(transform.forward * turboStrength * 2.5f * expo * Time.deltaTime, ForceMode.VelocityChange);

            Maxspeed = Mathf.Lerp(Maxspeed, Mathf.Lerp(boostedMax, GetCurrentBaseSpeed(), smooth), Time.deltaTime * 2f);

            yield return null;
        }
        Maxspeed = GetCurrentBaseSpeed();
        TurbeBoost = null;
    }

    void OnBrakePerformed(InputAction.CallbackContext ctx)
    {
        print("This brake was promised to me 3000 years ago and I will not let it be taken away");
    }

    // void OnBrakeCanceled(InputAction.CallbackContext ctx)
    // {
    //     print("This brake was promised to me 3000 years ago and I will not let it be taken away");
    // }
}
