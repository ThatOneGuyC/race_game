using UnityEngine;

public class BoostTurbo : Turbo
{
    protected override void Use()
    {
        carController.CarRb.AddForce(Vector3.ProjectOnPlane(carController.transform.forward, Vector3.up).normalized * strength, ForceMode.Acceleration);
        carController.TargetTorque = Mathf.Min(carController.BaseTargetTorque * 1.5f, carController.Acceleration);
    }
}
