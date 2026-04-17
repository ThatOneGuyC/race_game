using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class NewDoublefunszechuansauceWithAsideofNuggets : BaseCarController
{
    internal CarInputActions Controls;
    RacerScript racerScript;
    LogitechMovement LGM;
    private string CurrentControlScheme;
    PlayerInput PlayerInput;


    void Awake()
    {
        Controls = new CarInputActions();
        Controls.Enable();
        CarRb = GetComponent<Rigidbody>();
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

        // Controls.CarControls.Drift.performed   += OnDriftPerformed;
        // Controls.CarControls.Drift.canceled    += OnDriftCanceled;
    }

    private void OnDisable()
    {
        Controls.Disable();
        if (PlayerInput != null)
            PlayerInput.onControlsChanged -= OnControlsChanged;

        Controls.CarControls.Get().actionTriggered -= OnAnyActionTriggered;


        Controls.CarControls.Move.performed -= OnMovePerformed;
        Controls.CarControls.Move.canceled  -= OnMoveCanceled;
        // Controls.CarControls.Drift.performed -= OnDriftPerformed;
        // Controls.CarControls.Drift.canceled  -= OnDriftCanceled;
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
        Move();
        ApplySpeedLimit(Maxspeed / 3.6f);
        Decelerate();

    }
    
    //physics related will go here
    protected void FixedUpdate()
    {

        Applyturnsensitivity(GetSpeed());
    }

    private void Move()
    {
        CarMovement();
        foreach (var wheel in Wheels)
        {
            if (Controls.CarControls.Brake.IsPressed()) Brakes(wheel);
            else MotorTorgue(wheel);
        }  
    }

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
       
        float targetSpeedMs = Maxspeed  * forwardValue;
        Vector3 flatForwardVelocity = transform.forward * targetSpeedMs;
        CarRb.linearVelocity = new Vector3(flatForwardVelocity.x, CarRb.linearVelocity.y, flatForwardVelocity.z);
    }


    void Applyturnsensitivity(float speed)
    {
        TurnSensitivity = Mathf.Lerp(
            TurnSensitivityAtLowSpeed,
            TurnSensitivityAtHighSpeed,
            Mathf.Clamp01(speed / Maxspeed));
    }
}