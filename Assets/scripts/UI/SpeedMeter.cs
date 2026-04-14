using UnityEngine;

public class SpeedMeter : MonoBehaviour
{
    public Rigidbody target;

    public float maxSpeed = 240.0f;
    public float minSpeedArrowAngle = 20.0f;
    public float maxSpeedArrowAngle = -200.0f;

    private float speed => target.linearVelocity.magnitude * 3.6f;

    [Header("UI")]
    public RectTransform arrow;

    private void Start()
    {
        target = GameManager.instance.CurrentCar.GetComponentInChildren<Rigidbody>();
    }

    private void Update()
    {
        if (arrow != null) arrow.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(minSpeedArrowAngle, maxSpeedArrowAngle, speed / maxSpeed));
    }
}
