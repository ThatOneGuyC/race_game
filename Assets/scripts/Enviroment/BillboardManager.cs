using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BillboardManager : MonoBehaviour
{
    static readonly List<BillboardObject> objects = new();

    [Header("Global Billboard Settings")]
    public Camera billboardCamera;
    public float updateInterval = 0.2f;
    public float lenientAngle = 90f;

    float timer;

    void OnEnable()
    {
        if (billboardCamera == null) billboardCamera = Camera.main;
    }

    void Update()
    {
        //käytetään unscaledDeltaTimeä sillä ne puut ei muuten tykkää billboardata alussa.
        //JA että se kamera toivottavasti ei snappaa oudosti mapin alussa
        timer += Time.unscaledDeltaTime;
        if (timer < updateInterval) return; 
        timer = 0f;

        UpdateBillboarding();
    }

    void UpdateBillboarding()
    {
        if (billboardCamera == null) return;

        Vector3 camPos = billboardCamera.transform.position;
        Vector3 camForward = billboardCamera.transform.forward;

        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];
            if (obj == null) continue;

            Vector3 toObj = (obj.transform.position - camPos).normalized;

            if (Vector3.Angle(camForward, toObj) > lenientAngle) continue;

            Vector3 lookDir = camPos - obj.transform.position;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion baseRotation = Quaternion.LookRotation(-lookDir);
                obj.transform.rotation = baseRotation * obj.RotationOffset;
            }
        }
    }

    public static void Register(BillboardObject obj)
    {
        if (!objects.Contains(obj)) objects.Add(obj);
    }

    public static void Unregister(BillboardObject obj)
    {
        objects.Remove(obj);
    }
}
