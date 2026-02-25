using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

// Funny bezier maths :D
public static class BezierMath
{
    // quadratic bezier point
    public static Vector3 CalculateBezierPoint(float t, float y, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        return new(
            MathF.Pow(1 - t, 2f) * p0.x + 2 * t * (1 - t) * p1.x + MathF.Pow(t, 2f) * p2.x,
            y,
            MathF.Pow(1 - t, 2f) * p0.z + 2 * t * (1 - t) * p1.z + MathF.Pow(t, 2f) * p2.z
        );
    }
    
    // Cubic bezier point
    public static Vector3 CalculateBezierPoint(float t, float y, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return new(
            MathF.Pow(1 - t, 3f) * p0.x + 3 * MathF.Pow(1 - t, 2f) * t * p1.x + 3 * (1 - t) * MathF.Pow(t, 2f) * p2.x + MathF.Pow(t, 3f) * p3.x,
            y,
            MathF.Pow(1 - t, 3f) * p0.z + 3 * MathF.Pow(1 - t, 2f) * t * p1.z + 3 * (1 - t) * MathF.Pow(t, 2f) * p2.z + MathF.Pow(t, 3f) * p3.z
        );
    }

    // Quartic bezier point
    public static Vector3 CalculateBezierPoint(float t, float y, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
    {
        return new(
            MathF.Pow(1 - t, 4f) * p0.x + 4 * MathF.Pow(1 - t, 3f) * t * p1.x + 6 * MathF.Pow(1 - t, 2f) * MathF.Pow(t, 2f) * p2.x + 4 * (1 - t) * MathF.Pow(t, 3f) * p3.x + MathF.Pow(t, 4f) * p4.x,
            y,
            MathF.Pow(1 - t, 4f) * p0.z + 4 * MathF.Pow(1 - t, 3f) * t * p1.z + 6 * MathF.Pow(1 - t, 2f) * MathF.Pow(t, 2f) * p2.z + 4 * (1 - t) * MathF.Pow(t, 3f) * p3.z + MathF.Pow(t, 4f) * p4.z
        );
    }

    // Algorithm made by the french EWWWWWWWWWWWWWWW
    // De casteljau's algorithm for finding a point inside any amount of control points
    public static Vector3 CalculateBezierPoint(float t, List<Vector3> controlPoints)
    {
        if (controlPoints == null || controlPoints.Count() == 0)
        {
            Debug.Log("Control points are empty or null, returning fallback.");
            return Vector3.zero;
        }

        List<Vector3> points = new(controlPoints);

        while (points.Count() > 1)
        {
            for (int i = 0; i < points.Count() - 1; i++)
            {
                points[i] = Vector3.Lerp(points[i], points[i + 1], t);
            }
            points.RemoveAt(points.Count() - 1);
        }

        return points[0];
    }

    public static Vector3[] ComputeBezierPoints(int bezierResolution, int sampleSize, int timeOut, Vector3[] wayPoints)
    {
        long startTime = DateTime.Now.Ticks;

        List<Vector3> bezierPoints = new();
        int size = wayPoints.Count();

        float inverseResolution = 1f / bezierResolution;

        float halfSampleSize = sampleSize / 2f;
        float variance = sampleSize % 2 == 0 ? 0.5f : 0f;

        float minT = (Mathf.Floor(halfSampleSize) - variance) / sampleSize;
        float maxT = (Mathf.Ceil(halfSampleSize) + variance) / sampleSize;
    
        for (int i = 0; i < size; i++)
        {
            List<Vector3> samplePoints = new();
            // Grab sample points from waypoints (using .Skip().Take() would cause it to not wrap around)
            for (int j = i; j < i + sampleSize; j++) samplePoints.Add(wayPoints[j % size]);
            
            for (float t = minT; t <= maxT; t += inverseResolution)
            {
                bezierPoints.Add(CalculateBezierPoint(t, samplePoints));

                if ((DateTime.Now.Ticks - startTime) / 10_000_000 > timeOut) // Time out after a set amount of seconds
                {
                    Debug.Log($"Baking took too long, timing out. Is the loop infinite? Inverse resolution: {inverseResolution} (if 0 then loop is infinite)");
                    return null;
                }
            }
        }

        Debug.Log($"Bezier points computed in {(DateTime.Now.Ticks - startTime) / 10} microseconds");

        return bezierPoints.ToArray();
    }
}