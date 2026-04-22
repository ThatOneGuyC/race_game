using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

public class NewDoublefunszechuansauceWithAsideofNuggets : BaseCarController
{
    internal CarInputActions Controls;
    RacerScript racerScript;
    LogitechMovement LGM;
    private string CurrentControlScheme;
    PlayerInput PlayerInput;


    new void Awake()
    {
        Controls = new CarInputActions();
        Controls.Enable();
        CarRb = GetComponent<Rigidbody>();
        TurbeBar = GameManager.instance.CarUI.transform.Find("TurbeDisplay").GetComponentInChildren<Image>();
        AutoAssignWheelsAndMaterials();
        
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

       }
    private void OnEnable()
    {
        Controls.Enable();
        if (PlayerInput == null)
            PlayerInput = GetComponent<PlayerInput>();

        if (PlayerInput != null)
            PlayerInput.onControlsChanged += OnControlsChanged;

        Controls.CarControls.Get().actionTriggered += OnAnyActionTriggered;

        Controls.CarControls.Move.performed += OnMovePerformed;
        Controls.CarControls.Move.canceled  += OnMoveCanceled;

        Controls.CarControls.Drift.performed   += OnDriftPerformed;
        Controls.CarControls.Drift.canceled    += OnDriftCanceled;
        Controls.CarControls.Brake.performed += OnBrakePerformed;
        Controls.CarControls.Brake.canceled  += OnBrakeCanceled;
    }

    private void OnDisable()
    {
        Controls.Disable();
        if (PlayerInput != null)
            PlayerInput.onControlsChanged -= OnControlsChanged;

        Controls.CarControls.Get().actionTriggered -= OnAnyActionTriggered;


        Controls.CarControls.Move.performed -= OnMovePerformed;
        Controls.CarControls.Move.canceled  -= OnMoveCanceled;
        Controls.CarControls.Drift.performed -= OnDriftPerformed;
        Controls.CarControls.Drift.canceled  -= OnDriftCanceled;
        Controls.CarControls.Brake.performed -= OnBrakePerformed;
        Controls.CarControls.Brake.canceled -= OnBrakeCanceled;
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

    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        SteerInput = ctx.ReadValue<Vector2>().x;
        MoveInput = ctx.ReadValue<Vector2>().y;
    }
    void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        SteerInput = 0f;
        MoveInput = 0f;
    }

    protected override void Start()
    {
        racerScript = FindAnyObjectByType<RacerScript>();
        LGM = FindAnyObjectByType<LogitechMovement>();

        CarRb.centerOfMass = _CenterofMass;

        base.Start();
    }

    //movement or anykind of input related will go here
    protected void Update()
    {
        Animatewheels();
        GetInputs();

        Steer();
        CarMovement();
        ApplySpeedLimit();
        Decelerate();
    }

    
    
    //physics related will go here
    protected new void FixedUpdate()
    {
        Applyturnsensitivity(CarRb.linearVelocity.magnitude);
        // HandleTurbo();
    }

    //que
//    protected void HandleTurbo()
//     {
//         if (!CanUseTurbo) return;
//         Turbe.TURBO(this);
//         TurbeMeter();
//     }

    void GetInputs()
    {

        Vector2 move = Controls.CarControls.Move.ReadValue<Vector2>();
        SteerInput = move.x;
        MoveInput = move.y;

    }
    //Arcade car style movement
    protected void CarMovement()
    {
        float forwardValue = Mathf.Abs(MoveInput);
       
        float targetSpeed = Mathf.MoveTowards(CarRb.linearVelocity.magnitude, MaxSpeed  * forwardValue, Acceleration * Time.deltaTime);
        Vector3 flatForwardVelocity = transform.forward * targetSpeed;
        CarRb.linearVelocity = new Vector3(flatForwardVelocity.x, CarRb.linearVelocity.y, flatForwardVelocity.z);
    }


    void Applyturnsensitivity(float speed)
    {
        TurnSensitivity = Mathf.Lerp(
            TurnSensitivityAtLowSpeed,
            TurnSensitivityAtHighSpeed,
            Mathf.Clamp01(speed / MaxSpeed));
    }

    void OnBrakePerformed(InputAction.CallbackContext ctx)
    {
        foreach (var wheel in Wheels)
        {
            wheel.Brake(BrakeAcceleration);
        }
    }

    void OnBrakeCanceled(InputAction.CallbackContext ctx)
    {
        foreach (var wheel in Wheels)
        {
            wheel.MotorTorque(TargetTorque);
        }
    }

    void OnDriftPerformed(InputAction.CallbackContext ctx)
    {
        print("you're are drifting now");
    }

    void OnDriftCanceled(InputAction.CallbackContext ctx)
    {
        print("you stopped drifting");
    }
}