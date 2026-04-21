using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

[ExecuteAlways]
public class SplineMeshExtrude : MonoBehaviour
{
    private enum Axis
    {
        X, Y, Z,
        NegativeX, NegativeY, NegativeZ
    }

    [System.Serializable]
    public struct SegmentPreset
    {
        public Mesh templateMesh;
        public Material material;
    }

    [SerializeField] private List<SegmentPreset> segmentPresets = new List<SegmentPreset>();
    [SerializeField] private Axis extrusionAxis = Axis.X;
    [SerializeField] private Vector3 localMeshScale = Vector3.one;
    [SerializeField] private float extrusionInterval = 10f;
    [SerializeField] private bool smoothFaces = true;
    [SerializeField] private bool useWorldUp = true;
    [SerializeField] private Vector3 meshRotationOffset = Vector3.zero;

    private MeshCollider meshCollider;
    private MeshFilter meshFilter;
    private SplineContainer splineContainer;
    private Spline spline;

    private void OnEnable()
    {
        InitializeComponents();
        Spline.Changed += OnSplineChanged;
        RebuildMesh();
    }

    private void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
    }

    private void OnValidate()
    {
        if (meshFilter == null || splineContainer == null)
            InitializeComponents();

        RebuildMesh();
    }

    private void OnSplineChanged(Spline changedSpline, int knotIndex, SplineModification modificationType)
    {
        if (spline == changedSpline)
            RebuildMesh();
    }

    private void InitializeComponents()
    {
        meshCollider = GetComponent<MeshCollider>();
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            Debug.LogError($"SplineMeshExtrude: GameObject {gameObject.name} is missing a MeshFilter.");

        splineContainer = GetComponent<SplineContainer>();
        spline = splineContainer != null ? splineContainer.Spline : null;
    }

    private void RebuildMesh()
    {
        if (spline == null || meshFilter == null || segmentPresets == null || segmentPresets.Count == 0)
            return;

        Mesh generatedMesh = GenerateMesh();
        meshFilter.sharedMesh = generatedMesh;

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Material[] mats = new Material[segmentPresets.Count];
            for (int i = 0; i < segmentPresets.Count; i++)
                mats[i] = segmentPresets[i].material;
            renderer.sharedMaterials = mats;
        }

        if (meshCollider != null)
            meshCollider.sharedMesh = generatedMesh;
    }

    private Mesh GenerateMesh()
    {
        Mesh mesh = new Mesh();

        bool success = SplineUtil.SampleSplineInterval(
            spline,
            transform,
            extrusionInterval,
            out Vector3[] positions,
            out Vector3[] tangents,
            out Vector3[] upVectors
        );

        if (!success || positions == null || positions.Length < 2)
        {
            Debug.LogError("SplineMeshExtrudeFeaturePreserving: Failed to sample spline.");
            return mesh;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        List<List<int>> subMeshTriangles = new List<List<int>>();
        for (int i = 0; i < segmentPresets.Count; i++)
            subMeshTriangles.Add(new List<int>());

        int curveCount = Mathf.Max(1, spline.GetCurveCount());

        for (int i = 0; i < positions.Length - 1; i++)
        {
            float tValue = (float)i / (positions.Length - 1);
            int segmentIndex = Mathf.FloorToInt(tValue * curveCount);
            if (segmentIndex >= curveCount)
                segmentIndex = curveCount - 1;

            int presetIndex = segmentIndex < segmentPresets.Count ? segmentIndex : segmentPresets.Count - 1;
            SegmentPreset preset = segmentPresets[presetIndex];

            Mesh templateMesh = preset.templateMesh;
            if (templateMesh == null)
            {
                templateMesh = segmentPresets[0].templateMesh;
                presetIndex = 0;
                if (templateMesh == null)
                    continue;
            }

            bool isClosedSpline = spline.Closed;
            bool keepStartCap = !isClosedSpline && i == 0;
            bool keepEndCap = !isClosedSpline && i == positions.Length - 2;

            AppendMeshSegmentPreservingFeatures(
                vertices,
                subMeshTriangles[presetIndex],
                normals,
                uvs,
                positions[i],
                tangents[i],
                upVectors[i],
                positions[i + 1],
                tangents[i + 1],
                upVectors[i + 1],
                templateMesh,
                keepStartCap,
                keepEndCap
            );
        }

        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();

        if (normals.Count == vertices.Count)
            mesh.normals = normals.ToArray();
        else
            mesh.RecalculateNormals();

        mesh.subMeshCount = segmentPresets.Count;
        for (int i = 0; i < segmentPresets.Count; i++)
            mesh.SetTriangles(subMeshTriangles[i].ToArray(), i);

        mesh.RecalculateBounds();
        return mesh;
    }

    private void AppendMeshSegmentPreservingFeatures(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector3> normals,
        List<Vector2> uvs,
        Vector3 firstPos,
        Vector3 firstTangent,
        Vector3 firstUp,
        Vector3 secondPos,
        Vector3 secondTangent,
        Vector3 secondUp,
        Mesh templateMesh,
        bool keepStartCap,
        bool keepEndCap
    )
    {
        Vector3[] sourceVertices = templateMesh.vertices;
        if (sourceVertices == null || sourceVertices.Length == 0)
            return;

        Vector3[] sourceNormals = templateMesh.normals;
        Vector2[] sourceUVs = templateMesh.uv;
        int[] sourceTriangles = templateMesh.triangles;

        bool hasNormals = sourceNormals != null && sourceNormals.Length == sourceVertices.Length;
        bool hasUVs = sourceUVs != null && sourceUVs.Length == sourceVertices.Length;

        int axisIndex = GetAxisIndex(extrusionAxis);
        bool axisNegative = IsNegativeAxis(extrusionAxis);

        Vector3[] scaledVertices = new Vector3[sourceVertices.Length];
        float minAxis = float.PositiveInfinity;
        float maxAxis = float.NegativeInfinity;

        for (int i = 0; i < sourceVertices.Length; i++)
        {
            Vector3 scaled = Vector3.Scale(sourceVertices[i], localMeshScale);
            scaledVertices[i] = scaled;

            float axisValue = GetSignedAxisValue(scaled, axisIndex, axisNegative);
            if (axisValue < minAxis) minAxis = axisValue;
            if (axisValue > maxAxis) maxAxis = axisValue;
        }

        float axisRange = maxAxis - minAxis;
        if (axisRange < 0.00001f)
            axisRange = 1f;

        Quaternion offsetRotation = Quaternion.Euler(meshRotationOffset);
        Quaternion firstRotation = BuildRotation(firstTangent, firstUp) * offsetRotation;
        Quaternion secondRotation = BuildRotation(secondTangent, secondUp) * offsetRotation;

        Quaternion flatNormalRotation = Quaternion.identity;
        if (!smoothFaces)
        {
            Vector3 avgTangent = firstTangent + secondTangent;
            if (useWorldUp)
                avgTangent = new Vector3(avgTangent.x, 0f, avgTangent.z);

            Vector3 avgUp = useWorldUp ? Vector3.up : (firstUp + secondUp);
            flatNormalRotation = BuildRotation(avgTangent, avgUp) * offsetRotation;
        }

        int baseVertexIndex = vertices.Count;

        for (int i = 0; i < scaledVertices.Length; i++)
        {
            float axisValue = GetSignedAxisValue(scaledVertices[i], axisIndex, axisNegative);
            float t = Mathf.Clamp01((axisValue - minAxis) / axisRange);

            Vector3 center = Vector3.Lerp(firstPos, secondPos, t);
            Quaternion frameRotation = Quaternion.Slerp(firstRotation, secondRotation, t);

            Vector3 crossOffset = RemoveAxisComponent(scaledVertices[i], axisIndex);
            vertices.Add(center + (frameRotation * crossOffset));

            if (hasNormals)
            {
                Quaternion normalRotation = smoothFaces
                    ? Quaternion.Slerp(firstRotation, secondRotation, t)
                    : flatNormalRotation;
                normals.Add(normalRotation * sourceNormals[i]);
            }
            else
            {
                normals.Add(Vector3.up);
            }

            uvs.Add(hasUVs ? sourceUVs[i] : Vector2.zero);
        }

        for (int i = 0; i < sourceTriangles.Length; i++)
            triangles.Add(sourceTriangles[i] + baseVertexIndex);
    }

    private Quaternion BuildRotation(Vector3 tangent, Vector3 up)
    {
        Vector3 forward = useWorldUp
            ? new Vector3(tangent.x, 0f, tangent.z)
            : tangent;

        if (forward.sqrMagnitude < 0.000001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 upDir = useWorldUp ? Vector3.up : up;
        if (upDir.sqrMagnitude < 0.000001f)
            upDir = Vector3.up;
        upDir.Normalize();

        return Quaternion.LookRotation(forward, upDir);
    }

    private static int GetAxisIndex(Axis axis)
    {
        switch (axis)
        {
            case Axis.X:
            case Axis.NegativeX:
                return 0;
            case Axis.Y:
            case Axis.NegativeY:
                return 1;
            default:
                return 2;
        }
    }

    private static bool IsNegativeAxis(Axis axis)
    {
        return axis == Axis.NegativeX || axis == Axis.NegativeY || axis == Axis.NegativeZ;
    }

    private static float GetSignedAxisValue(Vector3 v, int axisIndex, bool negativeAxis)
    {
        float value = axisIndex == 0 ? v.x : (axisIndex == 1 ? v.y : v.z);
        return negativeAxis ? -value : value;
    }

    private static Vector3 RemoveAxisComponent(Vector3 v, int axisIndex)
    {
        if (axisIndex == 0) return new Vector3(0f, v.y, v.z);
        if (axisIndex == 1) return new Vector3(v.x, 0f, v.z);
        return new Vector3(v.x, v.y, 0f);
    }
}