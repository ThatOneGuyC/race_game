using System.Linq;
using UnityEditor.EditorTools;
using UnityEngine;

// I love baking beziers they taste so good

[ExecuteAlways]
[RequireComponent(typeof(AiCarManager))]
public class BezierBaker : MonoBehaviour
{
    [Header("Path Settings")]
    [Tooltip("Parent transform containing cachedPoints for the AI path.")]
    public Transform path;
    [Range(1, 100)]
    [SerializeField] private int bezierCurveResolution = 10;
    [Tooltip("How many points to sample for each bezier curve")]
    [Range(3, 10)]
    [SerializeField] private int sampleSize = 5;
    [Tooltip("Amount of time in seconds for when to time out on baking.")]
    [Range(1, 100)]
    [SerializeField] private int timeOut = 10;
    [SerializeField] private Vector3[] cachedPoints;
    [ContextMenu("Bake Bezier Curve")]
    void Bake()
    {
        if (path == null) return;
        cachedPoints = BezierMath.ComputeBezierPoints(bezierCurveResolution, sampleSize, timeOut, path);
    }

    public Vector3[] GetCachedPoints()
    {
        if (cachedPoints.Length == 0 || cachedPoints[0] == Vector3.zero)
        {
            Bake();
        }
        return cachedPoints;
    }

    #if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (cachedPoints.Count() <= 1) return;

        for (int i = 0; i < cachedPoints.Count(); i++)
        {
            Gizmos.DrawSphere(cachedPoints[i], 0.2f);
            Gizmos.DrawLine(cachedPoints[i], cachedPoints[(i+1) % cachedPoints.Count()]);
        }
    }

#endif
}
