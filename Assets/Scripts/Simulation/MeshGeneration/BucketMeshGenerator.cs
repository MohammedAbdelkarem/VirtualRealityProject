using System.Collections.Generic;
using UnityEngine;

public static class BucketMeshGenerator
{
    public static Mesh Build(
        float topRadius,
        float bottomRadius,
        float height,
        float wallThickness,
        int segments,
        float handleHeight,
        bool createBottomBase,
        float drainHoleRadius)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Procedural_Hollow_Bucket_With_Base_And_Drain_Hole";

        List<Vector3> vertices = new List<Vector3>();
        List<int> outerTriangles = new List<int>();
        List<int> innerTriangles = new List<int>();
        List<int> rimTriangles = new List<int>();
        List<int> baseTriangles = new List<int>();

        float topY = -handleHeight;
        float bottomY = topY - height;
        float baseTopY = bottomY + wallThickness;

        float innerTopRadius = Mathf.Max(0.01f, topRadius - wallThickness);
        float innerBottomRadius = Mathf.Max(0.01f, bottomRadius - wallThickness);
        float safeDrainHoleRadius = Mathf.Clamp(
            drainHoleRadius,
            0.01f,
            innerBottomRadius * 0.65f
        );

        for (int i = 0; i < segments; i++)
        {
            float angle = 2.0f * Mathf.PI * i / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices.Add(new Vector3(topRadius * cos, topY, topRadius * sin));
            vertices.Add(new Vector3(bottomRadius * cos, bottomY, bottomRadius * sin));
            vertices.Add(new Vector3(innerTopRadius * cos, topY - wallThickness, innerTopRadius * sin));
            vertices.Add(new Vector3(innerBottomRadius * cos, baseTopY, innerBottomRadius * sin));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            int topOuterA = i * 4;
            int bottomOuterA = i * 4 + 1;
            int topInnerA = i * 4 + 2;
            int bottomInnerA = i * 4 + 3;

            int topOuterB = next * 4;
            int bottomOuterB = next * 4 + 1;
            int topInnerB = next * 4 + 2;
            int bottomInnerB = next * 4 + 3;

            MeshTriangleUtility.AddQuad(outerTriangles, topOuterA, topOuterB, bottomOuterB, bottomOuterA);
            MeshTriangleUtility.AddQuad(innerTriangles, topInnerB, topInnerA, bottomInnerA, bottomInnerB);
            MeshTriangleUtility.AddQuad(rimTriangles, topOuterB, topOuterA, topInnerA, topInnerB);

            if (!createBottomBase)
            {
                MeshTriangleUtility.AddQuad(rimTriangles, bottomOuterA, bottomOuterB, bottomInnerB, bottomInnerA);
            }
        }

        if (createBottomBase)
        {
            AddBottomBase(
                vertices,
                baseTriangles,
                segments,
                innerBottomRadius,
                bottomRadius,
                safeDrainHoleRadius,
                baseTopY,
                bottomY
            );
        }

        mesh.SetVertices(vertices);
        mesh.subMeshCount = 4;
        mesh.SetTriangles(outerTriangles, 0);
        mesh.SetTriangles(innerTriangles, 1);
        mesh.SetTriangles(rimTriangles, 2);
        mesh.SetTriangles(baseTriangles, 3);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private static void AddBottomBase(
        List<Vector3> vertices,
        List<int> baseTriangles,
        int segments,
        float innerBottomRadius,
        float bottomRadius,
        float drainHoleRadius,
        float baseTopY,
        float bottomY)
    {
        int baseStart = vertices.Count;

        for (int i = 0; i < segments; i++)
        {
            float angle = 2.0f * Mathf.PI * i / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices.Add(new Vector3(innerBottomRadius * cos, baseTopY, innerBottomRadius * sin));
            vertices.Add(new Vector3(drainHoleRadius * cos, baseTopY, drainHoleRadius * sin));
            vertices.Add(new Vector3(bottomRadius * cos, bottomY, bottomRadius * sin));
            vertices.Add(new Vector3(drainHoleRadius * cos, bottomY, drainHoleRadius * sin));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            int topOuterA = baseStart + i * 4;
            int topHoleA = baseStart + i * 4 + 1;
            int bottomOuterA = baseStart + i * 4 + 2;
            int bottomHoleA = baseStart + i * 4 + 3;

            int topOuterB = baseStart + next * 4;
            int topHoleB = baseStart + next * 4 + 1;
            int bottomOuterB = baseStart + next * 4 + 2;
            int bottomHoleB = baseStart + next * 4 + 3;

            MeshTriangleUtility.AddTopAnnulusQuad(baseTriangles, topOuterA, topOuterB, topHoleA, topHoleB);
            MeshTriangleUtility.AddBottomAnnulusQuad(baseTriangles, bottomOuterA, bottomOuterB, bottomHoleA, bottomHoleB);
            MeshTriangleUtility.AddDoubleSidedQuad(baseTriangles, topHoleA, topHoleB, bottomHoleB, bottomHoleA);
        }
    }
}