using System.Collections.Generic;
using UnityEngine;

public static class PrimitiveMeshGenerator
{
    public static Mesh BuildSphere(float radius, int rings, int sectors)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Procedural_Sphere";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        int safeRings = Mathf.Max(3, rings);
        int safeSectors = Mathf.Max(6, sectors);
        float safeRadius = Mathf.Max(0.001f, radius);

        for (int ring = 0; ring <= safeRings; ring++)
        {
            float verticalT = ring / (float)safeRings;
            float phi = Mathf.PI * verticalT;
            float y = Mathf.Cos(phi) * safeRadius;
            float ringRadius = Mathf.Sin(phi) * safeRadius;

            for (int sector = 0; sector <= safeSectors; sector++)
            {
                float horizontalT = sector / (float)safeSectors;
                float theta = horizontalT * Mathf.PI * 2.0f;

                float x = Mathf.Cos(theta) * ringRadius;
                float z = Mathf.Sin(theta) * ringRadius;

                vertices.Add(new Vector3(x, y, z));
            }
        }

        for (int ring = 0; ring < safeRings; ring++)
        {
            for (int sector = 0; sector < safeSectors; sector++)
            {
                int current = ring * (safeSectors + 1) + sector;
                int next = current + safeSectors + 1;

                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);

                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}