using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class TrailerCameraFollow : MonoBehaviour
{
    [Header("Path Settings")]
    [Tooltip("Waypoints for the camera path (children of this object).")]
    public List<Transform> waypoints = new List<Transform>();
    [Tooltip("Resolution of the generated Bezier curve.")]
    public int bezierResolution = 30;

    [Header("Camera Settings")]
    [Tooltip("Camera to move along the path.")]
    public Camera trailerCamera;
    [Tooltip("Transform the camera should look at (assign in Inspector).")]
    public Transform lookAtTarget;
    [Tooltip("Speed at which the camera moves along the path (units per second).")]
    public float cameraSpeed = 10000f;
    [Tooltip("Should the camera move automatically in play mode?")]
    public bool autoPlay = true;

    [Header("Editor Visualization")]
    [Tooltip("Show the Bezier path in the editor.")]
    public bool showPathGizmos = true;
    [Tooltip("Color of the path in the editor.")]
    public Color pathColor = Color.cyan;

    [HideInInspector]
    public List<Vector3> bezierPoints = new List<Vector3>();

    [HideInInspector]
    public float pathLength = 0f;

    private float cameraPathT = 0f; // 0-1 along the path

    private bool isPlaying = false; // Manual play control

    private List<float> cumulativeDistances = new List<float>();

    private float distanceTravelled = 0f;

    private void OnValidate()
    {
        UpdateWaypoints();
        GenerateBezierPath();
    }

    private void Reset()
    {
        UpdateWaypoints();
        GenerateBezierPath();
    }

    private void UpdateWaypoints()
    {
        waypoints.Clear();
        foreach (Transform child in transform)
        {
            waypoints.Add(child);
        }
    }

    // Editor-only: Regenerate curve if waypoints change
#if UNITY_EDITOR
    private void Update()
    {
        if (!Application.isPlaying)
        {
            UpdateWaypoints();
            GenerateBezierPath();
            return;
        }

        /* // Start moving when Space is pressed
        if (Input.GetKeyDown(KeyCode.Space))
            isPlaying = true; */

        if ((autoPlay || isPlaying) && trailerCamera != null && bezierPoints.Count > 1)
        {
            MoveCameraAlongPath();
        }
    }
#else
    private void Update()
    {
        /* // Start moving when Space is pressed
        if (Input.GetKeyDown(KeyCode.Space))
            isPlaying = true; */

        if ((autoPlay || isPlaying) && trailerCamera != null && bezierPoints.Count > 1)
        {
            MoveCameraAlongPath();
        }
    }
#endif

    public void GenerateBezierPath()
    {
        bezierPoints.Clear();
        if (waypoints.Count < 2)
            return;

        int resolution = Mathf.Max(2, bezierResolution * waypoints.Count);
        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            bezierPoints.Add(CalculateBezierPoint(waypoints, t));
        }
        CalculatePathLength();
    }

    // De Casteljau's algorithm for multi-point Bezier
    private Vector3 CalculateBezierPoint(List<Transform> points, float t)
    {
        List<Vector3> pts = new List<Vector3>();
        foreach (var p in points) pts.Add(p.position);
        return CalculateBezierPoint(pts, t);
    }
    private Vector3 CalculateBezierPoint(List<Vector3> pts, float t)
    {
        if (pts.Count == 1) return pts[0];
        List<Vector3> next = new List<Vector3>();
        for (int i = 0; i < pts.Count - 1; i++)
            next.Add(Vector3.Lerp(pts[i], pts[i + 1], t));
        return CalculateBezierPoint(next, t);
    }

    private void CalculatePathLength()
    {
        pathLength = 0f;
        cumulativeDistances.Clear();
        cumulativeDistances.Add(0f);
        for (int i = 1; i < bezierPoints.Count; i++)
        {
            pathLength += Vector3.Distance(bezierPoints[i - 1], bezierPoints[i]);
            cumulativeDistances.Add(pathLength);
        }
    }

    private void MoveCameraAlongPath()
    {
        if (bezierPoints.Count < 2 || pathLength <= 0f) return;

        distanceTravelled += cameraSpeed * Time.deltaTime;
        distanceTravelled = Mathf.Clamp(distanceTravelled, 0f, pathLength);

        Vector3 camPos = GetPointAtDistance(distanceTravelled);
        trailerCamera.transform.position = camPos;

        if (lookAtTarget != null)
        {
            trailerCamera.transform.LookAt(lookAtTarget.position);
        }
    }

    public Vector3 GetPointOnPath(float t01)
    {
        if (bezierPoints.Count == 0) return transform.position;
        float total = t01 * (bezierPoints.Count - 1);
        int idx = Mathf.FloorToInt(total);
        float frac = total - idx;
        if (idx >= bezierPoints.Count - 1) return bezierPoints[bezierPoints.Count - 1];
        return Vector3.Lerp(bezierPoints[idx], bezierPoints[idx + 1], frac);
    }

    private Vector3 GetPointAtDistance(float distance)
    {
        if (bezierPoints.Count == 0) return transform.position;
        if (distance <= 0f) return bezierPoints[0];
        if (distance >= pathLength) return bezierPoints[bezierPoints.Count - 1];

        // Find the segment containing the distance
        int i = 1;
        while (i < cumulativeDistances.Count && cumulativeDistances[i] < distance)
            i++;

        float segDist = cumulativeDistances[i] - cumulativeDistances[i - 1];
        float t = (distance - cumulativeDistances[i - 1]) / segDist;
        return Vector3.Lerp(bezierPoints[i - 1], bezierPoints[i], t);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showPathGizmos || bezierPoints.Count < 2) return;
        Gizmos.color = pathColor;
        for (int i = 0; i < bezierPoints.Count - 1; i++)
        {
            Gizmos.DrawLine(bezierPoints[i], bezierPoints[i + 1]);
        }
        // Draw waypoints as spheres
        Gizmos.color = Color.yellow;
        foreach (var wp in waypoints)
        {
            if (wp != null)
                Gizmos.DrawSphere(wp.position, 0.2f);
        }
    }
#endif

    // Utility: Move camera to start of path
    public void MoveCameraToStart()
    {
        cameraPathT = 0f;
        distanceTravelled = 0f;
        if (trailerCamera != null && bezierPoints.Count > 0)
            trailerCamera.transform.position = bezierPoints[0];
    }

    // Utility: Move camera to end of path
    public void MoveCameraToEnd()
    {
        cameraPathT = 1f;
        if (trailerCamera != null && bezierPoints.Count > 0)
            trailerCamera.transform.position = bezierPoints[bezierPoints.Count - 1];
    }

    // Utility: Set camera to a specific percentage along the path (0-1)
    public void SetCameraPathPosition(float t01)
    {
        cameraPathT = Mathf.Clamp01(t01);
        if (trailerCamera != null && bezierPoints.Count > 0)
            trailerCamera.transform.position = GetPointOnPath(cameraPathT);
    }
}
