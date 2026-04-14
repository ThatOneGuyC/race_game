using UnityEngine;

public class BoostPad : MonoBehaviour
{
    private Rigidbody target;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() => target = GameManager.instance.CurrentCar.GetComponentInChildren<Rigidbody>();

    void OnTriggerEnter(Collider trigger)
    {
        Debug.Log("ronny touched things");
    }
}
