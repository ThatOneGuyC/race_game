using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public float moveSmoothness;
    public float rotSmoothness;


    public Vector3 moveOffset; 
    public Vector3 rotOffset;

    public Transform carTarget;
    
    private Camera Cam;
    private PlayerCarController carController;
    float normalFOV = 60;
    float ZoomFOV = 70;

    private void Start()
    {
        Cam = GetComponent<Camera>();
        carController = GameManager.instance.CurrentCar.GetComponentInChildren<PlayerCarController>();
    }

    private void FixedUpdate()
    {
        FollowTarget();
    }

    void FollowTarget()
    {
        HandleMovement();
        HandleRotation();
        Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, Mathf.Lerp(normalFOV, ZoomFOV, Mathf.Clamp01(carController.CarRb.linearVelocity.magnitude * 3.6f / carController.Maxspeed)), Time.deltaTime * moveSmoothness);
    }

    void HandleMovement()
    {
        Vector3 targetPos = carTarget.TransformPoint(moveOffset); 
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * moveSmoothness);
    }
    void HandleRotation()
    {
        var direction = carTarget.position - transform.position;
        var rotation = Quaternion.LookRotation(direction + rotOffset, Vector3.up);
        transform.rotation = rotation;
    }
}
