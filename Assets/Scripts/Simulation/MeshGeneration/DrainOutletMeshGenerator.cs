using System.Collections.Generic;
using UnityEngine;

public static class DrainOutletMeshGenerator
{
    public static Mesh Build(
        float drainHoleRadius,
        float drainOutletOuterRadius,
        int segments,
        float topY,
        float bottomY)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Procedural_Cylindrical_Drain_Outlet";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        float innerRadius = Mathf.Clamp(drainHoleRadius, 0.01f, drainOutletOuterRadius * 0.75f);
        float outerRadius = Mathf.Max(drainOutletOuterRadius, innerRadius + 0.01f);

        for (int i = 0; i < segments; i++)
        {
            float angle = 2.0f * Mathf.PI * i / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices.Add(new Vector3(outerRadius * cos, topY, outerRadius * sin));
            vertices.Add(new Vector3(outerRadius * cos, bottomY, outerRadius * sin));
            vertices.Add(new Vector3(innerRadius * cos, topY, innerRadius * sin));
            vertices.Add(new Vector3(innerRadius * cos, bottomY, innerRadius * sin));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            int outerTopA = i * 4;
            int outerBottomA = i * 4 + 1;
            int innerTopA = i * 4 + 2;
            int innerBottomA = i * 4 + 3;

            int outerTopB = next * 4;
            int outerBottomB = next * 4 + 1;
            int innerTopB = next * 4 + 2;
            int innerBottomB = next * 4 + 3;

            MeshTriangleUtility.AddQuad(triangles, outerTopA, outerTopB, outerBottomB, outerBottomA);
            MeshTriangleUtility.AddQuad(triangles, innerTopB, innerTopA, innerBottomA, innerBottomB);

            MeshTriangleUtility.AddDoubleSidedQuad(triangles, outerBottomA, outerBottomB, innerBottomB, innerBottomA);
            MeshTriangleUtility.AddDoubleSidedQuad(triangles, outerTopB, outerTopA, innerTopA, innerTopB);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}