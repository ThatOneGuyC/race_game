using UnityEngine;


public static class Turbe
{
    public static void TURBO(PlayerCarController car)
    {
        car.IsTurboActive = car.Controls.CarControls.turbo.IsPressed() && car.TurbeAmount > 0;
        if (car.IsTurboActive)
        {
            car.CarRb.AddForce(Vector3.ProjectOnPlane(car.transform.forward, Vector3.up).normalized * car.Turbepush, ForceMode.Acceleration);
            car.TargetTorque = car.BaseTargetTorque * 1.5f;
            car.TargetTorque = Mathf.Min(car.TargetTorque, car.MaxAcceleration);
        }
    }
}