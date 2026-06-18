using System.Collections.Generic;
using UnityEngine;

public static class BucketCollisionProxy
{
    private const string BucketShellObjectName = "Generated_BucketShell";

    public static List<Vector3> BuildLocalProxyPoints(
        Transform bucket,
        GroundCollisionSettings settings)
    {
        List<Vector3> points =
            new List<Vector3>();

        if (bucket == null)
        {
            return points;
        }

        MeshFilter shellMeshFilter =
            FindBucketShellMeshFilter(bucket);

        if (shellMeshFilter != null &&
            shellMeshFilter.sharedMesh != null)
        {
            AddMeshVerticesAsBucketLocalPoints(
                bucket,
                shellMeshFilter,
                points
            );

            return points;
        }

        AddFallbackBucketPoints(
            settings,
            points
        );

        return points;
    }

    private static void AddMeshVerticesAsBucketLocalPoints(
        Transform bucket,
        MeshFilter meshFilter,
        List<Vector3> points)
    {
        Vector3[] vertices =
            meshFilter.sharedMesh.vertices;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPoint =
                meshFilter.transform.TransformPoint(vertices[i]);

            Vector3 bucketLocalPoint =
                bucket.InverseTransformPoint(worldPoint);

            points.Add(bucketLocalPoint);
        }
    }

    private static void AddFallbackBucketPoints(
        GroundCollisionSettings settings,
        List<Vector3> points)
    {
        int segments = 24;

        float radius =
            settings != null
                ? settings.FallbackBucketRadius
                : 0.35f;

        float halfHeight =
            settings != null
                ? settings.FallbackBucketHeight * 0.5f
                : 0.325f;

        for (int i = 0; i < segments; i++)
        {
            float angle =
                2.0f * Mathf.PI * i / segments;

            float x =
                Mathf.Cos(angle) * radius;

            float z =
                Mathf.Sin(angle) * radius;

            points.Add(new Vector3(x, halfHeight, z));
            points.Add(new Vector3(x, -halfHeight, z));
            points.Add(new Vector3(x, 0.0f, z));
        }
    }

    private static MeshFilter FindBucketShellMeshFilter(Transform bucket)
    {
        MeshFilter[] meshFilters =
            bucket.GetComponentsInChildren<MeshFilter>();

        for (int i = 0; i < meshFilters.Length; i++)
        {
            if (meshFilters[i] != null &&
                meshFilters[i].name == BucketShellObjectName)
            {
                return meshFilters[i];
            }
        }

        for (int i = 0; i < meshFilters.Length; i++)
        {
            if (meshFilters[i] == null ||
                meshFilters[i].sharedMesh == null)
            {
                continue;
            }

            if (meshFilters[i].sharedMesh.name.Contains("Bucket"))
            {
                return meshFilters[i];
            }
        }

        return null;
    }
}