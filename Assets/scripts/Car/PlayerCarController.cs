using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Logitech;
using System.Collections;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCarController : BaseCarController
{
    public CarInputActions Controls { get; protected set; }
    protected RacerScript racerScript;
    protected LogitechMovement LGM;
    private PlayerInput PlayerInput;
    private string CurrentControlScheme = "Keyboard";
    [Header("Turbo Type")]
    internal int turbeChargeAmount = 3;
    internal Coroutine TurbeBoost;
    internal float LastNonWheelInputTime = 0f;
    internal float LastWheelInputTime = 0f;

    private Material carLightsMaterial;

    override protected void Awake()
    {
        Controls = new CarInputActions();
        Controls.Enable();
        PlayerInput = GetComponent<PlayerInput>();
        TurbeBar = GameManager.instance.CarUI.transform.Find("TurbeDisplay").GetComponentInChildren<Image>();
        carLightsMaterial = GetComponentInChildren<Renderer>().materials[1];
        AutoAssignWheelsAndMaterials();

        base.Awake();

        Controls.CarControls.turbo.started += context => { turbo.Activate(); };
        Controls.CarControls.turbo.performed += context => { turbo.Stop(); };
    }

    override protected void Start()
    {

        BaseMaxAccerelation = Acceleration;
        SmoothedMaxAcceleration = BaseMaxAccerelation;
        BaseTargetTorque = TargetTorque;

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

    override protected void FixedUpdate()
    {
        float speed = CarRb.linearVelocity.magnitude;
        UpdateDriftSpeed();
        Move();
        Steer();
        Decelerate();
        Applyturnsensitivity(speed);
        WheelEffects(IsDrifting);
        base.FixedUpdate();
    }

    protected void Update()
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

    // override protected void ApplySpeedLimit()
    // {
    //     MaxSpeed = Mathf.Clamp(MaxSpeed, 0, BaseMaxSpeed);
    //     if (CarRb.linearVelocity.magnitude * 3.6f > Maxspeed) CarRb.linearVelocity = Maxspeed / 3.6f * CarRb.linearVelocity.normalized;
    // }

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



    void UpdateDriftSpeed()
    {
        if (!IsDrifting) return;

        if (isTurboActive)
            MaxSpeed = Mathf.Lerp(MaxSpeed, BaseSpeed + Turbesped, Time.deltaTime * 0.5f);
        else
            MaxSpeed = Mathf.Lerp(MaxSpeed, DriftMaxSpeed, Time.deltaTime * 0.1f);

        
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
        if (nonWheelMove > 0.001f || Controls.CarControls.Drift.IsPressed() || Controls.CarControls.Brake.IsPressed())
        {
            if (LGM != null)
            {
                LGM.useLogitechWheel = false;
                LGM.allowAutoEnable = true;
                LGM.StopAllForceFeedback();
            }
        }
        
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
            Mathf.Clamp01(speed / MaxSpeed));
    }

    // protected void HandleTurbo()
    // {
    //     if (!CanUseTurbo) return;
    //     Turbe.TURBO(this);
    //     TurbeMeter();
    // }



    void Move()
    {
        UpdateTargetTorque();
        if (Controls.CarControls.Brake.IsPressed()) carLightsMaterial.SetVector("_EmissionColor", new Vector4(1f, 0.0491371f, 0f, 1f) * 2f);
        else if (carLightsMaterial.GetVector("_EmissionColor") != new Vector4(0f, 0f, 0f, 1f) * 2f) carLightsMaterial.SetVector("_EmissionColor", new Vector4(0f, 0f, 0f, 1f) * 2f);

        AdjustSuspension();
        foreach (var wheel in Wheels)
        {
            if (Controls.CarControls.Brake.IsPressed()) wheel.Brake(BrakeAcceleration);
            else wheel.MotorTorque(TargetTorque);
        }
    }

    private void UpdateTargetTorque()
    {
        float inputValue = Mathf.Abs(MoveInput);
        if (CurrentControlScheme == "Gamepad")
        {
            Vector2 moveVector = Controls.CarControls.Move.ReadValue<Vector2>();
            inputValue = Mathf.Max(inputValue, Mathf.Abs(moveVector.y));
        }

        float steerFactor = Mathf.Clamp01(Mathf.Abs(SteerInput));
        float driftPowerMultiplier = IsDrifting ? Mathf.Lerp(0.65f, 0.85f, steerFactor) : 1.0f;
        float targetMaxAcc = BaseMaxAccerelation * driftPowerMultiplier;

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
            MaxSpeed = Mathf.Lerp(MaxSpeed, isTurboActive ? BaseSpeed + Turbesped : BaseSpeed, Time.deltaTime);
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
        if (IsDrifting || !CanDrift || racerScript.raceFinished) return;

        IsDrifting = true;

        Acceleration = BaseMaxAccerelation * 0.95f;

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
        OnDriftEndBoostTheCar();
        Acceleration = BaseMaxAccerelation;
        TargetTorque = BaseTargetTorque;
        WheelEffects(false);
    }

    public void StopDrifting()
    {
        if (IsDrifting)
        {
            IsDrifting = false;
            Acceleration = BaseMaxAccerelation;
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

    protected IEnumerator BoostCoroutine(float turboStrength, float durationOverride = -1f)
    {

        float GetCurrentBaseSpeed() => IsDrifting
            ? (isTurboActive ? BaseSpeed + Turbesped : DriftMaxSpeed)
            : (isTurboActive ? BaseSpeed + Turbesped : BaseSpeed);

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

            MaxSpeed = Mathf.Lerp(MaxSpeed, Mathf.Lerp(boostedMax, GetCurrentBaseSpeed(), smooth), Time.deltaTime * 2f);

            yield return null;
        }
        MaxSpeed = GetCurrentBaseSpeed();
        TurbeBoost = null;
    }
}
