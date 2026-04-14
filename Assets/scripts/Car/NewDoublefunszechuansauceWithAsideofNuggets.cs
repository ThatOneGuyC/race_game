using UnityEngine;
using UnityEngine.InputSystem;


public class NewDoublefunszechuansauceWithAsideofNuggets : BaseCarController
{

    RacerScript racerScript;
    LogitechMovement LGM;

    void Awake()
    {
        CarRb = GetComponent<Rigidbody>();
        AutoAssignWheelsAndMaterials();
        
    }

    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        SteerInput = ctx.ReadValue<Vector2>().x;
    }
    void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        SteerInput = 0f;
    }

    protected override void Start()
    {
        racerScript = FindAnyObjectByType<RacerScript>();
        LGM = FindAnyObjectByType<LogitechMovement>();

        base.Start();
    }

    //movement or anykind of input related will go here
    protected void Update()
    {
        Animatewheels();
        Steer();
        Move();
    }
    
    //physics related will go here
    protected void FixedUpdate()
    {
        float speed = CarRb.linearVelocity.magnitude;
        ApplySpeedLimit(Maxspeed / 3.6f);
        Applyturnsensitivity(speed);
    }


    private void Move()
    {
        CarMovement();  
    }

    //Arcade car style movement
    protected void CarMovement()
    {
        float forwardvalue = Input.GetAxis("Vertical");
        Vector3 forwardMovement = transform.forward * Maxspeed * forwardvalue;
        forwardMovement.y = CarRb.linearVelocity.y; 
        CarRb.linearVelocity = forwardMovement;
    }

    void Applyturnsensitivity(float speed)
    {
        TurnSensitivity = Mathf.Lerp(
            TurnSensitivityAtLowSpeed,
            TurnSensitivityAtHighSpeed,
            Mathf.Clamp01(speed / Maxspeed));
    }
}